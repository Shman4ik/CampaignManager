using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

namespace CampaignManager.Web.Utilities.Api;

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

    private static async Task<IResult> HandleLogin(string? returnUrl, string? mode, HttpContext httpContext)
    {
        // Очищаем существующие куки
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var normalizedReturnUrl = NormalizeReturnUrl(returnUrl, httpContext);

        var selectedMode = string.IsNullOrWhiteSpace(mode)
            ? InteractiveModeValue
            : mode.Trim().ToLowerInvariant();
        var isSilent = string.Equals(selectedMode, SilentModeValue, StringComparison.Ordinal);

        // Устанавливаем путь перенаправления после аутентификации
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

        // Вызываем аутентификацию Google
        return Results.Challenge(properties, new[] { GoogleDefaults.AuthenticationScheme });
    }

    private static async Task<IResult> HandleLogout(string? returnUrl, HttpContext httpContext)
    {
        AuthenticationProperties properties = new() { RedirectUri = returnUrl ?? "/" };

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme, properties);
        return Results.Redirect(returnUrl ?? "/");
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