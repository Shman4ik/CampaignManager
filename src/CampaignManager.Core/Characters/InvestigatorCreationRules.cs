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
    /// «…от 15 до 90 лет» (стр. 30). Таблица модификаторов кончается строкой «80–89»; 90 лет книга
    /// разрешает, и им положена эта последняя строка (rules-findings F-S01, исправлено в T2.4).
    /// </summary>
    public const int MaxAge = 90;

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

    /// <summary>
    /// Строка таблицы для возраста (стр. 30). Строки не суммируются — берётся ровно одна. Возраст вне таблицы
    /// получает <b>ближайшую</b> строку: 90 лет (книга их разрешает) и старше — «80–89», младше 15 — «15–19».
    /// Сыщик вне 15–90 — только по договорённости с Хранителем (там же); в v1 такой возраст молча
    /// становился «Молодым» — без вычетов и с одной проверкой ОБР (rules-findings F-S01).
    /// </summary>
    public static AgeBand BandFor(int age) =>
        age < AgeBands[0].MinAge ? AgeBands[0]
        : AgeBands.FirstOrDefault(b => age >= b.MinAge && age <= b.MaxAge) ?? AgeBands[^1];

    /// <summary>Возраст в пределах, которые книга даёт игроку без договорённости с Хранителем (15–90, стр. 30).</summary>
    public static bool IsBookAge(int age) => age is >= MinAge and <= MaxAge;

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

    /// <summary>Все броски Удачи, положенные возрасту (у «Юного» — два, стр. 30).</summary>
    public static List<int> RollLuck(AgeBand band, IDiceRoller dice) =>
        [.. Enumerable.Range(0, Math.Max(1, band.LuckRolls)).Select(_ => RollLuck(dice).Value)];

    /// <summary>Удача из бросков: лучший (стр. 30), 0 — бросков нет.</summary>
    public static int BestLuck(IEnumerable<int> rolls) => rolls.DefaultIfEmpty(0).Max();

    /// <summary>
    /// Значение характеристики по сумме костей её формулы: 3d6 → × 5, 2d6 → (+ 6) × 5. Так вписывают
    /// броски со стола (любой бросок можно вписать).
    /// </summary>
    public static int FromDiceSum(Characteristic key, int sum) =>
        Info(key).Dice is CharacteristicDice.ThreeD6 ? sum * 5 : (sum + 6) * 5;

    /// <summary>Сумма костей, давшая значение (обратная <see cref="FromDiceSum"/>); null — значение не из костей.</summary>
    public static int? DiceSumOf(Characteristic key, int value)
    {
        if (value <= 0 || value % 5 != 0)
            return null;

        var sum = Info(key).Dice is CharacteristicDice.ThreeD6 ? value / 5 : value / 5 - 6;
        var (min, max) = Info(key).Dice is CharacteristicDice.ThreeD6 ? (3, 18) : (2, 12);
        return sum >= min && sum <= max ? sum : null;
    }

    /// <summary>
    /// Значения набора, ещё не занятые другими (вариант 3 и блиц, стр. 46): набор минус уже разложенное —
    /// повторы считаются поштучно. Один алгоритм на характеристики и блиц-навыки (в v1 — две копии).
    /// </summary>
    public static List<int> Available(IEnumerable<int> pool, IEnumerable<int> usedElsewhere)
    {
        var used = usedElsewhere.Where(v => v > 0).ToList();
        List<int> available = [];
        foreach (var value in pool)
        {
            if (value <= 0 || used.Remove(value))
                continue;
            available.Add(value);
        }

        return available;
    }

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
