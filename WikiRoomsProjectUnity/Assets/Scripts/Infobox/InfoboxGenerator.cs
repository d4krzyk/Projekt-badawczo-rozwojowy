using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LogicUI.FancyTextRendering;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class InfoboxGenerator : MonoBehaviour
{
    public GameObject abovePrefab;
    public GameObject textPrefab;
    public GameObject headerPrefab;
    public GameObject imagePrefab;
    public GameObject captionPrefab;
    public GameObject labelPrefab;

    public async Task PopulateUI(WikiPageRaw infoboxesData)
    {
        ClearContent();
        foreach (var infobox in infoboxesData.infobox)
        {
            foreach (var item in infobox)
            {
                await HandleInfoboxItemRaw(item);
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

    async Task HandleInfoboxItemRaw(InfoboxItemRaw item)
    {
        bool hasLabel = item.label != null;
        GameObject instantiatedObject = null;
        string stringContent = "";
        if (hasLabel)
        {
            GameObject labelObject = Instantiate(labelPrefab);
            string labelStr = HandleLabelRaw(item.label);
            labelObject.transform.SetParent(transform, false);
            LabelController labelController = labelObject.GetComponent<LabelController>();
            labelController.SetLabelText(labelStr);
            foreach (var value in item.value)
            {
                labelController.AddLabelValue(HandleValueRaw(value));
            }
        }
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
            case "image":
                instantiatedObject = Instantiate(imagePrefab);
                Image image = instantiatedObject.GetComponent<Image>();
                Debug.LogWarning("Zdjecia nie maja prawidlowych linkow ");
                string url = item.value[0].href;
                Sprite imageSprite = await GetImageFromURL(url);
                if (imageSprite != null)
                {
                    image.sprite = imageSprite;
                }
                instantiatedObject = Instantiate(captionPrefab);
                stringContent = HandleCaption(item.value[0].caption);
                break;
            case "data":
            case "full-data":
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

    string HandleLabelRaw(LabelRaw labelRaw)
    {
        if (labelRaw == null) return "";
        switch (labelRaw.@class)
        {
            case "text":
                if (labelRaw.value == null) return "";
                if (labelRaw.value is string textValue) return textValue;
                Debug.LogWarning($"Expected string value for text class, instead got {labelRaw.value.GetType()}");
                break;
            case "link":
                if (!string.IsNullOrEmpty(labelRaw.value)) return $"[{labelRaw.value}]({labelRaw.value})";
                Debug.LogWarning($"Expected correct text for link class");
                break;
            default:
                Debug.LogWarning($"Unknown value class: {labelRaw.@class}");
                break;
        }
        return "";
    }

    string HandleCaption(List<object> caption)
    {
        if (caption == null || caption.Count == 0) return "";
        string result = "";
        foreach (var item in caption)
        {
            if (item is string str)
            {
                result += str;
            }
            else if (item is ValueRaw valueRaw)
            {
                result += HandleValueRaw(valueRaw);
            }
            else
            {
                Debug.LogWarning($"Unknown caption item type: {item.GetType()}");
            }
        }
        return result;
    }

    async Task<Sprite> GetImageFromURL(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        using (UnityWebRequest texReq = UnityWebRequestTexture.GetTexture(url))
        {
            var texOp = texReq.SendWebRequest();
            while (!texOp.isDone)
                await Task.Yield();
            
            if (texReq.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(texReq);
                    Debug.Log($"{tex.width}, {tex.height}");
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Błąd tworzenia Texture2D z {url}: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"Failed to download texture {url}: {texReq.error}");
            }
        }
        return null;
    }

}
