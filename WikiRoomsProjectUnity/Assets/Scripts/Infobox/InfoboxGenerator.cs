using System;
using System.Collections.Generic;
using LogicUI.FancyTextRendering;
using UnityEngine;

public class InfoboxGenerator : MonoBehaviour
{
    public GameObject abovePrefab;
    public GameObject textPrefab;
    public GameObject headerPrefab;

    public void PopulateUI(WikiPageRaw infoboxesData)
    {
        foreach (var infobox in infoboxesData.infobox)
        {
            foreach (var item in infobox)
            {
                bool hasLabel = item.label != null;
                HandleInfoboxItemRaw(item);
            }
        }
    }

    public void ClearContent()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }

    void HandleInfoboxItemRaw(InfoboxItemRaw item)
    {
        GameObject instantiatedObject = null;
        string stringContent = "";
        foreach (var value in item.value)
        {
            stringContent += $"{HandleValueRaw(value)}\n";
        }
        switch (item.@class)
        {
            case "above":
                instantiatedObject = Instantiate(abovePrefab);
                break;
            case "text":
                instantiatedObject = Instantiate(textPrefab);
                break;
            case "header":
                instantiatedObject = Instantiate(headerPrefab);
                break;
            default:
                Debug.LogWarning($"Unknown infobox item class: {item.@class}");
                break;
        }
        if (instantiatedObject == null) return;
        instantiatedObject.transform.SetParent(this.transform, false);
        MarkdownRenderer mRenderer = instantiatedObject.GetComponent<MarkdownRenderer>();
        if (mRenderer == null) return;
        mRenderer.Source = stringContent;
    }

    string HandleValueRaw(ValueRaw valueRaw)
    {
        if (valueRaw == null) return "";
        switch (valueRaw.@class)
        {
            case "text":
                if (valueRaw.value == null) return "";
                if (valueRaw.value is string textValue) return textValue;
                Debug.LogWarning($"Expected string value for text class, instead got {valueRaw.value.GetType()}");
                break;
            case "link":
                if (!string.IsNullOrEmpty(valueRaw.text)) return $"[{valueRaw.text}]({valueRaw.href})";
                Debug.LogWarning($"Expected correct text for link class");
                break;
            default:
                Debug.LogWarning($"Unknown value class: {valueRaw.@class}");
                break;
        }
        return "";
    }

    

}
