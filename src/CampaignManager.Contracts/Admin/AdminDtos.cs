using CampaignManager.Core.Identity;

namespace CampaignManager.Contracts.Admin;

// Почта здесь есть, в отличие от остальных модулей: администратор управляет людьми, а сами люди
// различаются по почте (имена совпадают и бывают равны почте).

/// <param name="PendingApplications">Заявок на рассмотрении — счётчик у пункта «Заявки» в меню.</param>
public sealed record AdminSummaryDto(int PendingApplications);

/// <param name="IsMe">Это я — свою роль администратор может снять, только если он не последний администратор.</param>
/// <param name="HasPendingApplication">У человека заявка на рассмотрении.</param>
public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    bool IsMe,
    bool HasPendingApplication);

public sealed record ChangeRoleRequest(UserRole Role);

/// <param name="UserRole">Роль подавшего сейчас — одобрение администратору роль не понизит.</param>
/// <param name="ReviewerName">Кто рассмотрел; <c>null</c> — ещё не рассмотрена или рассмотревший удалён.</param>
public sealed record KeeperApplicationDto(
    Guid Id,
    Guid UserId,
    string UserName,
    string UserEmail,
    UserRole UserRole,
    string Message,
    KeeperApplicationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt,
    string? ReviewerName,
    string? ReviewComment);

/// <param name="Comment">Причина — её увидит подавший в кабинете; можно пусто.</param>
public sealed record RejectApplicationRequest(string? Comment);
