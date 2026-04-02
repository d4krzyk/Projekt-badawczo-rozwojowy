using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Główny lokalny provider Wikipedii przenoszący logikę z:
/// - webapp/router.py
/// - webapp/wikipedia_webscraping/router.py
/// - webapp/utils.py
/// 
/// Zamiast backendowego API Unity pobiera dane bezpośrednio z Wikipedii
/// i składa obiekty potrzebne przez grę lokalnie w C#.
/// </summary>
public static class WikipediaRuntimeClient
{
    static readonly Dictionary<string, string> CanonicalTitleCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, string> PageHtmlCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, ArticleStructure> ArticleCache = new Dictionary<string, ArticleStructure>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, WikiPageRaw> InfoboxCache = new Dictionary<string, WikiPageRaw>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, List<Dictionary<string, string>>> ImagesCache = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);

    public static async Task<ArticleStructure> GetArticleDataAsync(string articleInput)
    {
        string canonicalTitle = await ResolveCanonicalTitleAsync(articleInput);
        if (string.IsNullOrWhiteSpace(canonicalTitle))
            return null;

        if (ArticleCache.TryGetValue(canonicalTitle, out ArticleStructure cachedArticle))
            return cachedArticle;

        string pageHtml = await GetPageHtmlAsync(canonicalTitle);
        if (string.IsNullOrWhiteSpace(pageHtml))
            return null;

        string category = await WikipediaCategoryResolver.GetMainCategoryAsync(canonicalTitle);
        ArticleStructure article = WikipediaArticleAssembler.BuildArticleFromHtml(canonicalTitle, pageHtml, category);
        if (article != null)
            ArticleCache[canonicalTitle] = article;

        return article;
    }

    public static async Task<WikiPageRaw> GetInfoboxAsync(string articleInput)
    {
        string canonicalTitle = await ResolveCanonicalTitleAsync(articleInput);
        if (string.IsNullOrWhiteSpace(canonicalTitle))
            return null;

        if (InfoboxCache.TryGetValue(canonicalTitle, out WikiPageRaw cachedInfobox))
            return cachedInfobox;

        string pageHtml = await GetPageHtmlAsync(canonicalTitle);
        if (string.IsNullOrWhiteSpace(pageHtml))
            return null;

        WikiPageRaw infobox = WikipediaInfoboxAssembler.BuildFromHtml(canonicalTitle, pageHtml);
        if (infobox != null)
            InfoboxCache[canonicalTitle] = infobox;

        return infobox;
    }

    public static async Task<List<Dictionary<string, string>>> GetImagesAsync(string articleInput)
    {
        string canonicalTitle = await ResolveCanonicalTitleAsync(articleInput);
        if (string.IsNullOrWhiteSpace(canonicalTitle))
            return new List<Dictionary<string, string>>();

        if (ImagesCache.TryGetValue(canonicalTitle, out List<Dictionary<string, string>> cachedImages))
            return cachedImages;

        string pageHtml = await GetPageHtmlAsync(canonicalTitle);
        if (string.IsNullOrWhiteSpace(pageHtml))
            return new List<Dictionary<string, string>>();

        List<Dictionary<string, string>> images = WikipediaImageExtractor.Extract(pageHtml);
        ImagesCache[canonicalTitle] = images;
        return images;
    }

    public static async Task<string> ResolveCanonicalTitleAsync(string articleInput)
    {
        string normalizedInput = WikipediaRuntimeUtility.NormalizeArticleInput(articleInput);
        if (string.IsNullOrWhiteSpace(normalizedInput))
            return null;

        if (CanonicalTitleCache.TryGetValue(normalizedInput, out string cachedTitle))
            return cachedTitle;

        string requestUrl = $"{WikipediaRuntimeUtility.SearchEndpoint}{UnityWebRequest.EscapeURL(normalizedInput)}";
        JObject json = await GetJsonAsync(requestUrl);

        string title = null;
        if (json?["pages"] is JArray pages && pages.Count > 0)
            title = pages[0]?["title"]?.Value<string>();

        if (string.IsNullOrWhiteSpace(title))
            title = normalizedInput;

        CanonicalTitleCache[normalizedInput] = title;
        CanonicalTitleCache[title] = title;
        return title;
    }

    public static async Task<string> GetPageHtmlAsync(string articleTitle)
    {
        if (string.IsNullOrWhiteSpace(articleTitle))
            return null;

        if (PageHtmlCache.TryGetValue(articleTitle, out string cachedHtml))
            return cachedHtml;

        string url =
            $"{WikipediaRuntimeUtility.QueryEndpoint}" +
            $"?action=parse&format=json&redirects=1&disableeditsection=1&disabletoc=1" +
            $"&page={UnityWebRequest.EscapeURL(articleTitle)}&prop=text";

        JObject json = await GetJsonAsync(url);
        string html = json?["parse"]?["text"]?["*"]?.Value<string>();
        if (!string.IsNullOrWhiteSpace(html))
            PageHtmlCache[articleTitle] = html;

        return html;
    }

    public static async Task<JObject> GetJsonAsync(string url)
    {
        string text = await GetTextAsync(url);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            return JObject.Parse(text);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[WikipediaRuntimeClient] JSON parse failed for '{url}': {exception.Message}");
            return null;
        }
    }

    public static async Task<string> GetTextAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("accept", "application/json, text/html");
            request.SetRequestHeader("User-Agent", WikipediaRuntimeUtility.UserAgent);

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
                return request.downloadHandler.text;

            Debug.LogWarning($"[WikipediaRuntimeClient] Request failed ({request.responseCode}) for '{url}': {request.error}");
            return null;
        }
    }
}
