using System.Text.RegularExpressions;

namespace CampaignManager.Migrate.Catalogs;

/// <summary>Единообразие записи в справочниках (список «Для переноса» UX-1, #177, #179): правила дословные, без догадок.</summary>
public static partial class TextRules
{
    /// <summary>«20 метров», «3 метра», «10/20/50 метров», «СИЛ / 5 метров» → «20 м», «3 м», «10/20/50 м», «СИЛ / 5 м»: так же записаны «100 м» и «СИЛ/5м».</summary>
    public static string? Meters(string? range) =>
        range is null ? null : MetersWord().Replace(range, "$1 м");

    /// <summary>«>=1 магии» → «≥1 магии»: как в книге; «≥» разбор стоимости и так отбрасывает.</summary>
    public static string? GreaterOrEqual(string? text) =>
        text is null ? null : text.Replace(">=", "≥", StringComparison.Ordinal);

    [GeneratedRegex(@"(\d)\s*метр(?:а|ов)?(?![а-яё])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex MetersWord();
}
