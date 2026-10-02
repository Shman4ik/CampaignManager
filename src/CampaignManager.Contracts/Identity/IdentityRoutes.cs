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

    /// <summary>Вход через Auth0: <c>?method=google|email&amp;returnUrl=…</c>.</summary>
    public const string Login = "/account/login";

    /// <summary>Выход: своя кука, «забыть» браузер и сессия Auth0.</summary>
    public const string Logout = "/account/logout";

    public const string ReturnUrlParameter = "returnUrl";

    /// <summary>Адрес входа выбранным способом (<see cref="LoginMethods"/>) с возвратом на <paramref name="returnUrl"/>.</summary>
    public static string LoginUrl(string? method, string? returnUrl)
    {
        List<string> query = [];
        if (!string.IsNullOrEmpty(method))
        {
            query.Add($"method={Uri.EscapeDataString(method)}");
        }

        if (!string.IsNullOrEmpty(returnUrl))
        {
            query.Add($"{ReturnUrlParameter}={Uri.EscapeDataString(returnUrl)}");
        }

        return query.Count == 0 ? Login : $"{Login}?{string.Join('&', query)}";
    }
}

/// <summary>Способ входа — коннекшен тенанта Auth0; так он пишется в адресе входа и в куке прошлого входа.</summary>
public static class LoginMethods
{
    /// <summary><c>google-oauth2</c>.</summary>
    public const string Google = "google";

    /// <summary><c>Username-Password-Authentication</c>: почта и пароль, учётки заводит администратор.</summary>
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
