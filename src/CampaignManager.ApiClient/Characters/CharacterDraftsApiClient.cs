using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Characters;
using CampaignManager.Core.Characters;

namespace CampaignManager.ApiClient.Characters;

/// <summary>
/// Черновик помощника на сервере. Запись — с <c>If-Match</c> (черновик продолжают с другого устройства), первая — без неё.
/// Отказы — <see cref="Contracts.Platform.ApiException"/> с кодом.
/// </summary>
public sealed class CharacterDraftsApiClient(HttpClient http) : ICharacterDraftsApi
{
    private static ContractsJsonContext Json => ContractsJsonContext.Default;

    public async Task<CharacterDraftDto?> GetMineAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(CharacterDraftsRoutes.Mine(campaignId), cancellationToken);
        // Черновика нет — обычное состояние помощника, а не ошибка.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ApiResponses.ReadAsync(response, Json.CharacterDraftDto, cancellationToken, withCode: true);
    }

    public async Task<CharacterDraftDto> GetAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(CharacterDraftsRoutes.Player(campaignId, userId), cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.CharacterDraftDto, cancellationToken, withCode: true);
    }

    public async Task<CharacterSavedDto> SaveAsync(Guid campaignId, InvestigatorDraft draft, uint? version, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, CharacterDraftsRoutes.Mine(campaignId))
        {
            Content = JsonContent.Create(draft, Json.InvestigatorDraft),
        };
        if (version is { } known)
        {
            request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{known.ToString(CultureInfo.InvariantCulture)}\""));
        }

        using var response = await http.SendAsync(request, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.CharacterSavedDto, cancellationToken, withCode: true);
    }

    public async Task DeleteAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync(CharacterDraftsRoutes.Mine(campaignId), cancellationToken);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken, withCode: true);
    }
}
