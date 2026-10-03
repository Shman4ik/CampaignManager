namespace CampaignManager.UI.Music;

/// <summary>Тексты фонотеки для экрана.</summary>
public static class MusicText
{
    /// <summary>
    /// Имя файла для человека: файл, загруженный без исходного имени, хранится под идентификатором
    /// («01a0a90c-….mp3») — такое имя Хранителю ничего не говорит, его не показываем.
    /// </summary>
    public static string? FileLabel(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        return Guid.TryParse(Path.GetFileNameWithoutExtension(fileName), out _) ? null : fileName;
    }
}
