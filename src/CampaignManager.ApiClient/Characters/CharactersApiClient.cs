using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Characters;
using CampaignManager.Core.Characters;

namespace CampaignManager.ApiClient.Characters;

/// <summary>
/// Клиент листа. Каждая запись несёт <c>If-Match</c> с версией, которую правили: лист правят с двух устройств
/// (Хранитель и игрок), и устаревшая запись получает 409 <c>stale</c>, а не затирает чужую правку.
/// Отказы — <see cref="Contracts.Platform.ApiException"/> с кодом.
/// </summary>
public sealed class CharactersApiClient(HttpClient http) : ICharactersApi
{
    private static ContractsJsonContext Json => ContractsJsonContext.Default;

    public async Task<CharacterCreatedDto> CreateAsync(CreateCharacterRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(CharactersRoutes.Characters, request, Json.CreateCharacterRequest, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.CharacterCreatedDto, cancellationToken, withCode: true);
    }

    public async Task<CreationContextDto> GetCreationContextAsync(CharacterKind kind, Guid? campaignId, Guid? scenarioId,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(CharactersRoutes.New(kind, campaignId, scenarioId), cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.CreationContextDto, cancellationToken, withCode: true);
    }

    public async Task<IReadOnlyList<CharacterSummaryDto>> ListAsync(CharacterKind kind, bool archived, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(CharactersRoutes.Library(kind, archived), cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.IReadOnlyListCharacterSummaryDto, cancellationToken, withCode: true);
    }

    public async Task<CharacterDto> GetAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(CharactersRoutes.Character(characterId), cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.CharacterDto, cancellationToken, withCode: true);
    }

    public Task<CharacterSavedDto> SaveSheetAsync(Guid characterId, CharacterSheet sheet, uint version, CancellationToken cancellationToken = default) =>
        PutAsync(CharactersRoutes.Sheet(characterId), sheet, Json.CharacterSheet, version, cancellationToken);

    public Task<CharacterSavedDto> SetPortraitAsync(Guid characterId, Guid? fileId, uint version, CancellationToken cancellationToken = default) =>
        PutAsync(CharactersRoutes.Portrait(characterId), new SetPortraitRequest(fileId), Json.SetPortraitRequest, version, cancellationToken);

    public Task<CharacterSavedDto> SetStatusAsync(Guid characterId, CharacterStatus status, uint version, CancellationToken cancellationToken = default) =>
        PutAsync(CharactersRoutes.Status(characterId), new SetStatusRequest(status), Json.SetStatusRequest, version, cancellationToken);

    public async Task<IReadOnlyList<PartyMemberDto>> GetPartyAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(CharactersRoutes.Party(characterId), cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.IReadOnlyListPartyMemberDto, cancellationToken, withCode: true);
    }

    public async Task<IReadOnlyList<InvestigatorDto>> GetCampaignInvestigatorsAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(CharactersRoutes.CampaignInvestigators(campaignId), cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.IReadOnlyListInvestigatorDto, cancellationToken, withCode: true);
    }

    private async Task<CharacterSavedDto> PutAsync<T>(string url, T body, JsonTypeInfo<T> type, uint version, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body, type) };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{version.ToString(CultureInfo.InvariantCulture)}\""));
        using var response = await http.SendAsync(request, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.CharacterSavedDto, cancellationToken, withCode: true);
    }
}
