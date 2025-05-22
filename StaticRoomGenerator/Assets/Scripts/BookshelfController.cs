using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BookshelfController : MonoBehaviour
{
    public GameObject Book;
    public GameObject Sign;
    public void AddBook(string content)
    {
        Book = Instantiate(Book, transform);
        Book.transform.localPosition = new Vector3(-0.00671065785f, 0.00317066023f, -0.000581036031f);
        Book.transform.localRotation = new Quaternion(-0.500000715f, 0.499999225f, -0.499999315f, 0.500000834f);
        Book.transform.localScale = new Vector3(-0.0644469038f, -0.0108729266f, -0.00513079017f);
        Book.GetComponent<BookInteraction>().content = content;
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
