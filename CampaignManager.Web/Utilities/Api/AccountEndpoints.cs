using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http.HttpResults;

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
            .WithSummary("Initiate Google OAuth login flow")
            .WithDescription("Starts the Google OAuth authentication process and redirects to Google's login page")
            .AllowAnonymous();

        accountGroup.MapGet("/logout", HandleLogout)
            .WithName("Logout")
            .WithSummary("Log out the current user")
            .WithDescription("Signs out the authenticated user and clears authentication cookies")
            .AllowAnonymous();
    }

    internal const string AuthModeItemKey = "campaignmanager:authMode";
    internal const string FailureRedirectItemKey = "campaignmanager:failureRedirect";
    internal const string SilentModeValue = "silent";
    private const string InteractiveModeValue = "interactive";

    /// <summary>
    /// Initiates Google OAuth login flow
    /// </summary>
    /// <param name="returnUrl">URL to redirect to after successful authentication (default: "/")</param>
    /// <param name="mode">Authentication mode: 'silent' (no user interaction) or 'interactive' (show login prompt)</param>
    /// <param name="httpContext">HTTP context for the current request</param>
    /// <returns>
    /// <list type="bullet">
    /// <item><description>302 Found - Redirect to Google OAuth login page</description></item>
    /// </list>
    /// </returns>
    /// <response code="302">Redirects to Google OAuth authentication page</response>
    /// <remarks>
    /// The 'silent' mode uses the 'prompt=none' OAuth parameter which attempts to authenticate 
    /// without showing the Google login page. This is useful for automatic re-authentication.
    /// The 'interactive' mode (default) always shows the Google login page.
    /// </remarks>
    private static async Task<ChallengeHttpResult> HandleLogin(
        string? returnUrl,
        string? mode,
        HttpContext httpContext)
    {
        // Clear existing cookies
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var normalizedReturnUrl = NormalizeReturnUrl(returnUrl, httpContext);

        var selectedMode = string.IsNullOrWhiteSpace(mode)
            ? InteractiveModeValue
            : mode.Trim().ToLowerInvariant();
        var isSilent = string.Equals(selectedMode, SilentModeValue, StringComparison.Ordinal);

        // Set redirect path after authentication
        var properties = new GoogleChallengeProperties
        {
            RedirectUri = normalizedReturnUrl,
            IsPersistent = true
        };

        properties.Items[AuthModeItemKey] = isSilent ? SilentModeValue : InteractiveModeValue;
        properties.Items[FailureRedirectItemKey] = normalizedReturnUrl;

        if (isSilent)
        {
            properties.SetParameter("prompt", "none");
        }

        // Initiate Google authentication
        // Using TypedResults.Challenge marks this as an API endpoint for .NET 10
        return TypedResults.Challenge(properties, new[] { GoogleDefaults.AuthenticationScheme });
    }

    /// <summary>
    /// Logs out the current user
    /// </summary>
    /// <param name="returnUrl">URL to redirect to after logout (default: "/")</param>
    /// <param name="httpContext">HTTP context for the current request</param>
    /// <returns>
    /// <list type="bullet">
    /// <item><description>302 Found - Redirect to specified return URL</description></item>
    /// </list>
    /// </returns>
    /// <response code="302">Redirects to the specified return URL after successful logout</response>
    /// <remarks>
    /// This endpoint clears the authentication cookie and signs out the user from the application.
    /// Note: This does not revoke the Google OAuth tokens or sign out from Google accounts.
    /// </remarks>
    private static async Task<RedirectHttpResult> HandleLogout(
        string? returnUrl,
        HttpContext httpContext)
    {
        AuthenticationProperties properties = new() { RedirectUri = returnUrl ?? "/" };

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme, properties);

        // Using TypedResults.Redirect for consistency
        return TypedResults.Redirect(returnUrl ?? "/");
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
            return relativeUri.OriginalString.StartsWith('/') ? relativeUri.OriginalString : "/";
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

                return pathAndQuery;
            }
        }

        return "/";
    }
}