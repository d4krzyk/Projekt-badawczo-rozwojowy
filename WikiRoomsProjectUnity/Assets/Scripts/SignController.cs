using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SignController : MonoBehaviour
{
    public void SetSignText(string content)
    {
        // szukamy dowolnego komponentu tekstowego z TextMeshPro (TextMeshPro lub TextMeshProUGUI)
        TMPro.TMP_Text textComponent = GetComponentInChildren<TMPro.TMP_Text>();
        if (textComponent == null)
        {
            Debug.LogWarning($"SignController: brak komponentu TMP_Text w obiekcie '{gameObject.name}'. Upewnij się, że prefab Sign zawiera TextMeshPro / TextMeshProUGUI.");
            return;
        }

        textComponent.text = content;

        // bezpieczne ustawienie pozycji (jeśli potrzeba korekty)
        Vector3 lp = textComponent.transform.localPosition;
        textComponent.transform.localPosition = new Vector3(lp.x, 0.02f, lp.z);
    }
}
