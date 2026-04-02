using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Uproszczony port webapp/wikipedia_api/images.py.
/// Dla gameplayu wystarcza lista obrazów artykułu poza infoboxem,
/// bez backendowego endpointu /images/generator.
/// </summary>
public static class WikipediaImageExtractor
{
    static readonly Regex ImageTagRegex = new Regex(
        @"<img\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static List<Dictionary<string, string>> Extract(string pageHtml)
    {
        var result = new List<Dictionary<string, string>>();
        if (string.IsNullOrWhiteSpace(pageHtml))
            return result;

        string scanHtml = RemoveInfobox(pageHtml);
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match imageMatch in ImageTagRegex.Matches(scanHtml))
        {
            string tagHtml = imageMatch.Value;
            string imageUrl = WikipediaRuntimeUtility.ChooseBestImageUrl(tagHtml);
            if (string.IsNullOrWhiteSpace(imageUrl))
                continue;

            if (IsVerySmallImage(tagHtml))
                continue;

            if (!WikipediaRuntimeUtility.IsLikelyGameplayImage(imageUrl))
                continue;

            if (!seenUrls.Add(imageUrl))
                continue;

            string caption = BuildCaption(tagHtml);
            result.Add(new Dictionary<string, string>
            {
                { imageUrl, caption }
            });
        }

        return result;
    }

    static string RemoveInfobox(string html)
    {
        int infoboxStart = WikipediaRuntimeUtility.FindTagStartByClass(html, "table", "infobox");
        if (infoboxStart < 0)
            return html;

        string infobox = WikipediaRuntimeUtility.ExtractBalancedTagBlock(html, infoboxStart, "table");
        if (string.IsNullOrWhiteSpace(infobox))
            return html;

        return html.Remove(infoboxStart, infobox.Length);
    }

    static string BuildCaption(string imageTagHtml)
    {
        string alt = WikipediaRuntimeUtility.CleanupPlainText(WikipediaRuntimeUtility.GetAttributeValue(imageTagHtml, "alt"));
        if (!string.IsNullOrWhiteSpace(alt))
            return alt;

        string resource = WikipediaRuntimeUtility.GetAttributeValue(imageTagHtml, "resource");
        if (resource.StartsWith("./File:", StringComparison.OrdinalIgnoreCase))
        {
            string fileName = resource.Substring("./File:".Length);
            return Uri.UnescapeDataString(fileName.Replace('_', ' '));
        }

        string src = WikipediaRuntimeUtility.GetAttributeValue(imageTagHtml, "src");
        return string.IsNullOrWhiteSpace(src) ? "[image]" : src;
    }

    static bool IsVerySmallImage(string imageTagHtml)
    {
        string widthRaw = WikipediaRuntimeUtility.GetAttributeValue(imageTagHtml, "width");
        string heightRaw = WikipediaRuntimeUtility.GetAttributeValue(imageTagHtml, "height");

        if (int.TryParse(widthRaw, out int width) && int.TryParse(heightRaw, out int height))
            return width < 96 && height < 96;

        return false;
    }
}
