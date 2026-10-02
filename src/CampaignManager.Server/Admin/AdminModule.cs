using CampaignManager.Contracts.Admin;
using CampaignManager.Contracts.Identity;
using CampaignManager.Core.Identity;

namespace CampaignManager.Server.Admin;

/// <summary>
/// Админка: пользователи, роли, заявки. Знание модуля — <c>Admin/CLAUDE.md</c>. Сироты файлов — тоже админка,
/// но их API у модуля файлов (<c>FilesRoutes.Orphans</c>), страница — <c>/admin/files</c> в UI.
/// </summary>
public static class AdminModule
{
    private const string Tag = "Admin";

    public static WebApplicationBuilder AddAdminModule(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<AdminService>();
        return builder;
    }

    public static IEndpointRouteBuilder MapAdminApi(this IEndpointRouteBuilder app)
    {
        // Политика отсекает не-администратора ещё до сервиса; сервис всё равно проверяет сам.
        var admin = app.MapGroup("").RequireAuthorization(Policies.Admin).WithTags(Tag);

        admin.MapGet(AdminRoutes.Summary, (AdminService service, CancellationToken ct) => service.GetSummaryAsync(ct))
            .WithName("GetAdminSummary");
        admin.MapGet(AdminRoutes.Users, (AdminService service, CancellationToken ct) => service.GetUsersAsync(ct))
            .WithName("GetUsers");
        admin.MapPut(AdminRoutes.UserRolePattern, (Guid userId, ChangeRoleRequest request, AdminService service, CancellationToken ct) =>
                service.ChangeRoleAsync(userId, request.Role, ct))
            .WithName("ChangeUserRole");

        admin.MapGet(AdminRoutes.Applications, (KeeperApplicationStatus? status, AdminService service, CancellationToken ct) =>
                service.GetApplicationsAsync(status, ct))
            .WithName("GetKeeperApplications");
        admin.MapPost(AdminRoutes.ApprovePattern, (Guid applicationId, AdminService service, CancellationToken ct) =>
                service.ApproveAsync(applicationId, ct))
            .WithName("ApproveKeeperApplication");
        admin.MapPost(AdminRoutes.RejectPattern, (Guid applicationId, RejectApplicationRequest request, AdminService service,
                CancellationToken ct) => service.RejectAsync(applicationId, request.Comment, ct))
            .WithName("RejectKeeperApplication");

        return app;
    }
}
