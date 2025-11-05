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

    public ArticleStructure articleData; 

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

    public async Task<string> GetTexturesJsonAsync(string category)
    {
        string url = $"http://localhost:8000/gen2DTextures";
        string requestBody = "{\"category\": \"" + category + "\"}";

        using (UnityWebRequest request = UnityWebRequest.Post(url, requestBody, "application/json"))
        {
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


    void AddBooksFromSubsection(BookshelfController bookshelf, Sections[] sections)
    {
        foreach (Sections subsection in sections)
        {
            if (subsection.content != null)
            {
                bookshelf.AddBook(subsection.name, subsection.content);
            }
            if (subsection.subsections != null)
            {
                AddBooksFromSubsection(bookshelf, subsection.subsections);
            }
        }
    }

    public async void GenerateRoom(string articleName)
    {
        float enterTime = Time.time;
        Debug.Log("Loading " + articleName + "..."); 
        string json = await GetArticleAsync(articleName);
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("Failed to retrieve article data.");
            return;
        }
        articleData = JsonConvert.DeserializeObject<ArticleStructure>(json);
        Debug.Log("Waiting for " + articleData.category + " textures...");
        json = await GetTexturesJsonAsync(articleData.category);
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("Failed to retrieve article data.");
            return;
        }

        TexturesStructure texturesData = JsonConvert.DeserializeObject<TexturesStructure>(json);
        byte[] texData = Convert.FromBase64String(texturesData.images.bookcase);
        Texture2D bookshelfTex = new Texture2D(2, 2, TextureFormat.RGBA32, false); 
        bookshelfTex.LoadImage(texData);
        bookshelfTex.filterMode = FilterMode.Point;
        bookshelfTex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        Bookcase.mainTexture = bookshelfTex;

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

        int roomSize = articleData.content.Length;
        var a = Instantiate(BookshelfAttachment, gameObject.transform);
        a.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { Wall, Floor });


        Vector3 lastAttachmentPoint = AttachmentPoint.position;
        Vector3 offset = new Vector3(a.GetComponent<Renderer>().bounds.size.x / 2, 0, 0);
        DestroyImmediate(a, true);
        for (int i = 0; i < 4; i++)
        {
            GameObject b = Instantiate(Bookshelf, initialBookshelfPosition.position + new Vector3(initialBookshelfOffset * i, 0, 0), Quaternion.Euler(new Vector3(-90, 0, 270)), transform);
            b.GetComponent<MeshRenderer>().sharedMaterial = Bookcase;
            BookshelfController currentBookshelfController = b.GetComponent<BookshelfController>();
            if (i < roomSize)
            {
                currentBookshelfController.AddSign(articleData.content[i].name);
                if (articleData.content[i].content != null)
                {
                    currentBookshelfController.AddBook(articleData.content[i].name, articleData.content[i].content);
                }
                if (articleData.content[i].subsections != null)
                {
                    AddBooksFromSubsection(currentBookshelfController, articleData.content[i].subsections);
                }
            }
        }

        for (int i = 0; i < roomSize - 4; i++)
        {
            a = Instantiate(BookshelfAttachment, lastAttachmentPoint - offset, Quaternion.identity, transform);
            a.GetComponent<MeshRenderer>().SetMaterials(new List<Material> { Wall, Floor });
            GameObject b = Instantiate(Bookshelf, lastAttachmentPoint - offset, Quaternion.Euler(new Vector3(-90, 0, 270)), transform);
            b.GetComponent<MeshRenderer>().sharedMaterial = Bookcase;
            BookshelfController currentBookshelfController = b.GetComponent<BookshelfController>();
            b.transform.position = new Vector3(b.transform.position.x, 9.135365f, b.transform.position.z);

            currentBookshelfController.AddSign(articleData.content[i + 4].name);
            if (articleData.content[i + 4].content != null)
            {
                currentBookshelfController.AddBook(articleData.content[i + 4].name, articleData.content[i + 4].content);
            }
            if (articleData.content[i + 4].subsections != null)
            {
                AddBooksFromSubsection(currentBookshelfController, articleData.content[i + 4].subsections);

            }
            lastAttachmentPoint -= 2 * offset;
        }
        offset = new Vector3(ClosingAttachment.GetComponent<Renderer>().bounds.size.x / 2, 0, 0);
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
        logger.LogOnRoomExit(articleData.name, enterTime, exitTime, new List<BookLog>(), new List<LinkLog>(), previousRoom);
    }
}
