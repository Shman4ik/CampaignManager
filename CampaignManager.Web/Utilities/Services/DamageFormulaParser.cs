using System.Text.RegularExpressions;
using CampaignManager.Web.Components.Features.Weapons.Model;
using CampaignManager.Web.Model;

namespace CampaignManager.Web.Utilities.Services;

/// <summary>
/// Парсер текстовых формул урона в структурированную модель WeaponDamageInfo.
/// Поддерживает форматы из справочных таблиц CoC 7e:
/// <list type="bullet">
///   <item>Кубики с модификаторами: 1d6, 2d6+4, 1d10+1d4+2</item>
///   <item>Бонус к удару (Б.К.У.): 1d4+Бку, 1d6+БП, 1d3+1/2 БкУ</item>
///   <item>Дробовики (по дальности): 4d6/2d6/1d6</item>
///   <item>Взрывчатка (радиус): 4d10 / 3 метра</item>
///   <item>Спецэффекты: 2d6+горение, Шок</item>
/// </list>
/// </summary>
public static partial class DamageFormulaParser
{
    // ── Регулярные выражения ───────────────────────────────────────────

    /// <summary>Кость: необязательный знак, количество, 'd'/'D', грани.</summary>
    [GeneratedRegex(@"([+-]?\s*\d*)[dDдД](\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex DiceRegex();

    /// <summary>Плоский числовой модификатор (со знаком).</summary>
    [GeneratedRegex(@"([+-]\s*\d+)(?![dDдД])", RegexOptions.IgnoreCase)]
    private static partial Regex FlatModRegex();

    /// <summary>Маркеры Б.К.У. (разные написания из реальных данных).</summary>
    [GeneratedRegex(
        @"(?<half>1/2\s*|½\s*)?\+?\s*(?<db>Бку|БкУ|БП|БкУ|Б\.К\.У\.|бонус\s+к\s+урон)",
        RegexOptions.IgnoreCase)]
    private static partial Regex DbMarkerRegex();

    /// <summary>Метры в строке радиуса взрыва.</summary>
    [GeneratedRegex(@"(\d+)\s*метр", RegexOptions.IgnoreCase)]
    private static partial Regex BlastRadiusRegex();

    // Известные спецэффекты (текстовые слова в формуле урона)
    private static readonly string[] KnownEffects =
    [
        "горение", "шок", "burning", "shock"
    ];

    // ── Публичный API ─────────────────────────────────────────────────

    /// <summary>
    /// Парсит строку урона в <see cref="WeaponDamageInfo"/>.
    /// Всегда возвращает объект; поле IsParsed
    /// указывает, удался ли автоматический парсинг.
    /// </summary>
    public static WeaponDamageInfo Parse(string raw)
    {
        var result = new WeaponDamageInfo
        {
            RawText = raw
        };

        if (string.IsNullOrWhiteSpace(raw))
        {
            result.IsParsed = true; // Пустой урон — валидно
            return result;
        }

        var trimmed = raw.Trim();

        // ── 1. Взрывчатка: "4d10 / 3 метра" ──────────────────────────
        if (TryParseExplosive(trimmed, result))
            return result;

        // ── 2. Дробовик: "4d6/2d6/1d6" ───────────────────────────────
        if (TryParseShotgun(trimmed, result))
            return result;

        // ── 3. Стандартная формула ─────────────────────────────────────
        if (TryParseFormula(trimmed, out var expr) && expr is not null)
        {
            expr.RawText = raw;
            expr.IsParsed = true;
            result.Primary = expr;
            result.IsParsed = true;
            return result;
        }

        // ── 4. Чистый текст/эффект (например, "Шок") ──────────────────
        result.IsParsed = false;
        return result;
    }

    // ── Частные методы ────────────────────────────────────────────────

    /// <summary>
    /// Проверяет формат взрывчатки: "4d10 / 3 метра".
    /// Разделитель '/' с метрами на второй части.
    /// </summary>
    private static bool TryParseExplosive(string input, WeaponDamageInfo result)
    {
        var slashIdx = input.IndexOf('/');
        if (slashIdx < 0) return false;

        var rightPart = input[(slashIdx + 1)..].Trim();
        var radiusMatch = BlastRadiusRegex().Match(rightPart);
        if (!radiusMatch.Success) return false;

        var leftPart = input[..slashIdx].Trim();
        if (!TryParseFormula(leftPart, out var primary) || primary is null) return false;

        primary.RawText = leftPart;
        primary.IsParsed = true;
        result.Primary = primary;
        result.BlastRadiusMeters = int.Parse(radiusMatch.Groups[1].Value);
        result.IsParsed = true;
        return true;
    }

    /// <summary>
    /// Проверяет формат дробовика: "4d6/2d6/1d6" или "2d6 + 2 / 1d6+ 1 / 1d4".
    /// Все части должны содержать кубики (нет метров).
    /// </summary>
    private static bool TryParseShotgun(string input, WeaponDamageInfo result)
    {
        // Взрывчатка уже обработана, значит правая часть НЕ содержит "метр"
        var parts = input.Split('/');
        if (parts.Length < 2) return false;

        // Убедимся, что в каждой части есть кубики
        foreach (var part in parts)
            if (!DiceRegex().IsMatch(part)) return false;

        string[] rangeLabels = ["Близкая", "Средняя", "Дальняя", "Очень дальняя"];
        var entries = new List<RangeDamageEntry>();

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            if (!TryParseFormula(part, out var expr) || expr is null) return false;

            expr.RawText = part;
            expr.IsParsed = true;
            entries.Add(new RangeDamageEntry
            {
                RangeLabel = i < rangeLabels.Length ? rangeLabels[i] : $"Дальность {i + 1}",
                Damage = expr
            });
        }

        result.RangeDamages = entries;
        result.IsParsed = true;
        return true;
    }

    /// <summary>
    /// Парсит стандартную формулу урона: кубики + плоские модификаторы + Б.К.У. + эффекты.
    /// </summary>
    private static bool TryParseFormula(string input, out DamageExpression? expression)
    {
        expression = null;

        if (string.IsNullOrWhiteSpace(input)) return false;

        var working = input;
        var expr = new DamageExpression();
        bool hasAnyComponent = false;

        // ── Извлечь Б.К.У. ────────────────────────────────────────────
        var dbMatch = DbMarkerRegex().Match(working);
        if (dbMatch.Success)
        {
            var halfGroup = dbMatch.Groups["half"].Value;
            expr.DamageBonus = string.IsNullOrWhiteSpace(halfGroup)
                ? DamageBonusType.Full
                : DamageBonusType.Half;
            working = working.Remove(dbMatch.Index, dbMatch.Length);
            hasAnyComponent = true;
        }

        // ── Извлечь спецэффекты ────────────────────────────────────────
        foreach (var effect in KnownEffects)
        {
            var idx = working.IndexOf(effect, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                // Удалить предшествующий '+' если есть
                var start = idx;
                while (start > 0 && (working[start - 1] == '+' || working[start - 1] == ' '))
                    start--;
                expr.Effects.Add(effect.ToLowerInvariant());
                working = working.Remove(start, idx - start + effect.Length);
                hasAnyComponent = true;
            }
        }

        // ── Парсить кубики ─────────────────────────────────────────────
        working = working.Trim().TrimEnd('+', '-', ' ').Trim();
        var diceMatches = DiceRegex().Matches(working);

        foreach (Match m in diceMatches)
        {
            var countStr = m.Groups[1].Value.Replace(" ", "").Trim();
            var sidesStr = m.Groups[2].Value;

            if (!int.TryParse(sidesStr, out var sides)) continue;

            var isNegative = countStr.StartsWith('-');
            countStr = countStr.TrimStart('+', '-').Trim();
            var count = string.IsNullOrEmpty(countStr) ? 1 : int.Parse(countStr);

            expr.Dice.Add(new DiceTerm(count, sides, isNegative));
            hasAnyComponent = true;
        }

        // ── Убрать все кости из строки, чтобы найти плоский модификатор ──
        var withoutDice = DiceRegex().Replace(working, string.Empty);

        // ── Плоский числовой модификатор ───────────────────────────────
        var flatModifier = 0;
        foreach (Match m in FlatModRegex().Matches(withoutDice))
        {
            var valStr = m.Groups[1].Value.Replace(" ", "");
            if (int.TryParse(valStr, out var val))
            {
                flatModifier += val;
                hasAnyComponent = true;
            }
        }

        if (flatModifier != 0)
            expr.FlatModifier = flatModifier;

        if (!hasAnyComponent) return false;

        expression = expr;
        return true;
    }
}
