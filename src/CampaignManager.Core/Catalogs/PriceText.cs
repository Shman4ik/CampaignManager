using System.Globalization;

namespace CampaignManager.Core.Catalogs;

/// <summary>Цена в долларах для показа: «$9 000», «$0,10» — как в прейскуранте книги, независимо от культуры браузера.</summary>
public static class PriceText
{
    private static readonly CultureInfo Russian = BuildCulture();

    public static string Format(decimal price) =>
        "$" + (price % 1 == 0 ? price.ToString("N0", Russian) : price.ToString("N2", Russian));

    public static string? Format(decimal? price) => price is { } value ? Format(value) : null;

    private static CultureInfo BuildCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberGroupSeparator = " ";
        culture.NumberFormat.NumberDecimalSeparator = ",";
        return culture;
    }
}
