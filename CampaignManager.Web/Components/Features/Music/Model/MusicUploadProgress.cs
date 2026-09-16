namespace CampaignManager.Web.Components.Features.Music.Model;

/// <summary>
///     Ход пакетной загрузки файлов в фонотеку. Сорок треков по пятьдесят мегабайт идут минутами,
///     и без этого модалка молчала бы всё это время.
/// </summary>
/// <param name="Done">Сколько файлов уже разобрано — и заведённых, и пропущенных.</param>
/// <param name="Total">Сколько всего выбрано.</param>
/// <param name="FileName">Файл, который обрабатывается прямо сейчас.</param>
public readonly record struct MusicUploadProgress(int Done, int Total, string FileName);
