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
    public static void MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        var accountGroup = routes.MapGroup("/api/account");

        accountGroup.MapGet("/login", HandleLogin);
        accountGroup.MapGet("/logout", HandleLogout);
    }

    internal const string AuthModeItemKey = "campaignmanager:authMode";
    internal const string FailureRedirectItemKey = "campaignmanager:failureRedirect";
    internal const string SilentModeValue = "silent";
    private const string InteractiveModeValue = "interactive";

    /// <summary>
    /// Initiates Google OAuth login flow
    /// </summary>
    /// <param name="returnUrl">URL to redirect to after successful authentication</param>
    /// <param name="mode">Authentication mode: 'silent' or 'interactive'</param>
    /// <param name="httpContext">HTTP context</param>
    /// <returns>Challenge result to initiate OAuth flow</returns>
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
    /// <param name="returnUrl">URL to redirect to after logout</param>
    /// <param name="httpContext">HTTP context</param>
    /// <returns>Redirect result</returns>
    private static async Task<RedirectHttpResult> HandleLogout(
        string? returnUrl,
        HttpContext httpContext)
    {
        AuthenticationProperties properties = new() { RedirectUri = returnUrl ?? "/" };

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme, properties);

        // Using TypedResults.Redirect for consistency
        return TypedResults.Redirect(returnUrl ?? "/");
    }

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