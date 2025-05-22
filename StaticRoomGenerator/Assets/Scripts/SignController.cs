using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SignController : MonoBehaviour
{
    public void SetSignText(string content)
    {
        TextMesh textMesh = transform.GetChild(0).gameObject.GetComponent<TextMesh>();
        textMesh.text = content;
    }
}
