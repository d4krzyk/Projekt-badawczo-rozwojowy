using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Port logiki z webapp/wikipedia_api/category_batching.py.
/// Wykorzystuje lokalne shortcuts/stop z StreamingAssets,
/// ale działa bez backendowego endpointu /article.
/// </summary>
public static class WikipediaCategoryResolver
{
    static readonly Dictionary<string, string> CategoryShortcut = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<string> StopList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    static bool isInitialized;
    static Task initializationTask;

    public static async Task<string> GetMainCategoryAsync(string pageName)
    {
        string canonicalTitle = await WikipediaRuntimeClient.ResolveCanonicalTitleAsync(pageName);
        if (string.IsNullOrWhiteSpace(canonicalTitle))
            return WikipediaRuntimeUtility.DefaultTopCategory;

        await EnsureInitializedAsync();

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(canonicalTitle);

        while (queue.Count > 0 && visited.Count < 750)
        {
            var batch = new List<string>();
            while (queue.Count > 0 && batch.Count < 50)
            {
                string current = queue.Dequeue();
                if (string.IsNullOrWhiteSpace(current) || visited.Contains(current))
                    continue;

                if (StopList.Contains(current))
                    return ResolveShortcutChain(current);

                visited.Add(current);
                batch.Add(current);
            }

            if (batch.Count == 0)
                continue;

            foreach (string category in await ExtractCategoriesBatchAsync(batch))
            {
                if (!visited.Contains(category))
                    queue.Enqueue(category);
            }
        }

        return WikipediaRuntimeUtility.DefaultTopCategory;
    }

    static async Task EnsureInitializedAsync()
    {
        if (isInitialized)
            return;

        if (initializationTask == null)
            initializationTask = LoadLocalCachesAsync();

        await initializationTask;
    }

    static async Task LoadLocalCachesAsync()
    {
        string shortcutsJson = await WikipediaRuntimeUtility.LoadStreamingAssetTextAsync("shortcuts.json");
        if (!string.IsNullOrWhiteSpace(shortcutsJson))
        {
            try
            {
                Dictionary<string, string> shortcuts =
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(shortcutsJson);

                if (shortcuts != null)
                {
                    foreach (KeyValuePair<string, string> item in shortcuts)
                        CategoryShortcut[item.Key] = item.Value;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WikipediaCategoryResolver] Failed to parse shortcuts.json: {exception.Message}");
            }
        }

        string stopJson = await WikipediaRuntimeUtility.LoadStreamingAssetTextAsync("stop.json");
        if (!string.IsNullOrWhiteSpace(stopJson))
        {
            try
            {
                string[] stopEntries = JsonConvert.DeserializeObject<string[]>(stopJson);
                if (stopEntries != null)
                {
                    foreach (string item in stopEntries)
                        StopList.Add(item);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[WikipediaCategoryResolver] Failed to parse stop.json: {exception.Message}");
            }
        }

        isInitialized = true;
    }

    static async Task<List<string>> ExtractCategoriesBatchAsync(List<string> pageNames)
    {
        var result = new List<string>();
        if (pageNames == null || pageNames.Count == 0)
            return result;

        string titles = string.Join("|", pageNames);
        string url =
            $"{WikipediaRuntimeUtility.QueryEndpoint}" +
            $"?action=query&prop=categories&clshow=!hidden&formatversion=2&cllimit=100&format=json" +
            $"&titles={UnityWebRequest.EscapeURL(titles)}";

        JObject json = await WikipediaRuntimeClient.GetJsonAsync(url);
        JArray pages = json?["query"]?["pages"] as JArray;
        if (pages == null)
            return result;

        foreach (JToken page in pages)
        {
            JArray categories = page["categories"] as JArray;
            if (categories == null)
                continue;

            for (int i = categories.Count - 1; i >= 0; i--)
            {
                string title = categories[i]?["title"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(title))
                    result.Add(title);
            }
        }

        return result;
    }

    static string ResolveShortcutChain(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return WikipediaRuntimeUtility.DefaultTopCategory;

        string current = category;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (CategoryShortcut.TryGetValue(current, out string next) &&
               !string.IsNullOrWhiteSpace(next) &&
               !string.Equals(current, next, StringComparison.OrdinalIgnoreCase) &&
               seen.Add(current))
        {
            current = next;
        }

        return current.Split(new[] { "Category:" }, StringSplitOptions.None)[^1];
    }
}
