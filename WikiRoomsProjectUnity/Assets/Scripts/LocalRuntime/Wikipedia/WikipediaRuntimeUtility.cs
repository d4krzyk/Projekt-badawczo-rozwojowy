using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Narzędzia przeniesione z:
/// - webapp/utils.py
/// - webapp/wikipedia_webscraping/content.py
/// - webapp/wikipedia_webscraping/infobox.py
/// - webapp/wikipedia_api/images.py
/// 
/// Zastępują serwerowy scraping lokalnym parserem C# w Unity.
/// </summary>
public static class WikipediaRuntimeUtility
{
    public const string WikipediaBaseUrl = "https://en.wikipedia.org";
    public const string SearchEndpoint = "https://en.wikipedia.org/w/rest.php/v1/search/page?limit=1&q=";
    public const string QueryEndpoint = "https://en.wikipedia.org/w/api.php";
    public const string DefaultTopCategory = "Main topic articles";

    public static readonly HashSet<string> SkipSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "See also",
        "Further reading",
        "External links",
        "Notes",
        "References",
        "Bibliography",
        "Sources",
        "Citations",
    };

    public static readonly string[] HardExcludedImageSubstrings =
    {
        "Wikisource-logo.svg",
        "Wiki_letter_w_cropped.svg",
        "Wikimedia-logo.svg",
        "Wikipedia-logo",
    };

    public static readonly string[] ContextualMapSubstrings =
    {
        "Location_map",
        "BlankMap",
        "Locator_map",
    };

    public static readonly string[] DecorativeHintSubstrings =
    {
        "icon",
        "pictogram",
        "symbol",
    };

    static readonly Regex AnchorRegex = new Regex(
        @"<a\b(?<attrs>[^>]*)>(?<text>.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    static readonly Regex ReferenceRegex = new Regex(
        @"<sup\b[^>]*class\s*=\s*[""'][^""']*reference[^""']*[""'][^>]*>.*?</sup>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    static readonly Regex CommentRegex = new Regex(
        @"<!--.*?-->",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public static string UserAgent => "WikiRoomsUnityLocal/1.0 (+https://github.com/d4krzyk/Projekt-badawczo-rozwojowy)";

    public static string NormalizeArticleInput(string articleOrUrl)
    {
        if (string.IsNullOrWhiteSpace(articleOrUrl))
            return string.Empty;

        string trimmed = articleOrUrl.Trim();
        if (trimmed.IndexOf("/wiki/", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            int wikiIndex = trimmed.IndexOf("/wiki/", StringComparison.OrdinalIgnoreCase);
            if (wikiIndex >= 0)
            {
                string articlePart = trimmed.Substring(wikiIndex + "/wiki/".Length);
                int hashIndex = articlePart.IndexOf('#');
                if (hashIndex >= 0)
                    articlePart = articlePart.Substring(0, hashIndex);

                return Uri.UnescapeDataString(articlePart.Replace('_', ' '));
            }
        }

        return trimmed.Replace('_', ' ');
    }

    public static string BuildArticleUrl(string articleName)
    {
        if (string.IsNullOrWhiteSpace(articleName))
            return string.Empty;

        string title = articleName.Trim().Replace(' ', '_');
        return $"{WikipediaBaseUrl}/wiki/{title}";
    }

    public static string BuildWikipediaLink(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return string.Empty;

        if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return href;
        }

        if (href.StartsWith("//", StringComparison.OrdinalIgnoreCase))
            return $"https:{href}";

        if (href.StartsWith("./", StringComparison.OrdinalIgnoreCase))
            return $"{WikipediaBaseUrl}/wiki/{href.Substring(2)}";

        if (href.StartsWith("/wiki/", StringComparison.OrdinalIgnoreCase))
            return $"{WikipediaBaseUrl}{href}";

        return href;
    }

    public static string GetAttributeValue(string tagHtml, string attributeName)
    {
        if (string.IsNullOrWhiteSpace(tagHtml) || string.IsNullOrWhiteSpace(attributeName))
            return string.Empty;

        Match match = Regex.Match(
            tagHtml,
            $@"\b{Regex.Escape(attributeName)}\s*=\s*[""'](?<value>[^""']*)[""']",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return match.Success ? WebUtility.HtmlDecode(match.Groups["value"].Value) : string.Empty;
    }

    public static string CleanupPlainText(string html, bool preserveLineBreaks = false)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        string cleaned = html;
        cleaned = CommentRegex.Replace(cleaned, string.Empty);
        cleaned = ReferenceRegex.Replace(cleaned, string.Empty);
        cleaned = Regex.Replace(cleaned, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"</p\s*>", "\n", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"<li\b[^>]*>", "- ", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"</li\s*>", "\n", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"<[^>]+>", " ");
        cleaned = WebUtility.HtmlDecode(cleaned).Replace('\u00A0', ' ');

        if (!preserveLineBreaks)
        {
            cleaned = cleaned.Replace("\r", " ").Replace("\n", " ");
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ");
            return cleaned.Trim();
        }

        cleaned = cleaned.Replace("\r", string.Empty);
        cleaned = Regex.Replace(cleaned, @"[ \t]+\n", "\n");
        cleaned = Regex.Replace(cleaned, @"\n[ \t]+", "\n");
        cleaned = Regex.Replace(cleaned, @"[ \t]{2,}", " ");
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");

        string[] lines = cleaned
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        return string.Join("\n", lines);
    }

    public static string ConvertHtmlToMarkdownish(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        string working = html;
        working = CommentRegex.Replace(working, string.Empty);
        working = ReferenceRegex.Replace(working, string.Empty);
        working = Regex.Replace(working, @"<(table|style|script|noscript|math)\b.*?</\1>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        working = Regex.Replace(working, @"<img\b[^>]*>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        working = Regex.Replace(working, @"<span\b[^>]*class\s*=\s*[""']mw-editsection[""'][^>]*>.*?</span>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        working = Regex.Replace(working, @"<span\b[^>]*class\s*=\s*[""']mw-editsection-bracket[""'][^>]*>.*?</span>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        working = Regex.Replace(working, @"<li\b[^>]*>", "- ", RegexOptions.IgnoreCase);
        working = Regex.Replace(working, @"</li\s*>", "\n", RegexOptions.IgnoreCase);
        working = Regex.Replace(working, @"</p\s*>", "\n", RegexOptions.IgnoreCase);
        working = Regex.Replace(working, @"</div\s*>", "\n", RegexOptions.IgnoreCase);
        working = Regex.Replace(working, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        working = AnchorRegex.Replace(working, match =>
        {
            string href = GetAttributeValue(match.Value, "href");
            string text = CleanupPlainText(match.Groups["text"].Value);

            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            if (href.StartsWith("/wiki/", StringComparison.OrdinalIgnoreCase))
                return $"[{text}]({BuildWikipediaLink(href)})";

            return text;
        });

        working = CleanupPlainText(working, preserveLineBreaks: true);
        if (string.IsNullOrWhiteSpace(working))
            return string.Empty;

        return string.Join("<br>", working
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    public static List<ValueRaw> ConvertHtmlToValueRaws(string html)
    {
        var result = new List<ValueRaw>();
        if (string.IsNullOrWhiteSpace(html))
            return result;

        string working = CommentRegex.Replace(html, string.Empty);
        working = ReferenceRegex.Replace(working, string.Empty);
        working = Regex.Replace(working, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        working = Regex.Replace(working, @"<li\b[^>]*>", "- ", RegexOptions.IgnoreCase);
        working = Regex.Replace(working, @"</li\s*>", "\n", RegexOptions.IgnoreCase);

        int cursor = 0;
        foreach (Match match in AnchorRegex.Matches(working))
        {
            AddTextSegment(working.Substring(cursor, match.Index - cursor), result);

            string href = GetAttributeValue(match.Value, "href");
            string text = CleanupPlainText(match.Groups["text"].Value);
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (!string.IsNullOrWhiteSpace(href) &&
                    (href.StartsWith("/wiki/", StringComparison.OrdinalIgnoreCase) ||
                     href.StartsWith("./", StringComparison.OrdinalIgnoreCase) ||
                     href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                     href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(new ValueRaw
                    {
                        @class = "link",
                        text = text,
                        href = href,
                    });
                }
                else
                {
                    result.Add(new ValueRaw
                    {
                        @class = "text",
                        value = text,
                    });
                }
            }

            cursor = match.Index + match.Length;
        }

        AddTextSegment(working.Substring(cursor), result);
        return result;
    }

    public static string ExtractBalancedTagBlock(string html, int openTagIndex, string tagName)
    {
        if (string.IsNullOrWhiteSpace(html) || openTagIndex < 0 || string.IsNullOrWhiteSpace(tagName))
            return string.Empty;

        Regex tagRegex = new Regex(
            $@"<(?<close>/)?{Regex.Escape(tagName)}\b[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        int depth = 0;
        bool started = false;

        foreach (Match match in tagRegex.Matches(html, openTagIndex))
        {
            bool isClose = match.Groups["close"].Success;
            bool isSelfClosing = match.Value.EndsWith("/>", StringComparison.Ordinal);

            if (!started)
            {
                if (isClose)
                    continue;

                started = true;
            }

            if (!isClose && !isSelfClosing)
            {
                depth++;
            }
            else if (isClose)
            {
                depth--;
            }

            if (started && depth == 0)
            {
                int length = (match.Index + match.Length) - openTagIndex;
                return html.Substring(openTagIndex, length);
            }
        }

        return string.Empty;
    }

    public static int FindTagStartByClass(string html, string tagName, string className)
    {
        if (string.IsNullOrWhiteSpace(html))
            return -1;

        Match match = Regex.Match(
            html,
            $@"<{Regex.Escape(tagName)}\b[^>]*class\s*=\s*[""'][^""']*\b{Regex.Escape(className)}\b[^""']*[""'][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return match.Success ? match.Index : -1;
    }

    public static string ChooseBestImageUrl(string tagHtml)
    {
        if (string.IsNullOrWhiteSpace(tagHtml))
            return string.Empty;

        string srcSet = GetAttributeValue(tagHtml, "srcset");
        if (!string.IsNullOrWhiteSpace(srcSet))
        {
            string lastEntry = srcSet
                .Split(',')
                .Select(part => part.Trim())
                .LastOrDefault();

            if (!string.IsNullOrWhiteSpace(lastEntry))
            {
                string bestUrl = lastEntry.Split(' ')[0].Trim();
                return BuildWikipediaLink(bestUrl);
            }
        }

        string src = GetAttributeValue(tagHtml, "src");
        return BuildWikipediaLink(src);
    }

    public static string ChooseGameplayImageUrl(string tagHtml, int preferredWidth = 768)
    {
        if (string.IsNullOrWhiteSpace(tagHtml))
            return string.Empty;

        string srcSet = GetAttributeValue(tagHtml, "srcset");
        if (!string.IsNullOrWhiteSpace(srcSet))
        {
            string preferred = ChoosePreferredSrcSetUrl(srcSet, preferredWidth, 1.5f);
            if (!string.IsNullOrWhiteSpace(preferred))
                return BuildWikipediaLink(preferred);
        }

        string src = GetAttributeValue(tagHtml, "src");
        return BuildWikipediaLink(src);
    }

    static string ChoosePreferredSrcSetUrl(string srcSet, int preferredWidth, float preferredScale)
    {
        if (string.IsNullOrWhiteSpace(srcSet))
            return string.Empty;

        string firstUrl = string.Empty;
        string widthOverUrl = string.Empty;
        int widthOver = int.MaxValue;
        string widthUnderUrl = string.Empty;
        int widthUnder = -1;
        string scaleOverUrl = string.Empty;
        float scaleOver = float.MaxValue;
        string scaleUnderUrl = string.Empty;
        float scaleUnder = -1f;

        string[] entries = srcSet.Split(',');
        for (int idx = 0; idx < entries.Length; idx++)
        {
            string entry = entries[idx]?.Trim();
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            string[] tokens = entry.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                continue;

            string url = tokens[0].Trim();
            if (string.IsNullOrWhiteSpace(url))
                continue;

            if (string.IsNullOrWhiteSpace(firstUrl))
                firstUrl = url;

            int width = 0;
            float scale = 0f;
            for (int tokenIndex = 1; tokenIndex < tokens.Length; tokenIndex++)
            {
                string descriptor = tokens[tokenIndex].Trim();
                if (descriptor.EndsWith("w", StringComparison.OrdinalIgnoreCase))
                {
                    string number = descriptor.Substring(0, descriptor.Length - 1);
                    int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out width);
                }
                else if (descriptor.EndsWith("x", StringComparison.OrdinalIgnoreCase))
                {
                    string number = descriptor.Substring(0, descriptor.Length - 1);
                    float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out scale);
                }
            }

            if (width > 0)
            {
                if (width >= preferredWidth && width < widthOver)
                {
                    widthOver = width;
                    widthOverUrl = url;
                }
                else if (width < preferredWidth && width > widthUnder)
                {
                    widthUnder = width;
                    widthUnderUrl = url;
                }

                continue;
            }

            if (scale > 0f)
            {
                if (scale >= preferredScale && scale < scaleOver)
                {
                    scaleOver = scale;
                    scaleOverUrl = url;
                }
                else if (scale < preferredScale && scale > scaleUnder)
                {
                    scaleUnder = scale;
                    scaleUnderUrl = url;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(widthOverUrl))
            return widthOverUrl;
        if (!string.IsNullOrWhiteSpace(widthUnderUrl))
            return widthUnderUrl;
        if (!string.IsNullOrWhiteSpace(scaleOverUrl))
            return scaleOverUrl;
        if (!string.IsNullOrWhiteSpace(scaleUnderUrl))
            return scaleUnderUrl;

        return firstUrl;
    }

    public static bool IsLikelyGameplayImage(string imageUrl, string imageTagHtml = null)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return false;

        if (HardExcludedImageSubstrings.Any(bad =>
            imageUrl.IndexOf(bad, StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return false;
        }

        if (ContextualMapSubstrings.Any(bad =>
            imageUrl.IndexOf(bad, StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return false;
        }

        // Pozostawiamy heraldyczne i państwowe motywy (flag/seal/emblem/coat_of_arms),
        // bo często są kluczową treścią artykułu, a nie tylko dekoracją interfejsu.
        if (!string.IsNullOrWhiteSpace(imageTagHtml) && HasDecorativeUrlHint(imageUrl))
        {
            string alt = GetAttributeValue(imageTagHtml, "alt");
            bool hasMeaningfulAlt = !string.IsNullOrWhiteSpace(alt) && alt.Trim().Length >= 6;
            bool looksTiny = LooksVerySmallByTag(imageTagHtml);

            if (looksTiny && !hasMeaningfulAlt)
                return false;
        }

        return true;
    }

    static bool HasDecorativeUrlHint(string imageUrl)
    {
        return DecorativeHintSubstrings.Any(hint =>
            imageUrl.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    static bool LooksVerySmallByTag(string imageTagHtml)
    {
        if (string.IsNullOrWhiteSpace(imageTagHtml))
            return false;

        string widthRaw = GetAttributeValue(imageTagHtml, "width");
        string heightRaw = GetAttributeValue(imageTagHtml, "height");

        int width = 0;
        int height = 0;
        int.TryParse(widthRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out width);
        int.TryParse(heightRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out height);

        if (width > 0 && width <= 128)
            return true;
        if (height > 0 && height <= 128)
            return true;

        return false;
    }

    public static async Task<string> LoadStreamingAssetTextAsync(string fileName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, "WikiRoomsLocal", fileName);

        if (path.StartsWith("jar:", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("://"))
        {
            using (UnityWebRequest request = UnityWebRequest.Get(path))
            {
                request.SetRequestHeader("User-Agent", UserAgent);
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                    return request.downloadHandler.text;

                Debug.LogWarning($"[WikipediaRuntimeUtility] Failed to load StreamingAssets file '{fileName}': {request.error}");
                return null;
            }
        }

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[WikipediaRuntimeUtility] Missing StreamingAssets file: {path}");
            return null;
        }

        return await File.ReadAllTextAsync(path);
    }

    static void AddTextSegment(string rawHtml, List<ValueRaw> target)
    {
        string text = CleanupPlainText(rawHtml, preserveLineBreaks: true);
        if (string.IsNullOrWhiteSpace(text))
            return;

        target.Add(new ValueRaw
        {
            @class = "text",
            value = text.Replace("\n", " "),
        });
    }
}
