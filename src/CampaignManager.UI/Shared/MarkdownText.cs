using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CampaignManager.UI.Shared;

/// <summary>
/// Markdown → безопасный HTML для <see cref="Markdown"/>. Текст пишет один человек (Хранитель), а читают
/// другие (игроки), поэтому:
/// <list type="bullet">
/// <item>сырой HTML не проходит — <c>DisableHtml</c> выводит его текстом (v1 для этого держал
/// HtmlSanitizer на AngleSharp — в WebAssembly это лишние мегабайты);</item>
/// <item>ссылки и картинки — только <c>http(s)</c>, <c>mailto</c> и адреса внутри приложения:
/// <c>javascript:</c> и <c>data:</c> Markdig сам не режет.</item>
/// </list>
/// </summary>
public static class MarkdownText
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    public static string ToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var document = Markdig.Markdown.Parse(markdown, Pipeline);
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (!IsSafeUrl(link.Url))
            {
                link.Url = "#";
            }
        }

        foreach (var link in document.Descendants<AutolinkInline>())
        {
            if (!IsSafeUrl(link.Url))
            {
                link.Url = "#";
            }
        }

        return document.ToHtml(Pipeline);
    }

    /// <summary>
    /// Заголовки текста по порядку — для «Содержания» (<see cref="MarkdownHeading"/>): уровень, текст и номер среди всех
    /// заголовков документа (<c>h1…h6</c> в разметке идут в том же порядке, поэтому по номеру их находят без id).
    /// </summary>
    public static IReadOnlyList<MarkdownHeading> Headings(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return [];
        }

        var result = new List<MarkdownHeading>();
        foreach (var heading in Markdig.Markdown.Parse(markdown, Pipeline).Descendants<HeadingBlock>())
        {
            var text = heading.Inline is null
                ? ""
                : string.Concat(heading.Inline.Descendants().Select(i => i switch
                {
                    LiteralInline literal => literal.Content.ToString(),
                    CodeInline code => code.Content,
                    _ => "",
                }));
            result.Add(new MarkdownHeading(heading.Level, text.Trim(), result.Count));
        }

        return result;
    }

    /// <summary>Абсолютный адрес — только безопасные схемы; относительный (без схемы) — свой сайт.</summary>
    public static bool IsSafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        var trimmed = url.Trim();
        var colon = trimmed.IndexOf(':', StringComparison.Ordinal);
        var slash = trimmed.IndexOfAny(['/', '?', '#']);
        if (colon < 0 || (slash >= 0 && slash < colon))
        {
            return true;
        }

        var scheme = trimmed[..colon];
        return scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
               || scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
               || scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Заголовок Markdown-текста: уровень 1–6, текст и номер среди всех заголовков документа.</summary>
public sealed record MarkdownHeading(int Level, string Text, int Index);
