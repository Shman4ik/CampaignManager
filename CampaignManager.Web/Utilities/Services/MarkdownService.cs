using Ganss.Xss;
using Markdig;

namespace CampaignManager.Web.Utilities.Services;

public sealed class MarkdownService(ILogger<MarkdownService> logger)
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private readonly HtmlSanitizer _sanitizer = CreateSanitizer();

    /// <summary>
    ///     Converts markdown text to sanitized HTML
    /// </summary>
    /// <param name="markdown">The markdown text to convert</param>
    /// <returns>Sanitized HTML string or empty string if conversion fails</returns>
    public string ConvertToHtml(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return string.Empty;

        try
        {
            var rawHtml = Markdown.ToHtml(markdown, _pipeline);
            return _sanitizer.Sanitize(rawHtml);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error converting markdown to HTML");
            return string.Empty;
        }
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        // Allow common formatting tags from markdown
        sanitizer.AllowedTags.Add("table");
        sanitizer.AllowedTags.Add("thead");
        sanitizer.AllowedTags.Add("tbody");
        sanitizer.AllowedTags.Add("tr");
        sanitizer.AllowedTags.Add("th");
        sanitizer.AllowedTags.Add("td");
        sanitizer.AllowedTags.Add("details");
        sanitizer.AllowedTags.Add("summary");
        sanitizer.AllowedAttributes.Add("class");
        return sanitizer;
    }
}
