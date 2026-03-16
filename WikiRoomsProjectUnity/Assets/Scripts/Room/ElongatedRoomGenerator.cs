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
using TMPro; 

public class ElongatedRoomGenerator : MonoBehaviour
{
    const string GenAIEnabledKey = "GenAITexturesEnabled";

    [Serializable]
    public class RequestThrottlingSettings
    {
        [Min(0f)] public float infoboxMinIntervalSeconds = 0.6f;
        [Min(0f)] public float imagesListMinIntervalSeconds = 0.5f;
        [Min(0f)] public float imageDownloadMinIntervalSeconds = 0.2f;
        [Min(0)] public int textMax429Retries = 3;
        [Min(0f)] public float retryBaseDelaySeconds = 0.75f;
    }

    [Serializable]
    public class ImageDownloadSettings
    {
        [Min(1)] public int maxConcurrentDownloads = 4;
        [Min(0)] public int max429Retries = 4;
        [Min(0f)] public float extraThrottleStepSeconds = 0.15f;
        [Min(0f)] public float maxExtraThrottleSeconds = 1.5f;
        [Min(0f)] public float successThrottleDecaySeconds = 0.05f;
    }

    static readonly SemaphoreSlim InfoboxRequestGate = new SemaphoreSlim(1, 1);
    static readonly SemaphoreSlim ImagesListRequestGate = new SemaphoreSlim(1, 1);
    static readonly SemaphoreSlim ImageDownloadRequestGate = new SemaphoreSlim(1, 1);
    static readonly object RateLimitLock = new object();
    static DateTime lastInfoboxRequestUtc = DateTime.MinValue;
    static DateTime lastImagesListRequestUtc = DateTime.MinValue;
    static DateTime lastImageDownloadRequestUtc = DateTime.MinValue;
    static DateTime imageDownloadCooldownUntilUtc = DateTime.MinValue;
    static float dynamicImageExtraThrottleSeconds = 0f;

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

    [Header("Request throttling")]
    public RequestThrottlingSettings requestThrottling = new RequestThrottlingSettings();

    [Header("Image download")]
    public ImageDownloadSettings imageDownloads = new ImageDownloadSettings();

    [Header("Loading screen")]
    [Range(0f, 1f)] public float imageProgressToHideLoadingScreen = 0.5f;

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
    [HideInInspector] public ArticleStructure ArticleData;

    string articleLink;
    RoomsController roomsController;

    [Header("Article title in Room")]
    public TextMeshPro articleTitleText; // przypnij TextMeshProUGUI z Canvasu

    List<GameObject> spawnedExtensions = new List<GameObject>();
    string auth_header;
    public bool HasLoaded = false;
    SemaphoreSlim imageDownloadParallelGate;
    int imageDownloadParallelGateLimit = -1;
    
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
        HasLoaded = false;
        if(roomsController.elongatedRoom == this)
        {
            loadingScreen.SetActive(true);
            // Zresetuj animację loading screen
            var loadingMotion = loadingScreen.GetComponentInChildren<LoadingPuzzleMotion>();
            if (loadingMotion != null) loadingMotion.ResetAnimation();
        }
        auth_header = "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(backendConfig.username + ":" + backendConfig.password));
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
        bool flowControl = await HandleArticle(articleName);
        if (!flowControl) return;

        Debug.Log($"Loaded {articleName} data");


        if (onClick) 
        {
            roomsController.elongatedRoom.SetActivePortalNext(true);
        }

        // Bezpieczne użycie category
        Task textureTask = HandleTextures(articleName, roomsController);
        Task infoboxTask = HandleInfobox(articleName);
        await Task.WhenAll(textureTask, infoboxTask);

        SpawnExtensionsWithBookselfs();
        HasLoaded = true;
        await HandleImagesProgressively(articleName, hideWhenProgressReached: true);
        HideLoadingScreenIfNeeded();
        Debug.Log($"Loaded {articleName} successfully.");
    }

    private async Task HandleImagesProgressively(string articleName, bool hideWhenProgressReached)
    {
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

        if (imagesLinks.Count == 0)
        {
            if (hideWhenProgressReached)
                HideLoadingScreenIfNeeded();
            return;
        }

        int usableCount = Mathf.Min(imagesLinks.Count, slots.Count * 4);
        int loadedImagesCount = 0;
        int imagesTargetToHide = Mathf.CeilToInt(Mathf.Max(0f, Mathf.Min(1f, imageProgressToHideLoadingScreen)) * usableCount);
        var downloadedForCache = new List<RoomsController.CachedImageData>(usableCount);

        if (hideWhenProgressReached && imagesTargetToHide <= 0)
            HideLoadingScreenIfNeeded();

        var groupSizes = BuildGroupSizes(usableCount, slots.Count, 4);

        int imageIndex = 0;
        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            int count = groupSizes[slotIndex];
            if (count <= 0) continue;

            var tasksForSlot = new List<Task<ImagePayload?>>(count);
            for (int k = 0; k < count && imageIndex < usableCount; k++)
            {
                tasksForSlot.Add(DownloadImagePayloadAsync(imagesLinks[imageIndex]));
                imageIndex++;
            }

            SlotClusterResult slotResult = await CollectSlotPayloadsAsync(slotIndex, tasksForSlot);
            if (slotResult.payloads != null && slotResult.payloads.Count > 0)
            {
                SpawnImageCluster(slots[slotResult.slotIndex], slotResult.payloads);
                loadedImagesCount += slotResult.payloads.Count;
                AddPayloadsToCacheBuffer(downloadedForCache, slotResult.payloads);

                if (hideWhenProgressReached && loadingScreen != null && loadingScreen.activeSelf && loadedImagesCount >= imagesTargetToHide)
                {
                    HideLoadingScreenIfNeeded();
                }
            }
        }

        if (roomsController != null && downloadedForCache.Count > 0)
        {
            roomsController.CacheImages(articleName, downloadedForCache);
        }

    }

    private void SpawnCachedImages(List<RoomsController.CachedImageData> cachedImages, List<ImageSlot> slots)
    {
        if (cachedImages == null || cachedImages.Count == 0 || slots == null || slots.Count == 0)
            return;

        int usableCount = Mathf.Min(cachedImages.Count, slots.Count * 4);
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

        var groupSizes = BuildGroupSizes(payloads.Count, slots.Count, 4);
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
        Texture2D tex = await GetImageAsTexture(imgUrl);
        if (tex == null) return null;

        return new ImagePayload
        {
            texture = ProcessTransparency(tex),
            caption = caption,
            sizeClass = ClassifyImage(tex)
        };
    }


    private async Task HandleInfobox(string articleName, bool firstRoom = false)
    {
        string infoboxJson = await GetInfoboxAsync(articleName);
        if (string.IsNullOrEmpty(infoboxJson))
        {
            Debug.Log("Failed to retrieve infobox data.");
            if(firstRoom)
            {
                infoboxGenerator.HasFailed = true;
            }
            else
            {
                secInfoboxGenerator.HasFailed = true;
            }
        }
        else
        {
            WikiPageRaw infoboxData = InfoboxParser.Parse(infoboxJson);
            if (infoboxData.infobox == null)            {
                Debug.Log("Infobox parsing returned null.");
                if(firstRoom)
                {
                    secInfoboxGenerator.HasFailed = true;
                    await infoboxGenerator.PopulateUI(infoboxData);
                }
                else
                {
                    infoboxGenerator.HasFailed = true;
                    await secInfoboxGenerator.PopulateUI(infoboxData);
                }
                return;
            }
            if(infoboxData.infobox.Count == 0)
            {
                Debug.Log("Infobox parsing returned empty data.");
                if(firstRoom)
                {
                    secInfoboxGenerator.HasFailed = true;
                    await infoboxGenerator.PopulateUI(infoboxData);
                }
                else
                {
                    infoboxGenerator.HasFailed = true;
                    await secInfoboxGenerator.PopulateUI(infoboxData);

                }
                return;
            }
            if (firstRoom)
            {
                secInfoboxGenerator.HasFailed = false;
                await infoboxGenerator.PopulateUI(infoboxData);
            }
            else
            {
                infoboxGenerator.HasFailed = false;
                await secInfoboxGenerator.PopulateUI(infoboxData);
            }    
        }
    }

    private async Task HandleTextures(string articleName, RoomsController roomsController)
    {
        if (!IsGenAITexturesEnabled())
        {
            Debug.Log("GenAI textures disabled - using default materials.");
            SetDefaultMaterials();
            return;
        }

        string category = string.IsNullOrEmpty(ArticleData?.category) ? "default" : ArticleData.category;
        Debug.Log($"Waiting for {category} textures...");
        TexturesStructure cachedTextures = roomsController.GetCachedTextures(articleName);
        if (cachedTextures != null)
        {
            Debug.Log("Using cached textures.");
            ApplyTexturesToMaterials(cachedTextures);
        }
        else
        {
            string texturesJson = await GetTexturesJsonAsync(articleName, ArticleData.category);
            TexturesStructure texturesData;
            if (string.IsNullOrEmpty(texturesJson)) Debug.LogWarning("Failed to retrieve textures data.");
            else
            {
                texturesData = JsonConvert.DeserializeObject<TexturesStructure>(texturesJson);
                roomsController.CacheTextures(articleName, texturesData);
                ApplyTexturesToMaterials(texturesData);
            }
        }
    }

    private async Task<bool> HandleArticle(string articleName)
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
            ArticleData = cachedArticle;
        }
        else
        {
            string json = await GetArticleAsync(articleName);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("Failed to retrieve article data.");
                return false;
            }
            ArticleData = JsonConvert.DeserializeObject<ArticleStructure>(json);
            if (ArticleData == null)
            {
                Debug.LogError("Article deserialization returned null.");
                return false;
            }
            if (roomsController != null)
                roomsController.CacheArticle(articleName, ArticleData);
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

    bool IsGenAITexturesEnabled()
    {
        return PlayerPrefs.GetInt(GenAIEnabledKey, 1) == 1;
    }

    public async Task<string> GetArticleAsync(string article)
    {
        string encodedArticle = UnityWebRequest.EscapeURL(article);
        string url = $"{backendConfig.baseURL}/article?article={encodedArticle}&category_strategy=api";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("accept", "application/json");
            request.SetRequestHeader("Authorization", auth_header);

            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                return request.downloadHandler.text;
            }
            else
            {
                Debug.LogError("Request error: " + request.error);
                return null;
            }
        }
    }

    public async Task<string> GetTexturesJsonAsync(string article, string category)
    {
        string encodedArticle = UnityWebRequest.EscapeURL(article);
        string encodedCategory = UnityWebRequest.EscapeURL(category);
        string url = $"{backendConfig.baseURL}/cache/get_cached_texture?article={encodedArticle}&category={encodedCategory}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("accept", "application/json");
            request.SetRequestHeader("Authorization", auth_header);

            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                return request.downloadHandler.text;
            }
            else
            {
                Debug.LogError("Request error (textures): " + request.error);
                return null;
            }
        }
    }

    public async Task<string> GetInfoboxAsync(string article)
    {
        string encodedArticle = UnityWebRequest.EscapeURL(article);
        string url = $"{backendConfig.baseURL}/scraping/infobox?page_name={encodedArticle}";

        return await SendTextRequestRateLimitedAsync(
            () =>
            {
                UnityWebRequest request = UnityWebRequest.Get(url);
                request.SetRequestHeader("accept", "application/json");
                request.SetRequestHeader("Authorization", auth_header);
                return request;
            },
            InfoboxRequestGate,
            GetLastInfoboxRequestUtc,
            SetLastInfoboxRequestUtc,
            requestThrottling.infoboxMinIntervalSeconds,
            "infobox");
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
        GameObject extension = null;
        Section nextSection = ArticleData.content[0];
        int bookIndex = 0;
        int sectionIndex = 0;

        for (int i = 0; i < BookshelfCountForArticle(ArticleData); i++)
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
            while (nextSection.content == null && nextSection.subsections == null)
            {
                sectionIndex++;
                if (sectionIndex >= ArticleData.content.Length) break; // Sprawdź czy nie wyszliśmy poza zakres
                nextSection = ArticleData.content[sectionIndex];
                bookIndex = 0;
            }
            
            // Sprawdź czy nextSection jest prawidłowy przed użyciem
            if (sectionIndex < ArticleData.content.Length && nextSection != null)
            {
                currentBookshelfController.AddSign(nextSection.name, bookshelfContainer.transform);

                bookIndex = AddBooksForSection(currentBookshelfController, nextSection, bookshelfContainer.transform, bookIndex - 1);
                if (bookIndex == BookCountForSection(nextSection))
                {
                    sectionIndex++;
                    if (sectionIndex < ArticleData.content.Length) nextSection = ArticleData.content[sectionIndex];
                    bookIndex = 0;
                }
            }
        }
        nextExtensionPoint += offset / 2;
        extension = Instantiate(extensionRoomClosure, nextExtensionPoint, Quaternion.identity, transform);
        extension.name = "Extension Room Closure";
    }

    int BookCountForSection(Section section)
    {
        int bookCount = 0;
        if (string.IsNullOrEmpty(section.content) && section.subsections == null) return 0;
        Stack<Section> subsections = new Stack<Section>();
        subsections.Push(section);
        while (subsections.Count > 0)
        {
            Section s = subsections.Pop();
            if (string.IsNullOrEmpty(s.content) && s.subsections == null) continue;
            if (s.subsections != null)
            {
                foreach (Section sub in s.subsections) subsections.Push(sub);
            }
            bookCount++;
        }
        return bookCount;
    }

    int BookshelfCountForArticle(ArticleStructure article)
    {
        int bookshelfCount = 0;
        foreach (Section section in article.content)
        {
            if (string.IsNullOrEmpty(section.content) && section.subsections == null) continue;
            int bookshelfsPerSection = ((BookCountForSection(section) - 1) / maxBooksPerBookshelf) + 1;
            bookshelfCount += bookshelfsPerSection;
        }
        return bookshelfCount;
    }

    //Zwraca liczbę książek na bookshelfie po wyknoaniu funkcji
    int AddBooksForSection(BookshelfController bookshelf, Section initialSection, Transform parent, int lastBookIndex)
    {
        Stack<Section> sections = new Stack<Section>();
        sections.Push(initialSection);
        int i = 0;
        int addedBooks = 0;
        while (sections.Count > 0)
        {
            Section subsection = sections.Pop();
            if (subsection.content != null)
            {
                if (i > lastBookIndex)
                {
                    bookshelf.AddBook(subsection.name, subsection.content, articleLink, parent, addedBooks);
                    addedBooks++;
                }
            }
            if (subsection.subsections != null)
            {
                foreach (var s in subsection.subsections.Reverse()) sections.Push(s);
            }
            if (subsection.subsections == null && subsection.content == null) i--;
            i++;
            if (addedBooks == maxBooksPerBookshelf) return i;
        }
        return i;
    }

    public void LogRoom()
    {
        string previousRoomUrl = "https://en.wikipedia.org/wiki/" + PreviousRoom.Replace(" ", "_");
        logger.LogOnRoomExit(articleLink, EnterTime, ExitTime, previousRoomUrl);
    }

    public void ResetRoom()
    {
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

    private async Task<List<Dictionary<string, string>>> GetImagesListAsync(string pageName)
    {
        List<Dictionary<string, string>> imagesList = new List<Dictionary<string, string>>();
        string url = $"{backendConfig.baseURL}/images/generator?page_name={UnityWebRequest.EscapeURL(pageName)}";

        string json = await SendTextRequestRateLimitedAsync(
            () =>
            {
                UnityWebRequest request = UnityWebRequest.Get(url);
                request.SetRequestHeader("Authorization", auth_header);
                return request;
            },
            ImagesListRequestGate,
            GetLastImagesListRequestUtc,
            SetLastImagesListRequestUtc,
            requestThrottling.imagesListMinIntervalSeconds,
            "images-list");

        if (string.IsNullOrEmpty(json))
        {
            return imagesList;
        }

        ImagesResponse resp = null;
        try
        {
            resp = JsonConvert.DeserializeObject<ImagesResponse>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"JSON parse error: {e.Message}");
            return imagesList;
        }

        imagesList = resp?.GetImagesList() ?? new List<Dictionary<string, string>>();
        return imagesList;
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

    private async Task<string> SendTextRequestRateLimitedAsync(
        Func<UnityWebRequest> requestFactory,
        SemaphoreSlim gate,
        Func<DateTime> getLastRequestUtc,
        Action<DateTime> setLastRequestUtc,
        float minIntervalSeconds,
        string requestName)
    {
        int retriesLimit = Mathf.Max(0, requestThrottling.textMax429Retries);
        for (int attempt = 0; attempt <= retriesLimit; attempt++)
        {   
            int retryDelayMs = 0;
            await gate.WaitAsync();
            try
            {
                await WaitForMinIntervalAsync(minIntervalSeconds, getLastRequestUtc);
                setLastRequestUtc(DateTime.UtcNow);

                using (UnityWebRequest request = requestFactory())
                {
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Yield();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        return request.downloadHandler.text;
                    }

                    if (request.responseCode == 429 && attempt < retriesLimit)
                    {
                        retryDelayMs = GetRetryDelayMilliseconds(request, attempt);
                        Debug.LogWarning($"Request '{requestName}' got 429. Retry {attempt + 1}/{retriesLimit} in {retryDelayMs / 1000f:0.##}s.");
                    }
                    else
                    {
                        Debug.LogWarning($"Request '{requestName}' failed ({request.responseCode}): {request.error}");
                        return null;
                    }
                }
            }
            finally
            {
                gate.Release();
            }

            if (retryDelayMs > 0)
                await Task.Delay(retryDelayMs);
        }

        return null;
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
                        Debug.LogWarning($"Image request got 429 for '{imageUrl}'. Retry {attempt + 1}/{retriesLimit} in {retryDelayMs / 1000f:0.##}s. Extra throttle={GetDynamicImageExtraThrottleSeconds():0.##}s");
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

    private DateTime GetLastInfoboxRequestUtc()
    {
        lock (RateLimitLock) return lastInfoboxRequestUtc;
    }

    private void SetLastInfoboxRequestUtc(DateTime value)
    {
        lock (RateLimitLock) lastInfoboxRequestUtc = value;
    }

    private DateTime GetLastImagesListRequestUtc()
    {
        lock (RateLimitLock) return lastImagesListRequestUtc;
    }

    private void SetLastImagesListRequestUtc(DateTime value)
    {
        lock (RateLimitLock) lastImagesListRequestUtc = value;
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

    void SpawnImageHolder(Vector3 pos, Vector3 rotation, Texture2D tex, string caption, Transform extension, float baseHeight)
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

            SpawnImageHolder(localPos, Vector3.zero, payload.texture, payload.caption, anchor.transform, finalHeight);
        }
    }
}
