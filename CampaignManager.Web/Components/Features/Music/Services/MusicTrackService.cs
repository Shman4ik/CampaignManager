using CampaignManager.Web.Components.Features.Music.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Components.Features.Music.Services;

/// <summary>
///     Фонотека Хранителя — каталожный сервис в том же виде, что Items/Spells/Books:
///     весь CRUD делегирован в <see cref="CrudServiceHelper" />, чтение кэшируется на 15 минут.
/// </summary>
public sealed class MusicTrackService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    ILogger<MusicTrackService> logger)
{
    private const string TracksCacheKey = "AllMusicTracks";

    /// <summary>
    ///     Состав фонотеки изменился. Нужно панели плеера: она живёт в лэйауте и свой
    ///     <c>OnInitializedAsync</c> отрабатывает один раз за circuit, поэтому первый
    ///     заведённый трек иначе не показывался бы до перезагрузки страницы.
    /// </summary>
    public event Action? OnLibraryChanged;

    public Task<List<MusicTrack>> GetAllTracksAsync() =>
        CrudServiceHelper.GetAllCachedAsync<MusicTrack>(dbContextFactory, cache, TracksCacheKey, logger);

    public Task<MusicTrack?> GetTrackByIdAsync(Guid id) =>
        CrudServiceHelper.GetByIdAsync<MusicTrack>(dbContextFactory, id, logger);

    public async Task<MusicTrack?> CreateTrackAsync(MusicTrack track)
    {
        track.Tags = MusicSource.NormalizeTags(track.Tags);
        var created = await CrudServiceHelper.CreateAsync(dbContextFactory, cache, TracksCacheKey, track, logger);
        if (created is not null) OnLibraryChanged?.Invoke();
        return created;
    }

    public async Task<bool> UpdateTrackAsync(MusicTrack track)
    {
        track.Tags = MusicSource.NormalizeTags(track.Tags);
        var updated = await CrudServiceHelper.UpdateAsync(dbContextFactory, cache, TracksCacheKey, track, logger);
        if (updated) OnLibraryChanged?.Invoke();
        return updated;
    }

    public async Task<bool> DeleteTrackAsync(Guid id)
    {
        var deleted = await CrudServiceHelper.DeleteAsync<MusicTrack>(dbContextFactory, cache, TracksCacheKey, id, logger);
        if (deleted) OnLibraryChanged?.Invoke();
        return deleted;
    }

    /// <summary>
    ///     Все теги фонотеки, по одному разу и по алфавиту — для автодополнения и чипов фильтра.
    ///     Считается по кэшированному списку: отдельный запрос в базу ради jsonb-колонки не нужен.
    /// </summary>
    public async Task<List<string>> GetAllTagsAsync()
    {
        var tracks = await GetAllTracksAsync();
        return
        [
            .. tracks
                .SelectMany(t => t.Tags)
                .Distinct()
                .OrderBy(t => t, StringComparer.CurrentCulture)
        ];
    }

    /// <summary>
    ///     Пул треков сцены: всё, у чего есть хоть один из запрошенных тегов, плюс треки,
    ///     прибитые к сцене гвоздями. Порядок — как в библиотеке, перемешивает уже плеер.
    /// </summary>
    public async Task<List<MusicTrack>> ResolvePoolAsync(
        IEnumerable<string>? tags,
        IEnumerable<Guid>? trackIds)
    {
        var wanted = MusicSource.NormalizeTags(tags);
        var pinned = (trackIds ?? []).ToHashSet();

        if (wanted.Count == 0 && pinned.Count == 0) return [];

        var tracks = await GetAllTracksAsync();
        return
        [
            .. tracks.Where(t => pinned.Contains(t.Id) || t.Tags.Any(wanted.Contains))
        ];
    }
}
