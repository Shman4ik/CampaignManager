using System.Security.Claims;
using CampaignManager.Web.Utilities.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace CampaignManager.Web.Utilities.Authorization;

/// <summary>Способ входа — коннекшен тенанта Auth0.</summary>
public enum LoginMethod
{
    /// <summary><c>google-oauth2</c>.</summary>
    Google,

    /// <summary>
    ///     <c>Username-Password-Authentication</c> — почта с паролем или passkey (он живёт в той же
    ///     учётке), учётки заводит администратор.
    /// </summary>
    Email
}

/// <summary>Как этот браузер входил в прошлый раз.</summary>
public sealed record RememberedLogin(LoginMethod Method, string Email);

/// <summary>
///     Автовход: браузер, который уже входил через Google, входит сам, без единого клика.
///     <para>
///         Сессия приложения — кука на 30 дней. Когда её нет (месяц не заходили, вышли, почистили
///         куки), раньше выручал тихий вход <c>prompt=none</c>: при прямом входе через Google он
///         проверял сессию Google, а она в браузере живёт месяцами. После переезда на Auth0 тот же
///         <c>prompt=none</c> проверяет только сессию самого Auth0 — три дня без активности, — и
///         к Google за ней не ходит, поэтому почти всегда кончался кнопкой «Войти».
///     </para>
///     <para>
///         Теперь браузер помнит, как в нём входили (<see cref="RememberedCookie" /> на год: способ и
///         почта, не сессия), и загрузку страницы без сессии сразу уводит в Auth0 с
///         <c>connection=google-oauth2</c>: Auth0 свою страницу не показывает, Google по
///         <c>login_hint</c> узнаёт свой аккаунт и возвращает обратно. Пароль так не угадать —
///         входу по почте достаётся только подставленный адрес на странице Auth0.
///     </para>
/// </summary>
public static class AutoLogin
{
    private const string RememberedCookie = ".CampaignManager.LastLogin";

    // Одна попытка на сессию браузера. Без метки неудачный автовход — отказ, отмена или
    // брошенная страница входа Google — повторялся бы на каждой загрузке страницы.
    private const string AttemptCookie = ".CampaignManager.AutoLogin";

    // Префикс sub у учёток с паролем. У Google — google-oauth2|…, а у кук, выданных ещё
    // прямым входом через Google, там голый id Google.
    private const string EmailSubjectPrefix = "auth0|";

    /// <summary>
    ///     Встаёт между аутентификацией и авторизацией: страница под <c>[Authorize]</c> без сессии
    ///     тоже сначала пробует автовход, а не страницу Auth0.
    /// </summary>
    public static IApplicationBuilder UseAutoLogin(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (!IsPageLoad(context.Request))
            {
                await next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated == true)
            {
                Remember(context);
            }
            else if (GetRemembered(context.Request) is { Method: LoginMethod.Google }
                     && !context.Request.Cookies.ContainsKey(AttemptCookie))
            {
                context.Response.Cookies.Append(AttemptCookie, "1", CreateCookieOptions(expires: null));

                var returnUrl = $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
                await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme,
                    AccountEndpoints.CreateChallenge(context, returnUrl, LoginMethod.Google));
                return;
            }

            await next(context);
        });

    public static RememberedLogin? GetRemembered(HttpRequest request)
    {
        var value = request.Cookies[RememberedCookie];
        var separator = value?.IndexOf(':') ?? -1;
        if (value is null || separator <= 0 || separator == value.Length - 1)
            return null;

        return ParseMethod(value[..separator]) is { } method
            ? new RememberedLogin(method, value[(separator + 1)..])
            : null;
    }

    /// <summary>Выход — это «забудь меня»: после него браузер сам больше не входит.</summary>
    public static void Forget(HttpContext context) =>
        context.Response.Cookies.Delete(RememberedCookie, CreateCookieOptions(expires: null));

    /// <summary><c>google</c> / <c>email</c> — так способ пишется в куке прошлого входа.</summary>
    public static LoginMethod? ParseMethod(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "google" => LoginMethod.Google,
        "email" => LoginMethod.Email,
        _ => null
    };

    public static string GetConnectionName(LoginMethod method) => method switch
    {
        LoginMethod.Google => "google-oauth2",
        _ => "Username-Password-Authentication"
    };

    private static string FormatMethod(LoginMethod method) => method switch
    {
        LoginMethod.Google => "google",
        _ => "email"
    };

    /// <summary>
    ///     Настоящая загрузка страницы: не статика, не enhanced-навигация Blazor
    ///     (у неё <c>Sec-Fetch-Dest: empty</c>) и не <c>/api</c> — там вход и выход, которым
    ///     автовход только мешает.
    /// </summary>
    private static bool IsPageLoad(HttpRequest request) =>
        HttpMethods.IsGet(request.Method)
        && request.Headers["Sec-Fetch-Dest"] == "document"
        && !request.Path.StartsWithSegments("/api");

    /// <summary>
    ///     Кука пишется с первой же страницы после входа, а не в обработчике входа: так её получают
    ///     и те, кто вошёл ещё до автовхода, по действующей сессии. Перезаписывается, только если
    ///     в браузере вошёл уже кто-то другой.
    /// </summary>
    private static void Remember(HttpContext context)
    {
        var user = context.User;
        if (user.FindFirst(ClaimTypes.Email)?.Value is { } email)
        {
            var isEmailAccount = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                .StartsWith(EmailSubjectPrefix, StringComparison.Ordinal) == true;
            var value = $"{FormatMethod(isEmailAccount ? LoginMethod.Email : LoginMethod.Google)}:{email}";

            if (context.Request.Cookies[RememberedCookie] != value)
                context.Response.Cookies.Append(RememberedCookie, value, CreateCookieOptions(DateTimeOffset.UtcNow.AddYears(1)));
        }

        if (context.Request.Cookies.ContainsKey(AttemptCookie))
            context.Response.Cookies.Delete(AttemptCookie, CreateCookieOptions(expires: null));
    }

    private static CookieOptions CreateCookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Expires = expires
    };
}
