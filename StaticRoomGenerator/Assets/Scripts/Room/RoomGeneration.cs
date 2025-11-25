using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class RoomGeneration : MonoBehaviour
{
    public GameObject FrontRoom;
    public GameObject BackRoom;
    public Transform AttachmentPoint;
    public GameObject BookshelfAttachment;
    public GameObject ClosingAttachment;
    public GameObject Bookshelf;

    [Header("Room materials")]
    public Material bookshelfMat;
    public Material floorMat;
    public Material wallMat;

    [Header("Default materials")]
    public Material defBookcaseMat;
    public Material defFloorMat;
    public Material defWallMat;

    public float initialBookshelfOffset;
    public Transform initialBookshelfPosition;
    public string articleName;
    public float exitTime;
    public Logger logger;
    public string previousRoom;
    public int BookshelfPerRoom = 5;
    public ArticleStructure articleData;

    public float EnterTime;

    string articleLink;


    public async void GenerateRoom(string articleName)
    {
        SetDefaultMaterials();
        
        articleLink = "https://en.wikipedia.org/wiki/" + articleName.Replace(" ", "_");
        EnterTime = Time.time;
        Debug.Log($"Loading {articleName}..."); 

        // Pobieranie artykułu
        string json = await GetArticleAsync(articleName);
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("Failed to retrieve article data.");
            return;
        }
        articleData = JsonConvert.DeserializeObject<ArticleStructure>(json);

        // Pobieranie tekstur i updateuj materiały
        Debug.Log($"Waiting for {articleData.category} textures...");
        string texturesJson = await GetTexturesJsonAsync(articleData.category);
        TexturesStructure texturesData = null;
        if (string.IsNullOrEmpty(texturesJson)) Debug.LogWarning("Failed to retrieve textures data.");
        else
        {
            texturesData = JsonConvert.DeserializeObject<TexturesStructure>(texturesJson);
            ApplyTexturesToMaterials(texturesData);
        }

        // Przygotuj początkowe attachment point / offset
        Vector3 offset = new Vector3(BookshelfAttachment.GetComponent<Renderer>().bounds.size.x / 2, 0, 0);
        Vector3 lastAttachmentPoint = AttachmentPoint.position - offset;

        // Stwórz półki początkowe
        int roomSize = articleData.content.Length;
        int bookIndex = 0;
        CreateInitialBookshelves(roomSize, ref bookIndex);

        // Dodaj dodatkowe półki jeśli potrzeba
        CreateAdditionalBookshelves(roomSize, ref bookIndex, ref lastAttachmentPoint);

        // Dodaj zamknięcie pomieszczenia
        PlaceClosingAttachment(lastAttachmentPoint);
    }

    void SetDefaultMaterials()
    {
        FrontRoom.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { defWallMat, defFloorMat });
        BackRoom.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { defWallMat, defFloorMat });
        BookshelfAttachment.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { defWallMat, defFloorMat });
        ClosingAttachment.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { defWallMat, defFloorMat });
        Bookshelf.GetComponent<MeshRenderer>().sharedMaterial = defBookcaseMat;
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

    void AddBooksFromSubsection(BookshelfController bookshelf, Section[] sections, Transform parent, ref int bookIndex)
    {
        foreach (Section subsection in sections)
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

    void ApplyTexturesToMaterials(TexturesStructure texturesData)
    {
        byte[] texData = Convert.FromBase64String(texturesData.images.bookcase);
        Texture2D bookshelfTex = new Texture2D(2, 2, TextureFormat.RGBA32, false); 
        bookshelfTex.LoadImage(texData);
        bookshelfTex.filterMode = FilterMode.Point;
        bookshelfTex.Apply(false, false);
        bookshelfMat.mainTexture = bookshelfTex;

        // wygeneruj z kolorowej tekstury (użyje jasności jako height)
        Texture2D bookcaseNormalTex = CreateNormalMapFromGrayscale(bookshelfTex, 2.0f);
        bookshelfMat.SetTexture("_BumpMap", bookcaseNormalTex);
        bookshelfMat.SetFloat("_BumpScale", 1.0f);
        bookshelfMat.EnableKeyword("_NORMALMAP");
        if (bookshelfMat.HasProperty("_Glossiness")) bookshelfMat.SetFloat("_Glossiness", 0.35f);
        if (bookshelfMat.HasProperty("_Metallic")) bookshelfMat.SetFloat("_Metallic", 0.0f);

        texData = Convert.FromBase64String(texturesData.images.wall);
        Texture2D wallTex = new Texture2D(2, 2, TextureFormat.RGBA32, false); 
        wallTex.LoadImage(texData);
        wallTex.filterMode = FilterMode.Point;    
        wallTex.Apply(false, false);
        wallMat.mainTexture = wallTex;

        texData = Convert.FromBase64String(texturesData.images.floor);
        Texture2D floorTex = new Texture2D(2, 2, TextureFormat.RGBA32, false); 
        floorTex.LoadImage(texData);
        floorTex.filterMode = FilterMode.Point;
        floorTex.Apply(false, false);
        floorMat.mainTexture = floorTex;

        FrontRoom.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { wallMat, floorMat });
        BackRoom.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { wallMat, floorMat });
        BookshelfAttachment.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { wallMat, floorMat });
        ClosingAttachment.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { wallMat, floorMat });
        Bookshelf.GetComponent<MeshRenderer>().sharedMaterial = bookshelfMat;
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
            
            container.transform.position = new Vector3(container.transform.position.x, initialBookshelfPosition.position.y - 0.08f, container.transform.position.z);

            GameObject b = Instantiate(Bookshelf, container.transform.position, Quaternion.Euler(new Vector3(-90, 90, -90)), transform);
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
        logger.LogOnRoomExit(articleLink, EnterTime, exitTime, previousRoomUrl);
    }
}

