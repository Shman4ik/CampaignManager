using CampaignManager.Contracts.Scenarios;

namespace CampaignManager.Server.Scenarios;

/// <summary>Прохождения, ваншоты и брони (T2.5c) — в группе модуля сценариев (вошедшим; права — в <see cref="RunService"/>).</summary>
internal static class RunEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet(ScenariosRoutes.RunsPattern, (Guid scenarioId, RunService runs, CancellationToken ct) =>
            runs.ListAsync(scenarioId, ct)).WithName("ListScenarioRuns");
        group.MapPost(ScenariosRoutes.RunsPattern, async Task<IResult> (Guid scenarioId, PlayInCampaignRequest request, RunService runs,
            CancellationToken ct) =>
        {
            var run = await runs.PlayInCampaignAsync(scenarioId, request, ct);
            return TypedResults.Created(RunsRoutes.Run(run.Id), run);
        }).WithName("PlayScenarioInCampaign");
        group.MapPost(ScenariosRoutes.OneShotPattern, async Task<IResult> (Guid scenarioId, AnnounceOneShotRequest request, RunService runs,
            CancellationToken ct) =>
        {
            var run = await runs.AnnounceOneShotAsync(scenarioId, request, ct);
            return TypedResults.Created(RunsRoutes.Run(run.Id), run);
        }).WithName("AnnounceOneShot");

        group.MapGet(RunsRoutes.CampaignRunsPattern, (Guid campaignId, RunService runs, CancellationToken ct) =>
            runs.ListForCampaignAsync(campaignId, ct)).WithName("ListCampaignRuns");

        group.MapPut(RunsRoutes.RunPattern, (Guid runId, RunInput input, RunService runs, CancellationToken ct) =>
            runs.UpdateAsync(runId, input, ct)).WithName("UpdateRun");
        group.MapDelete(RunsRoutes.RunPattern, async (Guid runId, RunService runs, CancellationToken ct) =>
        {
            await runs.DeleteAsync(runId, ct);
            return TypedResults.NoContent();
        }).WithName("DeleteRun");

        group.MapPost(RunsRoutes.ReservationsPattern, (Guid runId, ReserveRequest request, RunService runs, CancellationToken ct) =>
            runs.ReserveAsync(runId, request.PregenId, ct)).WithName("ReservePregen");
        group.MapDelete(RunsRoutes.ReservationPattern, async (Guid runId, Guid pregenId, RunService runs, CancellationToken ct) =>
        {
            await runs.ReleaseAsync(runId, pregenId, ct);
            return TypedResults.NoContent();
        }).WithName("ReleasePregen");
    }
}
