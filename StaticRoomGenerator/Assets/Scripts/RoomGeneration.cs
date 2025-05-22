using UnityEngine;
using System.IO;
using System;

public class RoomGeneration : MonoBehaviour
{
    public Transform AttachmentPoint;
    public GameObject BookshelfAttachment;
    public GameObject ClosingAttachment;
    public GameObject Bookshelf;
    public float initialBookshelfOffset;
    public Transform initialBookshelfPosition;

    public void Start()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "article.json");
        string json = File.ReadAllText(path);
        ArticleStructure articleData = JsonUtility.FromJson<ArticleStructure>(json);
        GenerateRoom(articleData.paragraphs.Length, articleData);
        Debug.Log($"Length: {articleData.paragraphs.Length}, Category: {articleData.category}");
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
            if (i < roomSize)
            {
                b.GetComponent<BookshelfController>().AddBook(articleData.paragraphs[i].content);
                b.GetComponent<BookshelfController>().AddSign(articleData.paragraphs[i].name);
            }
        }

        for (int i = 0; i < roomSize - 4; i++)
        {
            Instantiate(BookshelfAttachment, lastAttachmentPoint - offset, Quaternion.identity);
            GameObject b = Instantiate(Bookshelf, lastAttachmentPoint - offset, Quaternion.Euler(new Vector3(-90, 0, 270)));
            b.transform.position = new Vector3(b.transform.position.x, 9.135365f, b.transform.position.z);
        
            b.GetComponent<BookshelfController>().AddBook(articleData.paragraphs[i + 4].content);
            b.GetComponent<BookshelfController>().AddSign(articleData.paragraphs[i + 4].name);
            
            lastAttachmentPoint -= 2 * offset;
        }
        offset = new Vector3(ClosingAttachment.GetComponent<Renderer>().bounds.size.x / 2, 0, 0);
        Instantiate(ClosingAttachment, lastAttachmentPoint - offset, Quaternion.identity);
    }

    public void SpawnBookshelf(Vector3 position, string name, string content)
    {
        GameObject bookshelf = Instantiate(Bookshelf);
        bookshelf.transform.position = position;
        bookshelf.GetComponent<BookshelfController>().AddBook(content);
        bookshelf.GetComponent<BookshelfController>().AddSign(name);
    }
} 
