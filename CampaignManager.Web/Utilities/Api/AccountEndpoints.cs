using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

namespace CampaignManager.Web.Utilities.Api;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder accountGroup = routes.MapGroup("/api/account");

        accountGroup.MapGet("/login", HandleLogin);
        accountGroup.MapGet("/logout", HandleLogout);
    }

    private static async Task<IResult> HandleLogin(string? returnUrl, HttpContext httpContext)
    {
        // Очищаем существующие куки
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Устанавливаем путь перенаправления после аутентификации
        AuthenticationProperties properties = new() { RedirectUri = returnUrl ?? "/", IsPersistent = true };

        // Вызываем аутентификацию Google
        return Results.Challenge(properties, new[] { GoogleDefaults.AuthenticationScheme });
    }

    private static async Task<IResult> HandleLogout(string? returnUrl, HttpContext httpContext)
    {
        AuthenticationProperties properties = new() { RedirectUri = returnUrl ?? "/" };

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme, properties);
        return Results.Redirect(returnUrl ?? "/");
    }
}