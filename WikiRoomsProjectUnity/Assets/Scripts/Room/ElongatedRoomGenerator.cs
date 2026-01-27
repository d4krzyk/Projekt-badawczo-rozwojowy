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

    [HideInInspector] public float EnterTime;
    [HideInInspector] public float ExitTime;
    [HideInInspector] public string PreviousRoom;
    [HideInInspector] public ArticleStructure ArticleData;

    string articleLink;
    RoomsController roomsController;

    [Header("Article title in Room")]
    public TextMeshPro articleTitleText; // przypnij TextMeshProUGUI z Canvasu

    // cache pobranych tekstur dla bieżącej strony
    List<Texture2D> wikiImages;
    List<string> wikiImageCaptions;
    
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
    
    public void Awake()
    {
        GameController gameController = FindAnyObjectByType<GameController>();
        if(gameController != null) articleName = gameController.ArticleName;
    }

    public async void GenerateRoom(string articleName, RoomsController roomsController)
    {
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
                return;
            }
            ArticleData = JsonConvert.DeserializeObject<ArticleStructure>(json);
            if (ArticleData == null)
            {
                Debug.LogError("Article deserialization returned null.");
                return;
            }
            if (this.roomsController != null)
                this.roomsController.CacheArticle(articleName, ArticleData);
        }

        // Bezpieczne użycie category
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

        // po pobraniu nazwy artykułu (lub gdzieś tam), pobierz obrazy:
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

        SpawnExtensionsWithBookselfs();
        Debug.Log($"Loaded {articleName} successfully.");
    }


    void SetDefaultMaterials()
    {
        spawnRoom.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { defWallMat, defFloorMat, defFloorMat });
        extensionRoom.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { defWallMat, defFloorMat });
        extensionRoomClosure.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { defWallMat, defFloorMat });
        bookshelf.GetComponent<MeshRenderer>().sharedMaterial = defBookcaseMat;
    }

    public async Task<string> GetArticleAsync(string article)
    {
        string encodedArticle = UnityWebRequest.EscapeURL(article);
        string url = $"http://localhost/article?article={encodedArticle}&category_strategy=api";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("accept", "application/json");

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
        string url = $"http://localhost/scraping/infobox?page_name={encodedArticle}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("accept", "application/json");

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
        extensionRoomClosure.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { wallMat, floorMat });
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
                if(i < wikiImages.Count/2) SpawnImageHolder(new Vector3(-roomSize.x/2 + 0.05f, 2f, roomSize.y/4), new Vector3(0, -90, 0), wikiImages[i], i < wikiImageCaptions.Count ? wikiImageCaptions[i] : null, extension.transform);
                if(i + 1 < wikiImages.Count/2) SpawnImageHolder(new Vector3(-roomSize.x/2 + 0.05f, 2f, -roomSize.y/4), new Vector3(0, -90, 0), wikiImages[i+1], (i+1)< wikiImageCaptions.Count ? wikiImageCaptions[i+1] : null, extension.transform);
                if((wikiImages.Count - (i + 1)) >= wikiImages.Count/2) SpawnImageHolder(new Vector3(roomSize.x/2 - 0.05f, 2f, roomSize.y/4), new Vector3(0, 90, 0), wikiImages[wikiImages.Count - (i + 1)], (wikiImages.Count - (i + 1)) < wikiImageCaptions.Count ? wikiImageCaptions[wikiImages.Count - (i + 1)] : null, extension.transform);
                if((wikiImages.Count - (i + 2)) >= wikiImages.Count/2) SpawnImageHolder(new Vector3(roomSize.x/2 - 0.05f, 2f, -roomSize.y/4), new Vector3(0, 90, 0), wikiImages[wikiImages.Count - (i+2)], (wikiImages.Count - (i+2)) < wikiImageCaptions.Count ? wikiImageCaptions[wikiImages.Count - (i+2)] : null, extension.transform);
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
                nextSection = ArticleData.content[sectionIndex];
                bookIndex = 0;
            }
            currentBookshelfController.AddSign(nextSection.name, bookshelfContainer.transform);

            bookIndex = AddBooksForSection(currentBookshelfController, nextSection, bookshelfContainer.transform, bookIndex - 1);
            if (bookIndex == BookCountForSection(nextSection))
            {
                sectionIndex++;
                if (sectionIndex < ArticleData.content.Length) nextSection = ArticleData.content[sectionIndex];
                bookIndex = 0;
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

        string url = $"http://localhost/images/generator?page_name={UnityWebRequest.EscapeURL(pageName)}";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            var op = request.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"GetImages list failed: {request.error}");
                return new ImagesResult { textures = new List<Texture2D>(), captions = new List<string>() };
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
                return new ImagesResult { textures = new List<Texture2D>(), captions = new List<string>() };
            }

            var textures = new List<Texture2D>();
            var captions = new List<string>();
            var imagesList = resp?.GetImagesList();
            if (imagesList == null || imagesList.Count == 0) return new ImagesResult { textures = textures, captions = captions };

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
                            textures.Add(tex);
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


    // Skaluje podany quad / plane tak, aby zachować proporcje tekstury.
    // Zakładamy, że quad ma jednostkowy proporcjonalny rozmiar w localScale (height bazowy na localScale.y).
    void SetImageQuadScale(GameObject quad, Texture2D tex)
    {
        if (quad == null || tex == null)
        {
            Debug.LogWarning("[SetImageQuadScale] Brak quad lub tekstury.");
            return;
        }

        Debug.Log($"{tex.width}, {tex.height}, {(float)tex.width / tex.height}");

        float texAspect = (float)tex.width / tex.height;
        // Pobierz aktualny localScale
        Vector3 ls = new Vector3(2, 2 , 1);
        ls.x = ls.y * texAspect;
        quad.transform.localScale = ls;

        Debug.Log($"[SetImageQuadScale] '{quad.name}' po localScale={quad.transform.localScale}");
    }

    void SpawnImageHolder(Vector3 pos, Vector3 rotation, Texture2D tex, string caption, Transform extension)
    {
        GameObject currentImageHolder = Instantiate(imageHolder, Vector3.zero, Quaternion.Euler(rotation));
        currentImageHolder.transform.parent = extension;
        currentImageHolder.transform.localPosition = pos;
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

        // skaluj GameObject ImageWikiQuad, żeby zachować aspect ratio tekstury (nie skalujemy materiału)
        SetImageQuadScale(currentImageHolder, tex);
    }
}
