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

    static readonly Regex FigcaptionRegex = new Regex(
        @"<figcaption\b[^>]*>(?<caption>.*?)</figcaption>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    static readonly Regex ThumbcaptionRegex = new Regex(
        @"<div\b[^>]*class\s*=\s*[""'][^""']*\bthumbcaption\b[^""']*[""'][^>]*>(?<caption>.*?)</div>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    static readonly Regex GalleryTextRegex = new Regex(
        @"<div\b[^>]*class\s*=\s*[""'][^""']*\bgallerytext\b[^""']*[""'][^>]*>(?<caption>.*?)</div>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static List<Dictionary<string, string>> Extract(string pageHtml)
    {
        var result = new List<Dictionary<string, string>>();
        if (string.IsNullOrWhiteSpace(pageHtml))
            return result;

        HashSet<string> infoboxImageKeys = CollectInfoboxImageKeys(pageHtml);
        string scanHtml = RemoveAllInfoboxes(pageHtml);
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match imageMatch in ImageTagRegex.Matches(scanHtml))
        {
            string tagHtml = imageMatch.Value;
            string imageUrl = WikipediaRuntimeUtility.ChooseGameplayImageUrl(tagHtml);
            if (string.IsNullOrWhiteSpace(imageUrl))
                continue;

            string imageKey = GetCanonicalImageKey(imageUrl);
            if (!string.IsNullOrWhiteSpace(imageKey) && infoboxImageKeys.Contains(imageKey))
                continue;

            if (IsVerySmallImage(tagHtml))
                continue;

            if (!WikipediaRuntimeUtility.IsLikelyGameplayImage(imageUrl, tagHtml))
                continue;

            if (!seenUrls.Add(imageUrl))
                continue;

            string caption = BuildCaption(tagHtml, scanHtml, imageMatch.Index);
            result.Add(new Dictionary<string, string>
            {
                { imageUrl, caption }
            });
        }

        return result;
    }

    static string RemoveAllInfoboxes(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        string remaining = html;
        while (true)
        {
            int infoboxStart = WikipediaRuntimeUtility.FindTagStartByClass(remaining, "table", "infobox");
            if (infoboxStart < 0)
                break;

            string infobox = WikipediaRuntimeUtility.ExtractBalancedTagBlock(remaining, infoboxStart, "table");
            if (string.IsNullOrWhiteSpace(infobox))
                break;

            remaining = remaining.Remove(infoboxStart, infobox.Length);
        }

        return remaining;
    }

    static HashSet<string> CollectInfoboxImageKeys(string html)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(html))
            return keys;

        string remaining = html;
        while (true)
        {
            int infoboxStart = WikipediaRuntimeUtility.FindTagStartByClass(remaining, "table", "infobox");
            if (infoboxStart < 0)
                break;

            string infobox = WikipediaRuntimeUtility.ExtractBalancedTagBlock(remaining, infoboxStart, "table");
            if (string.IsNullOrWhiteSpace(infobox))
                break;

            foreach (Match imageMatch in ImageTagRegex.Matches(infobox))
            {
                string infoboxImageTag = imageMatch.Value;
                string infoboxImageUrl = WikipediaRuntimeUtility.ChooseBestImageUrl(infoboxImageTag);
                if (string.IsNullOrWhiteSpace(infoboxImageUrl))
                    infoboxImageUrl = WikipediaRuntimeUtility.GetAttributeValue(infoboxImageTag, "src");

                string key = GetCanonicalImageKey(infoboxImageUrl);
                if (!string.IsNullOrWhiteSpace(key))
                    keys.Add(key);
            }

            remaining = remaining.Remove(infoboxStart, infobox.Length);
        }

        return keys;
    }

    static string GetCanonicalImageKey(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return string.Empty;

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri uri))
            return imageUrl.Trim();

        string path = Uri.UnescapeDataString(uri.AbsolutePath ?? string.Empty);
        if (string.IsNullOrWhiteSpace(path))
            return imageUrl.Trim();

        int thumbIdx = path.IndexOf("/thumb/", StringComparison.OrdinalIgnoreCase);
        if (thumbIdx >= 0)
        {
            string thumbPart = path.Substring(thumbIdx + "/thumb/".Length).TrimStart('/');
            string[] parts = thumbPart.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                // Dla thumb URL pomijamy końcowy segment "<size>px-<filename>", aby różne rozmiary mapowały się do tego samego obrazu.
                return string.Join("/", parts, 0, parts.Length - 1);
            }
        }

        int commonsIdx = path.IndexOf("/commons/", StringComparison.OrdinalIgnoreCase);
        if (commonsIdx >= 0)
            return path.Substring(commonsIdx + "/commons/".Length).TrimStart('/');

        string[] rawParts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (rawParts.Length > 0)
            return rawParts[rawParts.Length - 1];

        return path.Trim();
    }

    static string BuildCaption(string imageTagHtml, string scanHtml, int imageTagIndex)
    {
        string nearbyCaptionHtml = TryExtractNearbyCaptionHtml(scanHtml, imageTagIndex);
        string nearbyCaption = NormalizeCaptionFromHtml(nearbyCaptionHtml);
        if (IsUsableCaption(nearbyCaption))
            return nearbyCaption;

        string alt = WikipediaRuntimeUtility.CleanupPlainText(WikipediaRuntimeUtility.GetAttributeValue(imageTagHtml, "alt"));
        if (IsUsableCaption(alt))
            return alt;

        string resource = WikipediaRuntimeUtility.GetAttributeValue(imageTagHtml, "resource");
        if (resource.StartsWith("./File:", StringComparison.OrdinalIgnoreCase))
        {
            string fileName = resource.Substring("./File:".Length);
            string decoded = Uri.UnescapeDataString(fileName.Replace('_', ' '));
            if (IsUsableCaption(decoded))
                return decoded;
        }

        string src = WikipediaRuntimeUtility.GetAttributeValue(imageTagHtml, "src");
        string fromUrl = TryBuildCaptionFromImageUrl(src);
        if (IsUsableCaption(fromUrl))
            return fromUrl;

        return "[no caption]";
    }

    static string NormalizeCaptionFromHtml(string captionHtml)
    {
        if (string.IsNullOrWhiteSpace(captionHtml))
            return string.Empty;

        // Zachowaj linki z podpisu jako markdown, aby Hover UI mogło je renderować jako klikalne.
        string markdown = WikipediaRuntimeUtility.ConvertHtmlToMarkdownish(captionHtml);
        if (IsUsableCaption(markdown))
            return markdown;

        return WikipediaRuntimeUtility.CleanupPlainText(captionHtml);
    }

    static string TryExtractNearbyCaptionHtml(string scanHtml, int imageTagIndex)
    {
        if (string.IsNullOrWhiteSpace(scanHtml) || imageTagIndex < 0 || imageTagIndex >= scanHtml.Length)
            return string.Empty;

        int windowStart = Math.Max(0, imageTagIndex - 128);
        int windowEnd = Math.Min(scanHtml.Length, imageTagIndex + 2048);
        string window = scanHtml.Substring(windowStart, windowEnd - windowStart);

        int imageIndexInWindow = imageTagIndex - windowStart;
        int nextImageIndex = window.IndexOf("<img", imageIndexInWindow + 1, StringComparison.OrdinalIgnoreCase);
        string segment = nextImageIndex > imageIndexInWindow
            ? window.Substring(imageIndexInWindow, nextImageIndex - imageIndexInWindow)
            : window.Substring(imageIndexInWindow);

        string figcaption = ExtractCaptionGroup(segment, FigcaptionRegex);
        if (!string.IsNullOrWhiteSpace(figcaption))
            return figcaption;

        string thumbcaption = ExtractCaptionGroup(segment, ThumbcaptionRegex);
        if (!string.IsNullOrWhiteSpace(thumbcaption))
            return thumbcaption;

        return ExtractCaptionGroup(segment, GalleryTextRegex);
    }

    static string ExtractCaptionGroup(string html, Regex regex)
    {
        if (string.IsNullOrWhiteSpace(html) || regex == null)
            return string.Empty;

        Match match = regex.Match(html);
        return match.Success ? match.Groups["caption"].Value : string.Empty;
    }

    static string TryBuildCaptionFromImageUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        string path = url;
        int queryIndex = path.IndexOfAny(new[] { '?', '#' });
        if (queryIndex >= 0)
            path = path.Substring(0, queryIndex);

        string fileName = string.Empty;
        int thumbIndex = path.IndexOf("/thumb/", StringComparison.OrdinalIgnoreCase);
        if (thumbIndex >= 0)
        {
            string thumbPart = path.Substring(thumbIndex + "/thumb/".Length);
            string[] parts = thumbPart.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                fileName = parts[parts.Length - 2];
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
                fileName = parts[parts.Length - 1];
        }

        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        string decoded = Uri.UnescapeDataString(fileName).Replace('_', ' ');
        decoded = Regex.Replace(decoded, @"^\d+px-", string.Empty, RegexOptions.IgnoreCase);
        return decoded;
    }

    static bool IsUsableCaption(string caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
            return false;

        string trimmed = caption.Trim();
        if (trimmed.Equals("[image]", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("[no caption]", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("//", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
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
