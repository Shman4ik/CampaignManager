using System.Globalization;
using System.Text.RegularExpressions;
using CampaignManager.Web.Components.Features.Weapons.Model;

namespace CampaignManager.Web.Utilities.Services;

/// <summary>
/// Разбор текстовых колонок таблицы XVII «Оружие» (стр. 399–402) в структурированные типы:
/// дальность, число атак, боезапас, стоимость и порог осечки.
/// <para>
/// Устроен так же, как <see cref="DamageFormulaParser" />: методы всегда возвращают объект,
/// а флаг <c>IsParsed</c> говорит, удалось ли понять текст. Не разобранным законно остаётся
/// только то, что и в книге записано словом («Варьирует», «Шок»).
/// </para>
/// </summary>
public static partial class WeaponStatsParser
{
    /// <summary>
    /// Верхняя граница «раундов на одну атаку». Самое медленное оружие таблицы XVII —
    /// «1/4»; большее число в листе означает опечатку, а не сверхмедленный ствол.
    /// </summary>
    private const int MaxRoundsPerAttack = 20;

    // ── Регулярные выражения ───────────────────────────────────────────

    /// <summary>Одно или несколько чисел через «/»: «10/20/50», «20/30/32».</summary>
    [GeneratedRegex(@"^\s*(\d+(?:\s*/\s*\d+)*)")]
    private static partial Regex LeadingNumberListRegex();

    /// <summary>Метательное оружие: «СИЛ / 5 метров», «СИЛ/5м».</summary>
    [GeneratedRegex(@"^\s*сил\s*/\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex StrengthThrowRegex();

    /// <summary>Основное число атак и предел в скобках: «1», «1 (3)».</summary>
    [GeneratedRegex(@"^\s*(\d+)\s*(?:\(\s*(\d+)\s*\))?")]
    private static partial Regex ShotsRegex();

    /// <summary>Медленное оружие: «1/4» — один выстрел в четыре раунда.</summary>
    [GeneratedRegex(@"^\s*(\d+)\s*/\s*(\d+)\s*$")]
    private static partial Regex RoundsPerAttackRegex();

    /// <summary>Альтернатива числом: «1 или 2».</summary>
    [GeneratedRegex(@"\bили\s+(\d+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AlternativeShotsRegex();

    /// <summary>Длина очереди: «очередями по 3».</summary>
    [GeneratedRegex(@"очеред\w*\s+по\s+(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex BurstRegex();

    /// <summary>Денежная сумма: «$1000», «20 000», «0,65», «1600».</summary>
    [GeneratedRegex(@"\$?\s*(\d[\d ]*(?:[.,]\d+)?)")]
    private static partial Regex MoneyRegex();

    // ── Дальность ─────────────────────────────────────────────────────

    /// <summary>Разбирает колонку «Дальность» в <see cref="WeaponRangeInfo" />.</summary>
    public static WeaponRangeInfo ParseRange(string? raw)
    {
        var result = new WeaponRangeInfo { RawText = raw ?? string.Empty };
        var text = Normalize(raw);

        if (text.Length == 0 || IsNone(text))
        {
            result.Kind = WeaponRangeKind.None;
            result.IsParsed = true;
            return result;
        }

        // «Касание», «контакт», «Ближний бой» — оружие ближнего боя
        if (Contains(text, "касание") || Contains(text, "контакт") || Contains(text, "ближний бой"))
        {
            result.Kind = WeaponRangeKind.Touch;
            result.IsParsed = true;
            return result;
        }

        // «На месте» — стационарная закладка
        if (Contains(text, "на месте"))
        {
            result.Kind = WeaponRangeKind.Emplaced;
            result.IsParsed = true;
            return result;
        }

        // «СИЛ / 5 метров» — дальность броска считается от силы
        var throwMatch = StrengthThrowRegex().Match(text);
        if (throwMatch.Success && int.TryParse(throwMatch.Groups[1].Value, out var divisor) && divisor > 0)
        {
            result.Kind = WeaponRangeKind.StrengthThrow;
            result.ThrowDivisor = divisor;
            result.IsParsed = true;
            return result;
        }

        // «15 метров», «10/20/50 метров» — число либо список порогов
        var numbers = ParseNumberList(text);
        if (numbers.Count == 1)
        {
            result.Kind = WeaponRangeKind.Meters;
            result.BaseMeters = numbers[0];
            result.IsParsed = true;
            return result;
        }

        if (numbers.Count > 1)
        {
            result.Kind = WeaponRangeKind.RangeBands;
            result.Bands = numbers;
            result.BaseMeters = numbers[0];
            result.IsParsed = true;
            return result;
        }

        return result;
    }

    // ── Число атак ────────────────────────────────────────────────────

    /// <summary>Разбирает колонку «Атак» в <see cref="WeaponAttacksInfo" />.</summary>
    public static WeaponAttacksInfo ParseAttacks(string? raw)
    {
        var result = new WeaponAttacksInfo { RawText = raw ?? string.Empty };
        var text = Normalize(raw);

        if (text.Length == 0 || IsNone(text))
        {
            result.IsParsed = true;
            return result;
        }

        // «Однораз.», «Одноразовый», «На месте» — применяется один раз
        if (Contains(text, "однораз") || Contains(text, "на месте"))
        {
            result.IsSingleUse = true;
            result.ShotsPerRound = 1;
            result.IsParsed = true;
            return result;
        }

        var recognized = false;

        // «непр. огонь» — непрерывный огонь
        if (Contains(text, "непр"))
        {
            result.AllowsFullAuto = true;
            recognized = true;
        }

        // «очередями по 3»
        var burst = BurstRegex().Match(text);
        if (burst.Success && int.TryParse(burst.Groups[1].Value, out var burstSize))
        {
            result.AllowsBurst = true;
            result.BurstSize = burstSize;
            recognized = true;
        }

        // «1/4» — один выстрел в четыре раунда (только когда вся строка такая)
        var slow = RoundsPerAttackRegex().Match(text);
        if (slow.Success
            && int.TryParse(slow.Groups[1].Value, out var shots)
            && int.TryParse(slow.Groups[2].Value, out var rounds))
        {
            result.ShotsPerRound = shots;

            // Самое медленное оружие в книге стреляет раз в четыре раунда. Всё, что
            // больше MaxRoundsPerAttack, — опечатка в листе; такую строку оставляем
            // неразобранной, чтобы потребитель показал исходный текст.
            if (rounds is >= 1 and <= MaxRoundsPerAttack)
            {
                result.RoundsPerAttack = rounds;
                result.IsParsed = true;
            }

            return result;
        }

        // «1», «1 (3)» — базовое число выстрелов и предел на скорость
        var shotsMatch = ShotsRegex().Match(text);
        if (shotsMatch.Success && int.TryParse(shotsMatch.Groups[1].Value, out var perRound))
        {
            result.ShotsPerRound = perRound;
            recognized = true;

            if (shotsMatch.Groups[2].Success && int.TryParse(shotsMatch.Groups[2].Value, out var max))
                result.MaxShotsPerRound = max;
        }

        // «1 или 2» — альтернативный предел словом
        if (result.MaxShotsPerRound is null)
        {
            var alt = AlternativeShotsRegex().Match(text);
            if (alt.Success && int.TryParse(alt.Groups[1].Value, out var altMax))
            {
                result.MaxShotsPerRound = altMax;
                recognized = true;
            }
        }

        result.IsParsed = recognized;
        return result;
    }

    // ── Боезапас ──────────────────────────────────────────────────────

    /// <summary>Разбирает колонку «Боезапас» в <see cref="WeaponAmmoInfo" />.</summary>
    public static WeaponAmmoInfo ParseAmmo(string? raw)
    {
        var result = new WeaponAmmoInfo { RawText = raw ?? string.Empty };
        var text = Normalize(raw);

        if (text.Length == 0 || IsNone(text))
        {
            result.IsParsed = true;
            return result;
        }

        var recognized = false;

        // «Автоподача» — питание лентой, штатной ёмкости нет
        if (Contains(text, "автоподач"))
        {
            result.IsBeltFed = true;
            recognized = true;
        }

        // «Отдельно» — боеприпас поставляется сам по себе
        if (Contains(text, "отдельно"))
        {
            result.IsSuppliedSeparately = true;
            recognized = true;
        }

        // «Только 1», «Однораз.», «Одноразовая»
        if (Contains(text, "однораз") || Contains(text, "только 1"))
        {
            result.IsSingleUse = true;
            result.Capacity = 1;
            result.IsParsed = true;
            return result;
        }

        // «20/30/32» — варианты магазина; «25 доз», «Минимум 10» — число внутри текста
        var numbers = ParseNumberList(StripLeadingWords(text));
        if (numbers.Count > 0)
        {
            result.Capacity = numbers[0];
            if (numbers.Count > 1) result.CapacityOptions = numbers;
            recognized = true;
        }

        result.IsParsed = recognized;
        return result;
    }

    // ── Стоимость ─────────────────────────────────────────────────────

    /// <summary>
    /// Разбирает колонку «Стоимость» в <see cref="WeaponCostInfo" />.
    /// Формат книги — «1920-е / современность»; одиночное значение относится к 1920-м.
    /// </summary>
    public static WeaponCostInfo ParseCost(string? raw)
    {
        var result = new WeaponCostInfo { RawText = raw ?? string.Empty };
        var text = Normalize(raw);

        if (text.Length == 0)
        {
            result.IsParsed = true;
            return result;
        }

        // «Нет», «—» — оружие не продаётся ни в одну эпоху
        if (IsNone(text))
        {
            result.Unavailable1920 = true;
            result.UnavailableModern = true;
            result.IsParsed = true;
            return result;
        }

        // «$0,65 – 5,25» — вилка цены за одну эпоху: берём нижнюю границу как ориентир
        var rangeLow = SplitOnRangeDash(text);
        if (rangeLow is not null)
        {
            result.Cost1920 = ParseMoney(rangeLow);
            result.IsApproximate1920 = result.Cost1920 is not null;
            result.IsParsed = result.Cost1920 is not null;
            return result;
        }

        var parts = text.Split('/', 2);
        var era1920 = parts[0].Trim();
        var eraModern = parts.Length > 1 ? parts[1].Trim() : null;

        result.Cost1920 = ParseMoney(era1920);
        result.IsApproximate1920 = result.Cost1920 is not null && Contains(era1920, "от ");

        // Unavailable ставится только там, где эпоха записана явно и без цены
        // («—», «Нет», «редкое»). Одиночная цена — это цена 1920-х, а про современную
        // строка просто молчит: тогда обе — и цена, и флаг — остаются пустыми.
        if (result.Cost1920 is null) result.Unavailable1920 = true;

        if (eraModern is not null)
        {
            result.CostModern = ParseMoney(eraModern);
            result.IsApproximateModern = result.CostModern is not null && Contains(eraModern, "от ");
            if (result.CostModern is null) result.UnavailableModern = true;
        }

        result.IsParsed = result.Cost1920 is not null || result.CostModern is not null;
        return result;
    }

    // ── Осечка ────────────────────────────────────────────────────────

    /// <summary>
    /// Порог осечки числом (стр. 113). В листах он записан по-разному: числом («100»),
    /// по-процентному («00» — это 100 на процентных костях) или пустой строкой, если
    /// осечки нет. Null — порога нет либо он вне диапазона 1–100: иначе «0» из листа
    /// означал бы, что оружие клинит при любом броске.
    /// </summary>
    public static int? ParseMalfunction(string? raw)
    {
        var text = raw?.Trim();
        if (string.IsNullOrEmpty(text)) return null;

        var digits = new string(text.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return null;

        // «00» на процентных костях — это 100; одиночный «0» — незаполненное поле
        if (digits.All(d => d == '0'))
            return digits.Length < 2 ? null : 100;

        if (!int.TryParse(digits, out var value) || value is < 1 or > 100) return null;

        return value;
    }

    // ── Вспомогательное ───────────────────────────────────────────────

    /// <summary>Разделители вилки цены: «$0,65 – 5,25».</summary>
    private static readonly char[] Digits = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

    private static readonly string[] RangeDashes = [" – ", " — ", " - "];

    private static string Normalize(string? raw) =>
        raw?.Replace(' ', ' ').Replace(' ', ' ').Trim() ?? string.Empty;

    private static bool Contains(string text, string token) =>
        text.Contains(token, StringComparison.OrdinalIgnoreCase);

    /// <summary>«Нет» / «—» / «–» — значения нет.</summary>
    private static bool IsNone(string text) =>
        text.Equals("нет", StringComparison.OrdinalIgnoreCase)
        || text is "—" or "–" or "-";

    /// <summary>Числа в начале строки, возможно через «/»: «10/20/50 метров» → [10, 20, 50].</summary>
    private static List<int> ParseNumberList(string text)
    {
        var match = LeadingNumberListRegex().Match(text);
        return match.Success ? SplitNumbers(match.Groups[1].Value) : [];
    }

    private static List<int> SplitNumbers(string raw)
    {
        List<int> numbers = [];
        foreach (var piece in raw.Split('/'))
            if (int.TryParse(piece.Trim(), out var value))
                numbers.Add(value);

        return numbers;
    }

    /// <summary>Отбрасывает ведущие слова, чтобы добраться до числа: «Минимум 10» → «10».</summary>
    private static string StripLeadingWords(string text)
    {
        var index = text.IndexOfAny(Digits);
        return index < 0 ? text : text[index..];
    }

    /// <summary>
    /// «$0,65 – 5,25» — вилка цены через тире. Возвращает нижнюю границу либо null,
    /// если тире в строке нет.
    /// </summary>
    private static string? SplitOnRangeDash(string text)
    {
        foreach (var separator in RangeDashes)
        {
            var index = text.IndexOf(separator, StringComparison.Ordinal);
            if (index > 0) return text[..index];
        }

        return null;
    }

    /// <summary>
    /// Первая денежная сумма из фрагмента. «20 000» — пробел как разделитель тысяч,
    /// «0,65» — запятая как десятичный разделитель (книга свёрстана по-русски).
    /// </summary>
    private static decimal? ParseMoney(string fragment)
    {
        if (fragment.Length == 0 || IsNone(fragment)) return null;

        var match = MoneyRegex().Match(fragment);
        if (!match.Success) return null;

        var digits = match.Groups[1].Value.Replace(" ", string.Empty).Replace(',', '.');
        if (!decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            return null;

        // «65¢» — цена в центах, встречается в старых копиях оружия из листов
        var tail = fragment[(match.Index + match.Length)..].TrimStart();
        return tail.StartsWith('¢') ? value / 100m : value;
    }
}
