using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Encounters;
using CampaignManager.Core.Encounters;

namespace CampaignManager.ApiClient.Encounters;

/// <summary>
/// Клиент сцен. Запись состояния и завершение несут <c>If-Match</c> с версией, которую правили: устаревшая запись
/// получает 409 <c>stale</c>. Отказы — <see cref="Contracts.Platform.ApiException"/> с кодом.
/// </summary>
public sealed class EncountersApiClient(HttpClient http) : IEncountersApi
{
    private static ContractsJsonContext Json => ContractsJsonContext.Default;

    public async Task<IReadOnlyList<EncounterSummaryDto>> ListActiveAsync(EncounterKind? kind, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(EncountersRoutes.Active(kind), cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.IReadOnlyListEncounterSummaryDto, cancellationToken, withCode: true);
    }

    public async Task<IReadOnlyList<EncounterSummaryDto>> ListFinishedAsync(EncounterKind? kind, int take, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(EncountersRoutes.FinishedList(kind, take), cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.IReadOnlyListEncounterSummaryDto, cancellationToken, withCode: true);
    }

    public async Task<EncounterDto> StartAsync(StartEncounterRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(EncountersRoutes.Encounters, request, Json.StartEncounterRequest, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.EncounterDto, cancellationToken, withCode: true);
    }

    public async Task<EncounterDto> GetAsync(Guid encounterId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(EncountersRoutes.Encounter(encounterId), cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.EncounterDto, cancellationToken, withCode: true);
    }

    public async Task<EncounterSavedDto> SaveStateAsync(Guid encounterId, EncounterState state, uint version,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, EncountersRoutes.State(encounterId))
        {
            Content = JsonContent.Create(state, Json.EncounterState),
        };
        return await SendAsync(request, version, cancellationToken);
    }

    public async Task<EncounterSavedDto> SetRunAsync(Guid encounterId, Guid? runId, uint version, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, EncountersRoutes.Run(encounterId))
        {
            Content = JsonContent.Create(new SetEncounterRunRequest(runId), Json.SetEncounterRunRequest),
        };
        return await SendAsync(request, version, cancellationToken);
    }

    public async Task<EncounterSavedDto> FinishAsync(Guid encounterId, uint version, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, EncountersRoutes.Finish(encounterId));
        return await SendAsync(request, version, cancellationToken);
    }

    private async Task<EncounterSavedDto> SendAsync(HttpRequestMessage request, uint version, CancellationToken cancellationToken)
    {
        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{version.ToString(CultureInfo.InvariantCulture)}\""));
        using var response = await http.SendAsync(request, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.EncounterSavedDto, cancellationToken, withCode: true);
    }
}
