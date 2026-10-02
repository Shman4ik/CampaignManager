namespace CampaignManager.Server.Files;

/// <summary>Секция <c>Files</c>.</summary>
public sealed class FilesOptions
{
    public const string Section = "Files";

    /// <summary>Предел одного файла — 50 МБ, как у загрузки трека в v1.</summary>
    public long MaxUploadBytes { get; set; } = 50L * 1024 * 1024;

    /// <summary>
    /// Моложе этого файл не сирота: загрузка и сохранение ссылки на файл — два запроса, и между
    /// ними файл честно ни на что не ссылается (форма с портретом ещё открыта).
    /// </summary>
    public TimeSpan OrphanGracePeriod { get; set; } = TimeSpan.FromDays(1);
}
