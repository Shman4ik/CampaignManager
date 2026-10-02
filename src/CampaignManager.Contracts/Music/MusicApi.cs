using CampaignManager.Contracts.Catalogs;
using CampaignManager.Core.Music;

namespace CampaignManager.Contracts.Music;

public static class MusicRoutes
{
    /// <summary>Треки — справочник: <c>/api/v1/catalogs/music</c>, все операции <see cref="ICatalogApi{T}"/>.</summary>
    public static readonly CatalogRoute Tracks = new("music");

    /// <summary>
    /// <c>GET</c> — мои закреплённые настроения (<see cref="PinnedTagsDto"/>); <c>PUT</c> <see cref="PinnedTagsRequest"/>.
    /// Хранятся настройкой <see cref="Profile.PreferenceKeys.MusicPinnedTags"/> (та же строка, что у общего API
    /// настроек профиля); здесь — типизированно, с нормализацией тегов и умолчаниями.
    /// </summary>
    public const string PinnedTags = ApiRoutes.Prefix + "/music/pinned-tags";

    /// <summary><c>GET ?tag=…&amp;track=…</c> — треки пула сцены (<see cref="MusicPoolDto"/>).</summary>
    public const string Pool = ApiRoutes.Prefix + "/music/pool";

    public const string TagQuery = "tag";

    public const string TrackQuery = "track";

    public static string PoolUrl(MusicPool pool) =>
        Pool + "?" + string.Join('&',
            pool.Tags.Select(t => $"{TagQuery}={Uri.EscapeDataString(t)}")
                .Concat(pool.TrackIds.Select(id => $"{TrackQuery}={id}")));
}

/// <summary>
/// Фонотека сверх справочника: закреплённые настроения Хранителя и пул сцены. Треки — <see cref="ICatalogApi{T}"/>
/// с <see cref="MusicTrackDto"/>: читать — любой вошедший, писать — Хранитель.
/// </summary>
public interface IMusicApi
{
    Task<PinnedTagsDto> GetPinnedTagsAsync(CancellationToken cancellationToken = default);

    /// <summary>Записать свои настроения; пустой список — вернуть умолчания. Больше 30 или длиннее 40 знаков — 400.</summary>
    Task<PinnedTagsDto> SetPinnedTagsAsync(IReadOnlyList<string> tags, CancellationToken cancellationToken = default);

    /// <summary>
    /// Треки пула: с хоть одним из тегов или прибитые явно (музыка локации, кнопка настроения). Пустой пул —
    /// пустой ответ.
    /// </summary>
    Task<MusicPoolDto> GetPoolAsync(MusicPool pool, CancellationToken cancellationToken = default);
}
