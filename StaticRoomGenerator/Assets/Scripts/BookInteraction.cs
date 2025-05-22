using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BookInteraction : MonoBehaviour, IInteractable
{
    public string content;

    public void OnInteraction()
    {
        Debug.Log(content);
        Destroy(gameObject);
    }
}
