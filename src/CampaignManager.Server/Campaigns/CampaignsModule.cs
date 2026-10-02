using CampaignManager.Contracts.Campaigns;

namespace CampaignManager.Server.Campaigns;

/// <summary>Кампании, участники, журнал встреч и главная. Знание модуля — <c>Campaigns/CLAUDE.md</c>.</summary>
public static class CampaignsModule
{
    private const string Tag = "Campaigns";

    public static WebApplicationBuilder AddCampaignsModule(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddScoped<CampaignService>();
        services.AddScoped<JournalService>();
        services.AddScoped<HomeService>();
        services.AddExceptionHandler<CampaignRejectedExceptionHandler>();
        return builder;
    }

    /// <summary>
    /// Все эндпоинты — только вошедшим. Кто что может, решают сервисы через <c>AccessPolicy</c>:
    /// группа отсекает лишь анонима.
    /// </summary>
    public static IEndpointRouteBuilder MapCampaignsApi(this IEndpointRouteBuilder app)
    {
        app.MapGet(CampaignsRoutes.Home, (HomeService home, CancellationToken ct) => home.GetAsync(ct))
            .RequireAuthorization().WithName("GetHome").WithTags(Tag);

        app.MapGet(CampaignsRoutes.Campaigns, (CampaignService campaigns, CancellationToken ct) => campaigns.ListAsync(ct))
            .RequireAuthorization().WithName("GetCampaigns").WithTags(Tag);
        app.MapPost(CampaignsRoutes.Campaigns, (CampaignInput input, CampaignService campaigns, CancellationToken ct) =>
                campaigns.CreateAsync(input, ct))
            .RequireAuthorization().WithName("CreateCampaign").WithTags(Tag);
        app.MapGet(CampaignsRoutes.CampaignPattern, (Guid campaignId, CampaignService campaigns, CancellationToken ct) =>
                campaigns.GetAsync(campaignId, ct))
            .RequireAuthorization().WithName("GetCampaign").WithTags(Tag);
        app.MapPut(CampaignsRoutes.CampaignPattern, (Guid campaignId, CampaignInput input, CampaignService campaigns, CancellationToken ct) =>
                campaigns.UpdateAsync(campaignId, input, ct))
            .RequireAuthorization().WithName("UpdateCampaign").WithTags(Tag);
        app.MapDelete(CampaignsRoutes.CampaignPattern, async (Guid campaignId, CampaignService campaigns, CancellationToken ct) =>
            {
                await campaigns.DeleteAsync(campaignId, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization().WithName("DeleteCampaign").WithTags(Tag);

        app.MapPost(CampaignsRoutes.JoinPattern, (Guid campaignId, JoinCampaignRequest request, CampaignService campaigns,
                CancellationToken ct) => campaigns.JoinAsync(campaignId, request, ct))
            .RequireAuthorization().WithName("JoinCampaign").WithTags(Tag);
        app.MapPut(CampaignsRoutes.MemberPattern, (Guid campaignId, Guid userId, UpdateMemberRequest request, CampaignService campaigns,
                CancellationToken ct) => campaigns.UpdateMemberAsync(campaignId, userId, request, ct))
            .RequireAuthorization().WithName("UpdateCampaignMember").WithTags(Tag);
        app.MapDelete(CampaignsRoutes.MemberPattern, async (Guid campaignId, Guid userId, CampaignService campaigns, CancellationToken ct) =>
            {
                await campaigns.RemoveMemberAsync(campaignId, userId, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization().WithName("RemoveCampaignMember").WithTags(Tag);

        app.MapGet(CampaignsRoutes.JournalPattern, (Guid campaignId, JournalService journal, CancellationToken ct) =>
                journal.GetAsync(campaignId, ct))
            .RequireAuthorization().WithName("GetCampaignJournal").WithTags(Tag);
        app.MapPost(CampaignsRoutes.JournalPattern, (Guid campaignId, CampaignSessionInput input, JournalService journal,
                CancellationToken ct) => journal.AddAsync(campaignId, input, ct))
            .RequireAuthorization().WithName("AddCampaignSession").WithTags(Tag);
        app.MapPut(CampaignsRoutes.SessionPattern, (Guid campaignId, Guid sessionId, CampaignSessionInput input, JournalService journal,
                CancellationToken ct) => journal.UpdateAsync(campaignId, sessionId, input, ct))
            .RequireAuthorization().WithName("UpdateCampaignSession").WithTags(Tag);
        app.MapDelete(CampaignsRoutes.SessionPattern, async (Guid campaignId, Guid sessionId, JournalService journal, CancellationToken ct) =>
            {
                await journal.DeleteAsync(campaignId, sessionId, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization().WithName("DeleteCampaignSession").WithTags(Tag);

        return app;
    }
}
