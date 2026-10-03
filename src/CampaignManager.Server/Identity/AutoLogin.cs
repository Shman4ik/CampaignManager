using System.Security.Claims;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace CampaignManager.Server.Identity;

/// <summary>Как этот браузер входил в прошлый раз: способ (<see cref="LoginMethods"/>) и почта.</summary>
public sealed record RememberedLogin(string Method, string Email);

/// <summary>
/// Автовход: браузер, который уже входил через Google, входит сам, без единого клика.
/// <para>
/// Тихий вход Auth0 (<c>prompt=none</c>) проверяет только сессию самого Auth0 — три дня без
/// активности — и к Google за ней не ходит, поэтому в v1 почти всегда кончался кнопкой «Войти».
/// Вместо него браузер помнит способ прошлого входа (<see cref="RememberedCookie"/> на год: способ и
/// почта, не сессия), и загрузка страницы без сессии сразу уходит в Auth0 с
/// <c>connection=google-oauth2</c>: Auth0 свою страницу не показывает, Google по <c>login_hint</c>
/// узнаёт аккаунт и возвращает обратно. Пароль так не угадать — входу по почте достаётся только
/// подставленный адрес на странице Auth0.
/// </para>
/// <para>
/// В WebAssembly с сервера грузится только первая страница, дальше навигация клиентская — поэтому
/// автовход срабатывает ровно там, где нужен: на открытии приложения.
/// </para>
/// </summary>
public static class AutoLogin
{
    /// <summary>Имя без порта; в Development к нему дописан порт сервера (<see cref="AppCookies"/>).</summary>
    public const string RememberedCookie = AppCookies.LastLogin;

    // Одна попытка на несколько минут: без метки неудачный автовход — отказ, отмена или брошенная
    // страница Google — повторялся бы на каждой загрузке.
    public const string AttemptCookie = AppCookies.AutoLoginAttempt;

    /// <summary>
    /// Срок метки попытки. Не «до закрытия браузера»: Chrome с «Продолжить с того же места» хранит
    /// сессионные куки неделями, и одна неудачная попытка выключала автовход в этом браузере насовсем —
    /// человек снова и снова видел главную гостя с кнопкой «Войти».
    /// </summary>
    public static readonly TimeSpan AttemptLifetime = TimeSpan.FromMinutes(10);

    // sub учёток с паролем — auth0|…, у Google — google-oauth2|…, а у кук, выданных ещё прямым
    // входом через Google, — голый id Google.
    private const string EmailSubjectPrefix = "auth0|";

    /// <summary>Между аутентификацией и авторизацией: страница под <c>[Authorize]</c> сначала пробует автовход.</summary>
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
            else if (GetRemembered(context.Request) is { Method: LoginMethods.Google }
                     && !context.Request.Cookies.ContainsKey(AppCookies.For(context).AutoLoginAttemptName))
            {
                context.Response.Cookies.Append(AppCookies.For(context).AutoLoginAttemptName, "1",
                    CreateCookieOptions(DateTimeOffset.UtcNow.Add(AttemptLifetime)));

                var returnUrl = $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
                await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme,
                    AccountEndpoints.CreateChallenge(context, returnUrl, LoginMethods.Google));
                return;
            }

            await next(context);
        });

    public static RememberedLogin? GetRemembered(HttpRequest request)
    {
        var value = request.Cookies[AppCookies.For(request.HttpContext).LastLoginName];
        var separator = value?.IndexOf(':') ?? -1;
        if (value is null || separator <= 0 || separator == value.Length - 1)
        {
            return null;
        }

        return ParseMethod(value[..separator]) is { } method
            ? new RememberedLogin(method, value[(separator + 1)..])
            : null;
    }

    /// <summary>Выход — это «забудь меня»: после него браузер сам больше не входит.</summary>
    public static void Forget(HttpContext context) =>
        context.Response.Cookies.Delete(AppCookies.For(context).LastLoginName, CreateCookieOptions(expires: null));

    /// <summary><c>google</c> / <c>email</c> без учёта регистра; прочее — <c>null</c>.</summary>
    public static string? ParseMethod(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        LoginMethods.Google => LoginMethods.Google,
        LoginMethods.Email => LoginMethods.Email,
        _ => null,
    };

    /// <summary>Коннекшен тенанта Auth0 для способа входа.</summary>
    public static string GetConnectionName(string method) =>
        method == LoginMethods.Google ? "google-oauth2" : "Username-Password-Authentication";

    /// <summary>
    /// Настоящая загрузка страницы: не статика, не <c>fetch</c> (у него <c>Sec-Fetch-Dest: empty</c>),
    /// не API и не адреса самого входа и выхода — им автовход только мешает.
    /// </summary>
    private static bool IsPageLoad(HttpRequest request) =>
        HttpMethods.IsGet(request.Method)
        && request.Headers["Sec-Fetch-Dest"] == "document"
        && !request.Path.StartsWithSegments(ApiRoutes.Prefix)
        && !request.Path.StartsWithSegments("/account")
        && !request.Path.StartsWithSegments("/signin-oidc")
        && !request.Path.StartsWithSegments("/signout-callback-oidc");

    /// <summary>
    /// Кука пишется с первой же страницы после входа, а не в обработчике входа: так её получают и
    /// те, кто вошёл до автовхода. Перезаписывается, только если в браузере вошёл кто-то другой.
    /// </summary>
    private static void Remember(HttpContext context)
    {
        var user = context.User;
        var cookies = AppCookies.For(context);
        // Тестовая сессия Development — не способ входа этого браузера: запомни её, и после выхода
        // автовход повёл бы в Google с почтой dev-…@cm.test.
        if (user.FindFirst(ClaimTypes.Email)?.Value is { } email && !DevLogin.IsDevSession(user))
        {
            var isEmailAccount = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                .StartsWith(EmailSubjectPrefix, StringComparison.Ordinal) == true;
            var value = $"{(isEmailAccount ? LoginMethods.Email : LoginMethods.Google)}:{email}";

            if (context.Request.Cookies[cookies.LastLoginName] != value)
            {
                context.Response.Cookies.Append(cookies.LastLoginName, value, CreateCookieOptions(DateTimeOffset.UtcNow.AddYears(1)));
            }
        }

        if (context.Request.Cookies.ContainsKey(cookies.AutoLoginAttemptName))
        {
            context.Response.Cookies.Delete(cookies.AutoLoginAttemptName, CreateCookieOptions(expires: null));
        }
    }

    private static CookieOptions CreateCookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Expires = expires,
    };
}
