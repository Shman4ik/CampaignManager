using System.Text.RegularExpressions;

namespace CampaignManager.Core.Music;

/// <summary>Ролик YouTube: идентификатор из любой формы ссылки (v1, <c>MusicSource.TryParseYouTubeId</c>).</summary>
public static partial class YouTubeVideo
{
    /// <summary>
    /// Достаёт идентификатор из ссылки watch, youtu.be, embed, shorts, live, music.youtube.com — или
    /// принимает голый идентификатор (11 знаков алфавита YouTube).
    /// </summary>
    public static bool TryParseId(string? input, out string videoId)
    {
        videoId = "";
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var text = input.Trim();
        if (BareId().IsMatch(text))
        {
            videoId = text;
            return true;
        }

        var match = Link().Match(text);
        if (!match.Success)
        {
            return false;
        }

        videoId = match.Groups["id"].Value;
        return true;
    }

    /// <summary>Ссылка на ролик для человека (открыть на YouTube).</summary>
    public static string WatchUrl(string videoId) => $"https://www.youtube.com/watch?v={Uri.EscapeDataString(videoId)}";

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex BareId();

    [GeneratedRegex(@"(?:youtu\.be/|youtube\.com/(?:watch\?(?:.*&)?v=|embed/|shorts/|live/|v/))(?<id>[A-Za-z0-9_-]{11})",
        RegexOptions.IgnoreCase)]
    private static partial Regex Link();
}
