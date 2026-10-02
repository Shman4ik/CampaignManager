namespace CampaignManager.Server.Files;

/// <summary>
/// Что можно загрузить. Тип содержимого сервер берёт из этого списка по расширению, а не из
/// заголовка клиента: файл отдаётся с нашего origin, и подсунутый <c>text/html</c> исполнился бы
/// у нас. SVG поэтому тоже нет — это документ со скриптами, а не картинка.
/// </summary>
public static class FileTypes
{
    public sealed record FileType(string ContentType, string Folder);

    private const string Images = "images";
    private const string Music = "music";

    private static readonly Dictionary<string, FileType> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = new("image/jpeg", Images),
        [".jpeg"] = new("image/jpeg", Images),
        [".png"] = new("image/png", Images),
        [".gif"] = new("image/gif", Images),
        [".webp"] = new("image/webp", Images),
        [".avif"] = new("image/avif", Images),
        // Звук — набор v1 (MusicSource.GetAudioContentType).
        [".mp3"] = new("audio/mpeg", Music),
        [".ogg"] = new("audio/ogg", Music),
        [".opus"] = new("audio/ogg", Music),
        [".m4a"] = new("audio/mp4", Music),
        [".aac"] = new("audio/aac", Music),
        [".wav"] = new("audio/wav", Music),
        [".flac"] = new("audio/flac", Music),
    };

    public static string Supported => string.Join(", ", ByExtension.Keys.Order(StringComparer.Ordinal));

    public static FileType? Find(string fileName) =>
        ByExtension.GetValueOrDefault(Path.GetExtension(fileName));
}
