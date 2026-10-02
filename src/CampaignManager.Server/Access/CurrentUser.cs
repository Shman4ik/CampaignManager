using CampaignManager.Core.Identity;
using CampaignManager.Server.Identity;

namespace CampaignManager.Server.Access;

/// <summary>Кто делает запрос — строка <c>cm.users</c>, прочитанная один раз на запрос.</summary>
public sealed record SignedInUser(Guid Id, string Email, string DisplayName, UserRole Role)
{
    public bool IsAdmin => Role is UserRole.Admin;

    /// <summary>Хранитель по роли платформы: Хранитель или администратор.</summary>
    public bool IsKeeper => Role is UserRole.Keeper or UserRole.Admin;
}

/// <summary>
/// Текущий пользователь запроса: id, роль и почта — из базы, один раз на запрос (scoped).
/// Роль в куке не лежит и claims на каждый запрос не трансформируются: в v1 ради этого был
/// <c>UserClaimsCache</c> на пять минут, и смена роли доходила с опозданием.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor, UserDirectory directory)
{
    private Task<SignedInUser?>? _user;

    /// <summary><c>null</c> — запрос без сессии или сессия человека, которого в <c>cm.users</c> нет и завести нельзя.</summary>
    public Task<SignedInUser?> GetAsync(CancellationToken cancellationToken = default) =>
        _user ??= LoadAsync(cancellationToken);

    private async Task<SignedInUser?> LoadAsync(CancellationToken cancellationToken)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return await directory.ResolveAsync(principal, cancellationToken);
    }
}
