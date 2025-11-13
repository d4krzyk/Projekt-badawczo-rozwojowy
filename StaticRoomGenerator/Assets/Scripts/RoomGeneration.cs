using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using System.Text;

public class RoomGeneration : MonoBehaviour
{
    public GameObject FrontRoom;
    public GameObject BackRoom;
    public Transform AttachmentPoint;
    public GameObject BookshelfAttachment;
    public GameObject ClosingAttachment;
    public GameObject Bookshelf;

    public Material Bookcase;
    public Material Floor;
    public Material Wall;
    public float initialBookshelfOffset;
    public Transform initialBookshelfPosition;
    public string articleName;
    public float enterTime;
    public float exitTime;
    public Logger logger;
    public string previousRoom;
    public int BookshelfPerRoom = 5;
    
    public ArticleStructure articleData;
    public string articleLink;

    Dictionary<string, TexturesStructure> textureCache;

    void Awake()
    {
        textureCache = new Dictionary<string, TexturesStructure>();
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
                await Task.Yield(); // Wait asynchronously without blocking main thread

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


    public async Task<string> GetTexturesJsonAsync(string category, string articleName)
    {
        string url = $"http://localhost:8000/gen2DTextures";
        string requestBody = "{\"category\": \"" + category + "\"}";

        // cache path per-article
        string safeName = SanitizeFileName(articleName ?? category);
        string cacheFileName = $"textures_{safeName}.json";
        string cachePath = Path.Combine(Application.persistentDataPath, cacheFileName);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("accept", "application/json");

            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield(); // Wait asynchronously without blocking main thread

            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                try
                {
                    File.WriteAllText(cachePath, response, Encoding.UTF8);
                    Debug.Log($"Saved textures to cache: {cachePath}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Nie udało się zapisać cache: " + e.Message);
                }

                return response;
            }
            else
            {
                Debug.LogWarning("Request error (textures): " + request.error + " — próba odczytu z cache.");
                // spróbuj odczytać z cache
                if (File.Exists(cachePath))
                {
                    try
                    {
                        string cached = File.ReadAllText(cachePath, Encoding.UTF8);
                        Debug.Log($"Loaded textures from cache: {cachePath}");
                        return cached;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("Błąd odczytu cache: " + e.Message);
                    }
                }
                return null;
            }
        }
    }
    
    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "untitled";
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name);
        foreach (var c in invalid)
            sb.Replace(c, '_');
        return sb.ToString();
    }
    private Texture2D CreateNormalMapFromGrayscale(Texture2D source, float strength = 1.0f)
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

    private string CreateFallbackTexturesJson(string textureType)
    {
        try
        {
            string texturesDir = Path.Combine(Application.dataPath, "Textures");
            string fileName = textureType;
            if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                fileName += ".png";
            string fullPath = Path.Combine(texturesDir, fileName);

            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"Fallback texture not found: {fullPath}");
                return ""; // pusty string oznacza brak obrazka
            }

            byte[] bytes = File.ReadAllBytes(fullPath);
            return Convert.ToBase64String(bytes);
        }
        catch (Exception e)
        {
            Debug.LogWarning("CreateFallbackTexturesJson error: " + e.Message);
            return "";
        }
    }

    void AddBooksFromSubsection(BookshelfController bookshelf, Sections[] sections, Transform parent, ref int bookIndex)
    {
        foreach (Sections subsection in sections)
        {
            if (subsection.content != null)
            {
                bookshelf.AddBook(subsection.name, subsection.content, articleLink, parent, bookIndex);
                bookIndex++;
            }
            if (subsection.subsections != null)
            {
                AddBooksFromSubsection(bookshelf, subsection.subsections, parent, ref bookIndex);
            }
        }
    }

    public async void GenerateRoom(string articleName)
    {
        // zachowano oryginalne zachowanie (m.in. lokalne shadowing zmiennej enterTime)
        articleLink = "https://en.wikipedia.org/wiki/" + articleName.Replace(" ", "_");
        float enterTime = Time.time;
        Debug.Log("Loading " + articleName + "..."); 

        // 1. Pobierz artykuł
        string json = await GetArticleAsync(articleName);
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("Failed to retrieve article data.");
            return;
        }
        articleData = JsonConvert.DeserializeObject<ArticleStructure>(json);

        // 2. Pobierz tekstury (może z cache)
        Debug.Log("Waiting for " + articleData.category + " textures...");
        string texturesJson = await GetTexturesJsonAsync(articleData.category, articleName);

        TexturesStructure texturesData = null;
        if (string.IsNullOrEmpty(texturesJson))
        {
            Debug.LogWarning("Failed to retrieve textures data.");
            // nie przerywamy działania — spróbujemy fallbacków dalej
        }
        else
        {
            try
            {
                texturesData = JsonConvert.DeserializeObject<TexturesStructure>(texturesJson);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Nie udało się zdeserializować texturesJson: " + e.Message);
                texturesData = null;
            }
        }

        if (texturesData == null)
        {
            // spróbuj fallbacków (jeśli masz lokalne pliki)
            texturesData = new TexturesStructure();
            texturesData.images = new ImagesStructure();
            texturesData.images.bookcase = CreateFallbackTexturesJson("bookcase");
            texturesData.images.wall = CreateFallbackTexturesJson("wall");
            texturesData.images.floor = CreateFallbackTexturesJson("floor");
        }

        // 3. Zastosuj tekstury i materiały
        ApplyTexturesToMaterials(texturesData);

        // 4. Przygotuj początkowe attachment point / offset
        Vector3 lastAttachmentPoint = PrepareAttachmentPoint();

        // 5. Stwórz półki początkowe
        int roomSize = articleData.content.Length;
        int bookIndex = 0;
        CreateInitialBookshelves(roomSize, ref bookIndex);

        // 6. Dodaj dodatkowe półki jeśli potrzeba
        CreateAdditionalBookshelves(roomSize, ref bookIndex, ref lastAttachmentPoint);

        // 7. Dodaj zamknięcie pomieszczenia
        PlaceClosingAttachment(lastAttachmentPoint);
    }

    private void ApplyTexturesToMaterials(TexturesStructure texturesData)
    {
        byte[] texData = Convert.FromBase64String(texturesData.images.bookcase);
        Texture2D bookshelfTex = new Texture2D(2, 2, TextureFormat.RGBA32, false); 
        bookshelfTex.LoadImage(texData);
        bookshelfTex.filterMode = FilterMode.Point;
        bookshelfTex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        Bookcase.mainTexture = bookshelfTex;

        Texture2D bookcaseNormalTex = null;

        if (bookcaseNormalTex == null)
        {
            // wygeneruj z kolorowej tekstury (użyje jasności jako height)
            bookcaseNormalTex = CreateNormalMapFromGrayscale(bookshelfTex, strength: 2.0f);
        }

        if (bookcaseNormalTex != null)
        {
            Bookcase.SetTexture("_BumpMap", bookcaseNormalTex);
            Bookcase.SetFloat("_BumpScale", 1.0f);
            Bookcase.EnableKeyword("_NORMALMAP");
        }

        if (Bookcase.HasProperty("_Glossiness"))
            Bookcase.SetFloat("_Glossiness", 0.35f);
        if (Bookcase.HasProperty("_Metallic"))
            Bookcase.SetFloat("_Metallic", 0.0f);

        texData = Convert.FromBase64String(texturesData.images.wall);
        Texture2D wallTex = new Texture2D(2, 2, TextureFormat.RGBA32, false); 
        wallTex.LoadImage(texData, false);
        wallTex.filterMode = FilterMode.Point;    
        wallTex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        Wall.mainTexture = wallTex;

        texData = Convert.FromBase64String(texturesData.images.floor);
        Texture2D floorTex = new Texture2D(2, 2, TextureFormat.RGBA32, false); 
        floorTex.LoadImage(texData);
        floorTex.filterMode = FilterMode.Point;
        floorTex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        Floor.mainTexture = floorTex;

        FrontRoom.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { Wall, Floor });
        BackRoom.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { Wall, Floor });

        var a = Instantiate(BookshelfAttachment, gameObject.transform);
        a.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { Wall, Floor });
        DestroyImmediate(a, true);
    }

    private Vector3 PrepareAttachmentPoint()
    {
        // utwórz tymczasowo obiekt aby odczytać bounds, potem usuń
        var a = Instantiate(BookshelfAttachment, gameObject.transform);
        Vector3 offset = new Vector3(a.GetComponent<Renderer>().bounds.size.x / 2, 0, 0);
        DestroyImmediate(a, true);

        // zwróć punkt startowy skorygowany o połowę szerokości attachmentu
        Vector3 lastAttachmentPoint = AttachmentPoint.position - offset;
        return lastAttachmentPoint;
    }

    private void CreateInitialBookshelves(int roomSize, ref int bookIndex)
    {
        for (int i = 0; i < BookshelfPerRoom; i++)
        {
            GameObject container = new GameObject();
            container.transform.parent = transform;
            container.name = "BookshelfContainer_" + i;
            container.transform.position = initialBookshelfPosition.position + new Vector3(initialBookshelfOffset * i, 0, 0);
            
            GameObject b = Instantiate(Bookshelf, container.transform.position, Quaternion.Euler(new Vector3(-90, 90, -90)), transform);
            b.transform.parent = container.transform;
            b.GetComponent<MeshRenderer>().sharedMaterial = Bookcase;
            BookshelfController currentBookshelfController = b.GetComponent<BookshelfController>();
            if (i < roomSize)
            {
                currentBookshelfController.AddSign(articleData.content[i].name, container.transform);
                if (articleData.content[i].content != null)
                {
                    currentBookshelfController.AddBook(articleData.content[i].name, articleData.content[i].content, articleLink, container.transform, bookIndex);
                    bookIndex++;
                }
                if (articleData.content[i].subsections != null)
                {
                    AddBooksFromSubsection(currentBookshelfController, articleData.content[i].subsections, container.transform, ref bookIndex);
                }
            }
        }
    }

    private void CreateAdditionalBookshelves(int roomSize, ref int bookIndex, ref Vector3 lastAttachmentPoint)
    {
        // jeśli nie ma dodatkowych półek do dodania, nic nie rób
        if (roomSize <= BookshelfPerRoom)
            return;

        // zmierz realną szerokość attachmentu przez tymczasową instancję
        var tmp = Instantiate(BookshelfAttachment, AttachmentPoint.position, Quaternion.identity, transform);
        float fullWidth = tmp.GetComponent<Renderer>().bounds.size.x;
        DestroyImmediate(tmp, true);

        // krok będzie połową pełnej szerokości, dzięki temu łączenia są poprawne (unikamy podwójnego przesunięcia)
        float halfStep = fullWidth / 2f;

        for (int i = 0; i < roomSize - BookshelfPerRoom; i++)
        {
            // dla pierwszej dodatkowej półki użyj lastAttachmentPoint bez dodatkowego przesunięcia,
            // dla kolejnych przesuwaj o halfStep (co w sumie daje krok = fullWidth między kolejnymi półkami)
            Vector3 placement = (i == 0) ? lastAttachmentPoint : lastAttachmentPoint - new Vector3(halfStep, 0, 0);

            GameObject container = new GameObject();
            container.transform.parent = transform;
            container.name = "BookshelfContainerAdd_" + i;
            container.transform.position = placement;

            var a = Instantiate(BookshelfAttachment, container.transform.position, Quaternion.identity, transform);
            a.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { Wall, Floor });

            container.transform.position = new Vector3(container.transform.position.x, initialBookshelfPosition.position.y - 0.08f, container.transform.position.z);

            GameObject b = Instantiate(Bookshelf, container.transform.position, Quaternion.Euler(new Vector3(-90, 90, -90)), transform);
            b.GetComponent<MeshRenderer>().sharedMaterial = Bookcase;
            BookshelfController currentBookshelfController = b.GetComponent<BookshelfController>();
            b.transform.position = new Vector3(b.transform.position.x, 8.87f, b.transform.position.z);

            int contentIndex = i + BookshelfPerRoom;
            if (contentIndex < articleData.content.Length)
            {
                currentBookshelfController.AddSign(articleData.content[contentIndex].name, container.transform);
                if (articleData.content[contentIndex].content != null)
                {
                    currentBookshelfController.AddBook(articleData.content[contentIndex].name, articleData.content[contentIndex].content, articleLink, container.transform, bookIndex);
                    bookIndex++;
                }
                if (articleData.content[contentIndex].subsections != null)
                {
                    AddBooksFromSubsection(currentBookshelfController, articleData.content[contentIndex].subsections, container.transform, ref bookIndex);
                }
            }

            // zaktualizuj lastAttachmentPoint przesuwając o halfStep (razem z powyższym daje krok pełnej szerokości)
            lastAttachmentPoint = placement - new Vector3(halfStep, 0, 0);
        }
    }

    private void PlaceClosingAttachment(Vector3 lastAttachmentPoint)
    {
        Vector3 offset = new Vector3(ClosingAttachment.GetComponent<Renderer>().bounds.size.x / 2, 0, 0);
        var c = Instantiate(ClosingAttachment, lastAttachmentPoint - offset, Quaternion.identity, transform);
        c.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { Wall, Floor });
    }

    public void ResetRoom()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }

    public void LogRoom()
    { 
        string previousRoomUrl = "https://en.wikipedia.org/wiki/" + previousRoom.Replace(" ", "_");
        logger.LogOnRoomExit(articleLink, enterTime, exitTime, previousRoomUrl);
    }
}

