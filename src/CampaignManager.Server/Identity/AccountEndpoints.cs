using System.Globalization;
using System.Text;
using CampaignManager.Contracts.Identity;
using CampaignManager.Server.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace CampaignManager.Server.Identity;

/// <summary>Вход, выход и <c>GET /api/v1/me</c>.</summary>
public static class AccountEndpoints
{
    /// <summary>Параметр Auth0 <c>/authorize</c>: ведёт мимо его страницы прямо в коннекшен — только у автовхода.</summary>
    internal const string ConnectionParameter = "connection";

    public static IEndpointRouteBuilder MapIdentityApi(this IEndpointRouteBuilder app)
    {
        app.MapGet(IdentityRoutes.Me, GetMeAsync)
            .RequireAuthorization()
            .AllowMachine() // агент проверяет, от чьего имени он работает
            .WithName("GetMe")
            .WithTags("Identity");

        // Не API, а переходы браузера: OIDC-обмен и куки — забота сервера.
        app.MapGet(IdentityRoutes.Login, LoginAsync).AllowAnonymous().ExcludeFromDescription();
        app.MapGet(IdentityRoutes.Logout, LogoutAsync).AllowAnonymous().ExcludeFromDescription();

        // Жёсткая граница: вне Development адреса тестового входа нет вовсе — 404, а не отказ.
        if (app.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            app.MapDevLogin();
        }

        return app;
    }

    private static async Task<Results<Ok<MeResponse>, UnauthorizedHttpResult>> GetMeAsync(
        CurrentUser currentUser, CancellationToken cancellationToken) =>
        await currentUser.GetAsync(cancellationToken) is { } user
            ? TypedResults.Ok(new MeResponse(user.Id, user.Email, user.DisplayName, user.Role))
            : TypedResults.Unauthorized();

    /// <summary>
    /// Вход через Auth0 — всегда его страница: Google, passkey и почту с паролем предлагает она сама.
    /// <c>prompt</c> не шлём: при живой сессии Auth0 человек сразу возвращается, а сменить учётку
    /// позволяет выход — он гасит и сессию Auth0.
    /// </summary>
    private static async Task<Results<ChallengeHttpResult, RedirectHttpResult>> LoginAsync(
        string? returnUrl, HttpContext httpContext, IAuthenticationSchemeProvider schemes)
    {
        // Development без настроек Auth0: схемы OIDC нет, доступен только тестовый вход.
        if (await schemes.GetSchemeAsync(OpenIdConnectDefaults.AuthenticationScheme) is null)
        {
            return TypedResults.LocalRedirect($"{IdentityRoutes.LoginPage}?authStatus=unavailable");
        }

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.Challenge(
            CreateChallenge(httpContext, returnUrl, connectionMethod: null),
            [OpenIdConnectDefaults.AuthenticationScheme]);
    }

    /// <summary>
    /// Свойства входа — общие у кнопки входа и автовхода (<see cref="AutoLogin"/>). Коннекшен
    /// (<paramref name="connectionMethod"/>) задаёт только автовход: он ведёт мимо страницы Auth0 прямо в Google.
    /// </summary>
    internal static OpenIdConnectChallengeProperties CreateChallenge(HttpContext httpContext, string? returnUrl, string? connectionMethod)
    {
        var properties = new OpenIdConnectChallengeProperties
        {
            RedirectUri = ReturnUrl.Normalize(returnUrl, httpContext.Request.Host.Host),
            IsPersistent = true,
        };

        // Параметры, а не Items: в state они не нужны, в запрос к Auth0 их ставит
        // OnRedirectToIdentityProvider (IdentityModule).
        if (connectionMethod is not null)
        {
            properties.SetParameter(ConnectionParameter, AutoLogin.GetConnectionName(connectionMethod));
        }

        // Почта прошлого входа: в автовходе Google по ней сразу берёт нужный аккаунт. На странице Auth0
        // она встаёт в поле почты — это нужно учётке с паролем или passkey, а почта Google там только
        // сбила бы с толку: «Продолжить» с ней ведёт к паролю, которого у такой учётки нет.
        if (AutoLogin.GetRemembered(httpContext.Request) is { } remembered
            && remembered.Method == (connectionMethod ?? LoginMethods.Email))
        {
            properties.SetParameter(OpenIdConnectParameterNames.LoginHint, remembered.Email);
        }

        return properties;
    }

    /// <summary>
    /// Выход: своя кука, «забыть» браузер (автовход больше не сработает) и сессия Auth0 — иначе
    /// следующий вход молча пустил бы под той же учёткой. Google при этом не разлогинивается.
    /// Сессия тестового входа (<see cref="DevLogin"/>) к Auth0 не относится — у неё только кука.
    /// Переход с чужого сайта — только CSRF-выход, его отклоняем.
    /// </summary>
    private static async Task<Results<SignOutHttpResult, RedirectHttpResult, BadRequest<string>>> LogoutAsync(
        string? returnUrl, HttpContext httpContext, IAuthenticationSchemeProvider schemes)
    {
        if (!IsSameSiteRequest(httpContext.Request))
        {
            return TypedResults.BadRequest("Выход по ссылке с другого сайта не принимается.");
        }

        AutoLogin.Forget(httpContext);
        var redirectUri = ReturnUrl.Normalize(returnUrl, httpContext.Request.Host.Host);

        if (DevLogin.IsDevSession(httpContext.User)
            || await schemes.GetSchemeAsync(OpenIdConnectDefaults.AuthenticationScheme) is null)
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return TypedResults.LocalRedirect(redirectUri);
        }

        // Сначала своя кука, затем Auth0: обработчик OIDC уводит на /oidc/logout, а оттуда через
        // /signout-callback-oidc браузер возвращается на returnUrl.
        return TypedResults.SignOut(
            new AuthenticationProperties { RedirectUri = redirectUri },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
    }

    /// <summary>
    /// <c>Sec-Fetch-Site</c> браузер ставит на каждый переход; без заголовка (старые браузеры, прямые
    /// вызовы) пропускаем.
    /// </summary>
    internal static bool IsSameSiteRequest(HttpRequest request)
    {
        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
        return string.IsNullOrEmpty(fetchSite) || fetchSite is "none" or "same-origin" or "same-site";
    }
}

/// <summary>Куда вернуться после входа и выхода — только на свой хост, иначе это открытый редирект.</summary>
public static class ReturnUrl
{
    /// <summary>
    /// Относительный путь или абсолютный адрес своего <paramref name="host"/>; всё прочее — <c>/</c>.
    /// Результат идёт в заголовок <c>Location</c> (тестовый вход, колбэки входа и выхода Auth0), поэтому он
    /// закодирован (<see cref="EscapeForHeader"/>): <c>/skills?q=меч</c> → <c>/skills?q=%D0%BC%D0%B5%D1%87</c>.
    /// </summary>
    public static string Normalize(string? returnUrl, string host)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        if (Uri.TryCreate(returnUrl, UriKind.Relative, out var relative))
        {
            var path = relative.OriginalString.StartsWith('/') ? relative.OriginalString : "/" + relative.OriginalString;
            return SameHostOrRoot(path);
        }

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absolute)
            && absolute.Scheme is "http" or "https"
            && string.Equals(absolute.Host, host, StringComparison.OrdinalIgnoreCase))
        {
            return SameHostOrRoot(absolute.PathAndQuery + absolute.Fragment);
        }

        return "/";
    }

    private static string SameHostOrRoot(string path)
    {
        var escaped = EscapeForHeader(path);
        return IsSameHostPath(escaped) ? escaped : "/";
    }

    /// <summary>
    /// Kestrel не пускает в заголовок не-ASCII и управляющие символы — ответ 500. Их, как и пробел, — в
    /// <c>%XX</c> байтов UTF-8, как браузер кодирует адрес; остальной ASCII и уже закодированное
    /// (<c>%D0%BC</c>) — как есть. Табуляцию и перевод строки браузер из адреса выбросил бы, и «/\t/evil»
    /// стал бы «//evil», а закодированные они — просто символы пути.
    /// </summary>
    private static string EscapeForHeader(string path)
    {
        if (!path.Any(NeedsEscape))
        {
            return path;
        }

        var builder = new StringBuilder(path.Length * 3);
        Span<byte> utf8 = stackalloc byte[4];
        foreach (var rune in path.EnumerateRunes())
        {
            if (rune.IsAscii && !NeedsEscape((char)rune.Value))
            {
                builder.Append((char)rune.Value);
                continue;
            }

            foreach (var b in utf8[..rune.EncodeToUtf8(utf8)])
            {
                builder.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    private static bool NeedsEscape(char c) => c is <= ' ' or >= '\x7F';

    /// <summary>
    /// «/foo» ведёт на свой хост, а «//evil.com» и «/\evil.com» браузер считает адресом с чужим хостом,
    /// хотя формально это относительные URL. Раскодированный вид — чтобы не пролез «/%5Cevil.com».
    /// </summary>
    private static bool IsSameHostPath(string path) =>
        !StartsWithHostMarker(path) && !StartsWithHostMarker(Uri.UnescapeDataString(path));

    private static bool StartsWithHostMarker(string path) =>
        path.Length >= 2 && path[0] is '/' or '\\' && path[1] is '/' or '\\';
}
