using System.Text.Json.Serialization;
using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Characters;

/// <summary>Формула броска характеристики (стр. 28–29).</summary>
public enum CharacteristicDice
{
    /// <summary>3d6 × 5 — СИЛ, ВЫН, ЛВК, НАР, МОЩ.</summary>
    ThreeD6,

    /// <summary>(2d6 + 6) × 5 — ИНТ, ТЕЛ, ОБР.</summary>
    TwoD6Plus6,
}

/// <summary>Справочная строка о характеристике: как зовут и чем бросают.</summary>
public sealed record CharacteristicInfo(Characteristic Key, string Abbreviation, string Name, CharacteristicDice Dice)
{
    public string DiceText => Dice is CharacteristicDice.ThreeD6 ? "3d6 × 5" : "(2d6 + 6) × 5";
}

/// <summary>
/// Строка таблицы возрастных модификаторов (стр. 30, памятка — стр. 32). Штрафы к НАР и ОБР
/// фиксированные, а вычет из физических характеристик игрок распределяет сам — «вычтите суммарно».
/// </summary>
public sealed record AgeBand(
    int MinAge,
    int MaxAge,
    string Name,
    int EducationChecks,
    int EducationPenalty,
    int AppearancePenalty,
    // 15–19 лет: суммарный вычет из СИЛ и/или ТЕЛ.
    int StrengthSizePenalty,
    // 40+ лет: суммарный вычет из СИЛ, ВЫН и/или ЛВК.
    int PhysicalPenalty,
    int LuckRolls,
    int MovePenalty)
{
    public string Range => $"{MinAge}–{MaxAge}";

    /// <summary>Характеристики, между которыми распределяется вычет за возраст.</summary>
    public IReadOnlyList<Characteristic> PenaltyTargets => StrengthSizePenalty > 0
        ? [Characteristic.STR, Characteristic.SIZ]
        : PhysicalPenalty > 0
            ? [Characteristic.STR, Characteristic.CON, Characteristic.DEX]
            : [];

    /// <summary>Сколько пунктов распределить между <see cref="PenaltyTargets"/>.</summary>
    public int DistributedPenalty => StrengthSizePenalty > 0 ? StrengthSizePenalty : PhysicalPenalty;
}

/// <summary>Бросок характеристики: кости, надбавка формулы и итог в процентах.</summary>
public sealed record CharacteristicRoll(int[] Dice, int Bonus, int Value)
{
    public string Text => Bonus > 0
        ? $"({string.Join(" + ", Dice)} + {Bonus}) × 5 = {Value}"
        : $"({string.Join(" + ", Dice)}) × 5 = {Value}";
}

/// <summary>Одна проверка улучшения ОБР (стр. 30).</summary>
public sealed record EducationCheck(int Roll, int Before, int Gain)
{
    [JsonIgnore]
    public bool Success => Gain > 0;

    [JsonIgnore]
    public int After => Before + Gain;

    [JsonIgnore]
    public string Text => Success
        ? $"1d100 = {Roll} > {Before} → +{Gain} (ОБР {Before} → {After})"
        : $"1d100 = {Roll} ≤ {Before} → без изменений";
}

/// <summary>
/// Глава 3 «Создание сыщиков» (стр. 26–46): броски характеристик, возрастные модификаторы, проверка
/// улучшения ОБР, альтернативные способы (стр. 45–46). Вторичные атрибуты — за
/// <see cref="DerivedAttributeRules"/>. Любой бросок помощник даёт и вписать: правила принимают кости.
/// </summary>
public static class InvestigatorCreationRules
{
    /// <summary>«…любой возраст в промежутке от 15 до 90 лет» (стр. 30).</summary>
    public const int MinAge = 15;

    /// <summary>
    /// Последний возраст таблицы. Книга говорит «до 90», таблица кончается на 89; что делать с 90 и с
    /// возрастом вне таблицы — открытый вопрос (rules-findings F-S01), поведение v1 сохранено.
    /// </summary>
    public const int MaxAge = 89;

    /// <summary>Характеристика не поднимается выше 99 (стр. 30).</summary>
    public const int MaxCharacteristic = 99;

    /// <summary>Вариант 4: 460 пунктов на восемь характеристик (стр. 46).</summary>
    public const int PointBuyBudget = 460;

    public const int PointBuyMin = 15;
    public const int PointBuyMax = 90;

    /// <summary>«В ИНТ и ТЕЛ рекомендуем вкладывать от 40» — рекомендация, не запрет (стр. 46).</summary>
    public const int RecommendedIntelligenceSize = 40;

    /// <summary>Вариант 5 «блиц»: восемь готовых значений характеристик (стр. 46).</summary>
    public static IReadOnlyList<int> BlitzCharacteristics { get; } = [80, 70, 60, 60, 50, 50, 50, 40];

    /// <summary>Вариант 5: 70, два по 60, три по 50, три по 40 — восемь навыков профессии и Средства (стр. 46).</summary>
    public static IReadOnlyList<int> BlitzSkillValues { get; } = [70, 60, 60, 50, 50, 50, 40, 40, 40];

    /// <summary>Вариант 5: четыре личных навыка по +20% (стр. 46).</summary>
    public const int BlitzPersonalSkillCount = 4;

    public const int BlitzPersonalSkillBonus = 20;

    /// <summary>Необязательный лимит начальных навыков, пример книги — 75% (стр. 46).</summary>
    public const int OptionalSkillCap = 75;

    /// <summary>Вариант 6 «экстра-класс»: 1d10 − 1, от 0 до 9 пунктов (стр. 46).</summary>
    public const int ExtraClassMaxBonus = 9;

    public static IReadOnlyList<CharacteristicInfo> Characteristics { get; } =
    [
        new(Characteristic.STR, "СИЛ", "Сила", CharacteristicDice.ThreeD6),
        new(Characteristic.CON, "ВЫН", "Выносливость", CharacteristicDice.ThreeD6),
        new(Characteristic.SIZ, "ТЕЛ", "Телосложение", CharacteristicDice.TwoD6Plus6),
        new(Characteristic.DEX, "ЛВК", "Ловкость", CharacteristicDice.ThreeD6),
        new(Characteristic.APP, "НАР", "Наружность", CharacteristicDice.ThreeD6),
        new(Characteristic.INT, "ИНТ", "Интеллект", CharacteristicDice.TwoD6Plus6),
        new(Characteristic.POW, "МОЩ", "Мощь", CharacteristicDice.ThreeD6),
        new(Characteristic.EDU, "ОБР", "Образование", CharacteristicDice.TwoD6Plus6),
    ];

    /// <summary>Таблица возрастных модификаторов (стр. 30). Строки не суммируются — берётся ровно одна.</summary>
    public static IReadOnlyList<AgeBand> AgeBands { get; } =
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
            StrengthSizePenalty: 0, PhysicalPenalty: 80, LuckRolls: 1, MovePenalty: 5),
    ];

    public static CharacteristicInfo Info(Characteristic key) => Characteristics.First(c => c.Key == key);

    /// <summary>Строка таблицы для возраста; вне 15–89 — «Молодой», как в v1 (rules-findings F-S01).</summary>
    public static AgeBand BandFor(int age) =>
        AgeBands.FirstOrDefault(b => age >= b.MinAge && age <= b.MaxAge) ?? AgeBands[1];

    /// <summary>Бросок характеристики её формулой.</summary>
    public static CharacteristicRoll Roll(Characteristic key, IDiceRoller dice) =>
        Info(key).Dice is CharacteristicDice.ThreeD6 ? Roll3d6(dice) : Roll2d6Plus6(dice);

    public static CharacteristicRoll Roll3d6(IDiceRoller dice)
    {
        int[] faces = [dice.Die(6), dice.Die(6), dice.Die(6)];
        return new CharacteristicRoll(faces, 0, faces.Sum() * 5);
    }

    public static CharacteristicRoll Roll2d6Plus6(IDiceRoller dice)
    {
        int[] faces = [dice.Die(6), dice.Die(6)];
        return new CharacteristicRoll(faces, 6, (faces.Sum() + 6) * 5);
    }

    /// <summary>Вариант 3 (стр. 46): пять 3d6 и три 2d6 + 6, раскладываются по желанию; по убыванию.</summary>
    public static List<int> RollPool(IDiceRoller dice)
    {
        List<int> pool = [];
        for (var i = 0; i < 5; i++) pool.Add(Roll3d6(dice).Value);
        for (var i = 0; i < 3; i++) pool.Add(Roll2d6Plus6(dice).Value);
        pool.Sort((a, b) => b.CompareTo(a));
        return pool;
    }

    /// <summary>Удача: 3d6 × 5 (стр. 30). Юные бросают дважды и берут лучший результат.</summary>
    public static CharacteristicRoll RollLuck(IDiceRoller dice) => Roll3d6(dice);

    /// <summary>Проверка улучшения ОБР (стр. 30): 1d100 больше ОБР — +1d10, не выше 99.</summary>
    public static EducationCheck RollEducationCheck(int education, IDiceRoller dice)
    {
        var roll = dice.Percentile();
        if (roll <= education)
            return new EducationCheck(roll, education, 0);

        var after = Math.Min(MaxCharacteristic, education + dice.Roll(1, 10));
        return new EducationCheck(roll, education, after - education);
    }

    /// <summary>Вариант 6: 1d10 − 1 пунктов на распределение (стр. 46).</summary>
    public static int RollExtraClass(IDiceRoller dice) => dice.Die(10) - 1;
}
