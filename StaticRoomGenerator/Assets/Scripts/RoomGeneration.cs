using UnityEngine;
using System.IO;
using UnityEngine.Networking;
using System.Threading.Tasks;

public class RoomGeneration : MonoBehaviour
{
    public Transform AttachmentPoint;
    public GameObject BookshelfAttachment;
    public GameObject ClosingAttachment;
    public GameObject Bookshelf;
    public float initialBookshelfOffset;
    public Transform initialBookshelfPosition;

    async void Start()
    {
        string json = await GetArticleAsync("pope");
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("Failed to retrieve article data.");
            return;
        }

        ArticleStructure articleData = JsonUtility.FromJson<ArticleStructure>(json);
        GenerateRoom(articleData.sections.Length, articleData);
        Debug.Log($"Length: {articleData.sections.Length}, Category: {articleData.category}");
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


    public void GenerateRoom(int roomSize, ArticleStructure articleData)
    {
        var a = Instantiate(BookshelfAttachment);

        Vector3 lastAttachmentPoint = AttachmentPoint.position;
        Vector3 offset = new Vector3(a.GetComponent<Renderer>().bounds.size.x / 2, 0, 0);
        DestroyImmediate(a, true);
        for (int i = 0; i < 4; i++)
        {
            GameObject b = Instantiate(Bookshelf, initialBookshelfPosition.position + new Vector3(initialBookshelfOffset * i, 0, 0), Quaternion.Euler(new Vector3(-90, 0, 270)));
            BookshelfController currentBookshelfController = b.GetComponent<BookshelfController>();
            if (i < roomSize)
            {
                currentBookshelfController.AddSign(articleData.sections[i].name);
                if (articleData.sections[i].content != null)
                {
                    currentBookshelfController.AddBook(articleData.sections[i].name, articleData.sections[i].content);
                }
                if (articleData.sections[i].subsections != null)
                {
                    AddBooksFromSubsection(currentBookshelfController, articleData.sections[i].subsections);
                }
            }
        }

        for (int i = 0; i < roomSize - 4; i++)
        {
            Instantiate(BookshelfAttachment, lastAttachmentPoint - offset, Quaternion.identity);
            GameObject b = Instantiate(Bookshelf, lastAttachmentPoint - offset, Quaternion.Euler(new Vector3(-90, 0, 270)));
            BookshelfController currentBookshelfController = b.GetComponent<BookshelfController>();
            b.transform.position = new Vector3(b.transform.position.x, 9.135365f, b.transform.position.z);

            currentBookshelfController.AddSign(articleData.sections[i + 4].name);
            if (articleData.sections[i + 4].content != null)
            {
                currentBookshelfController.AddBook(articleData.sections[i + 4].name, articleData.sections[i + 4].content);
            }
            if (articleData.sections[i + 4].subsections != null)
            {
                AddBooksFromSubsection(currentBookshelfController, articleData.sections[i + 4].subsections);

            }
            lastAttachmentPoint -= 2 * offset;
        }
        offset = new Vector3(ClosingAttachment.GetComponent<Renderer>().bounds.size.x / 2, 0, 0);
        Instantiate(ClosingAttachment, lastAttachmentPoint - offset, Quaternion.identity);
    }
}
