using System.Security.Claims;

namespace CampaignManager.Server.Identity;

/// <summary>Приложение M2M тенанта, которому разрешено ходить в API, и чьими правами (<c>Authorization:MachineClients</c>).</summary>
public sealed class MachineClient
{
    /// <summary>client_id приложения Auth0 (в токене — <c>azp</c>).</summary>
    public string ClientId { get; set; } = "";

    /// <summary>Почта пользователя <c>cm.users</c>, от чьего имени работает агент: автор импорта, права — его.</summary>
    public string ActAs { get; set; } = "";
}

/// <summary>Адрес открыт токену агента: нужен этот scope; <c>null</c> — любой токен агента (например, <c>/me</c>).</summary>
public sealed record MachineScopeMetadata(string? Scope);

/// <summary>
/// Токены агентов — Auth0 client credentials (M2M). Токен не заводит и не привязывает пользователя: он работает от имени
/// <see cref="MachineClient.ActAs"/>, а ходить может только туда, где адрес помечен <see cref="AllowMachine{TBuilder}"/> и в
/// токене есть нужный scope; остальное — 403 (<see cref="UseMachineScopes"/>). Знание — <c>Identity/CLAUDE.md</c>.
/// </summary>
public static class MachineAccess
{
    /// <summary>Метка Auth0 у токена client credentials (claim <c>gty</c>).</summary>
    public const string GrantType = "client-credentials";

    /// <summary>Открыть адрес токену агента со scope <paramref name="scope"/> (<c>null</c> — любому токену агента).</summary>
    public static TBuilder AllowMachine<TBuilder>(this TBuilder builder, string? scope = null)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new MachineScopeMetadata(scope));

    public static bool IsMachineToken(ClaimsPrincipal token) => token.FindFirstValue("gty") == GrantType;

    public static bool IsMachine(ClaimsPrincipal principal) => principal.HasClaim(c => c.Type == CmClaims.Machine);

    public static bool HasScope(ClaimsPrincipal principal, string scope) =>
        principal.HasClaim(c => c.Type == CmClaims.Scope && c.Value == scope);

    /// <summary>
    /// Сессия по проверенному токену client credentials: приложение из белого списка → почта того, от чьего имени оно
    /// работает, и scope'ы токена. <c>null</c> и причина — если приложение не допущено.
    /// </summary>
    public static (ClaimsPrincipal? Principal, string? Failure) CreatePrincipal(ClaimsPrincipal token, IReadOnlyList<MachineClient> clients,
        string authenticationType)
    {
        var clientId = token.FindFirstValue("azp");
        var client = clients.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.ClientId) && c.ClientId == clientId);
        if (client is null || string.IsNullOrWhiteSpace(client.ActAs))
        {
            return (null, "Приложение не допущено к API: его нет в Authorization:MachineClients.");
        }

        List<Claim> claims =
        [
            new(CmClaims.Machine, client.ClientId),
            new(ClaimTypes.NameIdentifier, token.FindFirstValue("sub") ?? $"{client.ClientId}@clients"),
            new(ClaimTypes.Email, client.ActAs.Trim()),
        ];
        var scopes = token.FindFirstValue("scope")?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        claims.AddRange(scopes.Distinct(StringComparer.Ordinal).Select(scope => new Claim(CmClaims.Scope, scope)));
        return (new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType, ClaimTypes.Name, ClaimTypes.Role)), null);
    }

    /// <summary>
    /// Запрет по умолчанию: токен агента проходит только на адрес с <see cref="MachineScopeMetadata"/> и нужным scope. Стоит
    /// сразу за аутентификацией — эндпоинт к этому времени уже выбран (маршрутизацию <c>WebApplication</c> ставит первой).
    /// </summary>
    public static WebApplication UseMachineScopes(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (IsMachine(context.User))
            {
                var allowed = context.GetEndpoint()?.Metadata.GetMetadata<MachineScopeMetadata>();
                if (allowed is null || (allowed.Scope is { } scope && !HasScope(context.User, scope)))
                {
                    var detail = allowed?.Scope is { } needed
                        ? $"Токену агента нужен scope {needed}."
                        : "Токену агента сюда нельзя: адрес не открыт агентам.";
                    await TypedResults.Problem(detail, statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(context);
                    return;
                }
            }

            await next(context);
        });
        return app;
    }
}
