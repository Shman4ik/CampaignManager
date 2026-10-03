using System.Net;
using CampaignManager.Contracts.Identity;
using CampaignManager.Server.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CampaignManager.Server.Tests.Identity;

/// <summary>
/// Вход и выход — до редиректа на Auth0 (метаданные Auth0 заданы в <see cref="TestAuth"/>, в сеть
/// тесты не ходят). Сам обмен кода на токен проверяет человек в браузере.
/// </summary>
public sealed class AccountFlowTests(CmApp app) : IClassFixture<CmApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private HttpClient Browser() => app.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
    });

    // Вход — всегда страница Auth0 со всеми способами (Google, passkey, почта), по-русски;
    // код возвращается GET-ом (Lax-куки).
    [Fact]
    public async Task Login_shows_auth0_page_with_every_method()
    {
        var response = await Browser().GetAsync(IdentityRoutes.LoginUrl("/scenarios"), Cancellation);

        var query = AuthorizeQuery(response);
        Assert.Null(query["connection"]);
        Assert.Equal("ru", query["ui_locales"]);
        Assert.NotEqual("form_post", query["response_mode"]);
        Assert.Equal(CmApp.Auth0ClientId, query["client_id"]);
        Assert.Null(query["prompt"]);
    }

    // Прошлый вход по почте (пароль или passkey) подставляет адрес в поле Auth0, а почта Google — нет:
    // «Продолжить» с ней повело бы к паролю, которого у такой учётки нет.
    [Theory]
    [InlineData("email:a@example.test", "a@example.test")]
    [InlineData("google:keeper@example.test", null)]
    public async Task Login_prefills_only_remembered_email_account(string remembered, string? loginHint)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, IdentityRoutes.Login);
        request.Headers.Add("Cookie", $"{AutoLogin.RememberedCookie}={remembered}");

        var response = await Browser().SendAsync(request, Cancellation);

        Assert.Equal(loginHint, AuthorizeQuery(response)["login_hint"]);
    }

    // Браузер, входивший через Google, входит сам — с подсказкой аккаунта, один раз за сессию браузера.
    [Fact]
    public async Task Page_load_of_remembered_google_browser_logs_in_by_itself_once()
    {
        var request = PageLoad("/scenarios", $"{AutoLogin.RememberedCookie}=google:keeper@example.test");

        var response = await Browser().SendAsync(request, Cancellation);
        var query = AuthorizeQuery(response);

        Assert.Equal("google-oauth2", query["connection"]);
        Assert.Equal("keeper@example.test", query["login_hint"]);
        // Метка попытки — со сроком, а не до закрытия браузера: Chrome восстанавливает сессионные куки
        // неделями, и неудачная попытка выключала бы автовход насовсем.
        Assert.Contains(response.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith(AutoLogin.AttemptCookie + "=1;") && cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase));

        // Открытая страница: главная без сессии сама ведёт на вход, а здесь проверяется только, что автовход не повторился.
        var again = PageLoad("/about", $"{AutoLogin.RememberedCookie}=google:keeper@example.test; {AutoLogin.AttemptCookie}=1");
        Assert.Equal(HttpStatusCode.OK, (await Browser().SendAsync(again, Cancellation)).StatusCode);
    }

    // Пароль автовход не угадает: браузер, входивший по почте, просто открывает (открытую) страницу.
    [Fact]
    public async Task Email_login_is_not_repeated_automatically()
    {
        var response = await Browser().SendAsync(PageLoad("/about", $"{AutoLogin.RememberedCookie}=email:a@example.test"), Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Logout_from_another_site_is_rejected()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, IdentityRoutes.Logout);
        request.Headers.Add("Sec-Fetch-Site", "cross-site");

        Assert.Equal(HttpStatusCode.BadRequest, (await Browser().SendAsync(request, Cancellation)).StatusCode);
    }

    // Выход гасит и сессию Auth0 (/oidc/logout с client_id) и забывает способ входа браузера.
    [Fact]
    public async Task Logout_ends_auth0_session_and_forgets_browser()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, IdentityRoutes.Logout);
        request.Headers.Add("Sec-Fetch-Site", "same-origin");

        var response = await Browser().SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith($"https://{CmApp.Auth0Domain}/oidc/logout", response.Headers.Location!.ToString());
        Assert.Contains($"client_id={CmApp.Auth0ClientId}", response.Headers.Location.Query);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith(AutoLogin.RememberedCookie + "=;"));
    }

    // Прямая загрузка страницы без сессии: страница входа приложения, а не HTML ошибки.
    [Fact]
    public async Task Login_page_is_open_to_anonymous()
    {
        var response = await Browser().SendAsync(PageLoad(IdentityRoutes.LoginPage, cookie: null), Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Гость не видит своей витрины с «Войти»: любая страница без сессии, главная тоже, сразу ведёт на вход Auth0
    // (через /account/login с адресом возврата). API по-прежнему отвечает 401.
    [Theory]
    [InlineData("/", "%2F")]
    [InlineData("/campaigns?status=active", "%2Fcampaigns%3Fstatus%3Dactive")]
    public async Task Page_without_session_goes_straight_to_auth0_sign_in(string path, string returnUrl)
    {
        var response = await Browser().SendAsync(PageLoad(path, cookie: null), Cancellation);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"{IdentityRoutes.Login}?{IdentityRoutes.ReturnUrlParameter}={returnUrl}", response.Headers.Location!.PathAndQuery);

        var authorize = await Browser().SendAsync(PageLoad(response.Headers.Location.PathAndQuery, cookie: null), Cancellation);
        AuthorizeQuery(authorize);
    }

    [Theory]
    [InlineData(null, "/")]
    [InlineData("/scenarios?id=1", "/scenarios?id=1")]
    [InlineData("scenarios", "/scenarios")]
    [InlineData("https://localhost/campaigns#journal", "/campaigns#journal")]
    [InlineData("https://evil.example/", "/")]
    [InlineData("//evil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData("/%5Cevil.example", "/")]
    [InlineData("javascript:alert(1)", "/")]
    public void Return_url_stays_on_own_host(string? returnUrl, string expected) =>
        Assert.Equal(expected, ReturnUrl.Normalize(returnUrl, "localhost"));

    private static HttpRequestMessage PageLoad(string path, string? cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Sec-Fetch-Dest", "document");
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return request;
    }

    private static System.Collections.Specialized.NameValueCollection AuthorizeQuery(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal($"https://{CmApp.Auth0Domain}/authorize", location.GetLeftPart(UriPartial.Path));
        return System.Web.HttpUtility.ParseQueryString(location.Query);
    }
}
