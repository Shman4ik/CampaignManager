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
