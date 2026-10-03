using System.Globalization;
using System.Text.RegularExpressions;

namespace CampaignManager.Migrate.Catalogs;

/// <summary>
/// Цена предмета v1: у предмета не было поля цены, и в «Описании» лежал прейскурант книги («9000 доллара»,
/// «4000,00 долларов», «от 20 000,00 доллара», «0,05–0,20 доллара», «15 долларов за 50 штук», «3 доллара в неделю»).
/// Правило (решение владельца, #178): число — в <c>price</c>, остаток — в описание словами; описание, не похожее на
/// цену целиком, не трогается и идёт в раздел отчёта.
/// <list type="bullet">
/// <item>«N доллар(а|ов)» — цена N, описание пусто;</item>
/// <item>«от N …» — цена N, описание «Цена — от указанной суммы.»;</item>
/// <item>«N–M …» — цена N (нижняя граница), описание «Верхняя граница цены — $M.»;</item>
/// <item>хвост «за …»/«в …» («за ночь», «в неделю», «за 50 штук») — описание «Цена за ночь.»: сумма относится к единице из хвоста.</item>
/// </list>
/// </summary>
public static partial class ItemPriceParser
{
    public sealed record Result(decimal Price, string? Description, bool HasCondition);

    public static Result? TryParse(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var match = Shape().Match(description.Trim());
        if (!match.Success)
        {
            return null;
        }

        var price = Number(match.Groups["n"].Value);
        List<string> notes = [];
        if (match.Groups["from"].Success)
        {
            notes.Add("Цена — от указанной суммы.");
        }

        if (match.Groups["n2"].Success)
        {
            var upper = Number(match.Groups["n2"].Value);
            notes.Add($"Верхняя граница цены — {Format(upper)}.");
        }

        if (match.Groups["tail"].Success)
        {
            notes.Add($"Цена {match.Groups["tail"].Value.Trim()}.");
        }

        return new Result(price, notes.Count == 0 ? null : string.Join(' ', notes), notes.Count > 0);
    }

    private static decimal Number(string text) =>
        decimal.Parse(text.Replace(" ", "").Replace(" ", "").Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture);

    /// <summary>«$9 000», «$0,20»: так число видно в описании рядом с формой.</summary>
    private static string Format(decimal value) =>
        "$" + (value % 1 == 0 ? value.ToString("0", CultureInfo.InvariantCulture) : value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ','));

    // Число: «9000», «4000,00», «20 000,00» (пробел — разделитель тысяч).
    [GeneratedRegex(
        @"^(?<from>от\s+)?(?<n>\d{1,3}(?:[  ]\d{3})+(?:,\d+)?|\d+(?:,\d+)?)(?:\s*[–-]\s*(?<n2>\d{1,3}(?:[  ]\d{3})+(?:,\d+)?|\d+(?:,\d+)?))?\s+доллар(?:а|ов)?(?:\s+(?<tail>(?:за|в)\s+\S.{0,30}))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}
