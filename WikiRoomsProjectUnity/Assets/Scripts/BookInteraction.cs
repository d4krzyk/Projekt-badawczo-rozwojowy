using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BookInteraction : MonoBehaviour, IInteractable
{
    public string bookArticleLink;
    public string bookName;
    public string content;


    public void OnInteraction()
    {
        gameObject.GetComponent<MeshRenderer>().enabled = !gameObject.GetComponent<MeshRenderer>().enabled;
    }
}
