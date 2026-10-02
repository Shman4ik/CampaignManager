using CampaignManager.UI.Shared;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>Markdown пишет Хранитель, читают игроки: ни сырого HTML, ни опасных ссылок.</summary>
public sealed class MarkdownTests : KitContext
{
    [Fact]
    public void Formatting_is_rendered()
    {
        var html = MarkdownText.ToHtml("Нашли **дневник**.\n\n- раз\n- два");

        Assert.Contains("<strong>дневник</strong>", html);
        Assert.Contains("<li>раз</li>", html);
    }

    [Fact]
    public void Raw_html_is_text()
    {
        var html = MarkdownText.ToHtml("<script>alert(1)</script> <img src=x onerror=alert(2)>");

        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<img", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("[x](JaVaScRiPt:alert(1))")]
    [InlineData("![x](data:image/svg+xml;base64,AAAA)")]
    [InlineData("<javascript:alert(1)>")]
    public void Dangerous_links_are_neutralised(string markdown)
    {
        var html = MarkdownText.ToHtml(markdown);

        Assert.DoesNotContain("href=\"javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src=\"data:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://example.com/a")]
    [InlineData("/campaigns/1")]
    [InlineData("character/1")]
    [InlineData("mailto:keeper@example.test")]
    public void Ordinary_links_stay(string url)
    {
        Assert.Contains($"href=\"{url}\"", MarkdownText.ToHtml($"[x]({url})"));
    }

    [Fact]
    public void Component_renders_nothing_for_empty_text()
    {
        var cut = Render<Markdown>(p => p.Add(m => m.Text, "  "));

        Assert.Empty(cut.Markup.Trim());
    }
}
