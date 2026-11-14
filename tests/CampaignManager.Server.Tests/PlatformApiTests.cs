using System.Net;
using CampaignManager.ApiClient.Platform;
using Xunit;

namespace CampaignManager.Server.Tests;

public sealed class PlatformApiTests(CmApp app) : IClassFixture<CmApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ping_goes_through_api_client_to_postgres()
    {
        TestDatabase.SkipIfMissing();
        var api = new PingApiClient(app.CreateClient());

        var response = await api.PingAsync(Cancellation);

        Assert.InRange(response.DatabaseTime, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.Equal("Testing", response.Environment);
    }

    // ServiceDefaults v1 маппил /health только в Development.
    [Fact]
    public async Task Health_is_mapped_outside_development()
    {
        var response = await app.CreateClient().GetAsync("/health", Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_api_route_answers_problem_details_not_page()
    {
        var response = await app.CreateClient().GetAsync("/api/v1/no-such-route", Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // Адреса v1: на /legal ссылаются настройки Google-клиента и Auth0. Их читают боты без JS, поэтому текст
    // обязан быть в HTML ответа (страницы рендерит сервер статически, а не WebAssembly без пререндера).
    [Theory]
    [InlineData("/about", "ссылку-приглашение")]
    [InlineData("/about", "href=\"/legal\"")]
    [InlineData("/about", "Политика конфиденциальности")]
    [InlineData("/legal", "Chaosium Inc.")]
    [InlineData("/legal", "Политика конфиденциальности")]
    public async Task Info_pages_are_open_without_sign_in_with_text_in_html(string address, string phrase)
    {
        var response = await app.CreateClient().GetAsync(address, Cancellation);
        var html = await response.Content.ReadAsStringAsync(Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(phrase, html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_page_answers_404_with_app_shell()
    {
        var response = await app.CreateClient().GetAsync("/no-such-page", Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("<html lang=\"ru\">", await response.Content.ReadAsStringAsync(Cancellation));
    }

    // D1: страницы приложения не пререндерятся — сервер отдаёт оболочку, страницу рисует WebAssembly.
    [Fact]
    public async Task App_page_is_not_prerendered()
    {
        var html = await app.CreateClient().GetStringAsync("/dev/ping", Cancellation);

        Assert.Contains("id=\"app-loading\"", html);
        Assert.DoesNotContain("Проверка связи", html);
    }

    // .NET 11: окружение клиента WebAssembly приходит с сервера (WithBrowserOptions), а не запекается при сборке —
    // кнопки тестового входа и /dev/ui видны там же, где сервер маппит /dev/login.
    [Fact]
    public async Task App_page_tells_client_the_server_environment()
    {
        var html = await app.CreateClient().GetStringAsync("/dev/ping", Cancellation);

        var encoded = System.Text.RegularExpressions.Regex.Match(html, "<!--Blazor-Configuration:(?<options>[A-Za-z0-9+/=]+)-->");
        Assert.True(encoded.Success, "Страница не несёт настроек клиента (Blazor-Configuration).");
        using var options = System.Text.Json.JsonDocument.Parse(Convert.FromBase64String(encoded.Groups["options"].Value));
        Assert.Equal("Testing", options.RootElement.GetProperty("webAssembly").GetProperty("environmentName").GetString());
    }

    // Страница ошибки рендерится сервером статически — она не зависит от того, загрузится ли клиент.
    [Fact]
    public async Task Error_page_is_rendered_by_server()
    {
        var html = await app.CreateClient().GetStringAsync("/Error", Cancellation);

        Assert.Contains("Что-то пошло не так", html);
    }
}
