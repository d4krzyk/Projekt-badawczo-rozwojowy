using UnityEngine;

public class BookshelfController : MonoBehaviour
{
    public GameObject Book;
    public GameObject Sign;

    Vector3 lastPosition = new Vector3(-0.00671065785f, 0.00317066023f, -0.000581036031f);

    public void AddBook(string name, string content, string articleLink)
    {
        Book = Instantiate(Book, transform);
        Book.transform.localPosition = lastPosition;
        Book.transform.localRotation = new Quaternion(-0.500000715f, 0.499999225f, -0.499999315f, 0.500000834f);
        Book.transform.localScale = new Vector3(-0.0644469038f, -0.0108729266f, -0.00513079017f);
        if(name != null)
            Book.GetComponent<BookInteraction>().content = "# " + name + "\n";
        Book.GetComponent<BookInteraction>().bookName = name;
        Book.GetComponent<BookInteraction>().content = content;
        Book.GetComponent<BookInteraction>().bookArticleLink = articleLink + $"#{name.Replace(" ", "_")}";
        lastPosition += new Vector3(0.001f, 0f, 0f);
    }

    public void AddSign(string name)
    {
        Sign = Instantiate(Sign, transform);
        Sign.transform.localPosition = new Vector3(-0.00999994855f, 0.0373199396f, -0.00976934936f);
        Sign.transform.localRotation = new Quaternion(-0.499999344f, -0.500000775f, -0.499999225f, 0.500000775f);
        Sign.transform.localScale = new Vector3(-0.0108729266f, -0.00513078924f, -0.0644468963f);
        Sign.GetComponent<SignController>().SetSignText(name);
    }
}
