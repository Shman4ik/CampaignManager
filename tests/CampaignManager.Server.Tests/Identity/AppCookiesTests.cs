using CampaignManager.Server.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CampaignManager.Server.Tests.Identity;

/// <summary>Сервер в заданном окружении, слушающий (по конфигурации) <paramref name="urls"/>.</summary>
public sealed class ListeningApp(string environmentName, string? urls) : CmApp
{
    protected override string EnvironmentName => environmentName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        if (urls is not null)
        {
            builder.UseSetting(WebHostDefaults.ServerUrlsKey, urls);
        }
    }

    /// <summary>Имена всех кук, которые ставит приложение: вход, автовход, корреляция и nonce OIDC, antiforgery.</summary>
    public string[] CookieNames()
    {
        var cookies = Services.GetRequiredService<AppCookies>();
        var auth = Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var oidc = Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        var antiforgery = Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        return
        [
            auth.Cookie.Name!,
            cookies.LastLoginName,
            cookies.AutoLoginAttemptName,
            oidc.CorrelationCookie.Name!,
            oidc.NonceCookie.Name!,
            antiforgery.Cookie.Name!,
        ];
    }
}

public sealed class AppCookiesTests
{
    // Prod и beta: другое имя куки входа выбило бы всех пользователей. Адрес задан — суффикса всё равно нет.
    [Theory]
    [InlineData("Testing")]
    [InlineData("Production")]
    [InlineData("Beta")]
    public async Task Cookie_names_do_not_change_outside_development(string environment)
    {
        await using var server = new ListeningApp(environment, "https://localhost:8083");

        var names = server.CookieNames();

        Assert.Null(server.Services.GetRequiredService<AppCookies>().Port);
        Assert.Equal(
            [
                ".CampaignManager.Auth",
                ".CampaignManager.LastLogin",
                ".CampaignManager.AutoLogin",
                ".CampaignManager.Correlation",
                ".CampaignManager.Nonce.",
            ],
            names[..5]);
        // Имя antiforgery задаёт сам ASP.NET Core (с хешем приложения) — его мы вне Development не трогаем.
        Assert.StartsWith(".AspNetCore.Antiforgery.", names[5]);
        Assert.DoesNotContain("8083", names[5]);
    }

    // Два сервера на localhost — разные имена у каждой куки: вход на одном порту не перезаписывает другой.
    [Fact]
    public async Task Development_servers_on_two_ports_use_different_cookie_names()
    {
        await using var first = new ListeningApp("Development", "https://localhost:8083");
        await using var second = new ListeningApp("Development", "https://localhost:8084");

        var firstNames = first.CookieNames();
        var secondNames = second.CookieNames();

        Assert.Equal(
            [
                ".CampaignManager.Auth.8083",
                ".CampaignManager.LastLogin.8083",
                ".CampaignManager.AutoLogin.8083",
                ".CampaignManager.Correlation.8083.",
                ".CampaignManager.Nonce.8083.",
            ],
            firstNames[..5]);
        Assert.StartsWith(".AspNetCore.Antiforgery.", firstNames[5]);
        Assert.EndsWith(".8083", firstNames[5]);
        Assert.Empty(firstNames.Intersect(secondNames));
    }

    // Development без известного порта (ни urls, ни Kestrel:Endpoints) — имена прежние, сервер стартует.
    [Fact]
    public async Task Development_without_known_port_keeps_names()
    {
        await using var server = new ListeningApp("Development", null);

        Assert.Equal(".CampaignManager.Auth", server.CookieNames()[0]);
    }

    [Theory]
    [InlineData("https://localhost:8083", 8083)]
    [InlineData("http://localhost:5000;https://localhost:8085", 8085)]
    [InlineData("http://*:8086", 8086)]
    [InlineData("https://[::1]:8087", 8087)]
    [InlineData("http://127.0.0.1:0", null)]
    [InlineData("", null)]
    public void Port_comes_from_urls_preferring_https(string urls, int? expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [WebHostDefaults.ServerUrlsKey] = urls })
            .Build();

        Assert.Equal(expected, AppCookies.FindPort(configuration));
    }

    [Fact]
    public void Port_falls_back_to_kestrel_endpoints_and_https_ports()
    {
        var kestrel = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Kestrel:Endpoints:Https:Url"] = "https://localhost:8089" })
            .Build();
        var ports = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [WebHostDefaults.HttpsPortsKey] = "8090" })
            .Build();

        Assert.Equal(8089, AppCookies.FindPort(kestrel));
        Assert.Equal(8090, AppCookies.FindPort(ports));
    }
}
