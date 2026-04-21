using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public class InfoboxParser
{
    public static WikiPageRaw Parse(string json)
    {
        return JsonConvert.DeserializeObject<WikiPageRaw>(json);
    }

    public static void DebugPrintItem(InfoboxItemRaw item)
    {
        Debug.Log("Class: " + item.@class);

        if (item.label != null)
            Debug.Log("Label: " + item.label.value);

        if (item.value == null) return;

        foreach (var v in item.value)
        {
            DebugPrintValue(v);

            if (!string.IsNullOrEmpty(v.text))
                Debug.Log("Link: " + v.text);

            if (!string.IsNullOrEmpty(v.href))
                Debug.Log("Href: " + v.href);
        }
    }

    static void DebugPrintValue(ValueRaw v)
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
                    DebugPrintValue(vr); // recursion
                }
            }
        }

        if (!string.IsNullOrEmpty(v.text))
            Debug.Log("Link: " + v.text);

        if (!string.IsNullOrEmpty(v.href))
            Debug.Log("Href: " + v.href);
    }
}
