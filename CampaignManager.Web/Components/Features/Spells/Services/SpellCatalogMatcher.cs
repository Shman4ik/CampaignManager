using System.Text;
using System.Text.RegularExpressions;
using CampaignManager.Web.Components.Features.Spells.Model;

namespace CampaignManager.Web.Components.Features.Spells.Services;

/// <summary>
///     Сопоставляет название заклинания, записанное свободным текстом (список «Возможные
///     заклинания» книги Мифов), с каталогом заклинаний.
///     <para>
///         Сравнение только точное — после нормализации регистра, «ё», кавычек и пробелов — по
///         названию и альтернативным названиям. Книга пишет заклинания то в кавычках, то с
///         уточнением в скобках: «Связь с бесформенным отродьем Жотакуа («Связь с бесформенным
///         отродьем»)», поэтому проверяются целая строка, текст в «ёлочках» и часть до скобки.
///         Нечёткого поиска нет намеренно: «Связь с божеством: Кфулхут» не должна молча
///         превратиться в чужое заклинание — не нашлось, значит Хранитель сопоставит руками.
///     </para>
/// </summary>
public static partial class SpellCatalogMatcher
{
    public static Spell? Match(IEnumerable<Spell> catalog, string bookSpellName)
    {
        var candidates = Candidates(bookSpellName).ToHashSet(StringComparer.Ordinal);
        if (candidates.Count == 0)
            return null;

        return catalog.FirstOrDefault(spell =>
            candidates.Contains(Normalize(spell.Name))
            || spell.AlternativeNames.Any(alt => candidates.Contains(Normalize(alt))));
    }

    /// <summary>Знает ли сыщик заклинание с таким названием (по названию и альтернативным).</summary>
    public static bool IsKnown(IEnumerable<Spell> known, string spellName, IEnumerable<string>? alternativeNames = null)
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

    private static IEnumerable<string> Candidates(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            yield break;

        yield return Normalize(raw);

        foreach (Match quoted in QuotedPattern().Matches(raw))
            yield return Normalize(quoted.Groups["text"].Value);

        var bracket = raw.IndexOf('(');
        if (bracket > 0)
            yield return Normalize(raw[..bracket]);
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

    [GeneratedRegex("«(?<text>[^»]+)»")]
    private static partial Regex QuotedPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
