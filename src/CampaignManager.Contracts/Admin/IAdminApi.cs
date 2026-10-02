using CampaignManager.Core.Identity;

namespace CampaignManager.Contracts.Admin;

/// <summary>
/// Админка. Ошибки — <see cref="HttpRequestException"/> с текстом ProblemDetails: 403 — не администратор,
/// 404 — нет такого пользователя или заявки, 400 — форма, 409 — так нельзя по смыслу (снять роль с последнего
/// администратора, рассмотреть уже рассмотренную заявку).
/// </summary>
public interface IAdminApi
{
    Task<AdminSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<AdminUserDto> ChangeRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KeeperApplicationDto>> GetApplicationsAsync(KeeperApplicationStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>Одобрить: заявка — «одобрена», подавший — Хранитель; одной транзакцией.</summary>
    Task<KeeperApplicationDto> ApproveAsync(Guid applicationId, CancellationToken cancellationToken = default);

    Task<KeeperApplicationDto> RejectAsync(Guid applicationId, string? comment, CancellationToken cancellationToken = default);
}
