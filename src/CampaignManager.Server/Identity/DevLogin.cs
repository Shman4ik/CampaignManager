using System.Net.Mail;
using System.Security.Claims;
using CampaignManager.Contracts.Identity;
using CampaignManager.Core.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CampaignManager.Server.Identity;

/// <summary>
/// Тестовый вход — <b>только Development</b>: <see cref="AccountEndpoints.MapIdentityApi"/> маппит его
/// лишь там, в Testing, Production и Beta адреса нет (404). Нужен агентам и разработчику, чтобы
/// проверить страницу под ролью в браузере без Auth0: dev-приложение Auth0 пускает только на
/// <c>https://localhost:8080</c> и на localhost всегда спрашивает согласие.
/// <para>
/// <c>GET /dev/login?as=player|keeper|admin[&amp;email=…][&amp;returnUrl=…]</c>: пользователь заводится
/// или находится тем же <see cref="UserDirectory.SignInAsync"/>, что при входе через Auth0 (белый
/// список в силе), получает запрошенную роль, а браузер — ту же куку входа. Выход — обычный
/// <c>/account/logout</c>, только без похода в Auth0.
/// </para>
/// </summary>
public static class DevLogin
{
    /// <summary>Claim сессии тестового входа: по нему выход не идёт в Auth0, а автовход её не запоминает.</summary>
    public const string SessionClaim = "cm_dev";

    /// <summary>Домен почты тестовых пользователей по умолчанию: <c>dev-keeper@cm.test</c> и т. д.</summary>
    public const string EmailDomain = "cm.test";

    // sub тестовой учётки: у Auth0 таких префиксов нет (auth0|, google-oauth2|), с настоящими не пересечётся.
    private const string SubjectPrefix = "dev|";

    public static bool IsDevSession(ClaimsPrincipal user) => user.HasClaim(c => c.Type == SessionClaim);

    internal static IEndpointRouteBuilder MapDevLogin(this IEndpointRouteBuilder app)
    {
        app.MapGet(IdentityRoutes.DevLogin, LoginAsync).AllowAnonymous().ExcludeFromDescription();
        return app;
    }

    private static async Task<Results<RedirectHttpResult, BadRequest<string>>> LoginAsync(
        [FromQuery(Name = "as")] string? role,
        string? email,
        string? returnUrl,
        HttpContext httpContext,
        UserDirectory directory,
        CancellationToken cancellationToken)
    {
        // Только имена: Enum.TryParse принял бы и «2».
        if (role is null || !Enum.GetNames<UserRole>().Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            return TypedResults.BadRequest("Параметр as: player, keeper или admin.");
        }

        var userRole = Enum.Parse<UserRole>(role, ignoreCase: true);
        var defaultAccount = string.IsNullOrWhiteSpace(email);
        var address = defaultAccount ? $"dev-{userRole.ToString().ToLowerInvariant()}@{EmailDomain}" : email!.Trim();
        if (!MailAddress.TryCreate(address, out _))
        {
            return TypedResults.BadRequest("Параметр email — не адрес почты.");
        }

        var subject = SubjectPrefix + address.ToLowerInvariant();
        var user = await directory.DevSignInAsync(
            new ExternalLogin(subject, address, defaultAccount ? DisplayName(userRole) : null), userRole, cancellationToken);
        if (user is null)
        {
            return TypedResults.LocalRedirect($"{IdentityRoutes.LoginPage}?authStatus=accessDenied");
        }

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            IdentityModule.CreateSessionPrincipal(user, subject, CookieAuthenticationDefaults.AuthenticationScheme,
                new Claim(SessionClaim, "1")),
            new AuthenticationProperties { IsPersistent = true });

        return TypedResults.LocalRedirect(ReturnUrl.Normalize(returnUrl, httpContext.Request.Host.Host));
    }

    private static string DisplayName(UserRole role) => role switch
    {
        UserRole.Admin => "Тестовый администратор",
        UserRole.Keeper => "Тестовый Хранитель",
        _ => "Тестовый игрок",
    };
}
