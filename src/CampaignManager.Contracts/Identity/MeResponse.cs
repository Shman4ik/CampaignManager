using CampaignManager.Core.Identity;

namespace CampaignManager.Contracts.Identity;

/// <summary>
/// Ответ <c>GET /api/v1/me</c>. Роль — из <c>cm.users</c> на момент запроса: смена роли
/// администратором видна со следующей загрузки, без кэша claims на пять минут, как в v1.
/// </summary>
public sealed record MeResponse(Guid Id, string Email, string DisplayName, UserRole Role);

public interface IIdentityApi
{
    /// <summary>Текущий пользователь; <c>null</c> — сессии нет (сервер ответил 401).</summary>
    Task<MeResponse?> GetMeAsync(CancellationToken cancellationToken = default);
}
