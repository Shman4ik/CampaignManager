using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Scenarios;

namespace CampaignManager.ApiClient.Scenarios;

/// <summary>Клиент прохождений, ваншотов и броней (T2.5c). Отказы — <see cref="Contracts.Platform.ApiException"/> с кодом.</summary>
public sealed class RunsApiClient(HttpClient http) : IRunsApi
{
    private static ContractsJsonContext Json => ContractsJsonContext.Default;

    public async Task<IReadOnlyList<ScenarioRunDto>> ListForCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(RunsRoutes.CampaignRuns(campaignId), cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.IReadOnlyListScenarioRunDto, cancellationToken, withCode: true);
    }

    public Task<ScenarioRunDto> PlayInCampaignAsync(Guid scenarioId, PlayInCampaignRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, ScenariosRoutes.Runs(scenarioId), request, Json.PlayInCampaignRequest, Json.ScenarioRunDto, cancellationToken);

    public Task<ScenarioRunDto> AnnounceOneShotAsync(Guid scenarioId, AnnounceOneShotRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, ScenariosRoutes.OneShot(scenarioId), request, Json.AnnounceOneShotRequest, Json.ScenarioRunDto, cancellationToken);

    public Task<ScenarioRunDto> UpdateAsync(Guid runId, RunInput input, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, RunsRoutes.Run(runId), input, Json.RunInput, Json.ScenarioRunDto, cancellationToken);

    public async Task DeleteAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync(RunsRoutes.Run(runId), cancellationToken);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken, withCode: true);
    }

    public Task<ReservationDto> ReserveAsync(Guid runId, Guid pregenId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, RunsRoutes.Reservations(runId), new ReserveRequest(pregenId), Json.ReserveRequest, Json.ReservationDto,
            cancellationToken);

    public async Task ReleaseAsync(Guid runId, Guid pregenId, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync(RunsRoutes.Reservation(runId, pregenId), cancellationToken);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken, withCode: true);
    }

    private async Task<TResult> SendAsync<TBody, TResult>(HttpMethod method, string url, TBody body, JsonTypeInfo<TBody> bodyType,
        JsonTypeInfo<TResult> resultType, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body, bodyType) };
        using var response = await http.SendAsync(request, cancellationToken);
        return await ApiResponses.ReadAsync(response, resultType, cancellationToken, withCode: true);
    }
}
