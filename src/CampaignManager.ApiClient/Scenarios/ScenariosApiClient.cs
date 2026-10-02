using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Scenarios;

namespace CampaignManager.ApiClient.Scenarios;

/// <summary>
/// Клиент сценариев. Шапка и текст — с <c>If-Match</c> (версия корня), части — по строке без версии: каждая — своя строка,
/// чужую вкладку запись не трогает. Отказы — <see cref="Contracts.Platform.ApiException"/> с кодом.
/// </summary>
public sealed class ScenariosApiClient(HttpClient http) : IScenariosApi
{
    private static ContractsJsonContext Json => ContractsJsonContext.Default;

    public async Task<ScenarioListDto> ListAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(ScenariosRoutes.Scenarios, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.ScenarioListDto, cancellationToken, withCode: true);
    }

    public Task<ScenarioDto> CreateAsync(ScenarioInput input, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, ScenariosRoutes.Scenarios, input, Json.ScenarioInput, Json.ScenarioDto, cancellationToken);

    public async Task<ScenarioDto> GetAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(ScenariosRoutes.Scenario(scenarioId), cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.ScenarioDto, cancellationToken, withCode: true);
    }

    public Task<ScenarioSavedDto> UpdateAsync(Guid scenarioId, ScenarioInput input, uint version, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, ScenariosRoutes.Scenario(scenarioId), input, Json.ScenarioInput, Json.ScenarioSavedDto, cancellationToken, version);

    public Task<ScenarioSavedDto> SaveTextAsync(Guid scenarioId, string? bodyMd, uint version, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, ScenariosRoutes.Text(scenarioId), new ScenarioTextInput(bodyMd), Json.ScenarioTextInput, Json.ScenarioSavedDto,
            cancellationToken, version);

    public Task DeleteAsync(Guid scenarioId, CancellationToken cancellationToken = default) =>
        DeleteUrlAsync(ScenariosRoutes.Scenario(scenarioId), cancellationToken);

    public Task ReorderAsync(Guid scenarioId, ReorderRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, ScenariosRoutes.Order(scenarioId), request, Json.ReorderRequest, cancellationToken);

    public Task<ScenarioLocationDto> AddLocationAsync(Guid scenarioId, LocationInput input, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, ScenariosRoutes.Locations(scenarioId), input, Json.LocationInput, Json.ScenarioLocationDto, cancellationToken);

    public Task<ScenarioLocationDto> UpdateLocationAsync(Guid scenarioId, Guid locationId, LocationInput input, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, ScenariosRoutes.Location(scenarioId, locationId), input, Json.LocationInput, Json.ScenarioLocationDto, cancellationToken);

    public Task DeleteLocationAsync(Guid scenarioId, Guid locationId, CancellationToken cancellationToken = default) =>
        DeleteUrlAsync(ScenariosRoutes.Location(scenarioId, locationId), cancellationToken);

    public Task<ScenarioCheckDto> AddCheckAsync(Guid scenarioId, Guid locationId, CheckInput input, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, ScenariosRoutes.LocationChecks(scenarioId, locationId), input, Json.CheckInput, Json.ScenarioCheckDto, cancellationToken);

    public Task<ScenarioCheckDto> UpdateCheckAsync(Guid scenarioId, Guid checkId, CheckInput input, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, ScenariosRoutes.Check(scenarioId, checkId), input, Json.CheckInput, Json.ScenarioCheckDto, cancellationToken);

    public Task DeleteCheckAsync(Guid scenarioId, Guid checkId, CancellationToken cancellationToken = default) =>
        DeleteUrlAsync(ScenariosRoutes.Check(scenarioId, checkId), cancellationToken);

    public Task<KeyFactDto> AddFactAsync(Guid scenarioId, KeyFactInput input, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, ScenariosRoutes.Facts(scenarioId), input, Json.KeyFactInput, Json.KeyFactDto, cancellationToken);

    public Task<KeyFactDto> UpdateFactAsync(Guid scenarioId, Guid factId, KeyFactInput input, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, ScenariosRoutes.Fact(scenarioId, factId), input, Json.KeyFactInput, Json.KeyFactDto, cancellationToken);

    public Task DeleteFactAsync(Guid scenarioId, Guid factId, CancellationToken cancellationToken = default) =>
        DeleteUrlAsync(ScenariosRoutes.Fact(scenarioId, factId), cancellationToken);

    public Task<HandoutDto> AddHandoutAsync(Guid scenarioId, HandoutInput input, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, ScenariosRoutes.Handouts(scenarioId), input, Json.HandoutInput, Json.HandoutDto, cancellationToken);

    public Task<HandoutDto> UpdateHandoutAsync(Guid scenarioId, Guid handoutId, HandoutInput input, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, ScenariosRoutes.Handout(scenarioId, handoutId), input, Json.HandoutInput, Json.HandoutDto, cancellationToken);

    public Task DeleteHandoutAsync(Guid scenarioId, Guid handoutId, CancellationToken cancellationToken = default) =>
        DeleteUrlAsync(ScenariosRoutes.Handout(scenarioId, handoutId), cancellationToken);

    public Task<ScenarioCreatureDto> AddCreatureAsync(Guid scenarioId, ScenarioCreatureInput input, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, ScenariosRoutes.Creatures(scenarioId), input, Json.ScenarioCreatureInput, Json.ScenarioCreatureDto, cancellationToken);

    public Task<ScenarioCreatureDto> UpdateCreatureAsync(Guid scenarioId, Guid rowId, ScenarioCreatureInput input, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, ScenariosRoutes.Creature(scenarioId, rowId), input, Json.ScenarioCreatureInput, Json.ScenarioCreatureDto, cancellationToken);

    public Task DeleteCreatureAsync(Guid scenarioId, Guid rowId, CancellationToken cancellationToken = default) =>
        DeleteUrlAsync(ScenariosRoutes.Creature(scenarioId, rowId), cancellationToken);

    public Task<ScenarioItemDto> AddItemAsync(Guid scenarioId, ScenarioItemInput input, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, ScenariosRoutes.Items(scenarioId), input, Json.ScenarioItemInput, Json.ScenarioItemDto, cancellationToken);

    public Task<ScenarioItemDto> UpdateItemAsync(Guid scenarioId, Guid rowId, ScenarioItemInput input, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, ScenariosRoutes.Item(scenarioId, rowId), input, Json.ScenarioItemInput, Json.ScenarioItemDto, cancellationToken);

    public Task DeleteItemAsync(Guid scenarioId, Guid rowId, CancellationToken cancellationToken = default) =>
        DeleteUrlAsync(ScenariosRoutes.Item(scenarioId, rowId), cancellationToken);

    public Task CastNpcAsync(Guid scenarioId, Guid characterId, NpcCastInput input, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, ScenariosRoutes.Npc(scenarioId, characterId), input, Json.NpcCastInput, cancellationToken);

    public Task RemoveNpcAsync(Guid scenarioId, Guid characterId, CancellationToken cancellationToken = default) =>
        DeleteUrlAsync(ScenariosRoutes.Npc(scenarioId, characterId), cancellationToken);

    public Task<CharacterCreatedDto> AddPregenAsync(Guid scenarioId, Guid pregenId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, ScenariosRoutes.Pregens(scenarioId), new AddPregenRequest(pregenId), Json.AddPregenRequest, Json.CharacterCreatedDto,
            cancellationToken);

    public Task RemovePregenAsync(Guid scenarioId, Guid characterId, CancellationToken cancellationToken = default) =>
        DeleteUrlAsync(ScenariosRoutes.Pregen(scenarioId, characterId), cancellationToken);

    private async Task<TResult> SendAsync<TBody, TResult>(HttpMethod method, string url, TBody body, JsonTypeInfo<TBody> bodyType,
        JsonTypeInfo<TResult> resultType, CancellationToken cancellationToken, uint? version = null)
    {
        using var request = Request(method, url, body, bodyType, version);
        using var response = await http.SendAsync(request, cancellationToken);
        return await ApiResponses.ReadAsync(response, resultType, cancellationToken, withCode: true);
    }

    private async Task SendAsync<TBody>(HttpMethod method, string url, TBody body, JsonTypeInfo<TBody> bodyType, CancellationToken cancellationToken)
    {
        using var request = Request(method, url, body, bodyType, null);
        using var response = await http.SendAsync(request, cancellationToken);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken, withCode: true);
    }

    private async Task DeleteUrlAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(url, cancellationToken);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken, withCode: true);
    }

    private static HttpRequestMessage Request<TBody>(HttpMethod method, string url, TBody body, JsonTypeInfo<TBody> bodyType, uint? version)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body, bodyType) };
        if (version is { } v)
        {
            request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{v.ToString(CultureInfo.InvariantCulture)}\""));
        }

        return request;
    }
}
