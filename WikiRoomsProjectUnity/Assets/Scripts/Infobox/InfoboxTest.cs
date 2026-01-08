using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class InfoboxTest : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(LoadJson());
    }

    IEnumerator LoadJson()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "infobox.json");

        string json;

        // Android / WebGL needs UnityWebRequest
        if (path.Contains("://") || path.Contains(":///"))
        {
            using UnityWebRequest www = UnityWebRequest.Get(path);
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(www.error);
                yield break;
            }

            json = www.downloadHandler.text;
        }
        else
        {
            json = File.ReadAllText(path);
        }

        // PARSE
        var page = InfoboxParser.Parse(json);

        Debug.Log("PAGE: " + page.page_name);

        foreach (var section in page.infobox)
        {
            Debug.Log("---- INFOBOX ----");

            foreach (var item in section)
            {
                PrintItem(item);
            }
        }
    }

    void PrintItem(InfoboxItemRaw item)
    {
        Debug.Log("Class: " + item.@class);

        if (item.label != null)
            Debug.Log("Label: " + item.label.value);

        if (item.value == null) return;

        foreach (var v in item.value)
        {
            PrintValue(v);

            if (!string.IsNullOrEmpty(v.text))
                Debug.Log("Link: " + v.text);

            if (!string.IsNullOrEmpty(v.href))
                Debug.Log("Href: " + v.href);
        }
    }

    void PrintValue(ValueRaw v)
    {
        // STRING
        if (v.value is string s)
        {
            Debug.Log("Text: " + s);
        }

        // LIST
        else if (v.value is List<object> list)
        {
            Debug.Log("LIST:");

            foreach (var item in list)
            {
                if (item is string txt)
                {
                    Debug.Log(" - " + txt);
                }
                else if (item is ValueRaw vr)
                {
                    PrintValue(vr); // recursion
                }
            }
        }

        if (!string.IsNullOrEmpty(v.text))
            Debug.Log("Link: " + v.text);

        if (!string.IsNullOrEmpty(v.href))
            Debug.Log("Href: " + v.href);
    }

}
