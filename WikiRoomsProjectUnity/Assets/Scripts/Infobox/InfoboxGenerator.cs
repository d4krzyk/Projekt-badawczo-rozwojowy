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
    
    [Header("Content")]
    public RectTransform contentTransform;

    [HideInInspector] public bool HasFailed = false;
    int populationVersion = 0;

    public async Task PopulateUI(WikiPageRaw infoboxesData)
    {
        int populationId = BeginPopulation();
        ClearContent();
        if (infoboxesData == null || infoboxesData.infobox == null)
            return;
    
        foreach (var infobox in infoboxesData.infobox)
        {
            if (!IsPopulationCurrent(populationId))
                return;

            foreach (var item in infobox)
            {
                await HandleInfoboxItemRaw(item, populationId);
                if (!IsPopulationCurrent(populationId))
                    return;
            }
        }
    }

    public void CancelPopulation(bool clearContent = false)
    {
        populationVersion++;
        if (clearContent)
            ClearContent();
    }

    public void ClearContent()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }

    async Task HandleInfoboxItemRaw(InfoboxItemRaw item, int populationId)
    {
        if (!IsPopulationCurrent(populationId))
            return;

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
            string labelValueContent = "";
            foreach (var value in item.value)
            {
                labelValueContent += HandleValueRaw(value);
            }
            labelController.AddLabelValue(labelValueContent);
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
                string url = NormalizeImageUrl(item.value[0].href);
                Debug.Log($"Downloading image from URL: {url}");
                Sprite imageSprite = await GetImageFromURL(url);
                if (!IsPopulationCurrent(populationId))
                {
                    if (instantiatedObject != null)
                        Destroy(instantiatedObject);
                    return;
                }
                if (imageSprite != null)
                {
                    image.sprite = imageSprite;
                }
                instantiatedObject.transform.SetParent(this.transform, false);
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
        if (!IsPopulationCurrent(populationId))
        {
            Destroy(instantiatedObject);
            return;
        }
        instantiatedObject.transform.SetParent(this.transform, false);
        MarkdownRenderer mRenderer = null;
        if (item.@class == "header" || item.@class == "above")
        {
            // header ma TMP głębiej (Background -> TMP)
            mRenderer = instantiatedObject.GetComponentInChildren<MarkdownRenderer>();
        }
        else
        {
            // reszta prefabów ma renderer na root
            mRenderer = instantiatedObject.GetComponent<MarkdownRenderer>();
        }
        if (mRenderer == null) return;
        mRenderer.Source = stringContent;
    }

    int BeginPopulation()
    {
        populationVersion++;
        return populationVersion;
    }

    bool IsPopulationCurrent(int populationId)
    {
        return populationVersion == populationId;
    }

    string HandleValueRaw(ValueRaw valueRaw)
    {
        if (valueRaw == null) return "";
        switch (valueRaw.@class)
        {
            case "text_list_cont":
                if (valueRaw.value == null) return "";
                if (valueRaw.value is string text_list_cont) return $"{text_list_cont}\n";
                Debug.LogWarning($"Expected string value for text class, instead got {valueRaw.value.GetType()}");
                break;
            case "text":
                if (valueRaw.value == null) return "";
                if (valueRaw.value is string textValue) return textValue;
                Debug.LogWarning($"Expected string value for text class, instead got {valueRaw.value.GetType()}");
                break;
            case "link":
                if (!string.IsNullOrEmpty(valueRaw.text)) return $"[{valueRaw.text}]({BuildWikipediaUrl(valueRaw.href)})";
                Debug.LogWarning($"Expected correct text for link class");
                break;
            case "ulist":
                if (valueRaw.value == null) return "";
                string result = "";
                if (valueRaw.value is List<object> listValue)
                {
                    foreach (var item in listValue)
                    {
                        if(item is string str)
                        {
                            result += $"{str} ";
                        }
                        else if(item is ValueRaw vRaw)
                        {
                            result += $"{HandleValueRaw(vRaw)} ";
                        }
                        else
                        {
                            Debug.LogWarning($"Unknown ulist item type: {item.GetType()}");
                        }
                    }
                }
                return result;
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
                if (!string.IsNullOrEmpty(labelRaw.value)) return $"[{labelRaw.value}]({BuildWikipediaUrl(labelRaw.value)})";
                Debug.LogWarning($"Expected correct text for link class");
                break;
            default:
                Debug.LogWarning($"Unknown value class: {labelRaw.@class}");
                break;
        }
        return "";
    }

    string NormalizeImageUrl(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return rawUrl;
        if (rawUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            rawUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return rawUrl;
        }

        if (rawUrl.StartsWith("//", StringComparison.OrdinalIgnoreCase))
            return $"https:{rawUrl}";

        if (rawUrl.StartsWith("/"))
            return $"https://en.wikipedia.org{rawUrl}";

        if (rawUrl.StartsWith("./", StringComparison.OrdinalIgnoreCase))
            return $"https://en.wikipedia.org/wiki/{rawUrl.Substring(2)}";

        return rawUrl;
    }

    string BuildWikipediaUrl(string rawHref)
    {
        if (string.IsNullOrWhiteSpace(rawHref)) return "https://en.wikipedia.org";
        if (rawHref.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            rawHref.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return rawHref;
        }

        if (rawHref.StartsWith("//", StringComparison.OrdinalIgnoreCase))
            return $"https:{rawHref}";

        if (rawHref.StartsWith("/wiki/", StringComparison.OrdinalIgnoreCase))
            return $"https://en.wikipedia.org{rawHref}";

        if (rawHref.StartsWith("./", StringComparison.OrdinalIgnoreCase))
            return $"https://en.wikipedia.org/wiki/{rawHref.Substring(2)}";

        return $"https://en.wikipedia.org/wiki/{rawHref}";
    }

    string HandleCaption(object caption)
    {
        string result = "";
        if (caption is List<object> captionList)
        {    
            if (captionList == null || captionList.Count == 0) return "";
            foreach (var item in captionList)
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
        }
        else if (caption is string captionStr)
        {
            result = captionStr;
        }
        else if (caption is ValueRaw captionValueRaw)
        {
            result = HandleValueRaw(captionValueRaw);
        }
        else
        {
            Debug.LogWarning($"Unknown caption type: {caption.GetType()}");
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
