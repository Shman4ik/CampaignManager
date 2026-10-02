using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace CampaignManager.Server.Identity;

/// <summary>
/// Имена кук, которые ставит приложение: вход, автовход, корреляция и nonce OIDC, antiforgery.
/// <para>
/// <b>Только в Development</b> к имени дописывается порт сервера (<c>.CampaignManager.Auth.8083</c>):
/// куки привязаны к хосту, а не к порту, и несколько серверов на <c>localhost</c> (агенты запускают их
/// параллельно на 8083, 8084, …) иначе перезаписывали бы друг другу вход. В Testing, Production и Beta
/// <see cref="Port"/> — <c>null</c> и имена прежние: другое имя куки выбило бы всех пользователей.
/// </para>
/// <para>
/// Порт берётся из конфигурации адресов при сборке сервиса (<see cref="FindPort"/>), а не из
/// <c>IServerAddressesFeature</c> и не из запроса. Имена кук OIDC, входа и antiforgery — опции, они
/// одни на всё приложение, а antiforgery читает свои ещё при сборке конвейера, до старта Kestrel:
/// реальных адресов в фиче к этому моменту нет, есть только скопированные из той же конфигурации.
/// По запросу (<c>Host</c>) имя пришлось бы менять у каждой куки по-своему, а за прокси порт в
/// <c>Host</c> вообще не тот, на котором слушает сервер.
/// </para>
/// </summary>
public sealed class AppCookies(int? port)
{
    public const string Auth = ".CampaignManager.Auth";

    /// <summary>Способ и почта прошлого входа (<see cref="AutoLogin"/>), год.</summary>
    public const string LastLogin = ".CampaignManager.LastLogin";

    /// <summary>Метка попытки автовхода на сессию браузера (<see cref="AutoLogin"/>).</summary>
    public const string AutoLoginAttempt = ".CampaignManager.AutoLogin";

    /// <summary>Префикс куки корреляции OIDC: обработчик дописывает к нему id попытки входа.</summary>
    public const string CorrelationPrefix = ".CampaignManager.Correlation";

    /// <summary>Префикс куки nonce OIDC: обработчик дописывает к нему хеш nonce.</summary>
    public const string NoncePrefix = ".CampaignManager.Nonce.";

    /// <summary>Порт в именах кук; <c>null</c> — имена без изменений (всё, кроме Development).</summary>
    public int? Port { get; } = port;

    public string AuthName => Name(Auth);

    public string LastLoginName => Name(LastLogin);

    public string AutoLoginAttemptName => Name(AutoLoginAttempt);

    public string CorrelationName => Prefix(CorrelationPrefix);

    public string NonceName => Prefix(NoncePrefix);

    /// <summary><c>.CampaignManager.Auth</c> → <c>.CampaignManager.Auth.8083</c> (в Development).</summary>
    public string Name(string name) => Port is { } value ? $"{name}.{value}" : name;

    /// <summary>
    /// Префикс, к которому обработчик дописывает свою часть: <c>.CampaignManager.Nonce.</c> →
    /// <c>.CampaignManager.Nonce.8083.</c> — порт не сливается с дописанным хвостом.
    /// </summary>
    public string Prefix(string prefix) => Port is { } value ? $"{prefix.TrimEnd('.')}.{value}." : prefix;

    public static AppCookies For(HttpContext context) => context.RequestServices.GetRequiredService<AppCookies>();

    /// <summary>Порт в именах кук: в Development — порт сервера, если он известен; иначе <c>null</c>.</summary>
    public static AppCookies Create(IConfiguration configuration, IHostEnvironment environment) =>
        new(environment.IsDevelopment() ? FindPort(configuration) : null);

    /// <summary>
    /// Порт, на котором будет слушать сервер, — из тех же ключей, что читает хост: <c>urls</c>
    /// (<c>--urls</c>, <c>ASPNETCORE_URLS</c>, <c>applicationUrl</c> из <c>launchSettings.json</c>),
    /// затем <c>Kestrel:Endpoints:*:Url</c>, затем <c>https_ports</c>/<c>http_ports</c>. Из нескольких
    /// адресов — первый https, иначе первый. Порт 0 (выдаст система) и пустая конфигурация — <c>null</c>.
    /// </summary>
    public static int? FindPort(IConfiguration configuration)
    {
        List<string> urls = [.. Split(configuration[WebHostDefaults.ServerUrlsKey])];
        urls.AddRange(configuration.GetSection("Kestrel:Endpoints").GetChildren()
            .Select(endpoint => endpoint["Url"])
            .OfType<string>());

        var addresses = urls
            .Select(url => TryParse(url, out var address) ? address : null)
            .OfType<BindingAddress>()
            .ToList();
        var chosen = addresses.FirstOrDefault(a => string.Equals(a.Scheme, "https", StringComparison.OrdinalIgnoreCase)) ?? addresses.FirstOrDefault();
        if (chosen is not null)
        {
            return chosen.Port > 0 ? chosen.Port : null;
        }

        var ports = Split(configuration[WebHostDefaults.HttpsPortsKey])
            .Concat(Split(configuration[WebHostDefaults.HttpPortsKey]));
        return ports.Select(p => int.TryParse(p, out var value) && value > 0 ? value : (int?)null)
            .FirstOrDefault(p => p is not null);
    }

    /// <summary>Только в Development: дописывает порт к имени куки antiforgery, заданному ASP.NET Core.</summary>
    internal static void ConfigureAntiforgery(AntiforgeryOptions options, AppCookies cookies)
    {
        if (cookies.Port is not null && options.Cookie.Name is { } name)
        {
            options.Cookie.Name = cookies.Name(name);
        }
    }

    private static IEnumerable<string> Split(string? value) =>
        value?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

    private static bool TryParse(string url, out BindingAddress? address)
    {
        try
        {
            address = BindingAddress.Parse(url);
            return true;
        }
        catch (FormatException)
        {
            address = null;
            return false;
        }
    }
}
