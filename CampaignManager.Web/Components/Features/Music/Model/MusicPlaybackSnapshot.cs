namespace CampaignManager.Web.Components.Features.Music.Model;

/// <summary>
///     Снимок плеера, который Blazor сохраняет при паузе circuit и возвращает при возобновлении
///     (см. корневой CLAUDE.md, «Circuit State Persistence»). Обязан сериализоваться в JSON —
///     никаких ссылок на сущности EF, только идентификаторы.
///     Добавляешь поле в состояние плеера — добавь и сюда, иначе при возобновлении оно исчезнет.
/// </summary>
public sealed class MusicPlaybackSnapshot
{
    public Guid? TrackId { get; set; }

    public List<string> PoolTags { get; set; } = [];

    public List<Guid> PoolTrackIds { get; set; } = [];

    public string? PoolLabel { get; set; }

    public List<Guid> History { get; set; } = [];

    public int Volume { get; set; } = MusicPlaybackDefaults.Volume;

    public bool Muted { get; set; }

    public bool IsPlaying { get; set; }
}

/// <summary>Значения по умолчанию, общие для сервиса и снимка.</summary>
public static class MusicPlaybackDefaults
{
    /// <summary>Громкость при первом запуске: фон под разговор за столом, а не концерт.</summary>
    public const int Volume = 60;

    /// <summary>
    ///     Ключ в настройках пользователя, где лежат закреплённые теги-настроения
    ///     (через запятую). Читается через <c>UserPreferencesService</c>.
    /// </summary>
    public const string PinnedTagsPreferenceKey = "music.pinnedTags";

    /// <summary>Набор настроений, который получает Хранитель, пока сам ничего не закрепил.</summary>
    public static readonly string[] DefaultPinnedTags =
        ["бой", "погоня", "напряжение", "расследование", "ужас", "спокойствие"];
}
