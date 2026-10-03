using System.Net;
using CampaignManager.ApiClient.Identity;
using CampaignManager.Contracts.Identity;
using CampaignManager.Core.Identity;
using CampaignManager.Server.Identity;
using CampaignManager.Server.Tests.Schema;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampaignManager.Server.Tests.Identity;

/// <summary>Сервер в Development со своей базой со схемой <c>cm</c> и настроенным Auth0 — как на машине разработчика.</summary>
public sealed class DevLoginApp : CmApp, IAsyncLifetime
{
    public SchemaDatabase Database { get; } = new();

    protected override string EnvironmentName => "Development";

    protected override string ConnectionString =>
        TestDatabase.ConnectionString is null ? TestDatabase.Unreachable : Database.ConnectionString;

    ValueTask IAsyncLifetime.InitializeAsync() => Database.InitializeAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Database.DisposeAsync();
    }

    /// <summary>Браузер: куки помнит, редиректы не проходит.</summary>
    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
    });
}

/// <summary>Сервер в заданном окружении; <paramref name="withAuth0"/> = false — без настроек Auth0.</summary>
public sealed class EnvironmentApp(string environmentName, bool withAuth0 = true) : CmApp
{
    protected override string EnvironmentName => environmentName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        if (!withAuth0)
        {
            builder.UseSetting("Authentication:Auth0:Domain", "");
            builder.UseSetting("Authentication:Auth0:ClientId", "");
        }
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
    });
}

/// <summary>Сервер в Development на базе <paramref name="database"/>, слушающий (по конфигурации) <paramref name="urls"/>.</summary>
public sealed class DevLoginOnPortApp(SchemaDatabase database, string urls) : CmApp
{
    protected override string EnvironmentName => "Development";

    protected override string ConnectionString => database.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting(WebHostDefaults.ServerUrlsKey, urls);
    }

    /// <summary>Клиент без своих кук: куки «браузера» тест передаёт заголовком сам.</summary>
    public HttpClient Bare() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = false,
        BaseAddress = new Uri("https://localhost"),
    });
}

public sealed class DevLoginTests(DevLoginApp app) : IClassFixture<DevLoginApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Жёсткая граница: вне Development адреса нет вовсе, а обычный вход по-прежнему ведёт в Auth0.
    [Theory]
    [InlineData("Testing")]
    [InlineData("Production")]
    [InlineData("Beta")]
    public async Task Dev_login_is_404_outside_development_and_regular_login_is_unchanged(string environment)
    {
        await using var server = new EnvironmentApp(environment);
        var browser = server.Browser();

        var devLogin = await browser.GetAsync(IdentityRoutes.DevLoginUrl("keeper", "/"), Cancellation);
        Assert.Equal(HttpStatusCode.NotFound, devLogin.StatusCode);

        var login = await browser.GetAsync(IdentityRoutes.LoginUrl("/"), Cancellation);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.StartsWith($"https://{CmApp.Auth0Domain}/authorize", login.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("Production")]
    public void Server_without_auth0_does_not_start_outside_development(string environment)
    {
        using var server = new EnvironmentApp(environment, withAuth0: false);

        var error = Assert.ThrowsAny<Exception>(() => server.CreateClient());

        Assert.Contains("Auth0", Flatten(error));
    }

    // Development без Auth0 стартует: обычный вход честно говорит, что недоступен, тестовый — маппится.
    [Fact]
    public async Task Development_without_auth0_starts_with_dev_login_only()
    {
        await using var server = new EnvironmentApp("Development", withAuth0: false);
        var browser = server.Browser();

        var login = await browser.GetAsync(IdentityRoutes.LoginUrl("/"), Cancellation);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/login?authStatus=unavailable", login.Headers.Location?.ToString());

        var unknownRole = await browser.GetAsync($"{IdentityRoutes.DevLogin}?as=2", Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, unknownRole.StatusCode);

        // Без Auth0 и выход только локальный.
        var logout = await browser.GetAsync(IdentityRoutes.Logout, Cancellation);
        Assert.Equal("/", logout.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData("player", UserRole.Player)]
    [InlineData("keeper", UserRole.Keeper)]
    [InlineData("Admin", UserRole.Admin)]
    public async Task Dev_login_signs_in_test_user_with_role(string role, UserRole expected)
    {
        TestDatabase.SkipIfMissing();
        var browser = app.Browser();

        var response = await browser.GetAsync(IdentityRoutes.DevLoginUrl(role, "/scenarios?tab=npc"), Cancellation);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/scenarios?tab=npc", response.Headers.Location?.ToString());

        var me = await new IdentityApiClient(browser).GetMeAsync(Cancellation);
        Assert.NotNull(me);
        Assert.Equal(expected, me.Role);
        Assert.Equal($"dev-{expected.ToString().ToLowerInvariant()}@{DevLogin.EmailDomain}", me.Email);

        // Строка завелась тем же кодом, что при входе через Auth0: со своим sub и временем входа.
        await using var db = app.Database.CreateContext();
        var user = await db.Users.SingleAsync(u => u.Id == me.Id, Cancellation);
        Assert.Equal($"dev|{me.Email}", user.Auth0Sub);
        Assert.NotNull(user.LastLoginAt);
    }

    [Fact]
    public async Task Dev_login_with_email_changes_role_of_that_user()
    {
        TestDatabase.SkipIfMissing();
        var email = $"{Guid.NewGuid():N}@example.test";

        var asPlayer = app.Browser();
        await asPlayer.GetAsync($"{IdentityRoutes.DevLogin}?as=player&email={email}", Cancellation);
        var player = await new IdentityApiClient(asPlayer).GetMeAsync(Cancellation);

        var asKeeper = app.Browser();
        await asKeeper.GetAsync($"{IdentityRoutes.DevLogin}?as=keeper&email={email}", Cancellation);
        var keeper = await new IdentityApiClient(asKeeper).GetMeAsync(Cancellation);

        Assert.Equal(UserRole.Player, player?.Role);
        Assert.Equal(player?.Id, keeper?.Id);
        Assert.Equal(UserRole.Keeper, keeper?.Role);
    }

    // Выход обычный, но сессия тестового входа к Auth0 не относится — туда не уводим, даже если Auth0 настроен.
    [Fact]
    public async Task Logout_of_dev_session_stays_local()
    {
        TestDatabase.SkipIfMissing();
        var browser = app.Browser();
        await browser.GetAsync(IdentityRoutes.DevLoginUrl("keeper", null), Cancellation);

        var logout = await browser.GetAsync($"{IdentityRoutes.Logout}?returnUrl=/login", Cancellation);

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/login", logout.Headers.Location?.ToString());
        Assert.Null(await new IdentityApiClient(browser).GetMeAsync(Cancellation));
    }

    // Адрес возврата — только свой хост, как у обычного входа.
    [Fact]
    public async Task Dev_login_does_not_redirect_to_other_host()
    {
        TestDatabase.SkipIfMissing();

        var response = await app.Browser().GetAsync(IdentityRoutes.DevLoginUrl("player", "//evil.example"), Cancellation);

        Assert.Equal("/", response.Headers.Location?.ToString());
    }

    // Два сервера Development на одном localhost: у браузера одна банка кук на хост, порт её не делит.
    // С портом в имени кука входа одного сервера не перезаписывает куку соседа, и обе сессии живы.
    [Fact]
    public async Task Dev_logins_on_two_ports_do_not_overwrite_each_other()
    {
        TestDatabase.SkipIfMissing();
        await using var keeperServer = new DevLoginOnPortApp(app.Database, "https://localhost:8083");
        await using var playerServer = new DevLoginOnPortApp(app.Database, "https://localhost:8084");

        var keeperCookie = await DevLoginCookieAsync(keeperServer, "keeper");
        var playerCookie = await DevLoginCookieAsync(playerServer, "player");

        Assert.StartsWith(".CampaignManager.Auth.8083=", keeperCookie);
        Assert.StartsWith(".CampaignManager.Auth.8084=", playerCookie);

        // Браузер шлёт на любой порт localhost обе куки — каждый сервер читает свою.
        var jar = $"{keeperCookie}; {playerCookie}";
        Assert.Equal(UserRole.Keeper, (await MeAsync(keeperServer, jar))?.Role);
        Assert.Equal(UserRole.Player, (await MeAsync(playerServer, jar))?.Role);
    }

    private static async Task<string> DevLoginCookieAsync(DevLoginOnPortApp server, string role)
    {
        var response = await server.Bare().GetAsync(IdentityRoutes.DevLoginUrl(role, "/"), Cancellation);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".CampaignManager.Auth"));
        return cookie[..cookie.IndexOf(';')];
    }

    private static async Task<MeResponse?> MeAsync(DevLoginOnPortApp server, string cookies)
    {
        var client = server.Bare();
        client.DefaultRequestHeaders.Add("Cookie", cookies);
        return await new IdentityApiClient(client).GetMeAsync(Cancellation);
    }

    private static string Flatten(Exception error)
    {
        var messages = new List<string>();
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" | ", messages);
    }
}
