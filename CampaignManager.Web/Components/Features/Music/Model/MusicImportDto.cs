namespace CampaignManager.Web.Components.Features.Music.Model;

/// <summary>
///     Обменный формат фонотеки. Намеренно не повторяет <see cref="MusicTrack" />:
///     идентификаторов в нём нет, а источник задаётся <b>ссылкой целиком</b> — тот, кто готовит
///     файл, знает адрес ролика, а не одиннадцатисимвольный идентификатор внутри него.
/// </summary>
public sealed class MusicLibraryDto
{
    public List<MusicTrackDto> Tracks { get; set; } = [];
}

/// <summary>Один трек в обменном файле. Всё, кроме <c>name</c> и <c>source</c>, необязательно.</summary>
public sealed class MusicTrackDto
{
    public string? Name { get; set; }

    /// <summary>
    ///     Ссылка на ролик YouTube в любом виде либо имя объекта в хранилище
    ///     (<c>music/...</c>). Тип источника определяется по содержимому, если не задан явно.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>Необязательно: <c>youtube</c> или <c>storage</c>. Обычно выводится из <see cref="Source" />.</summary>
    public string? SourceType { get; set; }

    public List<string>? Tags { get; set; }

    public string? Notes { get; set; }

    public int? StartSeconds { get; set; }

    public bool? Loop { get; set; }

    public int? Volume { get; set; }
}

/// <summary>Отчёт об импорте: сколько завелось, что пропущено и почему.</summary>
public sealed class MusicImportResult
{
    public bool Success { get; set; }

    public string? Error { get; set; }

    public int Created { get; set; }

    public int Skipped { get; set; }

    public List<string> Warnings { get; set; } = [];
}
