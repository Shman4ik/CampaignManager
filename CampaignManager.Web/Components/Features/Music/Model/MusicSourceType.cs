namespace CampaignManager.Web.Components.Features.Music.Model;

/// <summary>
///     Откуда плеер берёт звук. Хранится строкой (<c>HasConversion&lt;string&gt;</c>),
///     чтобы добавление источника не сдвигало номера уже записанных треков.
/// </summary>
public enum MusicSourceType
{
    /// <summary>Ролик YouTube, играем встроенным IFrame-плеером. <c>Source</c> — идентификатор ролика.</summary>
    YouTube,

    /// <summary>Файл в объектном хранилище. <c>Source</c> — имя объекта, например <c>music/0198....mp3</c>.</summary>
    Storage
}
