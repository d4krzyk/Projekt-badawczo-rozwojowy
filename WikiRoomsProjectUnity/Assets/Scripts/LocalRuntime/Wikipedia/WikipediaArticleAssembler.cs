using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Port logiki z webapp/wikipedia_webscraping/content.py.
/// Buduje ArticleStructure bez pośredniego backendowego JSON.
/// </summary>
public static class WikipediaArticleAssembler
{
    static readonly Regex HeadingRegex = new Regex(
        @"<h(?<level>[234])\b[^>]*>(?<content>.*?)</h\k<level>>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    static readonly Regex HeadlineRegex = new Regex(
        @"<span\b[^>]*class\s*=\s*[""'][^""']*mw-headline[^""']*[""'][^>]*>(?<title>.*?)</span>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static ArticleStructure BuildArticleFromHtml(string articleTitle, string pageHtml, string category)
    {
        var topSections = new List<Section>();
        if (string.IsNullOrWhiteSpace(pageHtml))
        {
            return new ArticleStructure
            {
                name = articleTitle,
                url = WikipediaRuntimeUtility.BuildArticleUrl(articleTitle),
                category = string.IsNullOrWhiteSpace(category) ? WikipediaRuntimeUtility.DefaultTopCategory : category,
                content = Array.Empty<Section>(),
            };
        }

        MatchCollection headings = HeadingRegex.Matches(pageHtml);

        string introChunk = headings.Count > 0
            ? pageHtml.Substring(0, headings[0].Index)
            : pageHtml;

        string introText = WikipediaRuntimeUtility.ConvertHtmlToMarkdownish(introChunk);
        if (!string.IsNullOrWhiteSpace(introText))
        {
            topSections.Add(new Section
            {
                name = "Introduction",
                content = introText,
            });
        }

        var root = new SectionBuilder { Level = 1, Children = topSections };
        var allBuilders = new List<SectionBuilder> { root };
        var stack = new Stack<SectionBuilder>();
        stack.Push(root);

        for (int i = 0; i < headings.Count; i++)
        {
            Match heading = headings[i];
            int level = int.Parse(heading.Groups["level"].Value);
            string title = ExtractHeadingTitle(heading.Groups["content"].Value);
            if (string.IsNullOrWhiteSpace(title) || WikipediaRuntimeUtility.SkipSections.Contains(title))
                continue;

            int chunkStart = heading.Index + heading.Length;
            int chunkEnd = i + 1 < headings.Count ? headings[i + 1].Index : pageHtml.Length;
            string chunkHtml = pageHtml.Substring(chunkStart, chunkEnd - chunkStart);
            string content = WikipediaRuntimeUtility.ConvertHtmlToMarkdownish(chunkHtml);

            var section = new Section
            {
                name = title,
                content = content,
            };

            while (stack.Count > 1 && stack.Peek().Level >= level)
                stack.Pop();

            stack.Peek().Children.Add(section);

            var builder = new SectionBuilder
            {
                Level = level,
                Section = section,
                Children = new List<Section>(),
            };

            allBuilders.Add(builder);
            stack.Push(builder);
        }

        foreach (SectionBuilder builder in allBuilders)
        {
            if (builder.Section == null)
                continue;

            builder.Section.subsections = builder.Children.Count > 0
                ? builder.Children.ToArray()
                : null;
        }

        return new ArticleStructure
        {
            name = articleTitle,
            url = WikipediaRuntimeUtility.BuildArticleUrl(articleTitle),
            category = string.IsNullOrWhiteSpace(category) ? WikipediaRuntimeUtility.DefaultTopCategory : category,
            content = topSections.ToArray(),
        };
    }

    static string ExtractHeadingTitle(string headingInnerHtml)
    {
        if (string.IsNullOrWhiteSpace(headingInnerHtml))
            return string.Empty;

        Match headlineMatch = HeadlineRegex.Match(headingInnerHtml);
        if (headlineMatch.Success)
            return WikipediaRuntimeUtility.CleanupPlainText(headlineMatch.Groups["title"].Value);

        return WikipediaRuntimeUtility.CleanupPlainText(headingInnerHtml);
    }
    sealed class SectionBuilder
    {
        public int Level;
        public Section Section;
        public List<Section> Children;
    }
}
