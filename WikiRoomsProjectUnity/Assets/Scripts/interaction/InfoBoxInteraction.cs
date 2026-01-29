using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfoBoxInteraction : MonoBehaviour, IInteractable
{
    public string content;


    public void OnInteraction()
    {
        gameObject.GetComponent<MeshRenderer>().enabled = !gameObject.GetComponent<MeshRenderer>().enabled;
    }
}
