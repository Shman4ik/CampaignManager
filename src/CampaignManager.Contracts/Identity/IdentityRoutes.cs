namespace CampaignManager.Contracts.Identity;

/// <summary>
/// Вход и текущий пользователь. Вход и выход — не API, а переходы браузера на сервер: OIDC живёт
/// на сервере, клиент WebAssembly только уходит туда полной загрузкой страницы.
/// </summary>
public static class IdentityRoutes
{
    /// <summary><c>GET</c> — кто я: <see cref="MeResponse"/>, без сессии — 401.</summary>
    public const string Me = ApiRoutes.Prefix + "/me";

    /// <summary>Страница выбора способа входа (клиент). Сюда же сервер уводит со страницы под <c>[Authorize]</c>.</summary>
    public const string LoginPage = "/login";

    /// <summary>
    /// Вход через Auth0: <c>?returnUrl=…</c>. Способ входа (Google, passkey, почта и пароль) человек
    /// выбирает на странице Auth0 — приложение её не обходит.
    /// </summary>
    public const string Login = "/account/login";

    /// <summary>Выход: своя кука, «забыть» браузер и сессия Auth0.</summary>
    public const string Logout = "/account/logout";

    public const string ReturnUrlParameter = "returnUrl";

    /// <summary>
    /// Вход тестовым пользователем без Auth0 — <b>только в Development</b>, в остальных окружениях
    /// адреса нет (404): <c>?as=player|keeper|admin[&amp;email=…][&amp;returnUrl=…]</c>.
    /// </summary>
    public const string DevLogin = "/dev/login";

    /// <summary>Адрес тестового входа с ролью <paramref name="role"/> (имя члена <c>UserRole</c>) и возвратом на <paramref name="returnUrl"/>.</summary>
    public static string DevLoginUrl(string role, string? returnUrl) =>
        string.IsNullOrEmpty(returnUrl)
            ? $"{DevLogin}?as={Uri.EscapeDataString(role)}"
            : $"{DevLogin}?as={Uri.EscapeDataString(role)}&{ReturnUrlParameter}={Uri.EscapeDataString(returnUrl)}";

    /// <summary>Адрес входа с возвратом на <paramref name="returnUrl"/>.</summary>
    public static string LoginUrl(string? returnUrl) =>
        string.IsNullOrEmpty(returnUrl) ? Login : $"{Login}?{ReturnUrlParameter}={Uri.EscapeDataString(returnUrl)}";
}

/// <summary>
/// Способ прошлого входа — так он пишется в куке прошлого входа (автовход). Passkey — это учётка
/// <c>Username-Password-Authentication</c>, поэтому для приложения он тот же <see cref="Email"/>.
/// </summary>
public static class LoginMethods
{
    /// <summary><c>google-oauth2</c>.</summary>
    public const string Google = "google";

    /// <summary><c>Username-Password-Authentication</c>: почта с паролем или passkey, учётки заводит администратор.</summary>
    public const string Email = "email";
}

/// <summary>
/// Имена ролевых политик — общие у сервера и клиента. На страницах UI только они
/// (<c>[Authorize(Policy = Policies.Keeper)]</c>), а не <c>Roles</c>: сервер проверяет атрибут страницы
/// при прямой загрузке, а роли в его куке нет — она в <c>cm.users</c>.
/// </summary>
public static class Policies
{
    /// <summary>Хранитель или администратор.</summary>
    public const string Keeper = "Keeper";

    public const string Admin = "Admin";
}
