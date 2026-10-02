namespace CampaignManager.Core.Dice;

/// <summary>
/// Проверка d100 против значения навыка или характеристики — <b>единственное</b> место, где живут
/// пороги (стр. 80, 86–89). В v1 вокруг одной функции жили 13 проверок <c>roll &lt;= value</c> в обход
/// неё, и на краях (01 при нуле, 100 при значении от 100) они расходились с ней.
/// </summary>
public static class Check
{
    /// <summary>
    /// Уровень успеха. Достигнутый уровень считается от полного значения, а сложность влияет только на
    /// порог краха: 96–100 — крах, если для успеха нужно выбросить меньше 50; при трудной и
    /// чрезвычайной проверке это половина и пятая часть значения (стр. 88: «Работа в библиотеке» 55,
    /// трудная проверка — нужно 27, крах на 96–100). Пройдена ли проверка — <see cref="Passes"/>.
    /// </summary>
    public static SuccessLevel Evaluate(int roll, int value, Difficulty difficulty = Difficulty.Regular)
    {
        if (roll == 1) return SuccessLevel.Critical;
        if (roll == 100) return SuccessLevel.Fumble;
        if (roll >= 96 && Target(value, difficulty) < 50) return SuccessLevel.Fumble;
        if (roll > value) return SuccessLevel.Failure;
        if (value >= 5 && roll <= value / 5) return SuccessLevel.Extreme;
        if (value >= 2 && roll <= value / 2) return SuccessLevel.Hard;
        return SuccessLevel.Regular;
    }

    /// <summary>
    /// Наибольшее число, которое при этой сложности ещё успех: значение, его половина или пятая часть с
    /// округлением вниз (стр. 80).
    /// </summary>
    public static int Target(int value, Difficulty difficulty) => difficulty switch
    {
        Difficulty.Hard => value / 2,
        Difficulty.Extreme => value / 5,
        _ => value,
    };

    /// <summary>Уровень успеха, которого требует сложность.</summary>
    public static SuccessLevel Required(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Hard => SuccessLevel.Hard,
        Difficulty.Extreme => SuccessLevel.Extreme,
        _ => SuccessLevel.Regular,
    };

    /// <summary>Пройдена ли проверка этой сложности.</summary>
    public static bool Passes(SuccessLevel level, Difficulty difficulty) => level >= Required(difficulty);

    /// <summary>Бросок прошёл проверку: <see cref="Evaluate"/> и <see cref="Passes"/> разом.</summary>
    public static bool Succeeds(int roll, int value, Difficulty difficulty = Difficulty.Regular) =>
        Passes(Evaluate(roll, value, difficulty), difficulty);

    /// <summary>Успех любого уровня (критический тоже).</summary>
    public static bool IsSuccess(this SuccessLevel level) => level >= SuccessLevel.Regular;
}
