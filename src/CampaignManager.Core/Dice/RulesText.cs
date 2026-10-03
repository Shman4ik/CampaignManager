namespace CampaignManager.Core.Dice;

/// <summary>
/// Русские подписи костей и уровней — одна таблица на приложение. В v1 подписей сложности было пять
/// копий, и они разъезжались.
/// </summary>
public static class RulesText
{
    /// <summary>«критический успех», «провал», «крах».</summary>
    public static string Of(SuccessLevel level) => level switch
    {
        SuccessLevel.Critical => "критический успех",
        SuccessLevel.Extreme => "чрезвычайный успех",
        SuccessLevel.Hard => "трудный успех",
        SuccessLevel.Regular => "обычный успех",
        SuccessLevel.Failure => "провал",
        SuccessLevel.Fumble => "крах",
        _ => "неизвестно",
    };

    /// <summary>Сложность для подписей: «обычный», «трудный», «чрезвычайный» (уровень, который нужен).</summary>
    public static string Of(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Hard => "трудный",
        Difficulty.Extreme => "чрезвычайный",
        _ => "обычный",
    };

    /// <summary>«1 бонусная кость», «2 штрафные кости».</summary>
    public static string DescribeDice(int count, bool isBonus) =>
        isBonus ? DiceWord(count, "бонусная", "бонусные") : DiceWord(count, "штрафная", "штрафные");

    /// <summary>
    /// Расшифровка броска с дополнительными костями: « (бонусная кость: 24, 44; взято 24)». Для броска без
    /// них — пустая строка.
    /// </summary>
    public static string RollDetail(D100Roll? roll)
    {
        if (roll is null || !roll.HasExtraDice)
            return string.Empty;

        var kind = roll.BonusDice > 0
            ? DescribeDice(roll.BonusDice, isBonus: true)
            : DescribeDice(roll.PenaltyDice, isBonus: false);

        return $" ({kind}: {string.Join(", ", roll.Candidates)}; взято {roll.Result})";
    }

    private static string DiceWord(int count, string one, string many) =>
        count == 1 ? $"{one} кость" : $"{count} {many} кости";
}
