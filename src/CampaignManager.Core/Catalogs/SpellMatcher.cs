using System.Text;
using System.Text.RegularExpressions;

namespace CampaignManager.Core.Catalogs;

/// <summary>Заклинание справочника — то, что нужно сопоставлению и листу (<c>cm.spells</c>).</summary>
public sealed record SpellData(Guid Id, string Name)
{
    public IReadOnlyList<string> AlternativeNames { get; init; } = [];

    public string? Cost { get; init; }

    public string? CastingTime { get; init; }

    public string Description { get; init; } = "";
}

/// <summary>
/// Сопоставляет название заклинания, записанное свободным текстом (список «Возможные заклинания» книги
/// Мифов), с каталогом. Сравнение только точное — после нормализации регистра, «ё», кавычек и пробелов —
/// по названию и альтернативным названиям; проверяются целая строка, текст в «ёлочках» и часть до скобки.
/// Нечёткого поиска нет намеренно: «Связь с божеством: Кфулхут» не должна молча стать чужим заклинанием.
/// </summary>
public static partial class SpellMatcher
{
    public static SpellData? Match(IEnumerable<SpellData> catalog, string bookSpellName)
    {
        var candidates = Candidates(bookSpellName).ToHashSet(StringComparer.Ordinal);
        if (candidates.Count == 0)
            return null;

        return catalog.FirstOrDefault(spell =>
            candidates.Contains(Normalize(spell.Name))
            || spell.AlternativeNames.Any(alt => candidates.Contains(Normalize(alt))));
    }

    /// <summary>Есть ли среди известных заклинание с таким названием (по названию и другим названиям).</summary>
    public static bool IsKnown(IEnumerable<(string Name, IReadOnlyList<string> AlternativeNames)> known,
        string spellName, IEnumerable<string>? alternativeNames = null)
    {
        var names = new[] { spellName }
            .Concat(alternativeNames ?? [])
            .Select(Normalize)
            .Where(n => n.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        return known.Any(spell =>
            names.Contains(Normalize(spell.Name))
            || spell.AlternativeNames.Any(alt => names.Contains(Normalize(alt))));
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            switch (ch)
            {
                case '«' or '»' or '"' or '“' or '”' or '„' or '\'':
                    continue;
                case 'ё':
                    builder.Append('е');
                    break;
                default:
                    builder.Append(char.IsWhiteSpace(ch) ? ' ' : ch);
                    break;
            }
        }

        return WhitespacePattern().Replace(builder.ToString(), " ").Trim(' ', '.', ',', ';');
    }

    private static IEnumerable<string> Candidates(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            yield break;

        yield return Normalize(raw);

        foreach (Match quoted in QuotedPattern().Matches(raw))
            yield return Normalize(quoted.Groups["text"].Value);

        var bracket = raw.IndexOf('(', StringComparison.Ordinal);
        if (bracket > 0)
            yield return Normalize(raw[..bracket]);
    }

    [GeneratedRegex("«(?<text>[^»]+)»")]
    private static partial Regex QuotedPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
