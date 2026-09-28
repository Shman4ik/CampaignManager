using CampaignManager.Web.Utilities.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace CampaignManager.Web.Utilities.Api;

/// <summary>
/// API endpoints for authentication operations
/// </summary>
public static class AccountEndpoints
{
    /// <summary>
    /// Maps account authentication endpoints to the application's routing
    /// </summary>
    /// <param name="routes">The endpoint route builder</param>
    public static void MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        var accountGroup = routes.MapGroup("/api/account")
            .WithTags("Authentication");

        accountGroup.MapGet("/login", HandleLogin)
            .WithName("Login")
            .WithSummary("Initiate Auth0 login flow")
            .WithDescription("Starts the OpenID Connect flow and redirects to the Auth0 login page")
            .AllowAnonymous();

        accountGroup.MapGet("/logout", HandleLogout)
            .WithName("Logout")
            .WithSummary("Log out the current user")
            .WithDescription("Signs out the authenticated user and clears authentication cookies")
            .AllowAnonymous();
    }

    /// <summary>Параметр Auth0 /authorize, который ведёт мимо его страницы прямо к выбранному способу входа.</summary>
    internal const string ConnectionParameter = "connection";

    /// <summary>
    /// Initiates Auth0 (OpenID Connect) login flow
    /// </summary>
    /// <param name="returnUrl">URL to redirect to after successful authentication (default: "/")</param>
    /// <param name="method">Login method: 'google' goes straight to Google, 'email' to the password form;
    /// without it Auth0 shows its page with every method</param>
    /// <param name="httpContext">HTTP context for the current request</param>
    /// <returns>
    /// <list type="bullet">
    /// <item><description>302 Found - Redirect to Auth0 login page</description></item>
    /// </list>
    /// </returns>
    /// <response code="302">Redirects to Auth0 authentication page</response>
    /// <remarks>
    /// No 'prompt' is sent: with a live Auth0 session the user comes straight back. Switching accounts
    /// needs no forced login page, because logout ends the Auth0 session as well.
    /// </remarks>
    private static async Task<ChallengeHttpResult> HandleLogin(
        string? returnUrl,
        string? method,
        HttpContext httpContext)
    {
        // Clear existing cookies
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Using TypedResults.Challenge marks this as an API endpoint for .NET 10
        return TypedResults.Challenge(
            CreateChallenge(httpContext, returnUrl, AutoLogin.ParseMethod(method)),
            [OpenIdConnectDefaults.AuthenticationScheme]);
    }

    /// <summary>
    /// Свойства входа через Auth0 — общие у кнопок входа и у автовхода (<see cref="AutoLogin" />).
    /// </summary>
    internal static OpenIdConnectChallengeProperties CreateChallenge(
        HttpContext httpContext,
        string? returnUrl,
        LoginMethod? method)
    {
        var properties = new OpenIdConnectChallengeProperties
        {
            RedirectUri = NormalizeReturnUrl(returnUrl, httpContext),
            IsPersistent = true
        };

        // Параметры, а не Items: в state они не нужны, в запрос к Auth0 их ставит
        // OnRedirectToIdentityProvider в Program.cs.
        if (method is { } selected)
            properties.SetParameter(ConnectionParameter, AutoLogin.GetConnectionName(selected));

        // Почта прошлого входа: Google по ней сразу берёт нужный аккаунт, без списка аккаунтов,
        // а форма пароля подставляет её в поле. Другому способу входа она ни к чему.
        if (AutoLogin.GetRemembered(httpContext.Request) is { } remembered
            && (method is null || method == remembered.Method))
            properties.SetParameter(OpenIdConnectParameterNames.LoginHint, remembered.Email);

        return properties;
    }

    /// <summary>
    /// Logs out the current user
    /// </summary>
    /// <param name="returnUrl">URL to redirect to after logout (default: "/")</param>
    /// <param name="httpContext">HTTP context for the current request</param>
    /// <returns>
    /// <list type="bullet">
    /// <item><description>302 Found - Redirect to the Auth0 logout endpoint, which then returns to the return URL</description></item>
    /// <item><description>400 Bad Request - Request was initiated from another site</description></item>
    /// </list>
    /// </returns>
    /// <response code="302">Redirects through Auth0 logout back to the specified return URL</response>
    /// <response code="400">Cross-site logout request was rejected</response>
    /// <remarks>
    /// This endpoint clears the authentication cookie, forgets how this browser logged in and ends the
    /// Auth0 session, so the browser no longer logs in by itself and the next login asks for an account
    /// again instead of silently reusing the previous one.
    /// The return URL is validated the same way as on login, so it can only point back at this host.
    /// Note: This does not sign the user out of Google itself.
    /// </remarks>
    private static Results<SignOutHttpResult, BadRequest<string>> HandleLogout(
        string? returnUrl,
        HttpContext httpContext)
    {
        // A cross-site navigation to this endpoint is never a legitimate flow, only a CSRF logout.
        if (!IsSameSiteRequest(httpContext))
            return TypedResults.BadRequest("Cross-site logout requests are not allowed");

        // Same validation as the login endpoint: without it this is an open redirect off our domain.
        var normalizedReturnUrl = NormalizeReturnUrl(returnUrl, httpContext);

        AuthenticationProperties properties = new() { RedirectUri = normalizedReturnUrl };

        AutoLogin.Forget(httpContext);

        // Сначала своя кука, затем Auth0: обработчик OIDC сам уводит на /oidc/logout, а оттуда
        // через /signout-callback-oidc браузер возвращается на normalizedReturnUrl.
        return TypedResults.SignOut(properties,
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
    }

    /// <summary>
    /// Rejects requests initiated from another site, using the Sec-Fetch-Site hint browsers send on
    /// every navigation. Requests without the header (older browsers, direct calls) are allowed through.
    /// </summary>
    private static bool IsSameSiteRequest(HttpContext httpContext)
    {
        var fetchSite = httpContext.Request.Headers["Sec-Fetch-Site"].ToString();
        if (string.IsNullOrEmpty(fetchSite))
            return true;

        // "none" = typed in the address bar or a bookmark; "same-origin"/"same-site" = our own pages.
        return fetchSite is "none" or "same-origin" or "same-site";
    }

    /// <summary>
    /// Normalizes and validates a return URL to prevent open redirect vulnerabilities
    /// </summary>
    /// <param name="returnUrl">The URL to normalize</param>
    /// <param name="httpContext">HTTP context for host validation</param>
    /// <returns>A safe, normalized URL or "/" if validation fails</returns>
    /// <remarks>
    /// This method ensures that return URLs are either:
    /// <list type="bullet">
    /// <item><description>Relative URLs starting with "/"</description></item>
    /// <item><description>Absolute URLs matching the current request host</description></item>
    /// </list>
    /// Any other URL format is rejected and replaced with "/" to prevent open redirect attacks.
    /// </remarks>
    private static string NormalizeReturnUrl(string? returnUrl, HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        if (Uri.TryCreate(returnUrl, UriKind.Relative, out var relativeUri))
        {
            var path = relativeUri.OriginalString.StartsWith('/')
                ? relativeUri.OriginalString
                : "/" + relativeUri.OriginalString;

            return IsSameHostPath(path) ? path : "/";
        }

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absoluteUri))
        {
            var request = httpContext.Request;
            if (string.Equals(absoluteUri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase))
            {
                var pathAndQuery = absoluteUri.PathAndQuery;
                if (!string.IsNullOrEmpty(absoluteUri.Fragment))
                {
                    pathAndQuery += absoluteUri.Fragment;
                }

                return IsSameHostPath(pathAndQuery) ? pathAndQuery : "/";
            }
        }

        return "/";
    }

    /// <summary>
    /// Проверяет, что путь ведёт на наш же хост. «/foo» — ведёт, а «//evil.com» и «/\evil.com»
    /// браузер считает адресом с указанием чужого хоста, хотя формально это относительные URL.
    /// </summary>
    private static bool IsSameHostPath(string path)
    {
        if (path.Length < 2) return true;

        // Обратный слэш браузеры приводят к прямому уже после чтения заголовка, поэтому
        // сравниваем и раскодированный вид: «/%5Cevil.com» не должен пролезать следом.
        return !StartsWithHostMarker(path) && !StartsWithHostMarker(Uri.UnescapeDataString(path));
    }

    private static bool StartsWithHostMarker(string path) =>
        path.Length >= 2 && path[0] is '/' or '\\' && path[1] is '/' or '\\';
}