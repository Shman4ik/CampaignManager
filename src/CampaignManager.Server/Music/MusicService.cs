using System.Text.Json;
using CampaignManager.Contracts.Music;
using CampaignManager.Contracts.Profile;
using CampaignManager.Core.Music;
using CampaignManager.Data;
using CampaignManager.Data.Music;
using CampaignManager.Server.Access;
using CampaignManager.Server.Catalogs;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Music;

/// <summary>
/// Фонотека сверх справочника: закреплённые настроения (своя настройка пользователя) и пул сцены.
/// Треки правит <see cref="CatalogService{TEntity, TDto}"/> с <see cref="MusicTrackStore"/>.
/// </summary>
public sealed class MusicService(
    CmDbContext db,
    AccessPolicy access,
    CurrentUser currentUser,
    CatalogStore<MusicTrack, MusicTrackDto> store)
{
    /// <summary>Свои настроения или умолчания. Настройка — только своя: id из сессии, чужой не принимается.</summary>
    public async Task<PinnedTagsDto> GetPinnedTagsAsync(CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(cancellationToken);
        var row = await db.UserPreferences.AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == user.Id && p.Key == PreferenceKeys.MusicPinnedTags, cancellationToken);
        var tags = row is null ? [] : Read(row.Value.RootElement);
        return tags.Count > 0 ? new PinnedTagsDto(tags, IsDefault: false) : Defaults;
    }

    /// <summary>
    /// Записать настроения одной строкой <c>user_preferences</c> (upsert одним SQL: другие ключи и другие
    /// устройства не затрагиваются). Пустой список — строка удаляется, и снова действуют умолчания.
    /// </summary>
    public async Task<PinnedTagsDto> SetPinnedTagsAsync(PinnedTagsRequest request, CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(cancellationToken);
        var tags = MusicTags.NormalizeKeepingOrder(request.Tags ?? []);
        if (tags.Count > MusicTags.MaxCount)
        {
            throw ApiProblemException.Invalid($"Настроений больше {MusicTags.MaxCount} — на панели плеера столько не поместится.");
        }

        if (tags.FirstOrDefault(t => t.Length > MusicTags.MaxLength) is { } longTag)
        {
            throw ApiProblemException.Invalid($"Настроение «{longTag}» длиннее {MusicTags.MaxLength} знаков.");
        }

        if (tags.Count == 0)
        {
            await db.UserPreferences
                .Where(p => p.UserId == user.Id && p.Key == PreferenceKeys.MusicPinnedTags)
                .ExecuteDeleteAsync(cancellationToken);
            return Defaults;
        }

        var json = JsonSerializer.Serialize(tags);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             insert into cm.user_preferences (user_id, key, value, updated_at)
             values ({user.Id}, {PreferenceKeys.MusicPinnedTags}, {json}::jsonb, now())
             on conflict (user_id, key) do update set value = excluded.value, updated_at = excluded.updated_at
             """,
            cancellationToken);
        return new PinnedTagsDto(tags, IsDefault: false);
    }

    /// <summary>Треки пула: правило <see cref="MusicPool"/> из Core над всей фонотекой (она в сотню строк).</summary>
    public async Task<MusicPoolDto> GetPoolAsync(MusicPool pool, CancellationToken cancellationToken)
    {
        await access.ForCatalogAsync(cancellationToken).Demand(Operation.Read);
        if (pool.IsEmpty)
        {
            return new MusicPoolDto([]);
        }

        var tracks = await db.MusicTracks.AsNoTracking().OrderBy(t => t.Name).ToListAsync(cancellationToken);
        var selected = pool.Select(tracks, t => t.Id, t => t.Tags);
        return new MusicPoolDto(await store.ToDtosAsync(db, selected, cancellationToken));
    }

    private static PinnedTagsDto Defaults => new(MusicDefaults.PinnedTags, IsDefault: true);

    /// <summary>JSON-массив строк (2.0) или строка через запятую (так настройку хранил v1, перенос положил её как есть).</summary>
    internal static List<string> Read(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Array => MusicTags.NormalizeKeepingOrder(
            value.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString())),
        JsonValueKind.String => MusicTags.Parse(value.GetString()),
        _ => [],
    };

    private async Task<SignedInUser> RequireUserAsync(CancellationToken cancellationToken) =>
        await currentUser.GetAsync(cancellationToken) ?? throw AccessDeniedException.Forbidden();
}
