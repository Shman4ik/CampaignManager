using System.Net.Http.Json;
using CampaignManager.ApiClient.Catalogs;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Music;
using CampaignManager.Core.Music;

namespace CampaignManager.ApiClient.Music;

/// <summary>Треки фонотеки — общий клиент справочника.</summary>
public sealed class MusicTracksApiClient(HttpClient http)
    : CatalogApiClient<MusicTrackDto>(http, MusicRoutes.Tracks, ContractsJsonContext.Default.MusicTrackDto, ContractsJsonContext.Default.CatalogListMusicTrackDto);

public sealed class MusicApiClient(HttpClient http) : IMusicApi
{
    private static ContractsJsonContext Json => ContractsJsonContext.Default;

    public async Task<PinnedTagsDto> GetPinnedTagsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(MusicRoutes.PinnedTags, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.PinnedTagsDto, cancellationToken, withCode: true);
    }

    public async Task<PinnedTagsDto> SetPinnedTagsAsync(IReadOnlyList<string> tags, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync(MusicRoutes.PinnedTags, new PinnedTagsRequest(tags), Json.PinnedTagsRequest, cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.PinnedTagsDto, cancellationToken, withCode: true);
    }

    public async Task<MusicPoolDto> GetPoolAsync(MusicPool pool, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(MusicRoutes.PoolUrl(pool), cancellationToken);
        return await ApiResponses.ReadAsync(response, Json.MusicPoolDto, cancellationToken, withCode: true);
    }
}
