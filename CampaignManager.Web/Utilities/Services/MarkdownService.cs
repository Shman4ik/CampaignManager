using Markdig;
using Microsoft.Extensions.Logging;

namespace CampaignManager.Web.Utilities.Services
{
    public class MarkdownService
    {
        private readonly ILogger<MarkdownService> _logger;
        private readonly MarkdownPipeline _pipeline;

        public MarkdownService(ILogger<MarkdownService> logger)
        {
            _logger = logger;
            _pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();
        }

        /// <summary>
        /// Converts markdown text to HTML
        /// </summary>
        /// <param name="markdown">The markdown text to convert</param>
        /// <returns>HTML string or empty string if conversion fails</returns>
        public string ConvertToHtml(string? markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return string.Empty;
            }

            try
            {
                return Markdown.ToHtml(markdown, _pipeline);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting markdown to HTML");
                return string.Empty;
            }
        }
    }
}
