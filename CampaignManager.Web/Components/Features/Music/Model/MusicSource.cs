using System.Text.RegularExpressions;

namespace CampaignManager.Web.Components.Features.Music.Model;

/// <summary>
///     Разбор ссылок и нормализация тегов — всё, что нужно применить к вводу Хранителя
///     до того, как трек ляжет в базу.
/// </summary>
public static partial class MusicSource
{
    /// <summary>Расширения, которые принимаем на загрузку и умеем отдать браузеру.</summary>
    public static readonly string[] AudioExtensions = [".mp3", ".ogg", ".m4a", ".wav", ".opus", ".flac", ".aac"];

    /// <summary>
    ///     Достаёт идентификатор ролика из любой формы ссылки YouTube — watch, youtu.be, embed,
    ///     shorts, live, music.youtube.com — или принимает голый идентификатор.
    /// </summary>
    public static bool TryParseYouTubeId(string? input, out string videoId)
    {
        videoId = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var text = input.Trim();

        // Голый идентификатор: ровно 11 символов из алфавита YouTube.
        if (BareVideoIdRegex().IsMatch(text))
        {
            videoId = text;
            return true;
        }

        var match = YouTubeUrlRegex().Match(text);
        if (!match.Success) return false;

        videoId = match.Groups["id"].Value;
        return true;
    }

    /// <summary>
    ///     Приводит тег к каноническому виду: без пробелов по краям, в нижнем регистре,
    ///     «ё» сведена к «е». Иначе «Бой», «бой » и «бой» разошлись бы по трём пулам.
    /// </summary>
    public static string NormalizeTag(string? tag) =>
        (tag ?? string.Empty).Trim().ToLowerInvariant().Replace('ё', 'е');

    /// <summary>
    ///     Нормализует список тегов: чистит, выбрасывает пустые и повторы, сортирует.
    /// </summary>
    public static List<string> NormalizeTags(IEnumerable<string>? tags) =>
    [
        .. (tags ?? [])
            .Select(NormalizeTag)
            .Where(t => t.Length > 0)
            .Distinct()
            .OrderBy(t => t, StringComparer.Ordinal)
    ];

    /// <summary>Похоже ли имя файла на звук, который мы готовы принять.</summary>
    public static bool IsSupportedAudioFile(string fileName) =>
        AudioExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

    /// <summary>MIME-тип по расширению — для отдачи объекта из хранилища.</summary>
    public static string GetAudioContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".ogg" => "audio/ogg",
            ".opus" => "audio/ogg",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            ".wav" => "audio/wav",
            ".flac" => "audio/flac",
            _ => "application/octet-stream"
        };

    [GeneratedRegex(@"^[A-Za-z0-9_-]{11}$")]
    private static partial Regex BareVideoIdRegex();

    [GeneratedRegex(
        @"(?:youtu\.be/|youtube\.com/(?:watch\?(?:.*&)?v=|embed/|shorts/|live/|v/))(?<id>[A-Za-z0-9_-]{11})",
        RegexOptions.IgnoreCase)]
    private static partial Regex YouTubeUrlRegex();
}
