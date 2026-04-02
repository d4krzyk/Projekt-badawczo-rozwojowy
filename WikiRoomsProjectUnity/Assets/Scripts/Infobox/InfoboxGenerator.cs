using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using LogicUI.FancyTextRendering;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class InfoboxGenerator : MonoBehaviour
{
    static readonly object ImageCacheLock = new object();
    static readonly Dictionary<string, Sprite> ImageSpriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Task<Sprite>> ImageSpriteInFlight = new Dictionary<string, Task<Sprite>>(StringComparer.OrdinalIgnoreCase);
    static readonly SemaphoreSlim ImageRequestGate = new SemaphoreSlim(1, 1);
    static readonly object ImageThrottleLock = new object();
    static DateTime lastImageRequestUtc = DateTime.MinValue;
    static DateTime globalImageCooldownUntilUtc = DateTime.MinValue;

    const int MaxImage429Retries = 4;
    const float ImageRetryBaseDelaySeconds = 1.5f;
    const float ImageRetryMaxDelaySeconds = 30f;
    const float ImageMinIntervalSeconds = 0.35f;

    public GameObject abovePrefab;
    public GameObject textPrefab;
    public GameObject headerPrefab;
    public GameObject imagePrefab;
    public GameObject captionPrefab;
    public GameObject labelPrefab;
    
    [Header("Content")]
    public RectTransform contentTransform;

    [HideInInspector] public bool HasFailed = false;
    int populationVersion = 0;

    public async Task PopulateUI(WikiPageRaw infoboxesData)
    {
        int populationId = BeginPopulation();
        ClearContent();
        if (infoboxesData == null || infoboxesData.infobox == null)
            return;
    
        foreach (var infobox in infoboxesData.infobox)
        {
            if (!IsPopulationCurrent(populationId))
                return;

            foreach (var item in infobox)
            {
                try
                {
                    await HandleInfoboxItemRaw(item, populationId);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[InfoboxGenerator] Pominieto element infoboxu z powodu bledu: {exception.Message}");
                }

                if (!IsPopulationCurrent(populationId))
                    return;
            }
        }
    }

    public void CancelPopulation(bool clearContent = false)
    {
        populationVersion++;
        if (clearContent)
            ClearContent();
    }

    public void ClearContent()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }

    async Task HandleInfoboxItemRaw(InfoboxItemRaw item, int populationId)
    {
        if (!IsPopulationCurrent(populationId))
            return;
        if (item == null)
            return;

        List<ValueRaw> values = item.value ?? new List<ValueRaw>();

        bool hasLabel = item.label != null;
        GameObject instantiatedObject = null;
        string stringContent = "";
        if (hasLabel)
        {
            GameObject labelObject = Instantiate(labelPrefab);
            string labelStr = HandleLabelRaw(item.label);
            labelObject.transform.SetParent(transform, false);
            LabelController labelController = labelObject.GetComponent<LabelController>();
            labelController.SetLabelText(labelStr);
            string labelValueContent = "";
            foreach (var value in values)
            {
                labelValueContent += HandleValueRaw(value);
            }
            labelController.AddLabelValue(labelValueContent);
        }
        foreach (var value in values)
        {
            stringContent += $"{HandleValueRaw(value)}\n";
        }
        switch (item.@class)
        {
            case "above":
                instantiatedObject = Instantiate(abovePrefab);
                break;
            case "text":
                instantiatedObject = Instantiate(textPrefab);
                break;
            case "header":
                instantiatedObject = Instantiate(headerPrefab);
                break;
            case "image":
                instantiatedObject = Instantiate(imagePrefab);
                Image image = instantiatedObject.GetComponent<Image>();
                ValueRaw firstValue = values.Count > 0 ? values[0] : null;
                string url = NormalizeImageUrl(firstValue?.href);
                Sprite imageSprite = null;
                if (!string.IsNullOrWhiteSpace(url))
                {
                    Debug.Log($"Downloading image from URL: {url}");
                    imageSprite = await GetImageFromURL(url);
                }
                if (!IsPopulationCurrent(populationId))
                {
                    if (instantiatedObject != null)
                        Destroy(instantiatedObject);
                    return;
                }
                if (imageSprite != null)
                {
                    image.sprite = imageSprite;
                }
                instantiatedObject.transform.SetParent(this.transform, false);
                instantiatedObject = Instantiate(captionPrefab);
                stringContent = HandleCaption(firstValue?.caption);
                break;
            case "data":
            case "full-data":
                break;
            default:
                Debug.LogWarning($"Unknown infobox item class: {item.@class}");
                break;
        }
        if (instantiatedObject == null) return;
        if (!IsPopulationCurrent(populationId))
        {
            Destroy(instantiatedObject);
            return;
        }
        instantiatedObject.transform.SetParent(this.transform, false);
        MarkdownRenderer mRenderer = null;
        if (item.@class == "header" || item.@class == "above")
        {
            // header ma TMP głębiej (Background -> TMP)
            mRenderer = instantiatedObject.GetComponentInChildren<MarkdownRenderer>();
        }
        else
        {
            // reszta prefabów ma renderer na root
            mRenderer = instantiatedObject.GetComponent<MarkdownRenderer>();
        }
        if (mRenderer == null) return;
        mRenderer.Source = stringContent;
    }

    int BeginPopulation()
    {
        populationVersion++;
        return populationVersion;
    }

    bool IsPopulationCurrent(int populationId)
    {
        return populationVersion == populationId;
    }

    string HandleValueRaw(ValueRaw valueRaw)
    {
        if (valueRaw == null) return "";
        switch (valueRaw.@class)
        {
            case "text_list_cont":
                if (valueRaw.value == null) return "";
                if (valueRaw.value is string text_list_cont) return $"{text_list_cont}\n";
                Debug.LogWarning($"Expected string value for text class, instead got {valueRaw.value.GetType()}");
                break;
            case "text":
                if (valueRaw.value == null) return "";
                if (valueRaw.value is string textValue) return textValue;
                Debug.LogWarning($"Expected string value for text class, instead got {valueRaw.value.GetType()}");
                break;
            case "link":
                if (!string.IsNullOrEmpty(valueRaw.text)) return $"[{valueRaw.text}]({BuildWikipediaUrl(valueRaw.href)})";
                Debug.LogWarning($"Expected correct text for link class");
                break;
            case "ulist":
                if (valueRaw.value == null) return "";
                string result = "";
                if (valueRaw.value is List<object> listValue)
                {
                    foreach (var item in listValue)
                    {
                        if(item is string str)
                        {
                            result += $"{str} ";
                        }
                        else if(item is ValueRaw vRaw)
                        {
                            result += $"{HandleValueRaw(vRaw)} ";
                        }
                        else
                        {
                            Debug.LogWarning($"Unknown ulist item type: {item.GetType()}");
                        }
                    }
                }
                return result;
            default:
                Debug.LogWarning($"Unknown value class: {valueRaw.@class}");
                break;
        }
        return "";
    }

    string HandleLabelRaw(LabelRaw labelRaw)
    {
        if (labelRaw == null) return "";
        switch (labelRaw.@class)
        {
            case "text":
                if (labelRaw.value == null) return "";
                if (labelRaw.value is string textValue) return textValue;
                Debug.LogWarning($"Expected string value for text class, instead got {labelRaw.value.GetType()}");
                break;
            case "link":
                if (!string.IsNullOrEmpty(labelRaw.value)) return $"[{labelRaw.value}]({BuildWikipediaUrl(labelRaw.value)})";
                Debug.LogWarning($"Expected correct text for link class");
                break;
            default:
                Debug.LogWarning($"Unknown value class: {labelRaw.@class}");
                break;
        }
        return "";
    }

    string NormalizeImageUrl(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return rawUrl;
        if (rawUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            rawUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return rawUrl;
        }

        if (rawUrl.StartsWith("//", StringComparison.OrdinalIgnoreCase))
            return $"https:{rawUrl}";

        if (rawUrl.StartsWith("/"))
            return $"https://en.wikipedia.org{rawUrl}";

        if (rawUrl.StartsWith("./", StringComparison.OrdinalIgnoreCase))
            return $"https://en.wikipedia.org/wiki/{rawUrl.Substring(2)}";

        return rawUrl;
    }

    string BuildWikipediaUrl(string rawHref)
    {
        if (string.IsNullOrWhiteSpace(rawHref)) return "https://en.wikipedia.org";
        if (rawHref.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            rawHref.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return rawHref;
        }

        if (rawHref.StartsWith("//", StringComparison.OrdinalIgnoreCase))
            return $"https:{rawHref}";

        if (rawHref.StartsWith("/wiki/", StringComparison.OrdinalIgnoreCase))
            return $"https://en.wikipedia.org{rawHref}";

        if (rawHref.StartsWith("./", StringComparison.OrdinalIgnoreCase))
            return $"https://en.wikipedia.org/wiki/{rawHref.Substring(2)}";

        return $"https://en.wikipedia.org/wiki/{rawHref}";
    }

    string HandleCaption(object caption)
    {
        string result = "";
        if (caption == null)
            return result;

        if (caption is List<object> captionList)
        {    
            if (captionList == null || captionList.Count == 0) return "";
            foreach (var item in captionList)
            {
                if (item is string str)
                {
                    result += str;
                }
                else if (item is ValueRaw valueRaw)
                {
                    result += HandleValueRaw(valueRaw);
                }
                else
                {
                    Debug.LogWarning($"Unknown caption item type: {item.GetType()}");
                }
            }
        }
        else if (caption is string captionStr)
        {
            result = captionStr;
        }
        else if (caption is ValueRaw captionValueRaw)
        {
            result = HandleValueRaw(captionValueRaw);
        }
        else
        {
            Debug.LogWarning($"Unknown caption type: {caption.GetType()}");
        }
        return result;
    }

    async Task<Sprite> GetImageFromURL(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;

        Task<Sprite> pending = null;
        bool isOwner = false;

        lock (ImageCacheLock)
        {
            if (ImageSpriteCache.TryGetValue(url, out Sprite cachedSprite) && cachedSprite != null)
                return cachedSprite;

            if (!ImageSpriteInFlight.TryGetValue(url, out pending) || pending == null)
            {
                pending = DownloadImageSpriteWithRetryAsync(url);
                ImageSpriteInFlight[url] = pending;
                isOwner = true;
            }
        }

        try
        {
            Sprite downloaded = await pending;
            if (downloaded != null)
            {
                lock (ImageCacheLock)
                {
                    ImageSpriteCache[url] = downloaded;
                }
            }

            return downloaded;
        }
        finally
        {
            if (isOwner)
            {
                lock (ImageCacheLock)
                {
                    if (ImageSpriteInFlight.TryGetValue(url, out Task<Sprite> current) && current == pending)
                        ImageSpriteInFlight.Remove(url);
                }
            }
        }
    }

    async Task<Sprite> DownloadImageSpriteWithRetryAsync(string url)
    {
        for (int attempt = 0; attempt <= MaxImage429Retries; attempt++)
        {
            await ImageRequestGate.WaitAsync();
            try
            {
                await WaitForImageMinIntervalAsync();
                await WaitForGlobalImageCooldownAsync();
                SetLastImageRequestUtc(DateTime.UtcNow);
            }
            finally
            {
                ImageRequestGate.Release();
            }

            using (UnityWebRequest texReq = UnityWebRequestTexture.GetTexture(url))
            {
                texReq.SetRequestHeader("User-Agent", "WikiRoomsProjectUnity/1.0 (Unity client)");

                var texOp = texReq.SendWebRequest();
                while (!texOp.isDone)
                    await Task.Yield();

                if (texReq.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        Texture2D tex = DownloadHandlerTexture.GetContent(texReq);
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Błąd tworzenia Texture2D z {url}: {e.Message}");
                        return null;
                    }
                }

                bool is429 = texReq.responseCode == 429;
                if (is429 && attempt < MaxImage429Retries)
                {
                    int retryDelayMs = GetRetryDelayMilliseconds(texReq, attempt);
                    RegisterGlobalImageCooldown(retryDelayMs);
                    if (retryDelayMs > 0)
                        await Task.Delay(retryDelayMs);
                    continue;
                }

                Debug.LogWarning($"Nie udało się pobrać obrazu infoboxu {url} ({texReq.responseCode}): {texReq.error}");
                return null;
            }
        }

        Debug.LogWarning($"Nie udało się pobrać obrazu infoboxu {url} po {MaxImage429Retries + 1} próbach.");
        return null;
    }

    async Task WaitForImageMinIntervalAsync()
    {
        DateTime lastRequestUtc = GetLastImageRequestUtc();
        if (lastRequestUtc == DateTime.MinValue)
            return;

        double elapsedSeconds = (DateTime.UtcNow - lastRequestUtc).TotalSeconds;
        if (elapsedSeconds >= ImageMinIntervalSeconds)
            return;

        int waitMs = Mathf.CeilToInt((ImageMinIntervalSeconds - (float)elapsedSeconds) * 1000f);
        if (waitMs > 0)
            await Task.Delay(waitMs);
    }

    async Task WaitForGlobalImageCooldownAsync()
    {
        DateTime cooldownUntilUtc = GetGlobalImageCooldownUntilUtc();
        if (cooldownUntilUtc == DateTime.MinValue)
            return;

        int waitMs = Mathf.CeilToInt((float)(cooldownUntilUtc - DateTime.UtcNow).TotalMilliseconds);
        if (waitMs > 0)
            await Task.Delay(waitMs);
    }

    int GetRetryDelayMilliseconds(UnityWebRequest request, int attempt)
    {
        string retryAfterHeader = request.GetResponseHeader("Retry-After");
        int maxDelayMs = Mathf.CeilToInt(ImageRetryMaxDelaySeconds * 1000f);

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

        float expDelaySeconds = ImageRetryBaseDelaySeconds * Mathf.Pow(2f, attempt);
        int expDelayMs = Mathf.CeilToInt(Mathf.Max(0f, expDelaySeconds) * 1000f);
        return Mathf.Clamp(expDelayMs, 0, maxDelayMs);
    }

    void RegisterGlobalImageCooldown(int retryDelayMs)
    {
        DateTime untilUtc = DateTime.UtcNow.AddMilliseconds(Mathf.Max(0, retryDelayMs));
        lock (ImageThrottleLock)
        {
            if (untilUtc > globalImageCooldownUntilUtc)
                globalImageCooldownUntilUtc = untilUtc;
        }
    }

    DateTime GetLastImageRequestUtc()
    {
        lock (ImageThrottleLock)
            return lastImageRequestUtc;
    }

    void SetLastImageRequestUtc(DateTime value)
    {
        lock (ImageThrottleLock)
            lastImageRequestUtc = value;
    }

    DateTime GetGlobalImageCooldownUntilUtc()
    {
        lock (ImageThrottleLock)
            return globalImageCooldownUntilUtc;
    }
}
