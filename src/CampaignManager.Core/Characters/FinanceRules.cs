using System.Globalization;

namespace CampaignManager.Core.Characters;

/// <summary>
/// Таблица II «Наличные и активы» (стр. 45) и раздел «Достаток» (стр. 44) — единственное место этих
/// чисел: по ним считают и создание сыщика, и пересчёт денег в фазе развития (стр. 94).
/// <para>
/// Разбора денег из строки здесь нет: в листе 2.0 наличные — число. <c>TryParseMoney</c> v1 с его
/// причудами (rules-findings F-S06) нужен только переносу листов (T1.3).
/// </para>
/// </summary>
public static class FinanceRules
{
    /// <summary>Строка таблицы: уровень достатка и деньги. Числа отдельно от текста.</summary>
    public sealed record WealthTier(
        string Name,
        decimal Cash,
        decimal? Assets,
        decimal PocketMoney,
        bool AssetsAreMinimum)
    {
        public string CashText => Format(Cash);

        public string PocketMoneyText => Format(PocketMoney);

        /// <summary>У нищего активов нет, у сверхбогатого они «от».</summary>
        public string AssetsText => Assets is null ? "нет" : Format(Assets.Value) + (AssetsAreMinimum ? "+" : "");
    }

    /// <summary>Строка таблицы для Средств: столбец «1920-е» или «наше время».</summary>
    public static WealthTier GetTier(int creditRating, Era era)
    {
        var cr = (decimal)creditRating;

        return era == Era.Modern
            ? creditRating switch
            {
                <= 0 => new WealthTier("Нищий", 10, null, 10, false),
                <= 9 => new WealthTier("Бедный", cr * 20, cr * 200, 40, false),
                <= 49 => new WealthTier("Среднего класса", cr * 40, cr * 1000, 200, false),
                <= 89 => new WealthTier("Состоятельный", cr * 100, cr * 10000, 1000, false),
                <= 98 => new WealthTier("Богатый", cr * 400, cr * 40000, 5000, false),
                _ => new WealthTier("Сверхбогатый", 1000000, 100000000, 100000, true),
            }
            : creditRating switch
            {
                <= 0 => new WealthTier("Нищий", 0.5m, null, 0.5m, false),
                <= 9 => new WealthTier("Бедный", cr * 1, cr * 10, 2, false),
                <= 49 => new WealthTier("Среднего класса", cr * 2, cr * 50, 10, false),
                <= 89 => new WealthTier("Состоятельный", cr * 5, cr * 500, 50, false),
                <= 98 => new WealthTier("Богатый", cr * 20, cr * 2000, 250, false),
                _ => new WealthTier("Сверхбогатый", 50000, 5000000, 5000, true),
            };
    }

    /// <summary>Жильё и транспорт, положенные достатку (стр. 44).</summary>
    public sealed record Lifestyle(string Housing, string Transport);

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
            "Любой, какой захочется."),
    };

    /// <summary>
    /// Деньги нового сыщика по таблице II: наличные, карманные и активы словами (у нищего — «нет»).
    /// Одна запись для помощника и для пересчёта — в v1 было три формата строки.
    /// </summary>
    public static Finances ForNewInvestigator(int creditRating, Era era)
    {
        var tier = GetTier(creditRating, era);
        return new Finances { Cash = tier.Cash, PocketMoney = tier.PocketMoney, Assets = tier.AssetsText };
    }

    /// <summary>
    /// Трата больше карманных денег — вся сумма из наличных (стр. 93). Наличных не хватает — лист не меняется (false): активы
    /// обналичивают не сразу, это решает Хранитель.
    /// </summary>
    public static bool Spend(Finances finances, decimal amount)
    {
        if (amount <= 0 || finances.Cash is not { } cash || amount > cash)
            return false;

        finances.Cash = cash - amount;
        return true;
    }

    /// <summary>Целые суммы без копеек, полдоллара нищего — с ними.</summary>
    public static string Format(decimal value) => value == decimal.Truncate(value)
        ? ((long)value).ToString(CultureInfo.InvariantCulture)
        : value.ToString("0.00", CultureInfo.InvariantCulture);
}
