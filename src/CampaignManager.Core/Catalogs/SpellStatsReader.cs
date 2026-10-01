using System.Text.RegularExpressions;

namespace CampaignManager.Core.Catalogs;

/// <summary>
///     Читает числа из свободного текста заклинания: стоимость («8 пунктов магии, 1d6 пунктов
///     рассудка», «5 МОЩ», «3 пункта магии, 1d8 пунктов рассудка, 1 ПЗ за раунд») и время
///     сотворения («мгновенно», «5 раундов», «1d3 раунда», «1 час»).
///     <para>
///         Это <b>подсказка</b>, а не правда: в «Гримуаре» полно «варьирует», «за каждый
///         потраченный пункт» и «(Урон×2+1) магии за раунд». Боевая панель подставляет разобранное
///         в поля, а Хранитель подтверждает числа — разобрать всё и не ошибиться здесь нельзя,
///         поэтому неразобранное остаётся пустым, а не угадывается.
///     </para>
/// </summary>
public static partial class SpellStatsReader
{
    /// <summary>
    ///     Цена по видам. Значения — формулы в виде «8», «1d6», «1d4+3»: число подставляется
    ///     сразу, кости Хранитель бросает (или вписывает свои).
    /// </summary>
    public sealed record CostEstimate(string? MagicPoints, string? Sanity, string? Power, string? HitPoints)
    {
        public bool IsEmpty => MagicPoints is null && Sanity is null && Power is null && HitPoints is null;
    }

    /// <summary>
    ///     Время сотворения. <see cref="Rounds" /> — готовое число раундов (0 — мгновенно),
    ///     <see cref="RoundsFormula" /> — если раунды заданы костями. <see cref="MentionsLongerUnits" />
    ///     — в тексте есть минуты, часы, дни: в бою такое заклинание не творится или творится
    ///     с оговоркой, и Хранителю стоит перечитать описание.
    /// </summary>
    public sealed record CastingTimeEstimate(int? Rounds, string? RoundsFormula, bool IsInstant, bool MentionsLongerUnits);

    private const string Amount = @"(?<amt>\d+\s*d\s*\d+(?:\s*[+-]\s*\d+)?|\d+(?:\s*[+-]\s*\d+\s*d\s*\d+)?)";

    public static CostEstimate ParseCost(string? cost)
    {
        if (string.IsNullOrWhiteSpace(cost))
            return new CostEstimate(null, null, null, null);

        var text = Normalize(cost);
        return new CostEstimate(
            First(MagicPattern(), text),
            First(SanityPattern(), text),
            First(PowerPattern(), text),
            First(HitPointsPattern(), text));
    }

    public static CastingTimeEstimate ParseCastingTime(string? castingTime)
    {
        if (string.IsNullOrWhiteSpace(castingTime))
            return new CastingTimeEstimate(null, null, false, false);

        var text = Normalize(castingTime);
        var longer = LongerUnitsPattern().IsMatch(text);

        if (text.Contains("мгновен", StringComparison.Ordinal))
            return new CastingTimeEstimate(0, null, true, longer);

        // Кости раньше числа: в «1d6 + 4 раунда» простое число поймало бы четвёрку.
        if (RoundsFormulaPattern().Match(text) is { Success: true } formula)
            return new CastingTimeEstimate(null, Compact(formula.Groups["f"].Value), false, longer);

        if (RoundsPattern().Match(text) is { Success: true } rounds)
            return new CastingTimeEstimate(int.Parse(rounds.Groups["n"].Value), null, false, longer);

        return new CastingTimeEstimate(null, null, false, longer);
    }

    /// <summary>Формула без костей — просто число: его можно подставить без броска.</summary>
    public static int? FixedAmount(string? formula) =>
        int.TryParse(formula?.Trim(), out var value) && value >= 0 ? value : null;

    private static string? First(Regex pattern, string text) =>
        pattern.Match(text) is { Success: true } match ? Compact(match.Groups["amt"].Value) : null;

    private static string Compact(string value) => WhitespacePattern().Replace(value, "");

    /// <summary>Нижний регистр, «ё» → «е», кириллическая «д» между цифрами → латинская d, без «≥».</summary>
    private static string Normalize(string value)
    {
        var text = value.ToLowerInvariant().Replace('ё', 'е').Replace("≥", "");
        return CyrillicDicePattern().Replace(text, "${a}d${b}");
    }

    [GeneratedRegex(Amount + @"\s*(?:пункт\w*\s+)?(?:магии|пм|мп)\b")]
    private static partial Regex MagicPattern();

    [GeneratedRegex(Amount + @"\s*(?:пункт\w*\s+)?рассудка")]
    private static partial Regex SanityPattern();

    [GeneratedRegex(Amount + @"\s*(?:пункт\w*\s+)?мощ")]
    private static partial Regex PowerPattern();

    [GeneratedRegex(Amount + @"\s*(?:пз\b|пункт\w*\s+здоровья)")]
    private static partial Regex HitPointsPattern();

    [GeneratedRegex(@"(?<f>\d+\s*d\s*\d+(?:\s*\+\s*\d+)?)\)?\s*раунд")]
    private static partial Regex RoundsFormulaPattern();

    [GeneratedRegex(@"(?<n>\d+)\s*(?:или\s*\d+\s*)?раунд")]
    private static partial Regex RoundsPattern();

    [GeneratedRegex(@"минут|час|дн[яеийь]|день|недел|месяц|год|лет")]
    private static partial Regex LongerUnitsPattern();

    [GeneratedRegex(@"(?<a>\d)\s*д\s*(?<b>\d)")]
    private static partial Regex CyrillicDicePattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
