using System.Globalization;
using System.Text;

namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Цена в долларах для показа: «$1000», «$20 000», «$0,10» — одним написанием на всё приложение (правило 13:
/// «$1000», а не «$1 000»). Четырёхзначное число без разделителя, с пяти знаков — узкий неразрывный пробел: цена
/// не рвётся посреди числа на узком экране («$1000/20» и «000» на следующей строке). Не зависит от культуры браузера.
/// </summary>
public static class PriceText
{
    /// <summary>Узкий неразрывный пробел между разрядами.</summary>
    public const char ThinSpace = ' ';

    private static readonly CultureInfo Russian = BuildCulture();

    public static string Format(decimal price)
    {
        var number = price % 1 == 0 ? price.ToString("0", Russian) : price.ToString("0.00", Russian);
        return "$" + GroupThousands(number);
    }

    public static string? Format(decimal? price) => price is { } value ? Format(value) : null;

    /// <summary>«20000» → «20 000» (узким пробелом), «9000» и ниже — как есть; дробная часть не трогается.</summary>
    public static string GroupThousands(string number)
    {
        var comma = number.IndexOf(',', StringComparison.Ordinal);
        var whole = comma < 0 ? number : number[..comma];
        var tail = comma < 0 ? "" : number[comma..];
        if (whole.Length < 5 || !whole.All(char.IsAsciiDigit))
        {
            return number;
        }

        var grouped = new StringBuilder();
        for (var i = 0; i < whole.Length; i++)
        {
            if (i > 0 && (whole.Length - i) % 3 == 0)
            {
                grouped.Append(ThinSpace);
            }

            grouped.Append(whole[i]);
        }

        return grouped + tail;
    }

    private static CultureInfo BuildCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ",";
        return culture;
    }
}
