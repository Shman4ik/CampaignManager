using System.Text.RegularExpressions;

namespace CampaignManager.Web.Components.Features.Bestiary.Services;

/// <summary>
///     Разбор записи потери рассудка из бестиария («0/1d6», «1/1d10+2»).
///     Нужен привыканию к ужасному: предел — максимум провальной части (стр. 167).
/// </summary>
public static partial class SanityLossFormula
{
    /// <summary>
    ///     Максимум, который можно потерять за встречу с этим видом. Ноль — если запись пустая
    ///     или не разбирается: тогда предел придётся вписать Хранителю вручную.
    /// </summary>
    public static int MaxLoss(string? formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return 0;

        // «успех/провал» — берём провальную часть, она и задаёт предел.
        var slash = formula.LastIndexOf('/');
        var failure = slash >= 0 ? formula[(slash + 1)..] : formula;

        var total = 0;
        var matched = false;

        foreach (Match term in TermPattern().Matches(failure))
        {
            matched = true;
            var sign = term.Groups["sign"].Value == "-" ? -1 : 1;

            if (term.Groups["sides"].Success)
            {
                var count = term.Groups["count"].Success ? int.Parse(term.Groups["count"].Value) : 1;
                total += sign * count * int.Parse(term.Groups["sides"].Value);
            }
            else
            {
                total += sign * int.Parse(term.Groups["flat"].Value);
            }
        }

        return matched ? Math.Max(0, total) : 0;
    }

    [GeneratedRegex(@"(?<sign>[+-])?\s*(?:(?<count>\d+)?[dд](?<sides>\d+)|(?<flat>\d+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TermPattern();
}
