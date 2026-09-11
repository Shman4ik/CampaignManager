using System.Globalization;

namespace CampaignManager.Web.Components.Features.Characters.Services;

/// <summary>
///     Таблица II «Наличные и активы» («Зов Ктулху» 7e, стр. 45) — единственное место, где живут
///     эти числа. По ней считают деньги и генератор персонажа, и фаза развития
///     («Фаза развития сыщиков: занятия и Средства», стр. 94).
/// </summary>
public static class FinanceRules
{
    /// <summary>
    ///     Строка таблицы II: уровень достатка и деньги, которые ему соответствуют.
    ///     Числа отдельно от текста: фазе развития нужно складывать наличные, а листу — показывать.
    /// </summary>
    public sealed record WealthTier(
        string Name,
        decimal Cash,
        decimal? Assets,
        decimal PocketMoney,
        bool AssetsAreMinimum)
    {
        public string CashText => Format(Cash);
        public string PocketMoneyText => Format(PocketMoney);

        /// <summary>У нищего активов нет, у сверхбогатого они «от» — отсюда текст, а не просто число.</summary>
        public string AssetsText => Assets is null
            ? "нет"
            : Format(Assets.Value) + (AssetsAreMinimum ? "+" : "");
    }

    /// <summary>
    ///     Строка таблицы для указанных Средств. <paramref name="isModern" /> переключает
    ///     столбцы «1920-е» и «наше время».
    /// </summary>
    public static WealthTier GetTier(int creditRating, bool isModern)
    {
        var cr = (decimal)creditRating;

        return isModern
            ? creditRating switch
            {
                <= 0 => new WealthTier("Нищий", 10, null, 10, false),
                <= 9 => new WealthTier("Бедный", cr * 20, cr * 200, 40, false),
                <= 49 => new WealthTier("Среднего класса", cr * 40, cr * 1000, 200, false),
                <= 89 => new WealthTier("Состоятельный", cr * 100, cr * 10000, 1000, false),
                <= 98 => new WealthTier("Богатый", cr * 400, cr * 40000, 5000, false),
                _ => new WealthTier("Сверхбогатый", 1000000, 100000000, 100000, true)
            }
            : creditRating switch
            {
                <= 0 => new WealthTier("Нищий", 0.5m, null, 0.5m, false),
                <= 9 => new WealthTier("Бедный", cr * 1, cr * 10, 2, false),
                <= 49 => new WealthTier("Среднего класса", cr * 2, cr * 50, 10, false),
                <= 89 => new WealthTier("Состоятельный", cr * 5, cr * 500, 50, false),
                <= 98 => new WealthTier("Богатый", cr * 20, cr * 2000, 250, false),
                _ => new WealthTier("Сверхбогатый", 50000, 5000000, 5000, true)
            };
    }

    /// <summary>Уровень жизни, который даёт достаток: жильё и транспорт (стр. 44).</summary>
    public sealed record Lifestyle(string Housing, string Transport);

    /// <summary>
    ///     Раздел «Достаток» (стр. 44). Нужен помощнику создания: по Средствам сыщик уже
    ///     владеет жильём и транспортом своего уровня, и платить за них отдельно не надо.
    /// </summary>
    public static Lifestyle GetLifestyle(int creditRating) => creditRating switch
    {
        <= 0 => new Lifestyle(
            "Жить приходится на улице.",
            "Пешком, на попутках или зайцем на поезде или корабле."),
        <= 9 => new Lifestyle(
            "Самая дешёвая съёмная комната или ночлежка.",
            "Самый дешёвый общественный транспорт; личное средство передвижения — дешёвое и ненадёжное."),
        <= 49 => new Lifestyle(
            "Обычный дом или квартира, съёмные или в собственности; недорогие гостиницы.",
            "Обычные средства передвижения, но не первого класса."),
        <= 89 => new Lifestyle(
            "Просторная резиденция, возможно с прислугой; может быть второй дом за городом. Дорогие гостиницы.",
            "Первого класса; может быть дорогая машина или иное средство передвижения."),
        <= 98 => new Lifestyle(
            "Роскошная резиденция или поместье с целым отрядом слуг и дома за границей. Лучшие гостиницы.",
            "Первого класса; в наше время — несколько дорогих машин."),
        _ => new Lifestyle(
            "Как у богатого, но без всяких ограничений — это богатейшие люди мира.",
            "Любой, какой захочется.")
    };

    /// <summary>
    ///     Вытаскивает сумму из строки вида «$148» или «148,50 долларов». Возвращает null,
    ///     если Хранитель вписал туда текст — тогда пересчитывать нечего, решает он сам.
    /// </summary>
    public static decimal? TryParseMoney(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var digits = new string(text.Where(ch => char.IsDigit(ch) || ch is '.' or ',').ToArray())
            .Replace(',', '.');

        // Отсекаем разделители тысяч: «1.234.56» не число, а мусор из ручного ввода.
        if (digits.Count(ch => ch == '.') > 1)
            return null;

        return decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>Целые суммы пишем без копеек, полдоллара нищего — с ними.</summary>
    public static string Format(decimal value) => value == decimal.Truncate(value)
        ? ((long)value).ToString(CultureInfo.InvariantCulture)
        : value.ToString("0.00", CultureInfo.InvariantCulture);
}
