using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.VFX;
using TMPro; 

public class ElongatedRoomGenerator : MonoBehaviour
{
    const string GenAIEnabledKey = "GenAITexturesEnabled";
    static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    static readonly int PortalAccentColorPropertyId = Shader.PropertyToID("_PortalAccentColor");
    static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");

    [Serializable]
    public class RequestThrottlingSettings
    {
        [Min(0f)] public float imageDownloadMinIntervalSeconds = 0.35f;
        [Min(0f)] public float retryBaseDelaySeconds = 1.25f;
    }

    [Serializable]
    public class ImageDownloadSettings
    {
        [Min(1)] public int maxConcurrentDownloads = 3;
        [Min(0)] public int max429Retries = 6;
        [Min(0f)] public float extraThrottleStepSeconds = 0.2f;
        [Min(0f)] public float maxExtraThrottleSeconds = 2.0f;
        [Min(0f)] public float successThrottleDecaySeconds = 0.03f;
        public bool log429RetryAttempts = false;
    }

    static readonly SemaphoreSlim ImageDownloadRequestGate = new SemaphoreSlim(1, 1);
    static readonly object RateLimitLock = new object();
    static DateTime lastImageDownloadRequestUtc = DateTime.MinValue;
    static DateTime imageDownloadCooldownUntilUtc = DateTime.MinValue;
    static float dynamicImageExtraThrottleSeconds = 0f;
    static readonly object SharedImageCacheLock = new object();
    static readonly Dictionary<string, Texture2D> SharedImageTextureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Task<Texture2D>> SharedImageInFlightDownloads = new Dictionary<string, Task<Texture2D>>(StringComparer.OrdinalIgnoreCase);
    const int SharedImageCacheMaxEntries = 1024;

    public static void ClearSessionCaches()
    {
        lock (SharedImageCacheLock)
        {
            SharedImageTextureCache.Clear();
            SharedImageInFlightDownloads.Clear();
        }

        lock (RateLimitLock)
        {
            lastImageDownloadRequestUtc = DateTime.MinValue;
            imageDownloadCooldownUntilUtc = DateTime.MinValue;
            dynamicImageExtraThrottleSeconds = 0f;
        }
    }

    public GameObject spawnRoom, extensionRoom, extensionRoomClosure, bookshelf, imageHolder, stand, Altar;
    public Transform initialRoomPosition;
    public Material sampleImageMat; 
    [Header("Room materials")]
    public Material bookshelfMat;
    public Material floorMat;
    public Material wallMat;
    public Material standMat;
    [Header("Default materials")]
    public Material defBookcaseMat;
    public Material defFloorMat;
    public Material defWallMat;
    public Material defStandMat;
    [Space]
    public string articleName;
    public int maxBooksPerBookshelf;
    public Logger logger;
    public GameObject portalNext;
    public GameObject portalPrevious;
    public InfoboxGenerator infoboxGenerator;
    public InfoboxGenerator secInfoboxGenerator;
    public BackendConfig backendConfig;
    public GameObject loadingScreen;

    [Header("Portal color tuning")]
    [Range(0f, 1f)] public float portalColorIntensity = 1f;
    [Range(0f, 1f)] public float portalNextHueJitter = 0.7f;
    [Range(0f, 1f)] public float portalYellowAvoidanceStrength = 0.85f;
    [Range(10f, 90f)] public float portalYellowAvoidanceBandDegrees = 40f;
    [Range(0f, 1f)] public float portalNextMinSaturation = 0.82f;
    [Range(0f, 1f)] public float portalNextMinValue = 0.95f;
    [Range(0f, 1f)] public float portalPreviousHueShift = 0.09f;
    [Range(0f, 1f)] public float portalPreviousMinSaturation = 0.65f;
    [Range(0f, 1f)] public float portalPreviousMinValue = 0.82f;
    [Min(0f)] public float portalEmissionStrength = 1.35f;

    [Header("Request throttling")]
    public RequestThrottlingSettings requestThrottling = new RequestThrottlingSettings();

    [Header("Image download")]
    public ImageDownloadSettings imageDownloads = new ImageDownloadSettings();

    [Header("Loading screen")]
    [Range(0f, 1f)] public float imageProgressToHideLoadingScreen = 0.5f;

    [Header("Image request budget")]
    [Min(1)] public int maxImagesPerSlot = 2;

    [Header("Image size classification")]
    public int smallMaxPixels = 200000;
    public int mediumMaxPixels = 600000;

    [Header("Image class base heights")]
    public float smallImageHeight = 1.1f;
    public float mediumImageHeight = 1.4f;
    public float largeImageHeight = 2.0f;

    [Header("Image cluster layout")]
    public float clusterWidth = 2.2f;
    public float clusterHeight = 2.2f;
    public float clusterPadding = 0.15f;
    public float twoImageDiagonalOffsetY = 0.05f;
    public float twoImageDiagonalOffsetX = 0.35f;
    [Range(0.0f, 1.0f)]
    public float twoImageAspectSimilarityThreshold = 0.25f;

    [Header("Transparent images handling")]
    public bool fillTransparentWithWhite = true;
    [Range(0.0f, 1.0f)]
    public float transparentAlphaThreshold = 0.05f;
    [Range(0.0f, 1.0f)]
    public float transparentPixelRatioThreshold = 0.15f;

    [HideInInspector] public float EnterTime;
    [HideInInspector] public float ExitTime;
    [HideInInspector] public string PreviousRoom;
    [System.NonSerialized] public ArticleStructure ArticleData;

    string articleLink;
    RoomsController roomsController;

    [Header("Article title in Room")]
    public TextMeshPro articleTitleText; // przypnij TextMeshProUGUI z Canvasu

    List<GameObject> spawnedExtensions = new List<GameObject>();
    string auth_header;
    public bool HasLoaded = false;
    SemaphoreSlim imageDownloadParallelGate;
    int imageDownloadParallelGateLimit = -1;
    int generationVersion = 0;
    
    // struktura odpowiadająca JSON z /images/generator
    [Serializable]
    class ImagesResponse
    {
        public string page_name;
        public JToken images; // może być tablicą albo obiektem zawierającym pole "images"
        public float time;

        public List<Dictionary<string, string>> GetImagesList()
        {
            if (images == null) return null;
            if (images.Type == JTokenType.Array)
                return images.ToObject<List<Dictionary<string, string>>>();
            if (images.Type == JTokenType.Object)
            {
                var inner = images["images"];
                if (inner != null && inner.Type == JTokenType.Array)
                    return inner.ToObject<List<Dictionary<string, string>>>();
            }
            return null;
        }
    }

    enum ImageSizeClass
    {
        Small,
        Medium,
        Large
    }

    struct ImagePayload
    {
        public Texture2D texture;
        public string caption;
        public string sourceUrl;
        public ImageSizeClass sizeClass;
    }

    struct ImageSlot
    {
        public Transform parent;
        public Vector3 localPosition;
        public Vector3 localRotation;
    }

    struct SlotClusterResult
    {
        public int slotIndex;
        public List<ImagePayload> payloads;
    }
    
    public void Awake()
    {
        EnsureSettings();
        GameController gameController = FindAnyObjectByType<GameController>();
        if(gameController != null) articleName = gameController.ArticleName;
    }

    public async void GenerateRoom(string articleName, RoomsController roomsController, bool onClick = false)
    {
        EnsureSettings();
        EnsureImageDownloadParallelGate();
        ResetImageDownloadAdaptiveState();
        int generationId = BeginGeneration();
        HasLoaded = false;
        ArticleData = null;
        CancelInfoboxPopulation(clearContent: true);
        if(roomsController != null && roomsController.elongatedRoom == this)
        {
            loadingScreen.SetActive(true);
            // Zresetuj animację loading screen
            var loadingMotion = loadingScreen.GetComponentInChildren<LoadingPuzzleMotion>();
            if (loadingMotion != null) loadingMotion.ResetAnimation();
        }
        auth_header = string.Empty;
        string displayArticleName = Uri.UnescapeDataString(articleName);
        // nie odtwarzaj dźwięku dla portalu
        this.articleName = articleName;

        // zabezpiecz roomsController (jeśli przekazany null, spróbuj znaleźć w scenie)
        this.roomsController = roomsController ?? FindAnyObjectByType<RoomsController>();
        if (this.roomsController == null)
        {
            Debug.LogWarning("RoomsController is null; proceeding without cache.");
        }

        // Ustaw tytuł w UI od razu po otrzymaniu nazwy
        if (articleTitleText != null)
        {
            articleTitleText.text = displayArticleName;
        }

        SetDefaultMaterials();

        articleLink = "https://en.wikipedia.org/wiki/" + articleName.Replace(" ", "_");
        EnterTime = Time.time;
        Debug.Log($"Loading {articleName}...");

        // Pobieranie artykułu
        bool flowControl = await HandleArticle(articleName, generationId);
        if (!flowControl)
        {
            if (IsGenerationCurrent(generationId))
                HideLoadingScreenIfNeeded();
            return;
        }
        if (!IsGenerationCurrent(generationId)) return;

        Debug.Log($"Loaded {articleName} data");


        if (onClick) 
        {
            this.roomsController?.elongatedRoom?.SetActivePortalNext(true);
        }

        // Bezpieczne użycie category
        Task textureTask = HandleTextures(articleName, this.roomsController, generationId);
        Task infoboxTask = HandleInfobox(articleName, generationId);
        await Task.WhenAll(textureTask, infoboxTask);
        if (!IsGenerationCurrent(generationId)) return;

        SpawnExtensionsWithBookselfs();
        HasLoaded = true;
        await HandleImagesProgressively(articleName, hideWhenProgressReached: true, generationId);
        if (!IsGenerationCurrent(generationId)) return;
        HideLoadingScreenIfNeeded();
        Debug.Log($"Loaded {articleName} successfully.");
    }

    private async Task HandleImagesProgressively(string articleName, bool hideWhenProgressReached, int generationId)
    {
        if (!IsGenerationCurrent(generationId)) return;

        var slots = BuildImageSlots();
        if (slots.Count == 0)
        {
            if (hideWhenProgressReached)
                HideLoadingScreenIfNeeded();
            return;
        }

        if (roomsController != null)
        {
            List<RoomsController.CachedImageData> cachedImages = roomsController.GetCachedImages(articleName);
            if (cachedImages != null && cachedImages.Count > 0)
            {
                SpawnCachedImages(cachedImages, slots);
                if (hideWhenProgressReached)
                    HideLoadingScreenIfNeeded();
                return;
            }
        }

        var imagesLinks = new List<Dictionary<string, string>>();
        try
        {
            imagesLinks = await GetImagesListAsync(articleName);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Błąd pobierania obrazów dla {articleName}: {ex.Message}");
        }
        if (!IsGenerationCurrent(generationId)) return;

        if (imagesLinks.Count == 0)
        {
            if (hideWhenProgressReached)
                HideLoadingScreenIfNeeded();
            return;
        }

        int safeMaxPerSlot = Mathf.Max(1, maxImagesPerSlot);
        int targetImageCount = Mathf.Min(imagesLinks.Count, slots.Count * safeMaxPerSlot);
        int loadedImagesCount = 0;
        int imagesTargetToHide = Mathf.CeilToInt(Mathf.Max(0f, Mathf.Min(1f, imageProgressToHideLoadingScreen)) * targetImageCount);
        var downloadedForCache = new List<RoomsController.CachedImageData>(targetImageCount);

        if (hideWhenProgressReached && imagesTargetToHide <= 0)
            HideLoadingScreenIfNeeded();

        var groupSizes = BuildGroupSizes(targetImageCount, slots.Count, safeMaxPerSlot);

        int imageIndex = 0;
        var pendingSlots = new List<Task<SlotClusterResult>>();
        var slotHasWork = new bool[slots.Count];
        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            int count = groupSizes[slotIndex];
            if (count <= 0) continue;
            slotHasWork[slotIndex] = true;

            var tasksForSlot = new List<Task<ImagePayload?>>(count);
            for (int k = 0; k < count && imageIndex < targetImageCount; k++)
            {
                tasksForSlot.Add(DownloadImagePayloadAsync(imagesLinks[imageIndex]));
                imageIndex++;
            }

            pendingSlots.Add(CollectSlotPayloadsAsync(slotIndex, tasksForSlot));
        }

        int nextSlotToSpawn = 0;
        var readyBySlot = new Dictionary<int, SlotClusterResult>();

        async Task TrySpawnReadySlotsInOrderAsync()
        {
            while (nextSlotToSpawn < slots.Count)
            {
                if (!slotHasWork[nextSlotToSpawn])
                {
                    nextSlotToSpawn++;
                    continue;
                }

                if (!readyBySlot.TryGetValue(nextSlotToSpawn, out SlotClusterResult orderedResult))
                    break;

                readyBySlot.Remove(nextSlotToSpawn);

                int targetCountForSlot = groupSizes[nextSlotToSpawn];
                if (orderedResult.payloads == null)
                    orderedResult.payloads = new List<ImagePayload>();

                // Jeśli część obrazów nie doszła (np. transient 429), próbujemy dopełnić slot kolejnymi obrazami z listy.
                while (orderedResult.payloads.Count < targetCountForSlot && imageIndex < imagesLinks.Count)
                {
                    ImagePayload? topUpPayload = await DownloadImagePayloadAsync(imagesLinks[imageIndex]);
                    imageIndex++;
                    if (topUpPayload.HasValue)
                        orderedResult.payloads.Add(topUpPayload.Value);
                }

                if (orderedResult.payloads != null && orderedResult.payloads.Count > 0)
                {
                    SpawnImageCluster(slots[orderedResult.slotIndex], orderedResult.payloads);
                    loadedImagesCount += orderedResult.payloads.Count;
                    AddPayloadsToCacheBuffer(downloadedForCache, orderedResult.payloads);

                    if (hideWhenProgressReached && loadingScreen != null && loadingScreen.activeSelf && loadedImagesCount >= imagesTargetToHide)
                    {
                        HideLoadingScreenIfNeeded();
                    }
                }

                nextSlotToSpawn++;
            }
        }

        while (pendingSlots.Count > 0)
        {
            Task<SlotClusterResult> completedTask = await Task.WhenAny(pendingSlots);
            pendingSlots.Remove(completedTask);

            SlotClusterResult slotResult;
            try
            {
                slotResult = await completedTask;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Błąd pobierania grupy obrazów: {ex.Message}");
                continue;
            }

            if (!IsGenerationCurrent(generationId)) return;
            readyBySlot[slotResult.slotIndex] = slotResult;
            await TrySpawnReadySlotsInOrderAsync();
        }

        await TrySpawnReadySlotsInOrderAsync();

        if (roomsController != null && downloadedForCache.Count > 0)
        {
            roomsController.CacheImages(articleName, downloadedForCache);
        }

    }

    private void SpawnCachedImages(List<RoomsController.CachedImageData> cachedImages, List<ImageSlot> slots)
    {
        if (cachedImages == null || cachedImages.Count == 0 || slots == null || slots.Count == 0)
            return;

        int safeMaxPerSlot = Mathf.Max(1, maxImagesPerSlot);
        int usableCount = Mathf.Min(cachedImages.Count, slots.Count * safeMaxPerSlot);
        var payloads = new List<ImagePayload>(usableCount);

        for (int i = 0; i < usableCount; i++)
        {
            RoomsController.CachedImageData cached = cachedImages[i];
            if (cached == null || cached.texture == null) continue;

            payloads.Add(new ImagePayload
            {
                texture = cached.texture,
                caption = cached.caption ?? string.Empty,
                sizeClass = ClassifyImage(cached.texture)
            });
        }

        if (payloads.Count == 0) return;

        var groupSizes = BuildGroupSizes(payloads.Count, slots.Count, safeMaxPerSlot);
        int payloadIndex = 0;
        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            int count = groupSizes[slotIndex];
            if (count <= 0) continue;

            var slotPayloads = new List<ImagePayload>(count);
            for (int k = 0; k < count && payloadIndex < payloads.Count; k++)
            {
                slotPayloads.Add(payloads[payloadIndex]);
                payloadIndex++;
            }

            if (slotPayloads.Count > 0)
                SpawnImageCluster(slots[slotIndex], slotPayloads);
        }
    }

    private void AddPayloadsToCacheBuffer(List<RoomsController.CachedImageData> targetCache, List<ImagePayload> payloads)
    {
        if (targetCache == null || payloads == null || payloads.Count == 0)
            return;

        for (int i = 0; i < payloads.Count; i++)
        {
            if (payloads[i].texture == null) continue;

            targetCache.Add(new RoomsController.CachedImageData
            {
                texture = payloads[i].texture,
                caption = payloads[i].caption ?? string.Empty
            });
        }
    }

    private void HideLoadingScreenIfNeeded()
    {
        if (loadingScreen != null && loadingScreen.activeSelf)
            loadingScreen.SetActive(false);
    }

    private async Task<SlotClusterResult> CollectSlotPayloadsAsync(int slotIndex, List<Task<ImagePayload?>> tasksForSlot)
    {
        if (tasksForSlot == null || tasksForSlot.Count == 0)
        {
            return new SlotClusterResult
            {
                slotIndex = slotIndex,
                payloads = new List<ImagePayload>()
            };
        }

        ImagePayload?[] results = await Task.WhenAll(tasksForSlot);
        var payloads = new List<ImagePayload>(results.Length);
        for (int i = 0; i < results.Length; i++)
        {
            if (results[i].HasValue)
                payloads.Add(results[i].Value);
        }

        return new SlotClusterResult
        {
            slotIndex = slotIndex,
            payloads = payloads
        };
    }

    private async Task<ImagePayload?> DownloadImagePayloadAsync(Dictionary<string, string> imgObj)
    {
        if (imgObj == null || imgObj.Count == 0) return null;

        var kvp = imgObj.First();
        string imgUrl = kvp.Key;
        if (string.IsNullOrEmpty(imgUrl)) return null;

        string caption = kvp.Value ?? "[no caption]";
        Texture2D tex = await GetImageAsTextureCachedAsync(imgUrl);
        if (tex == null) return null;

        return new ImagePayload
        {
            texture = ProcessTransparency(tex),
            caption = caption,
            sourceUrl = imgUrl,
            sizeClass = ClassifyImage(tex)
        };
    }


    private async Task HandleInfobox(string articleName, int generationId, bool firstRoom = false)
    {
        List<InfoboxGenerator> targetGenerators = GetInfoboxGenerators(firstRoom);

        string infoboxJson = await GetInfoboxAsync(articleName);
        if (!IsGenerationCurrent(generationId)) return;
        if (string.IsNullOrEmpty(infoboxJson))
        {
            Debug.Log("Failed to retrieve infobox data.");
            SetInfoboxFailureState(targetGenerators, hasFailed: true);
        }
        else
        {
            WikiPageRaw infoboxData = InfoboxParser.Parse(infoboxJson);
            if (!IsGenerationCurrent(generationId)) return;
            if (infoboxData.infobox == null)
            {
                Debug.Log("Infobox parsing returned null.");
                SetInfoboxFailureState(targetGenerators, hasFailed: true);
                await PopulateInfoboxGenerators(targetGenerators, infoboxData, generationId);
                return;
            }
            if(infoboxData.infobox.Count == 0)
            {
                Debug.Log("Infobox parsing returned empty data.");
                SetInfoboxFailureState(targetGenerators, hasFailed: true);
                await PopulateInfoboxGenerators(targetGenerators, infoboxData, generationId);
                return;
            }

            SetInfoboxFailureState(targetGenerators, hasFailed: false);
            await PopulateInfoboxGenerators(targetGenerators, infoboxData, generationId);
        }
    }

    private async Task PopulateInfoboxGenerators(List<InfoboxGenerator> generators, WikiPageRaw infoboxData, int generationId)
    {
        if (generators == null || generators.Count == 0)
            return;

        for (int i = 0; i < generators.Count; i++)
        {
            if (!IsGenerationCurrent(generationId))
                return;

            if (generators[i] != null)
                await generators[i].PopulateUI(infoboxData);
        }
    }

    private void SetInfoboxFailureState(List<InfoboxGenerator> generators, bool hasFailed)
    {
        if (generators == null || generators.Count == 0)
            return;

        for (int i = 0; i < generators.Count; i++)
        {
            if (generators[i] != null)
                generators[i].HasFailed = hasFailed;
        }
    }

    private async Task HandleTextures(string articleName, RoomsController roomsController, int generationId)
    {
        if (!IsGenAITexturesEnabled())
        {
            Debug.Log("GenAI textures disabled - using default materials.");
            if (IsGenerationCurrent(generationId))
                SetDefaultMaterials();
            return;
        }

        string category = string.IsNullOrEmpty(ArticleData?.category) ? "default" : ArticleData.category;
        Debug.Log($"Waiting for {category} textures...");
        TexturesStructure cachedTextures = roomsController != null ? roomsController.GetCachedTextures(articleName) : null;
        if (cachedTextures != null)
        {
            Debug.Log("Using cached textures.");
            if (IsGenerationCurrent(generationId))
                ApplyTexturesToMaterials(cachedTextures);
        }
        else
        {
            string texturesJson = await GetTexturesJsonAsync(articleName, ArticleData.category);
            if (!IsGenerationCurrent(generationId)) return;
            TexturesStructure texturesData;
            if (string.IsNullOrEmpty(texturesJson)) Debug.LogWarning("Failed to retrieve textures data.");
            else
            {
                texturesData = JsonConvert.DeserializeObject<TexturesStructure>(texturesJson);
                if (roomsController != null)
                    roomsController.CacheTextures(articleName, texturesData);
                if (IsGenerationCurrent(generationId))
                    ApplyTexturesToMaterials(texturesData);
            }
        }
    }

    private async Task<bool> HandleArticle(string articleName, int generationId)
    {
        Debug.Log($"Waiting for {articleName} article data...");
        ArticleStructure cachedArticle = null;
        if (roomsController != null)
        {
            cachedArticle = roomsController.GetCachedArticle(articleName);
        }

        if (cachedArticle != null)
        {
            Debug.Log("Using cached article data.");
            ArticleData = NormalizeArticleData(cachedArticle);
        }
        else
        {
            string json = await GetArticleAsync(articleName);
            if (!IsGenerationCurrent(generationId)) return false;
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("Failed to retrieve article data.");
                return false;
            }
            ArticleData = NormalizeArticleData(JsonConvert.DeserializeObject<ArticleStructure>(json));
            if (ArticleData == null)
            {
                Debug.LogError("Article deserialization returned null.");
                return false;
            }
            if (roomsController != null)
                roomsController.CacheArticle(articleName, ArticleData);
        }

        if (ArticleData.content.Length == 0)
        {
            Debug.LogWarning($"Article '{articleName}' has no sections. Generating an empty room shell instead of failing.");
        }

        return true;
    }

    void SetDefaultMaterials()
    {
        ApplyRoomMaterials(defWallMat, defFloorMat, defBookcaseMat, defStandMat);
    }

    void ApplyRoomMaterials(Material wallMaterial, Material floorMaterial, Material shelfMaterial, Material standMaterial = null)
    {
        Vector2 defaultScale = Vector2.one;
        Vector2 defaultOffset = Vector2.zero;

        MeshRenderer spawnRenderer = spawnRoom.GetComponent<MeshRenderer>();
        MeshRenderer extensionRenderer = extensionRoom.GetComponent<MeshRenderer>();
        MeshRenderer closureRenderer = extensionRoomClosure.GetComponent<MeshRenderer>();
        MeshRenderer bookshelfRenderer = bookshelf.GetComponent<MeshRenderer>();
        MeshRenderer standRenderer = stand.GetComponent<MeshRenderer>();
        MeshRenderer altarRenderer = Altar.GetComponent<MeshRenderer>();

        spawnRenderer.SetMaterials(new List<Material> { wallMaterial, floorMaterial, floorMaterial });
        extensionRenderer.SetMaterials(new List<Material> { wallMaterial, floorMaterial });
        closureRenderer.SetMaterials(new List<Material> { wallMaterial });

        SetRendererMaterialTransform(closureRenderer, 0, defaultScale, defaultOffset);
        SetRendererMaterialTransform(extensionRenderer, 1, defaultScale, defaultOffset);
        SetRendererMaterialTransform(spawnRenderer, 0, defaultScale, defaultOffset);
        SetRendererMaterialTransform(spawnRenderer, 1, defaultScale, defaultOffset);

        bookshelfRenderer.sharedMaterial = shelfMaterial;
        standRenderer.sharedMaterial = standMaterial;
        SetMaterialTextureScaleAndOffset(standRenderer.sharedMaterial, new Vector2(0.65f, 0.58f), new Vector2(0.24f, 0f));


        altarRenderer.SetMaterials(new List<Material> { shelfMaterial, wallMaterial, shelfMaterial });
        SetRendererMaterialTransform(altarRenderer, 0, defaultScale, defaultOffset);
        SetRendererMaterialTransform(altarRenderer, 1, defaultScale, defaultOffset);

        UpdatePortalColorsFromRoomMaterials(wallMaterial, floorMaterial, shelfMaterial);
    }

    void SetRendererMaterialTransform(Renderer renderer, int materialIndex, Vector2 scale, Vector2 offset)
    {
        Material material = GetRendererMaterial(renderer, materialIndex);
        if (material == null) return;
        SetMaterialTextureScaleAndOffset(material, scale, offset);
    }

    Material GetRendererMaterial(Renderer renderer, int materialIndex)
    {
        if (renderer == null || materialIndex < 0) return null;
        Material[] materials = renderer.sharedMaterials;
        if (materials == null || materialIndex >= materials.Length) return null;
        return materials[materialIndex];
    }

    void SetMaterialTextureScaleAndOffset(Material material, Vector2 scale, Vector2 offset)
    {
        if (material == null) return;

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTextureScale("_BaseMap", scale);
            material.SetTextureOffset("_BaseMap", offset);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTextureScale("_MainTex", scale);
            material.SetTextureOffset("_MainTex", offset);
        }
    }

    void UpdatePortalColorsFromRoomMaterials(Material wallMaterial, Material floorMaterial, Material shelfMaterial)
    {
        Color basePortalColor = BuildPortalBaseColor(wallMaterial, floorMaterial, shelfMaterial);
        Color forwardPortalColor = ApplyColorIntensity(TuneForwardPortalColor(basePortalColor));
        Color backwardPortalColor = ApplyColorIntensity(TuneBackwardPortalColor(forwardPortalColor));

        ApplyPortalColor(portalNext, forwardPortalColor);
        ApplyPortalColor(portalPrevious, backwardPortalColor);

        Debug.Log($"[PortalColor] {articleName} | base={ColorToLogString(basePortalColor)} | next={ColorToLogString(forwardPortalColor)} | prev={ColorToLogString(backwardPortalColor)}", this);
    }

    Color BuildPortalBaseColor(Material wallMaterial, Material floorMaterial, Material shelfMaterial)
    {
        Color wallColor = GetRepresentativeMaterialColor(wallMaterial, new Color(0.42f, 0.58f, 0.66f, 1f));
        Color floorColor = GetRepresentativeMaterialColor(floorMaterial, new Color(0.34f, 0.42f, 0.50f, 1f));
        Color shelfColor = GetRepresentativeMaterialColor(shelfMaterial, new Color(0.52f, 0.38f, 0.26f, 1f));

        Color blended = wallColor * 0.5f + floorColor * 0.35f + shelfColor * 0.15f;
        blended.a = 1f;

        Color.RGBToHSV(blended, out float hue, out float saturation, out float value);
        saturation = Mathf.Clamp(Mathf.Max(saturation, 0.22f), 0f, 1f);
        value = Mathf.Clamp(Mathf.Max(value, 0.35f), 0f, 1f);
        return Color.HSVToRGB(hue, saturation, value);
    }

    Color GetRepresentativeMaterialColor(Material material, Color fallbackColor)
    {
        if (material == null)
            return fallbackColor;

        Color tintColor = GetMaterialTintColor(material, Color.white);
        Texture2D sampledTexture = GetMaterialTexture(material);
        if (TryGetAverageTextureColor(sampledTexture, out Color textureColor))
        {
            Color tintedTextureColor = new Color(
                textureColor.r * tintColor.r,
                textureColor.g * tintColor.g,
                textureColor.b * tintColor.b,
                1f);

            if (tintedTextureColor.maxColorComponent > 0.01f)
                return tintedTextureColor;
        }

        if (tintColor.maxColorComponent > 0.01f)
        {
            tintColor.a = 1f;
            return tintColor;
        }

        return fallbackColor;
    }

    Color GetMaterialTintColor(Material material, Color fallbackColor)
    {
        if (material == null)
            return fallbackColor;

        if (material.HasProperty(BaseColorPropertyId))
            return material.GetColor(BaseColorPropertyId);

        if (material.HasProperty(ColorPropertyId))
            return material.GetColor(ColorPropertyId);

        return fallbackColor;
    }

    Texture2D GetMaterialTexture(Material material)
    {
        if (material == null)
            return null;

        Texture texture = null;
        if (material.HasProperty("_BaseMap"))
            texture = material.GetTexture("_BaseMap");

        if (texture == null && material.HasProperty("_MainTex"))
            texture = material.GetTexture("_MainTex");

        return texture as Texture2D;
    }

    bool TryGetAverageTextureColor(Texture2D texture, out Color averageColor)
    {
        averageColor = Color.white;
        if (texture == null)
            return false;

        try
        {
            const int sampleGrid = 6;
            Color colorSum = Color.black;
            int sampleCount = 0;

            for (int y = 0; y < sampleGrid; y++)
            {
                for (int x = 0; x < sampleGrid; x++)
                {
                    float u = sampleGrid == 1 ? 0.5f : x / (float)(sampleGrid - 1);
                    float v = sampleGrid == 1 ? 0.5f : y / (float)(sampleGrid - 1);
                    colorSum += texture.GetPixelBilinear(u, v);
                    sampleCount++;
                }
            }

            if (sampleCount <= 0)
                return false;

            averageColor = colorSum / sampleCount;
            averageColor.a = 1f;
            return true;
        }
        catch (UnityException)
        {
            return false;
        }
    }

    Color TuneForwardPortalColor(Color baseColor)
    {
        Color.RGBToHSV(baseColor, out float hue, out float saturation, out float value);

        float jitterMagnitude = Mathf.Lerp(0.02f, 0.42f, Mathf.Clamp01(portalNextHueJitter));
        float randomSignedHueOffset = (GetDeterministicColorSeed(hue, saturation, value) * 2f - 1f) * jitterMagnitude;
        float jitteredHue = Mathf.Repeat(hue + randomSignedHueOffset, 1f);
        hue = PushHueAwayFromYellow(jitteredHue, portalYellowAvoidanceBandDegrees, portalYellowAvoidanceStrength);

        saturation = Mathf.Clamp01(Mathf.Max(Mathf.Lerp(saturation, 1f, 0.75f), portalNextMinSaturation));
        value = Mathf.Clamp01(Mathf.Max(Mathf.Lerp(value, 1f, 0.65f), portalNextMinValue));
        return Color.HSVToRGB(hue, saturation, value);
    }

    Color TuneBackwardPortalColor(Color forwardColor)
    {
        Color.RGBToHSV(forwardColor, out float hue, out float saturation, out float value);

        float hueShiftMagnitude = Mathf.Lerp(0.06f, 0.24f, Mathf.Clamp01(portalPreviousHueShift));
        float shiftedHue = Mathf.Repeat(hue + GetBackwardPortalHueShift(hue, saturation, value, hueShiftMagnitude), 1f);
        shiftedHue = PushHueAwayFromYellow(shiftedHue, portalYellowAvoidanceBandDegrees, portalYellowAvoidanceStrength * 1.1f);

        float targetSaturation = Mathf.Clamp01(Mathf.Max(saturation * 0.86f, portalPreviousMinSaturation));
        float targetValue = Mathf.Clamp01(Mathf.Max(value * 0.8f, portalPreviousMinValue));

        Color shiftedVariant = Color.HSVToRGB(
            shiftedHue,
            Mathf.Lerp(saturation, targetSaturation, 0.5f),
            Mathf.Lerp(value, targetValue, 0.6f));
        shiftedVariant.a = 1f;

        Color blendedColor = Color.Lerp(forwardColor, shiftedVariant, 0.55f);
        blendedColor.a = 1f;
        return blendedColor;
    }

    float GetBackwardPortalHueShift(float hue, float saturation, float value, float magnitude)
    {
        float seed = Mathf.Sin((hue * 12.73f + saturation * 4.37f + value * 2.19f + GetDeterministicColorSeed(hue, saturation, value)) * Mathf.PI * 2f);
        float signedShift = seed >= 0f ? magnitude : -magnitude;

        const float yellowHue = 0.15f;
        float currentDistanceFromYellow = Mathf.Abs(Mathf.DeltaAngle(yellowHue * 360f, hue * 360f));
        float candidateHue = Mathf.Repeat(hue + signedShift, 1f);
        float candidateDistanceFromYellow = Mathf.Abs(Mathf.DeltaAngle(yellowHue * 360f, candidateHue * 360f));

        if (currentDistanceFromYellow < portalYellowAvoidanceBandDegrees && candidateDistanceFromYellow < currentDistanceFromYellow)
            signedShift *= -1f;

        return signedShift;
    }

    float GetDeterministicColorSeed(float hue, float saturation, float value)
    {
        int nameHash = 17;
        if (!string.IsNullOrEmpty(articleName))
        {
            for (int i = 0; i < articleName.Length; i++)
                nameHash = nameHash * 31 + articleName[i];
        }

        float baseNoise = Mathf.Sin((nameHash * 0.00013f + hue * 13.17f + saturation * 5.29f + value * 3.91f) * Mathf.PI * 2f);
        return baseNoise * 0.5f + 0.5f;
    }

    float PushHueAwayFromYellow(float hue, float bandDegrees, float strength)
    {
        const float yellowHue = 0.15f;
        float clampedStrength = Mathf.Clamp01(strength);
        float clampedBand = Mathf.Clamp(bandDegrees, 1f, 120f);
        float offsetFromYellow = Mathf.DeltaAngle(yellowHue * 360f, hue * 360f);
        float distanceFromYellow = Mathf.Abs(offsetFromYellow);

        if (distanceFromYellow >= clampedBand || clampedStrength <= 0f)
            return hue;

        float pushDirection = offsetFromYellow >= 0f ? 1f : -1f;
        if (Mathf.Approximately(offsetFromYellow, 0f))
            pushDirection = GetDeterministicColorSeed(hue, 1f, 1f) >= 0.5f ? 1f : -1f;

        float normalizedProximity = 1f - (distanceFromYellow / clampedBand);
        float pushDegrees = normalizedProximity * clampedBand * clampedStrength;
        float pushedOffset = offsetFromYellow + pushDirection * pushDegrees;
        return Mathf.Repeat(yellowHue + pushedOffset / 360f, 1f);
    }

    Color ApplyColorIntensity(Color color)
    {
        Color.RGBToHSV(color, out float hue, out float saturation, out float value);
        float intensity = Mathf.Clamp01(portalColorIntensity);

        // Intensity affects only the perceived vividness/brightness of the portal tint.
        saturation = Mathf.Clamp01(Mathf.Lerp(0.05f, saturation, intensity));
        value = Mathf.Clamp01(Mathf.Lerp(value * 0.55f, value, intensity));

        Color result = Color.HSVToRGB(hue, saturation, value);
        result.a = 1f;
        return result;
    }

    void ApplyPortalColor(GameObject portalRoot, Color color)
    {
        if (portalRoot == null)
            return;

        Renderer[] renderers = portalRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is VFXRenderer)
                continue;

            Material[] materials = renderers[i].materials;
            for (int j = 0; j < materials.Length; j++)
            {
                ApplyColorToMaterial(materials[j], color);
            }
        }

        Light[] portalLights = portalRoot.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < portalLights.Length; i++)
        {
            portalLights[i].color = color;
        }

        PortalVignetteController[] vignetteControllers = portalRoot.GetComponentsInChildren<PortalVignetteController>(true);
        for (int i = 0; i < vignetteControllers.Length; i++)
        {
            vignetteControllers[i].SetVignetteColor(color);
        }

        VisualEffect[] visualEffects = portalRoot.GetComponentsInChildren<VisualEffect>(true);
        Vector4 portalColor = new Vector4(color.r, color.g, color.b, color.a);
        bool appliedToVfxColor = false;
        for (int i = 0; i < visualEffects.Length; i++)
        {
            if (visualEffects[i].HasVector4(PortalAccentColorPropertyId))
            {
                visualEffects[i].SetVector4(PortalAccentColorPropertyId, portalColor);
                appliedToVfxColor = true;
            }

            visualEffects[i].Reinit();
            visualEffects[i].Play();
        }

        if (visualEffects.Length > 0 && !appliedToVfxColor)
        {
            Debug.LogWarning($"[PortalColor] Portal '{portalRoot.name}' has VisualEffect but no exposed '_PortalAccentColor' property. Renderer tint applied only.", portalRoot);
        }
    }

    void ApplyColorToMaterial(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty(BaseColorPropertyId))
            material.SetColor(BaseColorPropertyId, color);

        if (material.HasProperty(ColorPropertyId))
            material.SetColor(ColorPropertyId, color);

        if (material.HasProperty(EmissionColorPropertyId))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionColorPropertyId, color * portalEmissionStrength);
        }
    }

    string ColorToLogString(Color color)
    {
        return $"({color.r:0.00}, {color.g:0.00}, {color.b:0.00})";
    }

    bool IsGenAITexturesEnabled()
    {
        return PlayerPrefs.GetInt(GenAIEnabledKey, 1) == 1;
    }

    public async Task<string> GetArticleAsync(string article)
    {
        ArticleStructure articleData = await WikipediaRuntimeClient.GetArticleDataAsync(article);
        return articleData == null ? null : JsonConvert.SerializeObject(articleData);
    }

    public async Task<string> GetTexturesJsonAsync(string article, string category)
    {
        TexturesStructure textures = await LocalTextureCacheService.GetTextureSetAsync(article, category);
        return textures == null ? null : JsonConvert.SerializeObject(textures);
    }

    public async Task<string> GetInfoboxAsync(string article)
    {
        WikiPageRaw infobox = await WikipediaRuntimeClient.GetInfoboxAsync(article);
        return infobox == null ? null : JsonConvert.SerializeObject(infobox);
    }

    void ApplyTexturesToMaterials(TexturesStructure texturesData)
    {
        byte[] textureByteData = Convert.FromBase64String(texturesData.images.bookcase);
        Texture2D bookshelfTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        bookshelfTexture.LoadImage(textureByteData);
        bookshelfTexture.filterMode = FilterMode.Point;
        bookshelfTexture.Apply(false, false);
        bookshelfMat.mainTexture = bookshelfTexture;

        // wygeneruj z kolorowej tekstury (użyje jasności jako height)
        Texture2D bookcaseNormalTexture = CreateNormalMapFromGrayscale(bookshelfTexture, 2.0f);
        bookshelfMat.SetTexture("_BumpMap", bookcaseNormalTexture);
        bookshelfMat.SetFloat("_BumpScale", 1.0f);
        bookshelfMat.EnableKeyword("_NORMALMAP");
        if (bookshelfMat.HasProperty("_Glossiness")) bookshelfMat.SetFloat("_Glossiness", 0.35f);
        if (bookshelfMat.HasProperty("_Metallic")) bookshelfMat.SetFloat("_Metallic", 0.0f);

        textureByteData = Convert.FromBase64String(texturesData.images.wall);
        Texture2D wallTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        wallTexture.LoadImage(textureByteData);
        wallTexture.filterMode = FilterMode.Point;
        wallTexture.Apply(false, false);
        wallMat.mainTexture = wallTexture;

        textureByteData = Convert.FromBase64String(texturesData.images.floor);
        Texture2D floorTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        floorTexture.LoadImage(textureByteData);
        floorTexture.filterMode = FilterMode.Point;
        floorTexture.Apply(false, false);
        floorMat.mainTexture = floorTexture;

        Texture2D standTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        standTexture.LoadImage(textureByteData);
        standTexture.filterMode = FilterMode.Point;
        standTexture.Apply(false, false);
        standMat.mainTexture = standTexture;

        ApplyRoomMaterials(wallMat, floorMat, bookshelfMat, standMat);
    }

    Texture2D CreateNormalMapFromGrayscale(Texture2D source, float strength = 1.0f)
    {
        int w = source.width;
        int h = source.height;
        Texture2D normal = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] src = source.GetPixels();
        Color[] dst = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float hl = src[y * w + Mathf.Max(x - 1, 0)].grayscale;
                float hr = src[y * w + Mathf.Min(x + 1, w - 1)].grayscale;
                float hd = src[Mathf.Max(y - 1, 0) * w + x].grayscale;
                float hu = src[Mathf.Min(y + 1, h - 1) * w + x].grayscale;

                float dx = (hr - hl) * 0.5f * strength;
                float dy = (hu - hd) * 0.5f * strength;
                Vector3 n = new Vector3(-dx, -dy, 1.0f).normalized;

                dst[i] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        }

        normal.SetPixels(dst);
        normal.Apply();
        normal.wrapMode = source.wrapMode;
        normal.filterMode = source.filterMode;
        return normal;
    }
    void SpawnExtensionsWithBookselfs()
    {
        Vector3 roomBounds = extensionRoom.GetComponent<Renderer>().bounds.size;
        Vector2 roomSize = new Vector2(roomBounds.x, roomBounds.z);
        Vector3 offset = new Vector3(0, 0, roomBounds.z);
        Vector3 nextExtensionPoint = initialRoomPosition.position - offset;
        int bookshelfCount = BookshelfCountForArticle(ArticleData);
        if (bookshelfCount <= 0)
        {
            SpawnExtensionClosure(nextExtensionPoint + offset / 2f);
            return;
        }

        GameObject extension = null;
        Section[] sections = ArticleData.content;
        Section nextSection = sections[0];
        int bookIndex = 0;
        int sectionIndex = 0;

        for (int i = 0; i < bookshelfCount; i++)
        {
            if (i % 6 == 0)
            {
                extension = Instantiate(extensionRoom, nextExtensionPoint, Quaternion.identity, transform);
                extension.name = $"Extension Room {(i / 6) + 1}";
                spawnedExtensions.Add(extension);
                nextExtensionPoint -= offset;
            }
            GameObject bookshelfContainer = new GameObject($"Bookshelf Container {i}");
            bookshelfContainer.transform.parent = extension.transform;

            GameObject currentBookshelf = Instantiate(bookshelf, bookshelfContainer.transform);
            Vector3 bookshelfTranlation = new Vector3(roomSize.x / 4, 0, 0);
            Vector3 bookshelfPos;
            if ((i % 6) < 3) bookshelfPos = new Vector3(-roomSize.x / 4, 0.72f, roomSize.y / 4) + bookshelfTranlation * (i % 3);
            else bookshelfPos = new Vector3(-roomSize.x / 4, 0.72f, -roomSize.y / 4) + bookshelfTranlation * (i % 3);
            bookshelfContainer.transform.localPosition = bookshelfPos;
            currentBookshelf.transform.localPosition = Vector3.zero;

            BookshelfController currentBookshelfController = currentBookshelf.GetComponent<BookshelfController>();
            while (nextSection == null || !SectionHasAnyBooks(nextSection))
            {
                sectionIndex++;
                if (sectionIndex >= sections.Length) break; // Sprawdź czy nie wyszliśmy poza zakres
                nextSection = sections[sectionIndex];
                bookIndex = 0;
            }
            
            // Sprawdź czy nextSection jest prawidłowy przed użyciem
            if (sectionIndex < sections.Length && nextSection != null)
            {
                currentBookshelfController.AddSign(nextSection.name, bookshelfContainer.transform);

                bookIndex = AddBooksForSection(currentBookshelfController, nextSection, bookshelfContainer.transform, bookIndex - 1);
                if (bookIndex == BookCountForSection(nextSection))
                {
                    sectionIndex++;
                    if (sectionIndex < sections.Length) nextSection = sections[sectionIndex];
                    bookIndex = 0;
                }
            }
        }
        SpawnExtensionClosure(nextExtensionPoint + offset / 2f);
    }

    int BookCountForSection(Section section)
    {
        if (section == null) return 0;
        int bookCount = 0;
        Stack<Section> subsections = new Stack<Section>();
        subsections.Push(section);
        while (subsections.Count > 0)
        {
            Section s = subsections.Pop();
            if (s == null) continue;
            if (SectionHasBookContent(s))
                bookCount++;
            if (s.subsections != null)
            {
                foreach (Section sub in s.subsections.Reverse()) subsections.Push(sub);
            }
        }
        return bookCount;
    }

    int BookshelfCountForArticle(ArticleStructure article)
    {
        if (article == null || article.content == null || article.content.Length == 0) return 0;

        int bookshelfCount = 0;
        int safeMaxBooksPerBookshelf = Mathf.Max(1, maxBooksPerBookshelf);
        foreach (Section section in article.content)
        {
            if (section == null) continue;
            int bookCountForSection = BookCountForSection(section);
            if (bookCountForSection <= 0) continue;
            int bookshelfsPerSection = ((bookCountForSection - 1) / safeMaxBooksPerBookshelf) + 1;
            bookshelfCount += bookshelfsPerSection;
        }
        return bookshelfCount;
    }

    //Zwraca liczbę książek na bookshelfie po wyknoaniu funkcji
    int AddBooksForSection(BookshelfController bookshelf, Section initialSection, Transform parent, int lastBookIndex)
    {
        if (bookshelf == null || initialSection == null) return 0;

        Stack<Section> sections = new Stack<Section>();
        sections.Push(initialSection);
        int processedBookCount = 0;
        int addedBooks = 0;
        int safeMaxBooksPerBookshelf = Mathf.Max(1, maxBooksPerBookshelf);
        while (sections.Count > 0)
        {
            Section subsection = sections.Pop();
            if (subsection == null) continue;
            if (SectionHasBookContent(subsection))
            {
                if (processedBookCount > lastBookIndex)
                {
                    bookshelf.AddBook(subsection.name, subsection.content, articleLink, parent, addedBooks);
                    addedBooks++;
                }
                processedBookCount++;
            }
            if (subsection.subsections != null)
            {
                foreach (var s in subsection.subsections.Reverse()) sections.Push(s);
            }
            if (addedBooks == safeMaxBooksPerBookshelf) return processedBookCount;
        }
        return processedBookCount;
    }

    bool SectionHasBookContent(Section section)
    {
        return section != null && !string.IsNullOrWhiteSpace(section.content);
    }

    bool SectionHasAnyBooks(Section section)
    {
        return BookCountForSection(section) > 0;
    }

    public void LogRoom()
    {
        string previousRoomUrl = "https://en.wikipedia.org/wiki/" + PreviousRoom.Replace(" ", "_");
        logger.LogOnRoomExit(articleLink, EnterTime, ExitTime, previousRoomUrl);
    }

    public void ResetRoom()
    {
        InvalidateGeneration();
        HasLoaded = false;
        ArticleData = null;
        CancelInfoboxPopulation(clearContent: true);
        spawnedExtensions.Clear();
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }

    public void SetActivePortalNext(bool isActive)
    {
        portalNext.SetActive(isActive);
    }

    public void SetActivePortalPrevious(bool isActive)
    {
        portalPrevious.SetActive(isActive);
    }

    int BeginGeneration()
    {
        generationVersion++;
        return generationVersion;
    }

    void InvalidateGeneration()
    {
        generationVersion++;
    }

    bool IsGenerationCurrent(int generationId)
    {
        return generationVersion == generationId;
    }

    void CancelInfoboxPopulation(bool firstRoom = false, bool clearContent = false)
    {
        List<InfoboxGenerator> targetGenerators = GetInfoboxGenerators(firstRoom);
        for (int i = 0; i < targetGenerators.Count; i++)
        {
            if (targetGenerators[i] != null)
                targetGenerators[i].CancelPopulation(clearContent);
        }
    }

    List<InfoboxGenerator> GetInfoboxGenerators(bool firstRoom)
    {
        var result = new List<InfoboxGenerator>(2);

        void AddIfUnique(InfoboxGenerator generator)
        {
            if (generator == null) return;
            if (!result.Contains(generator))
                result.Add(generator);
        }

        if (firstRoom)
        {
            AddIfUnique(infoboxGenerator);
            AddIfUnique(secInfoboxGenerator);
        }
        else
        {
            AddIfUnique(secInfoboxGenerator);
            AddIfUnique(infoboxGenerator);
        }

        return result;
    }

    InfoboxGenerator GetContentInfoboxGenerator(bool firstRoom)
    {
        return firstRoom ? infoboxGenerator : secInfoboxGenerator;
    }

    InfoboxGenerator GetInfoboxStatusGenerator(bool firstRoom)
    {
        return firstRoom ? secInfoboxGenerator : infoboxGenerator;
    }

    ArticleStructure NormalizeArticleData(ArticleStructure article)
    {
        if (article == null) return null;
        if (article.content == null)
            article.content = Array.Empty<Section>();
        return article;
    }

    void SpawnExtensionClosure(Vector3 position)
    {
        GameObject closure = Instantiate(extensionRoomClosure, position, Quaternion.identity, transform);
        closure.name = "Extension Room Closure";
    }

    private async Task<List<Dictionary<string, string>>> GetImagesListAsync(string pageName)
    {
        return await WikipediaRuntimeClient.GetImagesAsync(pageName);
    }

    private async Task<Texture2D> GetImageAsTexture(string imageUrl)
    {
        return await SendTextureRequestRateLimitedAsync(
            () =>
            {
                UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
                request.SetRequestHeader("User-Agent", "WikiRoomsProjectUnity/1.0 (Unity client)");
                return request;
            },
            ImageDownloadRequestGate,
            imageDownloadParallelGate,
            GetLastImageDownloadRequestUtc,
            SetLastImageDownloadRequestUtc,
            requestThrottling.imageDownloadMinIntervalSeconds,
            imageUrl);
    }

    private async Task<Texture2D> GetImageAsTextureCachedAsync(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        Task<Texture2D> pendingTask = null;
        bool isOwner = false;

        lock (SharedImageCacheLock)
        {
            if (SharedImageTextureCache.TryGetValue(imageUrl, out Texture2D cached) && cached != null)
                return cached;

            if (!SharedImageInFlightDownloads.TryGetValue(imageUrl, out pendingTask) || pendingTask == null)
            {
                pendingTask = GetImageAsTexture(imageUrl);
                SharedImageInFlightDownloads[imageUrl] = pendingTask;
                isOwner = true;
            }
        }

        try
        {
            Texture2D downloaded = await pendingTask;
            if (downloaded != null && isOwner)
            {
                lock (SharedImageCacheLock)
                {
                    SharedImageTextureCache[imageUrl] = downloaded;

                    if (SharedImageTextureCache.Count > SharedImageCacheMaxEntries)
                    {
                        string oldestKey = SharedImageTextureCache.Keys.FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(oldestKey))
                            SharedImageTextureCache.Remove(oldestKey);
                    }
                }
            }

            return downloaded;
        }
        finally
        {
            if (isOwner)
            {
                lock (SharedImageCacheLock)
                {
                    if (SharedImageInFlightDownloads.TryGetValue(imageUrl, out Task<Texture2D> current) && current == pendingTask)
                        SharedImageInFlightDownloads.Remove(imageUrl);
                }
            }
        }
    }

    private async Task<Texture2D> SendTextureRequestRateLimitedAsync(
        Func<UnityWebRequest> requestFactory,
        SemaphoreSlim gate,
        SemaphoreSlim parallelGate,
        Func<DateTime> getLastRequestUtc,
        Action<DateTime> setLastRequestUtc,
        float minIntervalSeconds,
        string imageUrl)
    {
        if (parallelGate != null)
            await parallelGate.WaitAsync();

        try
        {
        int retriesLimit = Mathf.Max(0, imageDownloads.max429Retries);
        for (int attempt = 0; attempt <= retriesLimit; attempt++)
        {
            int retryDelayMs = 0;
            await gate.WaitAsync();
            try
            {
                await WaitForMinIntervalAsync(minIntervalSeconds, getLastRequestUtc);
                await WaitForImageDownloadCooldownAsync();
                setLastRequestUtc(DateTime.UtcNow);
            }
            finally
            {
                gate.Release();
            }

                using (UnityWebRequest texReq = requestFactory())
                {
                    var texOp = texReq.SendWebRequest();
                    while (!texOp.isDone)
                        await Task.Yield();

                    if (texReq.result == UnityWebRequest.Result.Success)
                    {
                        RegisterImageDownloadSuccess();
                        try
                        {
                            Texture2D tex = DownloadHandlerTexture.GetContent(texReq);
                            return tex;
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"Błąd tworzenia Texture2D z {imageUrl}: {e.Message}");
                            return null;
                        }
                    }

                    if (texReq.responseCode == 429 && attempt < retriesLimit)
                    {
                        retryDelayMs = GetRetryDelayMilliseconds(texReq, attempt);
                        RegisterImageDownload429(retryDelayMs);
                        if (imageDownloads.log429RetryAttempts)
                        {
                            Debug.Log($"Image request got 429 for '{imageUrl}'. Retry {attempt + 1}/{retriesLimit} in {retryDelayMs / 1000f:0.##}s. Extra throttle={GetDynamicImageExtraThrottleSeconds():0.##}s");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to download texture {imageUrl} ({texReq.responseCode}): {texReq.error}");
                        return null;
                    }
                }

            if (retryDelayMs > 0)
                await Task.Delay(retryDelayMs);
        }

        return null;
        }
        finally
        {
            if (parallelGate != null)
                parallelGate.Release();
        }
    }

    void EnsureImageDownloadParallelGate()
    {
        int desiredLimit = Mathf.Max(1, imageDownloads.maxConcurrentDownloads);
        if (imageDownloadParallelGate != null && imageDownloadParallelGateLimit == desiredLimit)
            return;

        imageDownloadParallelGate?.Dispose();
        imageDownloadParallelGate = new SemaphoreSlim(desiredLimit, desiredLimit);
        imageDownloadParallelGateLimit = desiredLimit;
    }

    private async Task WaitForMinIntervalAsync(float minIntervalSeconds, Func<DateTime> getLastRequestUtc)
    {
        if (minIntervalSeconds <= 0f) return;

        DateTime lastRequestUtc = getLastRequestUtc();
        if (lastRequestUtc == DateTime.MinValue) return;

        double elapsedSeconds = (DateTime.UtcNow - lastRequestUtc).TotalSeconds;
        if (elapsedSeconds >= minIntervalSeconds) return;

        int waitMs = Mathf.CeilToInt((minIntervalSeconds - (float)elapsedSeconds) * 1000f);
        if (waitMs > 0)
            await Task.Delay(waitMs);
    }

    private int GetRetryDelayMilliseconds(UnityWebRequest request, int attempt)
    {
        string retryAfterHeader = request.GetResponseHeader("Retry-After");
        if (!string.IsNullOrEmpty(retryAfterHeader))
        {
            if (int.TryParse(retryAfterHeader, out int retryAfterSeconds))
            {
                return Mathf.Max(0, retryAfterSeconds) * 1000;
            }

            if (DateTimeOffset.TryParse(retryAfterHeader, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset retryAfterDate))
            {
                double delayMs = (retryAfterDate.UtcDateTime - DateTime.UtcNow).TotalMilliseconds;
                if (delayMs > 0)
                    return Mathf.CeilToInt((float)delayMs);
            }
        }

        float expDelaySeconds = requestThrottling.retryBaseDelaySeconds * Mathf.Pow(2f, attempt);
        return Mathf.CeilToInt(Mathf.Max(0f, expDelaySeconds) * 1000f);
    }

    private async Task WaitForImageDownloadCooldownAsync()
    {
        float extraThrottle = GetDynamicImageExtraThrottleSeconds();
        if (extraThrottle > 0f)
        {
            int extraMs = Mathf.CeilToInt(extraThrottle * 1000f);
            if (extraMs > 0)
                await Task.Delay(extraMs);
        }

        DateTime cooldownUntilUtc = GetImageDownloadCooldownUntilUtc();
        if (cooldownUntilUtc == DateTime.MinValue) return;

        int waitMs = Mathf.CeilToInt((float)(cooldownUntilUtc - DateTime.UtcNow).TotalMilliseconds);
        if (waitMs > 0)
            await Task.Delay(waitMs);
    }

    private void RegisterImageDownload429(int retryDelayMs)
    {
        DateTime newCooldownUntil = DateTime.UtcNow.AddMilliseconds(Mathf.Max(0, retryDelayMs));
        lock (RateLimitLock)
        {
            if (newCooldownUntil > imageDownloadCooldownUntilUtc)
                imageDownloadCooldownUntilUtc = newCooldownUntil;

            dynamicImageExtraThrottleSeconds = Mathf.Min(
                imageDownloads.maxExtraThrottleSeconds,
                dynamicImageExtraThrottleSeconds + imageDownloads.extraThrottleStepSeconds);
        }
    }

    private void RegisterImageDownloadSuccess()
    {
        lock (RateLimitLock)
        {
            dynamicImageExtraThrottleSeconds = Mathf.Max(0f, dynamicImageExtraThrottleSeconds - imageDownloads.successThrottleDecaySeconds);
        }
    }

    private void EnsureSettings()
    {
        if (requestThrottling == null)
            requestThrottling = new RequestThrottlingSettings();
        if (imageDownloads == null)
            imageDownloads = new ImageDownloadSettings();
    }

    private DateTime GetImageDownloadCooldownUntilUtc()
    {
        lock (RateLimitLock) return imageDownloadCooldownUntilUtc;
    }

    private float GetDynamicImageExtraThrottleSeconds()
    {
        lock (RateLimitLock) return dynamicImageExtraThrottleSeconds;
    }

    private void ResetImageDownloadAdaptiveState()
    {
        lock (RateLimitLock)
        {
            imageDownloadCooldownUntilUtc = DateTime.MinValue;
            dynamicImageExtraThrottleSeconds = 0f;
        }
    }

    private DateTime GetLastImageDownloadRequestUtc()
    {
        lock (RateLimitLock) return lastImageDownloadRequestUtc;
    }

    private void SetLastImageDownloadRequestUtc(DateTime value)
    {
        lock (RateLimitLock) lastImageDownloadRequestUtc = value;
    }


    // Skaluje podany quad / plane tak, aby zachować proporcje tekstury.
    // Zakładamy, że quad ma jednostkowy proporcjonalny rozmiar w localScale (height bazowy na localScale.y).
    void SetImageQuadScale(GameObject quad, Texture2D tex, float baseHeight)
    {
        if (quad == null || tex == null)
        {
            Debug.LogWarning("[SetImageQuadScale] Brak quad lub tekstury.");
            return;
        }

        Debug.Log($"{tex.width}, {tex.height}, {(float)tex.width / tex.height}");

        float texAspect = (float)tex.width / tex.height;
        // Pobierz aktualny localScale
        Vector3 ls = new Vector3(baseHeight * texAspect, baseHeight , 1);
        quad.transform.localScale = ls;

        Debug.Log($"[SetImageQuadScale] '{quad.name}' po localScale={quad.transform.localScale}");
    }

    void SpawnImageHolder(Vector3 pos, Vector3 rotation, Texture2D tex, string caption, string sourceUrl, Transform extension, float baseHeight)
    {
        GameObject currentImageHolder = Instantiate(imageHolder, extension);
        currentImageHolder.transform.localPosition = pos;
        currentImageHolder.transform.localRotation = Quaternion.Euler(rotation);

        Material currentImageMat = new Material(sampleImageMat);
        tex.filterMode = FilterMode.Point; // ostry pixel-art
        tex.wrapMode = TextureWrapMode.Clamp;
        // przypisz teksturę do materiału (bez skalowania materiału)
        currentImageMat.mainTexture = tex;
        currentImageHolder.GetComponent<MeshRenderer>().material = currentImageMat;

        // przypisz caption do komponentu
        var imgComp = currentImageHolder.GetComponent<ImageInteraction>();
        if (imgComp == null) imgComp = currentImageHolder.AddComponent<ImageInteraction>();
        imgComp.caption = caption ?? string.Empty;
        imgComp.texture = tex;
        imgComp.imageUrl = sourceUrl;




        // skaluj GameObject ImageWikiQuad, żeby zachować aspect ratio tekstury (nie skalujemy materiału)
        SetImageQuadScale(currentImageHolder, tex, baseHeight);
    }

    ImageSizeClass ClassifyImage(Texture2D tex)
    {
        if (tex == null) return ImageSizeClass.Medium;
        int pixels = tex.width * tex.height;
        if (pixels <= smallMaxPixels) return ImageSizeClass.Small;
        if (pixels <= mediumMaxPixels) return ImageSizeClass.Medium;
        return ImageSizeClass.Large;
    }

    Texture2D ProcessTransparency(Texture2D tex)
    {
        if (!fillTransparentWithWhite || tex == null) return tex;

        try
        {
            Color32[] pixels = tex.GetPixels32();
            if (pixels == null || pixels.Length == 0) return tex;

            int transparentCount = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a <= (byte)(transparentAlphaThreshold * 255f))
                    transparentCount++;
            }

            float ratio = (float)transparentCount / pixels.Length;
            if (ratio < transparentPixelRatioThreshold) return tex;

            var flattened = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            Color32 white = new Color32(255, 255, 255, 255);
            byte alphaCut = (byte)(transparentAlphaThreshold * 255f);

            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a <= alphaCut)
                {
                    pixels[i] = white;
                }
                else
                {
                    pixels[i].a = 255;
                }
            }

            flattened.SetPixels32(pixels);
            flattened.Apply(false, false);
            flattened.wrapMode = tex.wrapMode;
            flattened.filterMode = tex.filterMode;
            return flattened;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ProcessTransparency] Nie udało się przetworzyć tekstury: {e.Message}");
            return tex;
        }
    }

    float GetBaseHeightForClass(ImageSizeClass sizeClass)
    {
        switch (sizeClass)
        {
            case ImageSizeClass.Small:
                return smallImageHeight;
            case ImageSizeClass.Large:
                return largeImageHeight;
            default:
                return mediumImageHeight;
        }
    }

    bool AreAspectSimilar(Texture2D a, Texture2D b)
    {
        if (a == null || b == null) return false;
        float ra = a.height == 0 ? 1f : (float)a.width / a.height;
        float rb = b.height == 0 ? 1f : (float)b.width / b.height;
        float max = Mathf.Max(ra, rb);
        if (max <= 0f) return false;
        float diff = Mathf.Abs(ra - rb) / max;
        return diff <= twoImageAspectSimilarityThreshold;
    }

    List<ImageSlot> BuildImageSlots()
    {
        var slots = new List<ImageSlot>();
        if (spawnedExtensions == null || spawnedExtensions.Count == 0) return slots;

        foreach (var extension in spawnedExtensions)
        {
            if (extension == null) continue;
            Renderer renderer = extension.GetComponent<Renderer>();
            if (renderer == null) continue;

            Vector3 roomBounds = renderer.bounds.size;
            Vector2 roomSize = new Vector2(roomBounds.x, roomBounds.z);

            Vector3[] imagePositions = new Vector3[]
            {
                new Vector3(roomSize.x / 2 - 0.05f, 2f, roomSize.y / 4),
                new Vector3(-roomSize.x / 2 + 0.05f, 2f, roomSize.y / 4),
                new Vector3(roomSize.x / 2 - 0.05f, 2f, -roomSize.y / 4),
                new Vector3(-roomSize.x / 2 + 0.05f, 2f, -roomSize.y / 4)
            };

            Vector3[] imageRotations = new Vector3[]
            {
                new Vector3(0, 90, 0),
                new Vector3(0, -90, 0),
                new Vector3(0, 90, 0),
                new Vector3(0, -90, 0)
            };

            for (int i = 0; i < 4; i++)
            {
                slots.Add(new ImageSlot
                {
                    parent = extension.transform,
                    localPosition = imagePositions[i],
                    localRotation = imageRotations[i]
                });
            }
        }

        return slots;
    }

    List<int> BuildGroupSizes(int imageCount, int slotCount, int maxPerSlot)
    {
        var sizes = new List<int>(new int[slotCount]);
        if (imageCount <= 0 || slotCount <= 0) return sizes;

        if (imageCount <= slotCount)
        {
            for (int i = 0; i < imageCount; i++) sizes[i] = 1;
            return sizes;
        }

        for (int i = 0; i < slotCount; i++) sizes[i] = 1;
        int remaining = imageCount - slotCount;
        int idx = 0;
        while (remaining > 0)
        {
            if (sizes[idx] < maxPerSlot)
            {
                sizes[idx]++;
                remaining--;
            }
            idx = (idx + 1) % slotCount;
        }
        return sizes;
    }

    void SpawnImageCluster(ImageSlot slot, List<ImagePayload> payloads)
    {
        if (payloads == null || payloads.Count == 0 || slot.parent == null) return;

        GameObject anchor = new GameObject("ImageSlot");
        anchor.transform.SetParent(slot.parent, false);
        anchor.transform.localPosition = slot.localPosition;
        anchor.transform.localRotation = Quaternion.Euler(slot.localRotation);
        anchor.transform.localScale = Vector3.one;

        int count = payloads.Count;
        int rows = count == 1 ? 1 : 2;
        int cols = count == 1 ? 1 : (count == 2 ? 1 : 2);
        bool applyDiagonalOffset = count == 2 && AreAspectSimilar(payloads[0].texture, payloads[1].texture);

        float cellHeight = (clusterHeight / rows) - (clusterPadding * 2);
        float cellWidth = (clusterWidth / cols) - (clusterPadding * 2);

        float startY = (clusterHeight / 2f) - (cellHeight / 2f) - clusterPadding;
        float startX = -(clusterWidth / 2f) + (cellWidth / 2f) + clusterPadding;

        for (int i = 0; i < payloads.Count; i++)
        {
            int r = (cols == 1) ? i : i / cols;
            int c = (cols == 1) ? 0 : i % cols;

            Vector3 localPos = new Vector3(startX + c * (cellWidth + clusterPadding * 2), startY - r * (cellHeight + clusterPadding * 2), 0f);
            if (count == 3 && cols == 2 && r == 1)
            {
                localPos.x = 0f;
            }
            if (applyDiagonalOffset)
            {
                float sign = (i == 0) ? -1f : 1f;
                localPos.y -= sign * twoImageDiagonalOffsetY;
                localPos.x += sign * twoImageDiagonalOffsetX;
            }

            var payload = payloads[i];
            float baseHeight = GetBaseHeightForClass(payload.sizeClass);
            float aspect = payload.texture != null && payload.texture.height != 0 ? (float)payload.texture.width / payload.texture.height : 1f;
            float desiredWidth = baseHeight * aspect;
            float scaleFactor = 1f;
            if (desiredWidth > cellWidth || baseHeight > cellHeight)
            {
                float widthFactor = desiredWidth > 0 ? cellWidth / desiredWidth : 1f;
                float heightFactor = baseHeight > 0 ? cellHeight / baseHeight : 1f;
                scaleFactor = Mathf.Min(widthFactor, heightFactor);
            }
            float finalHeight = baseHeight * scaleFactor;

            SpawnImageHolder(localPos, Vector3.zero, payload.texture, payload.caption, payload.sourceUrl, anchor.transform, finalHeight);
        }
    }
}
