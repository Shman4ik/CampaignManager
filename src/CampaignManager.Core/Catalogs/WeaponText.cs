using System.Text.RegularExpressions;

namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Строки оружия — как их показывает справочник: одним написанием (правило 13). Данные хранятся как ввёл человек или
/// перенёс импорт («Нет», «Только 1», «СИЛ/5м», «1 метр»), и в одном столбце оказывались три способа написать
/// «ничего» и три — «сила на пять метров». Здесь они приводятся к словарю; <c>null</c> — показывать нечего (в таблице
/// «—», в карточке метка не рисуется). Хранимое не меняется: форма правки показывает то, что записано.
/// </summary>
public static partial class WeaponText
{
    [GeneratedRegex(@"(?<=\d)\s*метр(?:а|ов)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MetersWord();

    [GeneratedRegex(@"(?<=\d)м\b", RegexOptions.CultureInvariant)]
    private static partial Regex GluedMeters();

    [GeneratedRegex(@"^сил\s*/\s*5\s*м$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StrengthRange();

    [GeneratedRegex(@"\d{1,3}(?:[   ]\d{3})+|\d{5,}", RegexOptions.CultureInvariant)]
    private static partial Regex Number();

    /// <summary>Пусто, «Нет», «—» — значения нет.</summary>
    private static string? Clean(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) || text is "—" or "-" || text.Equals("нет", StringComparison.CurrentCultureIgnoreCase) ? null : text;
    }

    /// <summary>«15 м», «10/20/50 м», «СИЛ / 5 м», «Касание»: «метр» и «5м» — «м», сила — одним написанием.</summary>
    public static string? Range(string? value)
    {
        if (Clean(value) is not { } text)
        {
            return null;
        }

        text = GluedMeters().Replace(MetersWord().Replace(text, " м"), " м");
        return StrengthRange().IsMatch(text) ? "СИЛ / 5 м" : text;
    }

    /// <summary>Урон: те же «метры», что в дальности («2d10 / 1 метр» → «2d10 / 1 м»); кости форматирует <c>DiceText</c>.</summary>
    public static string? Damage(string? value) =>
        Clean(value) is { } text ? MetersWord().Replace(text, " м") : null;

    public static string? Attacks(string? value) => Clean(value);

    /// <summary>«Только 1», «Одноразовая», «Однораз.» — «1 (одноразовое)»; остальное как есть («20/30/32», «25 доз»).</summary>
    public static string? Ammo(string? value)
    {
        if (Clean(value) is not { } text)
        {
            return null;
        }

        return text.StartsWith("Однораз", StringComparison.CurrentCultureIgnoreCase)
            || text.Equals("Только 1", StringComparison.CurrentCultureIgnoreCase)
            ? "1 (одноразовое)"
            : text;
    }

    /// <summary>«$1000/20 000»: тысячи — узким неразрывным пробелом с пяти знаков, четыре знака — слитно (как <see cref="PriceText"/>).</summary>
    public static string? Cost(string? value) =>
        Clean(value) is { } text
            ? Number().Replace(text, m => PriceText.GroupThousands(new string([.. m.Value.Where(char.IsAsciiDigit)])))
            : null;
}
