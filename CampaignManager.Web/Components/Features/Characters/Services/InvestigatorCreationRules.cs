using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Characters.Services;

/// <summary>
///     Глава 3 «Создание сыщиков» («Зов Ктулху» 7e, стр. 26–46): броски характеристик,
///     возрастные модификаторы, проверка улучшения ОБР и альтернативные способы создания
///     (стр. 45–46). Единственное место, где живут эти числа — помощник создания и генератор
///     считают по нему, вторичные атрибуты остаются за <see cref="DerivedAttributeRules" />.
/// </summary>
public static class InvestigatorCreationRules
{
    /// <summary>«Игрок может выбрать для своего сыщика любой возраст в промежутке от 15 до 90 лет» (стр. 30).</summary>
    public const int MinAge = 15;

    public const int MaxAge = 89;

    /// <summary>Ни одна характеристика не поднимается выше 99 (стр. 30).</summary>
    public const int MaxCharacteristic = 99;

    /// <summary>Вариант 4 «покупка характеристик»: 460 пунктов на восемь характеристик (стр. 46).</summary>
    public const int PointBuyBudget = 460;

    public const int PointBuyMin = 15;
    public const int PointBuyMax = 90;

    /// <summary>«В ИНТ и ТЕЛ рекомендуем вкладывать от 40 пунктов» — рекомендация, не запрет (стр. 46).</summary>
    public const int RecommendedIntelligenceSize = 40;

    /// <summary>Вариант 5 «блиц-метод»: восемь готовых значений характеристик (стр. 46).</summary>
    public static readonly int[] BlitzCharacteristics = [80, 70, 60, 60, 50, 50, 50, 40];

    /// <summary>
    ///     Вариант 5: «один навык 70%, два навыка по 60%, три по 50% и три по 40%» —
    ///     девять значений на восемь профессиональных навыков и Средства (стр. 46).
    /// </summary>
    public static readonly int[] BlitzSkillValues = [70, 60, 60, 50, 50, 50, 40, 40, 40];

    /// <summary>Вариант 5: «выберите четыре личных навыка и вложите в них по 20%» (стр. 46).</summary>
    public const int BlitzPersonalSkillCount = 4;

    public const int BlitzPersonalSkillBonus = 20;

    /// <summary>Необязательное правило «лимит начальных значений навыков», пример из книги — 75% (стр. 46).</summary>
    public const int OptionalSkillCap = 75;

    /// <summary>Вариант 6 «сыщики экстра-класса»: 1d10 даёт от 0 до 9 пунктов на распределение (стр. 46).</summary>
    public const int ExtraClassMaxBonus = 9;

    public static readonly IReadOnlyList<CharacteristicInfo> Characteristics =
    [
        new(CharacteristicKey.Strength, "СИЛ", "Сила", CharacteristicDice.ThreeD6),
        new(CharacteristicKey.Constitution, "ВЫН", "Выносливость", CharacteristicDice.ThreeD6),
        new(CharacteristicKey.Size, "ТЕЛ", "Телосложение", CharacteristicDice.TwoD6Plus6),
        new(CharacteristicKey.Dexterity, "ЛВК", "Ловкость", CharacteristicDice.ThreeD6),
        new(CharacteristicKey.Appearance, "НАР", "Наружность", CharacteristicDice.ThreeD6),
        new(CharacteristicKey.Intelligence, "ИНТ", "Интеллект", CharacteristicDice.TwoD6Plus6),
        new(CharacteristicKey.Power, "МОЩ", "Мощь", CharacteristicDice.ThreeD6),
        new(CharacteristicKey.Education, "ОБР", "Образование", CharacteristicDice.TwoD6Plus6)
    ];

    /// <summary>Таблица возрастных модификаторов (стр. 30). Диапазоны не суммируются — берётся ровно один.</summary>
    public static readonly IReadOnlyList<AgeBand> AgeBands =
    [
        new(15, 19, "Юный", EducationChecks: 0, EducationPenalty: 5, AppearancePenalty: 0,
            StrengthSizePenalty: 5, PhysicalPenalty: 0, LuckRolls: 2, MovePenalty: 0),
        new(20, 39, "Молодой", EducationChecks: 1, EducationPenalty: 0, AppearancePenalty: 0,
            StrengthSizePenalty: 0, PhysicalPenalty: 0, LuckRolls: 1, MovePenalty: 0),
        new(40, 49, "Средний", EducationChecks: 2, EducationPenalty: 0, AppearancePenalty: 5,
            StrengthSizePenalty: 0, PhysicalPenalty: 5, LuckRolls: 1, MovePenalty: 1),
        new(50, 59, "Зрелый", EducationChecks: 3, EducationPenalty: 0, AppearancePenalty: 10,
            StrengthSizePenalty: 0, PhysicalPenalty: 10, LuckRolls: 1, MovePenalty: 2),
        new(60, 69, "Пожилой", EducationChecks: 4, EducationPenalty: 0, AppearancePenalty: 15,
            StrengthSizePenalty: 0, PhysicalPenalty: 20, LuckRolls: 1, MovePenalty: 3),
        new(70, 79, "Старый", EducationChecks: 4, EducationPenalty: 0, AppearancePenalty: 20,
            StrengthSizePenalty: 0, PhysicalPenalty: 40, LuckRolls: 1, MovePenalty: 4),
        new(80, 89, "Престарелый", EducationChecks: 4, EducationPenalty: 0, AppearancePenalty: 25,
            StrengthSizePenalty: 0, PhysicalPenalty: 80, LuckRolls: 1, MovePenalty: 5)
    ];

    public static CharacteristicInfo Info(CharacteristicKey key) =>
        Characteristics.First(c => c.Key == key);

    public static AgeBand BandFor(int age) =>
        AgeBands.FirstOrDefault(b => age >= b.MinAge && age <= b.MaxAge) ?? AgeBands[1];

    /// <summary>Один бросок характеристики: сами кости и итог в процентах.</summary>
    public static CharacteristicRoll Roll(CharacteristicKey key)
    {
        var info = Info(key);
        return info.Dice is CharacteristicDice.ThreeD6 ? Roll3d6() : Roll2d6Plus6();
    }

    public static CharacteristicRoll Roll3d6()
    {
        int[] dice = [Dice.Roll(1, 6), Dice.Roll(1, 6), Dice.Roll(1, 6)];
        return new CharacteristicRoll(dice, 0, dice.Sum() * 5);
    }

    public static CharacteristicRoll Roll2d6Plus6()
    {
        int[] dice = [Dice.Roll(1, 6), Dice.Roll(1, 6)];
        return new CharacteristicRoll(dice, 6, (dice.Sum() + 6) * 5);
    }

    /// <summary>
    ///     Вариант 3 (стр. 46): пять бросков 3d6 и три броска 2d6 + 6 — значения раскладывают
    ///     по характеристикам как захочется.
    /// </summary>
    public static List<int> RollPool()
    {
        List<int> pool = [];
        for (var i = 0; i < 5; i++) pool.Add(Roll3d6().Value);
        for (var i = 0; i < 3; i++) pool.Add(Roll2d6Plus6().Value);
        pool.Sort((a, b) => b.CompareTo(a));
        return pool;
    }

    /// <summary>Удача: 3d6 × 5 (стр. 30). Юные бросают дважды и берут лучший результат.</summary>
    public static CharacteristicRoll RollLuck() => Roll3d6();

    /// <summary>
    ///     Проверка улучшения ОБР (стр. 30): бросьте 1d100; если выпало больше текущего ОБР,
    ///     прибавьте 1d10 (но не выше 99).
    /// </summary>
    public static EducationCheck RollEducationCheck(int education)
    {
        var roll = Dice.Percentile();
        if (roll <= education)
            return new EducationCheck(roll, education, 0);

        var gain = Dice.Roll(1, 10);
        var after = Math.Min(MaxCharacteristic, education + gain);
        return new EducationCheck(roll, education, after - education);
    }
}

/// <summary>Бросок характеристики: кости, надбавка формулы и итог в процентах.</summary>
public sealed record CharacteristicRoll(int[] Dice, int Bonus, int Value)
{
    public string Text => Bonus > 0
        ? $"({string.Join(" + ", Dice)} + {Bonus}) × 5 = {Value}"
        : $"({string.Join(" + ", Dice)}) × 5 = {Value}";
}

/// <summary>Результат одной проверки улучшения ОБР (стр. 30).</summary>
public sealed record EducationCheck(int Roll, int Before, int Gain)
{
    public bool Success => Gain > 0;

    public int After => Before + Gain;

    public string Text => Success
        ? $"1d100 = {Roll} > {Before} → +{Gain} (ОБР {Before} → {After})"
        : $"1d100 = {Roll} ≤ {Before} → без изменений";
}
