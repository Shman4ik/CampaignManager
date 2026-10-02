namespace CampaignManager.Core.Music;

/// <summary>
/// Теги фонотеки — настроения («бой», «погоня»). Один вид на всё приложение: без пробелов по краям,
/// в нижнем регистре, «ё» сведена к «е». Иначе «Бой», «бой » и «бой» разошлись бы по трём пулам (v1,
/// <c>MusicSource.NormalizeTag</c>).
/// </summary>
public static class MusicTags
{
    /// <summary>Длиннее тег не нужен: это одно-два слова настроения.</summary>
    public const int MaxLength = 40;

    /// <summary>Столько тегов у трека или закреплённых настроений — с запасом на любую фонотеку.</summary>
    public const int MaxCount = 30;

    public static string Normalize(string? tag) =>
        string.Join(' ', (tag ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant()
            .Replace('ё', 'е');

    /// <summary>Чистит список: нормализует, выбрасывает пустые и повторы, сортирует.</summary>
    public static List<string> Normalize(IEnumerable<string?>? tags) =>
    [
        .. (tags ?? [])
            .Select(Normalize)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>
    /// Список, сохраняя порядок (закреплённые настроения Хранитель расставляет сам): нормализует и
    /// выбрасывает пустые и повторы, но не сортирует.
    /// </summary>
    public static List<string> NormalizeKeepingOrder(IEnumerable<string?>? tags) =>
    [
        .. (tags ?? [])
            .Select(Normalize)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.Ordinal),
    ];

    /// <summary>Строка через запятую (поле ввода, настройка v1) → теги.</summary>
    public static List<string> Parse(string? text) =>
        NormalizeKeepingOrder((text ?? "").Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries));
}
