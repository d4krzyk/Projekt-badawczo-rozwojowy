using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using TMPro; 

public class ElongatedRoomGenerator : MonoBehaviour
{
    const string GenAIEnabledKey = "GenAITexturesEnabled";

    public GameObject spawnRoom, extensionRoom, extensionRoomClosure, bookshelf, imageHolder;
    public Transform initialRoomPosition;
    public Material sampleImageMat; 
    [Header("Room materials")]
    public Material bookshelfMat;
    public Material floorMat;
    public Material wallMat;
    [Header("Default materials")]
    public Material defBookcaseMat;
    public Material defFloorMat;
    public Material defWallMat;
    [Space]
    public string articleName;
    public int maxBooksPerBookshelf;
    public Logger logger;
    public GameObject portalNext;
    public GameObject portalPrevious;
    public InfoboxGenerator infoboxGenerator;
    public BackendConfig backendConfig;
    public GameObject loadingScreen;

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
    public float clusterPadding = 0.05f;
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

    // wynik pobierania obrazów
    public class ImagesResult
    {
        public List<Texture2D> textures;
        public List<string> captions;
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
    
    public void Awake()
    {
        GameController gameController = FindAnyObjectByType<GameController>();
        if(gameController != null) articleName = gameController.ArticleName;
    }

    public async void GenerateRoom(string articleName, RoomsController roomsController)
    {
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

        // Bezpieczne użycie category
        Task textureTask = HandleTextures(articleName, roomsController);
        Task infoboxTask = HandleInfobox(articleName);
        await Task.WhenAll(textureTask, infoboxTask);

        SpawnExtensionsWithBookselfs();
        loadingScreen.SetActive(false);
        HasLoaded = true;
        await HandleImagesOneAtATime(articleName);
        Debug.Log($"Loaded {articleName} successfully.");
    }

    private async Task HandleImagesAllAtOnce(string articleName)
    {
        List<Texture2D> wikiImages;
        List<string> wikiImageCaptions;
        try
        {
            var imagesResult = await GetImagesAsTextures(articleName);
            wikiImages = imagesResult?.textures ?? new List<Texture2D>();
            wikiImageCaptions = imagesResult?.captions ?? new List<string>();
            // teraz masz listę Texture2D w wikiImages oraz odpowiadające markdowny w wikiImageCaptions
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Błąd pobierania obrazów dla {articleName}: {ex.Message}");
            wikiImages = new List<Texture2D>();
            wikiImageCaptions = new List<string>();
        }
        var slots = BuildImageSlots();
        if (slots.Count == 0 || wikiImages.Count == 0) return;

        int usableCount = Mathf.Min(wikiImages.Count, slots.Count * 4);
        var groupSizes = BuildGroupSizes(usableCount, slots.Count, 4);
        int imageIndex = 0;

        for (int s = 0; s < slots.Count; s++)
        {
            int count = groupSizes[s];
            if (count <= 0) continue;

            var payloads = new List<ImagePayload>();
            for (int k = 0; k < count && imageIndex < usableCount; k++)
            {
                Texture2D tex = wikiImages[imageIndex];
                string caption = imageIndex < wikiImageCaptions.Count ? wikiImageCaptions[imageIndex] : null;
                if (tex != null)
                {
                    payloads.Add(new ImagePayload
                    {
                        texture = ProcessTransparency(tex),
                        caption = caption,
                        sizeClass = ClassifyImage(tex)
                    });
                }
                imageIndex++;
            }

            SpawnImageCluster(slots[s], payloads);
        }

    }

    private async Task HandleImagesOneAtATime(string articleName)
    {
        var imagesLinks = new List<Dictionary<string, string>>();
        try
        {
            imagesLinks = await GetImagesListAsync(articleName);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Błąd pobierania obrazów dla {articleName}: {ex.Message}");
        }

        var slots = BuildImageSlots();
        if (slots.Count == 0 || imagesLinks.Count == 0) return;

        int usableCount = Mathf.Min(imagesLinks.Count, slots.Count * 4);
        var groupSizes = BuildGroupSizes(usableCount, slots.Count, 4);
        int imageIndex = 0;

        for (int s = 0; s < slots.Count; s++)
        {
            int count = groupSizes[s];
            if (count <= 0) continue;

            var payloads = new List<ImagePayload>();
            for (int k = 0; k < count && imageIndex < usableCount; k++)
            {
                var imgObj = imagesLinks[imageIndex];
                imageIndex++;
                if (imgObj == null || imgObj.Count == 0) continue;
                var kvp = imgObj.First();
                string imgUrl = kvp.Key;
                string caption = kvp.Value ?? "[no caption]";

                Texture2D tex = await GetImageAsTexture(imgUrl);
                if (tex == null) continue;

                payloads.Add(new ImagePayload
                {
                    texture = ProcessTransparency(tex),
                    caption = caption,
                    sizeClass = ClassifyImage(tex)
                });
            }

            SpawnImageCluster(slots[s], payloads);
        }

    }


    private async Task HandleInfobox(string articleName)
    {
        string infoboxJson = await GetInfoboxAsync(articleName);
        if (string.IsNullOrEmpty(infoboxJson))
        {
            Debug.LogError("Failed to retrieve infobox data.");
        }
        else
        {
            WikiPageRaw infoboxData = InfoboxParser.Parse(infoboxJson);
            await infoboxGenerator.PopulateUI(infoboxData);
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
            string texturesJson = await GetTexturesJsonAsync(ArticleData.category);
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
        if (this.roomsController != null)
        {
            cachedArticle = this.roomsController.GetCachedArticle(articleName);
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
            if (this.roomsController != null)
                this.roomsController.CacheArticle(articleName, ArticleData);
        }

        return true;
    }

    void SetDefaultMaterials()
    {
        spawnRoom.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { defWallMat, defFloorMat, defFloorMat });
        extensionRoom.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { defWallMat, defFloorMat });
        extensionRoomClosure.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { defWallMat});
        bookshelf.GetComponent<MeshRenderer>().sharedMaterial = defBookcaseMat;
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

    public async Task<string> GetTexturesJsonAsync(string category)
    {
        string url = $"http://localhost:8000/gen2DTextures";
        string requestBody = "{\"category\": \"" + category + "\"}";

        using (UnityWebRequest request = UnityWebRequest.Post(url, requestBody, "application/json"))
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                return request.downloadHandler.text;
            }
            else
            {
                Debug.LogWarning($"Request error (textures): {request.error}");
                return null;
            }
        }
    }

    public async Task<string> GetInfoboxAsync(string article)
    {
        string encodedArticle = UnityWebRequest.EscapeURL(article);
        string url = $"{backendConfig.baseURL}/scraping/infobox?page_name={encodedArticle}";

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

        spawnRoom.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { wallMat, floorMat, floorMat });
        extensionRoom.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { wallMat, floorMat });
        extensionRoomClosure.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { wallMat});
        bookshelf.GetComponent<MeshRenderer>().sharedMaterial = bookshelfMat;
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

    // Pobiera listę URL-i z API i zwraca listę Texture2D oraz odpowiadające im opisy markdown
    public async Task<ImagesResult> GetImagesAsTextures(string pageName)
    {
        if (string.IsNullOrEmpty(pageName)) return new ImagesResult { textures = new List<Texture2D>(), captions = new List<string>() };
        List<Dictionary<string, string>> imagesList = null;
        var textures = new List<Texture2D>();
        var captions = new List<string>();

        string url = $"{backendConfig.baseURL}/images/generator?page_name={UnityWebRequest.EscapeURL(pageName)}";
        imagesList = await GetImagesListAsync(pageName);
        if (imagesList == null || imagesList.Count == 0) return new ImagesResult { textures = textures, captions = captions };

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", auth_header);
            int imagesDownloaded = 0;

            foreach (var imgObj in imagesList)
            {
                if (imgObj == null || imgObj.Count == 0) continue;
                var kvp = imgObj.First();
                string imgUrl = kvp.Key;
                string caption = kvp.Value ?? "[no caption]";

                if (string.IsNullOrEmpty(imgUrl)) continue;
                using (UnityWebRequest texReq = UnityWebRequestTexture.GetTexture(imgUrl))
                {
                    Debug.Log($"Loading image {imagesDownloaded+1}/{imagesList.Count}");
                    var texOp = texReq.SendWebRequest();
                    while (!texOp.isDone)
                        await Task.Yield();

                    if (texReq.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            Texture2D tex = DownloadHandlerTexture.GetContent(texReq);
                            Debug.Log($"{tex.width}, {tex.height}");
                            textures.Add(ProcessTransparency(tex));
                            captions.Add(caption);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"Błąd tworzenia Texture2D z {imgUrl}: {e.Message}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to download texture {imgUrl}: {texReq.error}");
                    }
                    imagesDownloaded++;
                }
            }

            return new ImagesResult { textures = textures, captions = captions };
        }
    }

    private async Task<List<Dictionary<string, string>>> GetImagesListAsync(string pageName)
    {
        List<Dictionary<string, string>> imagesList = new List<Dictionary<string, string>>();
        string url = $"{backendConfig.baseURL}/images/generator?page_name={UnityWebRequest.EscapeURL(pageName)}";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", auth_header);
            var op = request.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"GetImages list failed: {request.error}");
                return imagesList;
            }

            string json = request.downloadHandler.text;
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

            
            imagesList = resp?.GetImagesList();
        }
        return imagesList;
    }

    private async Task<Texture2D> GetImageAsTexture(string imageUrl)
    {
        using (UnityWebRequest texReq = UnityWebRequestTexture.GetTexture(imageUrl))
        {   
            var texOp = texReq.SendWebRequest();
            while (!texOp.isDone)
                await Task.Yield();

            if (texReq.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(texReq);
                    return tex;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Błąd tworzenia Texture2D z {imageUrl}: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"Failed to download texture {imageUrl}: {texReq.error}");
            }
        }
        return null;
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
        float startZ = -(clusterWidth / 2f) + (cellWidth / 2f) + clusterPadding;

        for (int i = 0; i < payloads.Count; i++)
        {
            int r = (cols == 1) ? i : i / cols;
            int c = (cols == 1) ? 0 : i % cols;

            Vector3 localPos = new Vector3(0f, startY - r * (cellHeight + clusterPadding * 2), startZ + c * (cellWidth + clusterPadding * 2));
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
