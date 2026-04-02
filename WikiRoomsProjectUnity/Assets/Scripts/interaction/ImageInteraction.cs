using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ImageInteraction : MonoBehaviour, IInteractable
{
    static readonly object FullResCacheLock = new object();
    static readonly Dictionary<string, Texture2D> FullResTextureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Task<Texture2D>> FullResInFlightDownloads = new Dictionary<string, Task<Texture2D>>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, DateTime> FullResRetryCooldownUntilUtc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
    static readonly SemaphoreSlim FullResRequestGate = new SemaphoreSlim(1, 1);
    static readonly SemaphoreSlim FullResParallelGate = new SemaphoreSlim(1, 1);
    static readonly object FullResThrottleLock = new object();
    static DateTime lastFullResRequestUtc = DateTime.MinValue;
    static DateTime fullResGlobalCooldownUntilUtc = DateTime.MinValue;
    static int latestOpenRequestToken = 0;

    public static void ClearSessionCaches()
    {
        lock (FullResCacheLock)
        {
            FullResTextureCache.Clear();
            FullResInFlightDownloads.Clear();
            FullResRetryCooldownUntilUtc.Clear();
        }

        lock (FullResThrottleLock)
        {
            lastFullResRequestUtc = DateTime.MinValue;
            fullResGlobalCooldownUntilUtc = DateTime.MinValue;
        }

        Interlocked.Exchange(ref latestOpenRequestToken, 0);
    }

    const int FullResMax429Retries = 2;
    const float FullResMinIntervalSeconds = 1.0f;
    const float FullResRetryBaseDelaySeconds = 2.5f;
    const float FullResMaxRetryDelaySeconds = 60f;

    [Header("Image Data")]
    public string caption;
    public Texture texture;
    public string imageUrl;

    [Header("Full-res retry")]
    [Min(0f)] public float fullResRetryCooldownSeconds = 30f;

    Texture2D fullResolutionTexture;
    string resolvedFullResolutionUrl;

    public async void OnInteraction()
    {
        int requestToken = Interlocked.Increment(ref latestOpenRequestToken);

        // Pobierz ImageUI z PlayerController
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogError("Nie znaleziono PlayerController!");
            return;
        }

        GameObject imageUI = player.ImageUI;
        if (imageUI == null)
        {
            Debug.LogError("ImageUI nie jest przypisane w PlayerController!");
            return;
        }

        Debug.Log($"ImageInteraction: Ustawiam obraz. Caption: {caption}, Texture: {texture?.name}");

        // Znajdź RawImage w dzieciach imageUI
        RawImage rawImage = imageUI.GetComponentInChildren<RawImage>(true);
        if (rawImage != null)
        {
            Texture visibleTexture = fullResolutionTexture != null ? fullResolutionTexture : texture;
            rawImage.texture = visibleTexture;
            rawImage.SetNativeSize();
            Debug.Log($"Ustawiono texture na RawImage: {rawImage.name}");
        }
        else
        {
            Debug.LogError("Nie znaleziono RawImage w ImageUI!");
            return;
        }

        if (fullResolutionTexture != null)
            return;

        string fullResUrl = !string.IsNullOrWhiteSpace(resolvedFullResolutionUrl)
            ? resolvedFullResolutionUrl
            : TryBuildFullResolutionUrl(imageUrl);
        if (string.IsNullOrWhiteSpace(fullResUrl))
            return;

        if (TryGetFullResCooldownRemaining(fullResUrl, out float remainingSeconds))
        {
            Debug.Log($"Full-res cooldown aktywny dla {fullResUrl}. Pozostało {remainingSeconds:0.0}s.");
            return;
        }

        Texture2D fullTexture = await GetFullResolutionTextureCachedAsync(imageUrl, fullResUrl);
        if (fullTexture == null)
        {
            RegisterFullResFailureCooldown(fullResUrl, fullResRetryCooldownSeconds);
            return;
        }

        if (requestToken != latestOpenRequestToken)
            return;

        if (player == null || player.ImageUI == null || !player.ImageUI.activeInHierarchy)
            return;

        fullResolutionTexture = fullTexture;
        resolvedFullResolutionUrl = fullResUrl;
        texture = fullTexture;
        imageUrl = fullResUrl;
        ClearFullResFailureCooldown(fullResUrl);

        if (rawImage != null)
        {
            rawImage.texture = fullTexture;
            rawImage.SetNativeSize();
        }

        // Znajdź TextMeshProUGUI dla caption
        // TMPro.TextMeshProUGUI captionText = imageUI.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        // if (captionText != null)
        // {
        //     captionText.text = caption;
        //     Debug.Log($"Ustawiono caption: {caption}");
        // }
        // else
        // {
        //     Debug.LogWarning("Nie znaleziono TextMeshProUGUI dla caption w ImageUI!");
        // }
    }

    private static string TryBuildFullResolutionUrl(string thumbnailUrl)
    {
        if (string.IsNullOrWhiteSpace(thumbnailUrl))
            return null;

        if (!Uri.TryCreate(thumbnailUrl, UriKind.Absolute, out Uri uri))
            return null;

        string path = uri.AbsolutePath;
        int thumbMarkerIndex = path.IndexOf("/thumb/", StringComparison.OrdinalIgnoreCase);
        if (thumbMarkerIndex < 0)
            return thumbnailUrl;

        string beforeThumb = path.Substring(0, thumbMarkerIndex);
        string afterThumb = path.Substring(thumbMarkerIndex + "/thumb/".Length).TrimStart('/');
        int lastSlash = afterThumb.LastIndexOf('/');
        if (lastSlash <= 0)
            return thumbnailUrl;

        string fullAssetPath = afterThumb.Substring(0, lastSlash);
        string rebuiltPath = string.Concat(beforeThumb, "/", fullAssetPath);

        var builder = new UriBuilder(uri)
        {
            Path = rebuiltPath,
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri.AbsoluteUri;
    }

    private static async Task<Texture2D> GetFullResolutionTextureCachedAsync(string thumbnailUrl, string fullResUrl)
    {
        if (string.IsNullOrWhiteSpace(fullResUrl))
            return null;

        Task<Texture2D> pending = null;
        bool isOwner = false;

        lock (FullResCacheLock)
        {
            if (FullResTextureCache.TryGetValue(fullResUrl, out Texture2D cached) && cached != null)
                return cached;

            if (!string.IsNullOrWhiteSpace(thumbnailUrl) &&
                FullResTextureCache.TryGetValue(thumbnailUrl, out Texture2D thumbnailAliasCached) &&
                thumbnailAliasCached != null)
            {
                FullResTextureCache[fullResUrl] = thumbnailAliasCached;
                return thumbnailAliasCached;
            }

            if (!FullResInFlightDownloads.TryGetValue(fullResUrl, out pending) || pending == null)
            {
                pending = DownloadTextureAsync(thumbnailUrl, fullResUrl);
                FullResInFlightDownloads[fullResUrl] = pending;
                isOwner = true;
            }
        }

        try
        {
            Texture2D downloaded = await pending;
            if (downloaded != null)
            {
                lock (FullResCacheLock)
                {
                    FullResTextureCache[fullResUrl] = downloaded;
                    if (!string.IsNullOrWhiteSpace(thumbnailUrl))
                        FullResTextureCache[thumbnailUrl] = downloaded;
                }
            }

            return downloaded;
        }
        finally
        {
            if (isOwner)
            {
                lock (FullResCacheLock)
                {
                    if (FullResInFlightDownloads.TryGetValue(fullResUrl, out Task<Texture2D> current) && current == pending)
                        FullResInFlightDownloads.Remove(fullResUrl);
                }
            }
        }
    }

    private static bool TryGetFullResCooldownRemaining(string fullResUrl, out float remainingSeconds)
    {
        remainingSeconds = 0f;
        if (string.IsNullOrWhiteSpace(fullResUrl))
            return false;

        lock (FullResCacheLock)
        {
            if (!FullResRetryCooldownUntilUtc.TryGetValue(fullResUrl, out DateTime cooldownUntilUtc))
                return false;

            double remaining = (cooldownUntilUtc - DateTime.UtcNow).TotalSeconds;
            if (remaining <= 0)
            {
                FullResRetryCooldownUntilUtc.Remove(fullResUrl);
                return false;
            }

            remainingSeconds = (float)remaining;
            return true;
        }
    }

    private static void RegisterFullResFailureCooldown(string fullResUrl, float cooldownSeconds)
    {
        if (string.IsNullOrWhiteSpace(fullResUrl) || cooldownSeconds <= 0f)
            return;

        DateTime untilUtc = DateTime.UtcNow.AddSeconds(cooldownSeconds);
        lock (FullResCacheLock)
        {
            if (FullResRetryCooldownUntilUtc.TryGetValue(fullResUrl, out DateTime existing) && existing > untilUtc)
                return;

            FullResRetryCooldownUntilUtc[fullResUrl] = untilUtc;
        }
    }

    private static void ClearFullResFailureCooldown(string fullResUrl)
    {
        if (string.IsNullOrWhiteSpace(fullResUrl))
            return;

        lock (FullResCacheLock)
        {
            FullResRetryCooldownUntilUtc.Remove(fullResUrl);
        }
    }

    private static async Task<Texture2D> DownloadTextureAsync(string thumbnailUrl, string fullResUrl)
    {
        List<string> candidates = BuildHighResCandidateUrls(thumbnailUrl, fullResUrl);
        if (candidates == null || candidates.Count == 0)
            return null;

        await FullResParallelGate.WaitAsync();
        try
        {
            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                string candidateUrl = candidates[candidateIndex];
                Texture2D tex = await DownloadSingleCandidateAsync(candidateUrl);
                if (tex != null)
                    return tex;

                if (candidateIndex < candidates.Count - 1)
                    Debug.LogWarning($"Full-res candidate failed: {candidateUrl}. Próba kolejnego wariantu.");
            }

            return null;
        }
        finally
        {
            FullResParallelGate.Release();
        }
    }

    private static async Task<Texture2D> DownloadSingleCandidateAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        for (int attempt = 0; attempt <= FullResMax429Retries; attempt++)
        {
            await FullResRequestGate.WaitAsync();
            try
            {
                await WaitForFullResMinIntervalAsync();
                await WaitForFullResGlobalCooldownAsync();
                SetLastFullResRequestUtc(DateTime.UtcNow);
            }
            finally
            {
                FullResRequestGate.Release();
            }

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                request.SetRequestHeader("User-Agent", "WikiRoomsProjectUnity/1.0 (Unity client)");
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        return DownloadHandlerTexture.GetContent(request);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Błąd dekodowania full-res obrazu {url}: {ex.Message}");
                        return null;
                    }
                }

                bool is429 = request.responseCode == 429;
                if (is429 && attempt < FullResMax429Retries)
                {
                    int retryDelayMs = GetFullResRetryDelayMilliseconds(request, attempt);
                    RegisterFullResGlobalCooldown(retryDelayMs);
                    RegisterFullResFailureCooldown(url, retryDelayMs / 1000f);
                    Debug.LogWarning($"429 przy full-res {url}. Retry {attempt + 1}/{FullResMax429Retries} za {retryDelayMs / 1000f:0.##}s");
                    if (retryDelayMs > 0)
                        await Task.Delay(retryDelayMs);
                    continue;
                }

                if (!is429)
                    Debug.LogWarning($"Nie udało się pobrać full-res obrazu {url} ({request.responseCode}): {request.error}");

                return null;
            }
        }

        return null;
    }

    private static List<string> BuildHighResCandidateUrls(string thumbnailUrl, string fullResUrl)
    {
        var result = new List<string>();

        if (!string.IsNullOrWhiteSpace(fullResUrl))
            result.Add(fullResUrl);

        foreach (string fallback in BuildSizedThumbFallbackUrls(thumbnailUrl, new[] { 1600, 1280, 1024 }))
            result.Add(fallback);

        return result
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> BuildSizedThumbFallbackUrls(string thumbnailUrl, int[] widths)
    {
        if (string.IsNullOrWhiteSpace(thumbnailUrl) || widths == null || widths.Length == 0)
            yield break;

        if (!Uri.TryCreate(thumbnailUrl, UriKind.Absolute, out Uri uri))
            yield break;

        string path = uri.AbsolutePath;
        int thumbMarkerIndex = path.IndexOf("/thumb/", StringComparison.OrdinalIgnoreCase);
        if (thumbMarkerIndex < 0)
            yield break;

        string beforeThumb = path.Substring(0, thumbMarkerIndex);
        string afterThumb = path.Substring(thumbMarkerIndex + "/thumb/".Length).TrimStart('/');
        int lastSlash = afterThumb.LastIndexOf('/');
        if (lastSlash <= 0)
            yield break;

        string fullAssetPath = afterThumb.Substring(0, lastSlash);
        string originalFileName = fullAssetPath.Split('/').LastOrDefault();
        if (string.IsNullOrWhiteSpace(originalFileName))
            yield break;

        string currentThumbFileName = afterThumb.Substring(lastSlash + 1);
        string sizedThumbFileName = ResolveSizedThumbFileName(currentThumbFileName, originalFileName);
        if (string.IsNullOrWhiteSpace(sizedThumbFileName))
            yield break;

        for (int i = 0; i < widths.Length; i++)
        {
            int width = widths[i];
            if (width <= 0)
                continue;

            string rebuiltPath = string.Concat(beforeThumb, "/thumb/", fullAssetPath, "/", width, "px-", sizedThumbFileName);
            var builder = new UriBuilder(uri)
            {
                Path = rebuiltPath,
                Query = string.Empty,
                Fragment = string.Empty
            };

            yield return builder.Uri.AbsoluteUri;
        }
    }

    private static string ResolveSizedThumbFileName(string currentThumbFileName, string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(currentThumbFileName))
            return originalFileName;

        int pxMarkerIndex = currentThumbFileName.IndexOf("px-", StringComparison.OrdinalIgnoreCase);
        if (pxMarkerIndex <= 0)
            return originalFileName;

        for (int i = 0; i < pxMarkerIndex; i++)
        {
            if (!char.IsDigit(currentThumbFileName[i]))
                return originalFileName;
        }

        string suffix = currentThumbFileName.Substring(pxMarkerIndex + 3);
        if (string.IsNullOrWhiteSpace(suffix))
            return originalFileName;

        return suffix;
    }

    private static async Task WaitForFullResMinIntervalAsync()
    {
        DateTime lastRequestUtc = GetLastFullResRequestUtc();
        if (lastRequestUtc == DateTime.MinValue)
            return;

        double elapsedSeconds = (DateTime.UtcNow - lastRequestUtc).TotalSeconds;
        if (elapsedSeconds >= FullResMinIntervalSeconds)
            return;

        int waitMs = Mathf.CeilToInt((FullResMinIntervalSeconds - (float)elapsedSeconds) * 1000f);
        if (waitMs > 0)
            await Task.Delay(waitMs);
    }

    private static async Task WaitForFullResGlobalCooldownAsync()
    {
        DateTime cooldownUntilUtc = GetFullResGlobalCooldownUntilUtc();
        if (cooldownUntilUtc == DateTime.MinValue)
            return;

        int waitMs = Mathf.CeilToInt((float)(cooldownUntilUtc - DateTime.UtcNow).TotalMilliseconds);
        if (waitMs > 0)
            await Task.Delay(waitMs);
    }

    private static int GetFullResRetryDelayMilliseconds(UnityWebRequest request, int attempt)
    {
        string retryAfterHeader = request.GetResponseHeader("Retry-After");
        int maxDelayMs = Mathf.CeilToInt(FullResMaxRetryDelaySeconds * 1000f);

        if (!string.IsNullOrEmpty(retryAfterHeader))
        {
            if (int.TryParse(retryAfterHeader, out int retryAfterSeconds))
                return Mathf.Clamp(Mathf.Max(0, retryAfterSeconds) * 1000, 0, maxDelayMs);

            if (DateTimeOffset.TryParse(retryAfterHeader, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset retryAfterDate))
            {
                double delayMs = (retryAfterDate.UtcDateTime - DateTime.UtcNow).TotalMilliseconds;
                if (delayMs > 0)
                    return Mathf.Clamp(Mathf.CeilToInt((float)delayMs), 0, maxDelayMs);
            }
        }

        float expDelaySeconds = FullResRetryBaseDelaySeconds * Mathf.Pow(2f, attempt);
        int expDelayMs = Mathf.CeilToInt(Mathf.Max(0f, expDelaySeconds) * 1000f);
        return Mathf.Clamp(expDelayMs, 0, maxDelayMs);
    }

    private static void RegisterFullResGlobalCooldown(int retryDelayMs)
    {
        DateTime untilUtc = DateTime.UtcNow.AddMilliseconds(Mathf.Max(0, retryDelayMs));
        lock (FullResThrottleLock)
        {
            if (untilUtc > fullResGlobalCooldownUntilUtc)
                fullResGlobalCooldownUntilUtc = untilUtc;
        }
    }

    private static DateTime GetLastFullResRequestUtc()
    {
        lock (FullResThrottleLock)
            return lastFullResRequestUtc;
    }

    private static void SetLastFullResRequestUtc(DateTime value)
    {
        lock (FullResThrottleLock)
            lastFullResRequestUtc = value;
    }

    private static DateTime GetFullResGlobalCooldownUntilUtc()
    {
        lock (FullResThrottleLock)
            return fullResGlobalCooldownUntilUtc;
    }
}