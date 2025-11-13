using UnityEngine;

public class BookshelfController : MonoBehaviour
{
    public GameObject Book;
    public GameObject Sign;

    public int booksPerRow = 2;
    public float zStep = -0.63f;   // przesunięcie w osi Z między książkami w rzędzie
    public float rowYOffset = 0.525f; // obniżenie Y po każdym pełnym wierszu

    // TODO: naprawić rowYOffset, żeby nie przesuwać książek za bardzo w dół

    Vector3 startPosition = new Vector3(0,1.05f,0.36f);

    private int bookCount = 0;

    // DODANE: proste logowanie localPos do konsoli Unity
    public bool logLocalPositions = true;

    // Prostsza, deterministyczna siatka książek: kolumny = booksPerRow, wiersze rosną automatycznie
    public void AddBook(string name, string content, string articleLink, Transform parent, int index)
    {
        if (Book == null)
        {
            Debug.LogWarning("BookshelfController: brak prefab Book");
            return;
        }

        int col = bookCount % booksPerRow;
        int row = bookCount / booksPerRow;

        Vector3 localPos = new Vector3(
            startPosition.x,
            startPosition.y - row * rowYOffset,
            startPosition.z + col * zStep
        );

        GameObject bookObj = Instantiate(Book, parent);
        bookObj.name = "Book_" + index;
        bookObj.transform.localPosition = localPos;
        bookObj.transform.localRotation = Quaternion.identity;
        bookObj.transform.localScale = Vector3.one;

        if (logLocalPositions)
            Debug.Log($"[Bookshelf:{gameObject.name}] Book_{index} localPos={localPos}");

        var bi = bookObj.GetComponent<BookInteraction>();
        if (bi != null)
        {
            bi.bookName = name;
            bi.content = content != null ? content : ("# " + name + "\n");
            bi.bookArticleLink = articleLink + $"#{(name ?? "").Replace(" ", "_")}";
        }

        bookCount++;
    }

    public void AddSign(string name, Transform parent)
    {
        if (Sign == null)
        {
            Debug.LogWarning("BookshelfController: brak prefab Sign");
            return;
        }

        GameObject signObj = Instantiate(Sign);
        signObj.transform.SetParent(parent, false);
        signObj.transform.localPosition = new Vector3(-0.42f, 1f, 0.66f);
        signObj.transform.localRotation = Quaternion.Euler(0f, -270f, 90f);
        signObj.transform.localScale = Vector3.one;

        var sc = signObj.GetComponent<SignController>();
        if (sc != null) sc.SetSignText(name);
    }

    public void ResetLayout()
    {
        bookCount = 0;
    }
}
