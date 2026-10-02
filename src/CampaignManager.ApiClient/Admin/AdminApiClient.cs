using System.Net.Http.Json;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Admin;
using CampaignManager.Core.Identity;

namespace CampaignManager.ApiClient.Admin;

public sealed class AdminApiClient(HttpClient http) : IAdminApi
{
    private static ContractsJsonContext Json => ContractsJsonContext.Default;

    public async Task<AdminSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(AdminRoutes.Summary, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.AdminSummaryDto, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(AdminRoutes.Users, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.IReadOnlyListAdminUserDto, cancellationToken);
    }

    public async Task<AdminUserDto> ChangeRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync(AdminRoutes.UserRole(userId), new ChangeRoleRequest(role), Json.ChangeRoleRequest,
            cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.AdminUserDto, cancellationToken);
    }

    public async Task<IReadOnlyList<KeeperApplicationDto>> GetApplicationsAsync(KeeperApplicationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(AdminRoutes.ApplicationsWith(status), cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.IReadOnlyListKeeperApplicationDto, cancellationToken);
    }

    public async Task<KeeperApplicationDto> ApproveAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync(AdminRoutes.Approve(applicationId), content: null, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.KeeperApplicationDto, cancellationToken);
    }

    public async Task<KeeperApplicationDto> RejectAsync(Guid applicationId, string? comment, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(AdminRoutes.Reject(applicationId), new RejectApplicationRequest(comment),
            Json.RejectApplicationRequest, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.KeeperApplicationDto, cancellationToken);
    }
}
