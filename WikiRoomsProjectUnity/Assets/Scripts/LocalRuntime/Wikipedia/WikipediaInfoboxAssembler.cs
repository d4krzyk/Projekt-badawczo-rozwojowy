using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Port logiki z webapp/wikipedia_webscraping/infobox.py.
/// Renderuje uproszczony, ale lokalny odpowiednik backendowego JSON infoboxu.
/// </summary>
public static class WikipediaInfoboxAssembler
{
    static readonly Regex RowRegex = new Regex(
        @"<tr\b[^>]*>.*?</tr>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static WikiPageRaw BuildFromHtml(string articleTitle, string pageHtml)
    {
        if (string.IsNullOrWhiteSpace(pageHtml))
        {
            return new WikiPageRaw
            {
                page_name = articleTitle,
                infobox = new List<List<InfoboxItemRaw>>(),
            };
        }

        int infoboxStart = WikipediaRuntimeUtility.FindTagStartByClass(pageHtml, "table", "infobox");
        if (infoboxStart < 0)
        {
            return new WikiPageRaw
            {
                page_name = articleTitle,
                infobox = new List<List<InfoboxItemRaw>>(),
            };
        }

        string infoboxTable = WikipediaRuntimeUtility.ExtractBalancedTagBlock(pageHtml, infoboxStart, "table");
        if (string.IsNullOrWhiteSpace(infoboxTable))
        {
            return new WikiPageRaw
            {
                page_name = articleTitle,
                infobox = new List<List<InfoboxItemRaw>>(),
            };
        }

        var items = new List<InfoboxItemRaw>();
        foreach (Match rowMatch in RowRegex.Matches(infoboxTable))
        {
            string rowHtml = rowMatch.Value;
            InfoboxItemRaw item = TryBuildItem(rowHtml);
            if (item != null)
                items.Add(item);
        }

        return new WikiPageRaw
        {
            page_name = articleTitle,
            infobox = items.Count > 0
                ? new List<List<InfoboxItemRaw>> { items }
                : new List<List<InfoboxItemRaw>>(),
        };
    }

    static InfoboxItemRaw TryBuildItem(string rowHtml)
    {
        if (string.IsNullOrWhiteSpace(rowHtml))
            return null;

        string aboveHtml = ExtractCellByClass(rowHtml, "infobox-above");
        if (!string.IsNullOrWhiteSpace(aboveHtml))
            return CreateTextualItem("above", aboveHtml);

        string headerHtml = ExtractCellByClass(rowHtml, "infobox-header");
        if (!string.IsNullOrWhiteSpace(headerHtml))
            return CreateTextualItem("header", headerHtml);

        string belowHtml = ExtractCellByClass(rowHtml, "infobox-below");
        if (!string.IsNullOrWhiteSpace(belowHtml))
            return CreateTextualItem("text", belowHtml);

        string imageHtml = ExtractCellByClass(rowHtml, "infobox-image");
        if (!string.IsNullOrWhiteSpace(imageHtml))
            return CreateImageItem(imageHtml);

        string fullDataHtml = ExtractCellByClass(rowHtml, "infobox-full-data");
        if (!string.IsNullOrWhiteSpace(fullDataHtml))
            return CreateDataItem(null, fullDataHtml, "full-data");

        string labelHtml = ExtractCellByClass(rowHtml, "infobox-label");
        string dataHtml = ExtractCellByClass(rowHtml, "infobox-data");
        if (!string.IsNullOrWhiteSpace(labelHtml) || !string.IsNullOrWhiteSpace(dataHtml))
            return CreateDataItem(labelHtml, dataHtml, "data");

        return null;
    }

    static InfoboxItemRaw CreateTextualItem(string className, string innerHtml)
    {
        List<ValueRaw> values = WikipediaRuntimeUtility.ConvertHtmlToValueRaws(innerHtml);
        if (values.Count == 0)
            return null;

        return new InfoboxItemRaw
        {
            @class = className,
            value = values,
        };
    }

    static InfoboxItemRaw CreateDataItem(string labelHtml, string dataHtml, string className)
    {
        List<ValueRaw> values = WikipediaRuntimeUtility.ConvertHtmlToValueRaws(dataHtml ?? string.Empty);
        if (values.Count == 0 && string.IsNullOrWhiteSpace(labelHtml))
            return null;

        LabelRaw label = null;
        string labelText = WikipediaRuntimeUtility.CleanupPlainText(labelHtml);
        if (!string.IsNullOrWhiteSpace(labelText))
        {
            label = new LabelRaw
            {
                @class = "text",
                value = labelText,
            };
        }

        return new InfoboxItemRaw
        {
            @class = className,
            label = label,
            value = values,
        };
    }

    static InfoboxItemRaw CreateImageItem(string imageCellHtml)
    {
        Match imageMatch = Regex.Match(imageCellHtml, @"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!imageMatch.Success)
            return null;

        string imageUrl = WikipediaRuntimeUtility.ChooseBestImageUrl(imageMatch.Value);
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        string captionHtml = ExtractCellByClass(imageCellHtml, "infobox-caption");
        List<ValueRaw> captionValues = WikipediaRuntimeUtility.ConvertHtmlToValueRaws(captionHtml);

        return new InfoboxItemRaw
        {
            @class = "image",
            value = new List<ValueRaw>
            {
                new ValueRaw
                {
                    @class = "link",
                    href = imageUrl,
                    caption = captionValues.Cast<object>().ToList(),
                },
            },
        };
    }

    static string ExtractCellByClass(string html, string className)
    {
        if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(className))
            return string.Empty;

        Match match = Regex.Match(
            html,
            $@"<(?<tag>\w+)\b[^>]*class\s*=\s*[""'][^""']*\b{Regex.Escape(className)}\b[^""']*[""'][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!match.Success)
            return string.Empty;

        string tagName = match.Groups["tag"].Value;
        string fullBlock = WikipediaRuntimeUtility.ExtractBalancedTagBlock(html, match.Index, tagName);
        if (string.IsNullOrWhiteSpace(fullBlock))
            return string.Empty;

        int contentStart = fullBlock.IndexOf('>');
        int contentEnd = fullBlock.LastIndexOf($"</{tagName}", StringComparison.OrdinalIgnoreCase);
        if (contentStart < 0 || contentEnd <= contentStart)
            return string.Empty;

        return fullBlock.Substring(contentStart + 1, contentEnd - contentStart - 1);
    }
}
