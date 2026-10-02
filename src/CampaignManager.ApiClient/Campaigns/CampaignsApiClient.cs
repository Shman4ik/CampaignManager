using System.Net.Http.Json;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Campaigns;

namespace CampaignManager.ApiClient.Campaigns;

public sealed class CampaignsApiClient(HttpClient http) : ICampaignsApi
{
    private static ContractsJsonContext Json => ContractsJsonContext.Default;

    public async Task<HomeDto> GetHomeAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(CampaignsRoutes.Home, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.HomeDto, cancellationToken);
    }

    public async Task<IReadOnlyList<CampaignSummaryDto>> GetCampaignsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(CampaignsRoutes.Campaigns, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.IReadOnlyListCampaignSummaryDto, cancellationToken);
    }

    public async Task<CampaignDetailsDto> GetCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(CampaignsRoutes.Campaign(campaignId), cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.CampaignDetailsDto, cancellationToken);
    }

    public async Task<CampaignSummaryDto> CreateCampaignAsync(CampaignInput input, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(CampaignsRoutes.Campaigns, input, Json.CampaignInput, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.CampaignSummaryDto, cancellationToken);
    }

    public async Task<CampaignSummaryDto> UpdateCampaignAsync(Guid campaignId, CampaignInput input, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync(CampaignsRoutes.Campaign(campaignId), input, Json.CampaignInput, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.CampaignSummaryDto, cancellationToken);
    }

    public async Task DeleteCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync(CampaignsRoutes.Campaign(campaignId), cancellationToken);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<CampaignDetailsDto> JoinAsync(Guid campaignId, JoinCampaignRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(CampaignsRoutes.Join(campaignId), request, Json.JoinCampaignRequest, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.CampaignDetailsDto, cancellationToken);
    }

    public async Task<CampaignMemberDto> UpdateMemberAsync(Guid campaignId, Guid userId, UpdateMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync(CampaignsRoutes.Member(campaignId, userId), request, Json.UpdateMemberRequest,
            cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.CampaignMemberDto, cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync(CampaignsRoutes.Member(campaignId, userId), cancellationToken);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<CampaignJournalDto> GetJournalAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(CampaignsRoutes.Journal(campaignId), cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.CampaignJournalDto, cancellationToken);
    }

    public async Task<CampaignSessionDto> AddSessionAsync(Guid campaignId, CampaignSessionInput input, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(CampaignsRoutes.Journal(campaignId), input, Json.CampaignSessionInput, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.CampaignSessionDto, cancellationToken);
    }

    public async Task<CampaignSessionDto> UpdateSessionAsync(Guid campaignId, Guid sessionId, CampaignSessionInput input,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync(CampaignsRoutes.Session(campaignId, sessionId), input, Json.CampaignSessionInput,
            cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.CampaignSessionDto, cancellationToken);
    }

    public async Task DeleteSessionAsync(Guid campaignId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync(CampaignsRoutes.Session(campaignId, sessionId), cancellationToken);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken);
    }
}
