using System.Globalization;
using System.Text.RegularExpressions;
using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Разбор колонки «Урон» таблицы XVII (стр. 399–402) в <see cref="DamageInfo"/>:
/// <list type="bullet">
///   <item>кости с модификаторами: 1d6, 2d6+4, 1d10+1d4+2;</item>
///   <item>бонус к урону: 1d4+БкУ, 1d6+БП, 1d3+1/2 БкУ;</item>
///   <item>дробовики по дальностям: 4d6/2d6/1d6;</item>
///   <item>взрывчатка с радиусом: 4d10 / 3 метра;</item>
///   <item>эффекты при костях: 2d6+горение. Эффект без костей («Шок») формулой не считается и
///   остаётся текстом (rules-findings F-P13).</item>
/// </list>
/// Всё, что понимает бросок (<see cref="DiceFormula"/>), понимает и этот разбор — в v1 голое «5» и
/// «1 d 6» бросок понимал, а разборщик нет (rules-findings F-P12).
/// </summary>
public static partial class DamageFormulaParser
{
    [GeneratedRegex(@"([+-]?\s*\d*)[dDдД](\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex DiceRegex();

    [GeneratedRegex(@"([+-]\s*\d+)(?![dDдД])", RegexOptions.IgnoreCase)]
    private static partial Regex FlatModRegex();

    /// <summary>Написания бонуса к урону из реальных данных; «урону» — целым словом, чтобы снятая отметка не оставляла хвост.</summary>
    [GeneratedRegex(@"(?<half>1/2\s*|½\s*)?\+?\s*(?<db>Бку|БкУ|БП|Б\.К\.У\.|бонус\s+к\s+урон\w*)", RegexOptions.IgnoreCase)]
    private static partial Regex DbMarkerRegex();

    [GeneratedRegex(@"\+\s*\+")]
    private static partial Regex DoublePlusRegex();

    [GeneratedRegex(@"(\d+)\s*метр", RegexOptions.IgnoreCase)]
    private static partial Regex BlastRadiusRegex();

    private static readonly string[] KnownEffects = ["горение", "шок", "burning", "shock"];

    private static readonly string[] RangeLabels = ["Близкая", "Средняя", "Дальняя", "Очень дальняя"];

    /// <summary>Всегда возвращает объект; <see cref="DamageInfo.IsParsed"/> говорит, удалось ли.</summary>
    public static DamageInfo Parse(string? raw)
    {
        var result = new DamageInfo { RawText = raw ?? string.Empty };

        if (string.IsNullOrWhiteSpace(raw))
        {
            result.IsParsed = true; // пустой урон — валидно
            return result;
        }

        var trimmed = raw.Trim();

        if (TryParseExplosive(trimmed, result) || TryParseShotgun(trimmed, result))
            return result;

        if (TryParseFormula(trimmed, out var expr))
        {
            expr.RawText = raw;
            expr.IsParsed = true;
            result.Primary = expr;
            result.IsParsed = true;
        }

        return result;
    }

    /// <summary>Текст урона без отметки бонуса к урону: «1d3+БкУ» → «1d3», «1d6 + ½ БкУ» → «1d6»; без отметки — как есть.</summary>
    public static string WithoutDamageBonus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var marker = DbMarkerRegex().Match(raw);
        if (!marker.Success)
            return raw.Trim();

        var rest = DoublePlusRegex().Replace(raw.Remove(marker.Index, marker.Length), "+");
        return rest.Trim().Trim('+').Trim();
    }

    /// <summary>«4d10 / 3 метра»: справа от «/» — метры.</summary>
    private static bool TryParseExplosive(string input, DamageInfo result)
    {
        var slash = input.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0) return false;

        var radius = BlastRadiusRegex().Match(input[(slash + 1)..].Trim());
        if (!radius.Success) return false;

        var left = input[..slash].Trim();
        if (!TryParseFormula(left, out var primary)) return false;

        primary.RawText = left;
        primary.IsParsed = true;
        result.Primary = primary;
        result.BlastRadiusMeters = int.Parse(radius.Groups[1].Value, CultureInfo.InvariantCulture);
        result.IsParsed = true;
        return true;
    }

    /// <summary>«4d6/2d6/1d6» или «2d6 + 2 / 1d6+ 1 / 1d4»: в каждой части есть кости.</summary>
    private static bool TryParseShotgun(string input, DamageInfo result)
    {
        var parts = input.Split('/');
        if (parts.Length < 2 || !parts.All(p => DiceRegex().IsMatch(p))) return false;

        List<RangeDamageEntry> entries = [];
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            if (!TryParseFormula(part, out var expr)) return false;

            expr.RawText = part;
            expr.IsParsed = true;
            entries.Add(new RangeDamageEntry
            {
                RangeLabel = i < RangeLabels.Length ? RangeLabels[i] : $"Дальность {i + 1}",
                Damage = expr,
            });
        }

        result.RangeDamages = entries;
        result.IsParsed = true;
        return true;
    }

    /// <summary>Кости, плоские прибавки, бонус к урону и эффекты.</summary>
    private static bool TryParseFormula(string input, out DamageExpression expression)
    {
        expression = new DamageExpression();
        if (string.IsNullOrWhiteSpace(input)) return false;

        var working = input;
        var hasAnyComponent = false;

        var db = DbMarkerRegex().Match(working);
        if (db.Success)
        {
            expression.DamageBonus = string.IsNullOrWhiteSpace(db.Groups["half"].Value)
                ? DamageBonusType.Full
                : DamageBonusType.Half;
            working = working.Remove(db.Index, db.Length);
            hasAnyComponent = true;
        }

        foreach (var effect in KnownEffects)
        {
            var idx = working.IndexOf(effect, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;

            var start = idx;
            while (start > 0 && working[start - 1] is '+' or ' ')
                start--;
            expression.Effects.Add(effect.ToLowerInvariant());
            working = working.Remove(start, idx - start + effect.Length);
        }

        working = working.Trim().TrimEnd('+', '-', ' ').Trim();

        foreach (Match m in DiceRegex().Matches(working))
        {
            if (!int.TryParse(m.Groups[2].Value, CultureInfo.InvariantCulture, out var sides)) continue;

            var countText = m.Groups[1].Value.Replace(" ", "", StringComparison.Ordinal);
            var negative = countText.StartsWith('-');
            countText = countText.TrimStart('+', '-');
            var count = countText.Length == 0 ? 1 : int.Parse(countText, CultureInfo.InvariantCulture);

            expression.Dice.Add(new DiceTerm(count, sides, negative));
            hasAnyComponent = true;
        }

        var flat = 0;
        foreach (Match m in FlatModRegex().Matches(DiceRegex().Replace(working, string.Empty)))
        {
            if (!int.TryParse(m.Groups[1].Value.Replace(" ", "", StringComparison.Ordinal),
                    NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)) continue;
            flat += value;
            hasAnyComponent = true;
        }

        expression.FlatModifier = flat;

        if (hasAnyComponent)
            return true;

        // Эффект без костей («Шок») — не формула урона: строка остаётся текстом (F-P13).
        if (expression.Effects.Count > 0)
            return false;

        // Голое число и кости с пробелами внутри («1 d 6») — как у броска (F-P12).
        var formula = DiceFormula.Parse(working);
        if (!formula.IsValid || working.Length == 0)
            return false;

        expression.Dice.AddRange(formula.Dice);
        expression.FlatModifier = formula.Constant;
        return true;
    }
}
