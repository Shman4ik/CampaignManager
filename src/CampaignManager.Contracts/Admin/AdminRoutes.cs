using CampaignManager.Core.Identity;

namespace CampaignManager.Contracts.Admin;

/// <summary>
/// Админка: пользователи, роли, заявки на Хранителя. Только администратору (политика <c>Admin</c>).
/// Сироты файлов — тоже админка, но маршруты у модуля файлов (<c>FilesRoutes.Orphans</c>).
/// </summary>
public static class AdminRoutes
{
    public const string Admin = ApiRoutes.Prefix + "/admin";

    /// <summary><c>GET</c> — сводка для меню: <see cref="AdminSummaryDto"/>.</summary>
    public const string Summary = Admin + "/summary";

    /// <summary><c>GET</c> — все пользователи (их десятки — фильтр и страницы на клиенте).</summary>
    public const string Users = Admin + "/users";

    /// <summary><c>PUT</c> <see cref="ChangeRoleRequest"/> — сменить роль.</summary>
    public const string UserRolePattern = Users + "/{userId:guid}/role";

    /// <summary><c>GET</c> — заявки на Хранителя, новые сверху; <c>?status=</c> — только с этим статусом.</summary>
    public const string Applications = Admin + "/keeper-applications";

    /// <summary><c>POST</c> — одобрить: статус и роль одной транзакцией.</summary>
    public const string ApprovePattern = Applications + "/{applicationId:guid}/approve";

    /// <summary><c>POST</c> <see cref="RejectApplicationRequest"/> — отклонить.</summary>
    public const string RejectPattern = Applications + "/{applicationId:guid}/reject";

    public const string StatusParameter = "status";

    public static string UserRole(Guid userId) => $"{Users}/{userId}/role";

    public static string ApplicationsWith(KeeperApplicationStatus? status) =>
        status is null ? Applications : $"{Applications}?{StatusParameter}={status}";

    public static string Approve(Guid applicationId) => $"{Applications}/{applicationId}/approve";

    public static string Reject(Guid applicationId) => $"{Applications}/{applicationId}/reject";
}
