using System.Text.Json.Serialization;
using CampaignManager.Contracts.Catalogs;

namespace CampaignManager.Contracts.Music;

/// <summary>
/// Трек фонотеки. Фонотека — справочник (<see cref="ICatalogApi{T}"/> на <see cref="MusicRoutes.Tracks"/>):
/// тот же класс — ответ, тело записи и строка файла обмена. Источник — ровно одно из двух: ролик YouTube
/// (<see cref="YoutubeId"/>) или загруженный звуковой файл (<see cref="FileId"/>). Кода книги и страницы-
/// источника у трека нет: <see cref="CatalogItemDto.Code"/> и <see cref="CatalogItemDto.Source"/> всегда пусты.
/// </summary>
public sealed class MusicTrackDto : CatalogItemDto
{
    /// <summary>
    /// Идентификатор ролика. При записи можно прислать ссылку целиком (watch, youtu.be, shorts…) — сервер
    /// достанет идентификатор сам; в ответе всегда голый идентификатор.
    /// </summary>
    public string? YoutubeId { get; set; }

    /// <summary>Звуковой файл из <c>cm.files</c> (загрузка — <c>IFilesApi</c>).</summary>
    public Guid? FileId { get; set; }

    /// <summary>Адрес содержимого — наш origin, с Range (Web Audio на iPad). Только в ответе.</summary>
    public string? FileUrl { get; set; }

    /// <summary>Имя загруженного файла — подсказка в форме. Только в ответе.</summary>
    public string? FileName { get; set; }

    /// <summary>С какой секунды начинать.</summary>
    public int StartSeconds { get; set; }

    /// <summary>Крутить по кругу; одноразовый трек по окончании уводит сцену на следующий из пула.</summary>
    public bool Loop { get; set; } = true;

    /// <summary>Поправка громкости трека, 0–100. На iPad действует только на файлы: громкость YouTube iOS не даёт менять.</summary>
    public int Volume { get; set; } = 100;

    /// <summary>Настроения («бой», «погоня»): нижний регистр, «ё» → «е» (<c>Core/Music/MusicTags</c>).</summary>
    public List<string> Tags { get; set; } = [];

    public string? Notes { get; set; }

    [JsonIgnore]
    public bool IsYouTube => !string.IsNullOrEmpty(YoutubeId);
}

/// <param name="Tags">Закреплённые настроения в порядке Хранителя.</param>
/// <param name="IsDefault">Своих Хранитель не закреплял — это умолчания (<c>MusicDefaults.PinnedTags</c>).</param>
public sealed record PinnedTagsDto(IReadOnlyList<string> Tags, bool IsDefault);

/// <param name="Tags">Новый список; пустой — вернуть умолчания.</param>
public sealed record PinnedTagsRequest(IReadOnlyList<string> Tags);

/// <summary>Треки пула сцены — правило <c>Core/Music/MusicPool</c>.</summary>
public sealed record MusicPoolDto(IReadOnlyList<MusicTrackDto> Tracks);
