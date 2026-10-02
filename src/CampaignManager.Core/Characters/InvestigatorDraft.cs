using System.Text.Json.Serialization;
using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Characters;

/// <summary>Способ создания: стандартный и варианты 3–5 (стр. 45–46).</summary>
public enum CreationMethod
{
    /// <summary>Каждая характеристика бросается своей формулой.</summary>
    Standard,

    /// <summary>Вариант 3: восемь бросков раскладывают по характеристикам.</summary>
    AssignRolls,

    /// <summary>Вариант 4: 460 пунктов между восемью характеристиками.</summary>
    PointBuy,

    /// <summary>Вариант 5, блиц: готовые значения характеристик и навыков.</summary>
    Blitz,
}

/// <summary>
/// Черновик помощника создания сыщика — всё, что игрок выбрал и набросал. Живёт в браузере
/// (<c>localStorage</c>) и переживает уход со страницы; поэтому сериализуется тем же <c>CmJson</c>.
/// <para>
/// Навыки в словарях — по <b>ключу навыка помощника</b> (<see cref="CreationPlan.KeyOf"/>): <c>Id</c> справочника
/// строкой, а для своей специализации — её уточнение («латынь»; родитель — в <see cref="AddedSpecializations"/>).
/// Здесь арифметика главы 3 и правки шагов «Способ» и «Характеристики» (сброс, броски, проверки ОБР, Удача);
/// профессия, навыки и проверки шагов — <see cref="CreationPlan"/>; лист — <see cref="SheetBuilder.FromDraft"/>.
/// Помощник только показывает и зовёт эти методы (в v1 бросок ОБР, экстра-класс и Удача жили в разметке — AUDIT,
/// «Правила в разметке»).
/// </para>
/// </summary>
public sealed class InvestigatorDraft
{
    public int StepIndex { get; set; }

    public CreationMethod Method { get; set; } = CreationMethod.Standard;

    /// <summary>Эпоха: от неё столбец таблицы «Наличные и активы» и состав навыков.</summary>
    public Era Era { get; set; } = Era.Classic;

    public int Age { get; set; } = 25;

    /// <summary>Необязательный лимит начальных навыков (стр. 46); null — без лимита.</summary>
    public int? SkillCap { get; set; }

    // ── Шаг 1: характеристики ───────────────────────────────────────────────

    /// <summary>Выпавшее до возрастных модификаторов.</summary>
    public Dictionary<Characteristic, int> Rolled { get; set; } = [];

    /// <summary>Вариант 3: броски, ещё не разложенные по характеристикам.</summary>
    public List<int> Pool { get; set; } = [];

    /// <summary>Текст бросков для игрока: «(4 + 3 + 6) × 5 = 65».</summary>
    public Dictionary<Characteristic, string> RollText { get; set; } = [];

    /// <summary>Сколько возрастного вычета снято с каждой характеристики.</summary>
    public Dictionary<Characteristic, int> AgePenaltyDistribution { get; set; } = [];

    /// <summary>Вариант 6: куда вложены выпавшие 0–9 пунктов.</summary>
    public Dictionary<Characteristic, int> ExtraClassBonus { get; set; } = [];

    public bool ExtraClass { get; set; }

    /// <summary>Выпавшие 0–9 пунктов варианта 6; null — ещё не бросали.</summary>
    public int? ExtraClassPool { get; set; }

    public List<EducationCheck> EducationChecks { get; set; } = [];

    public List<int> LuckRolls { get; set; } = [];

    public int Luck { get; set; }

    // ── Шаг 2: род занятий ──────────────────────────────────────────────────

    public Guid? OccupationId { get; set; }

    public string OccupationName { get; set; } = "";

    /// <summary>Характеристика, выбранная в формуле очков профессии; null — выбора нет.</summary>
    public Characteristic? FormulaChoice { get; set; }

    /// <summary>Выбор игрока по слотам профессии; индекс — порядок слотов.</summary>
    public List<string> SlotChoices { get; set; } = [];

    // ── Шаг 3: навыки ───────────────────────────────────────────────────────

    /// <summary>Очки профессии по ключу навыка.</summary>
    public Dictionary<string, int> OccupationPoints { get; set; } = [];

    /// <summary>Очки личного интереса по ключу навыка.</summary>
    public Dictionary<string, int> PersonalPoints { get; set; } = [];

    /// <summary>Средства: пункты идут из очков профессии (стр. 34).</summary>
    public int CreditRating { get; set; }

    /// <summary>Блиц: какое из готовых значений получил каждый навык профессии.</summary>
    public Dictionary<string, int> BlitzValues { get; set; } = [];

    /// <summary>Блиц: четыре личных навыка по +20 (стр. 46).</summary>
    public List<string> BlitzPersonalSkills { get; set; } = [];

    /// <summary>Свои специализации сверх справочника: уточнение («латынь») → Id родителя.</summary>
    public Dictionary<string, Guid> AddedSpecializations { get; set; } = [];

    // ── Шаг 4: биография ────────────────────────────────────────────────────

    public PersonalInfo Personal { get; set; } = new();

    public Biography Biography { get; set; } = new();

    /// <summary>Графа, отмеченная ключевой связью (стр. 43) — ключ графы, а не текст.</summary>
    public string KeyConnectionSection { get; set; } = "";

    // ── Шаг 5: снаряжение ───────────────────────────────────────────────────

    public List<EquipmentItem> Equipment { get; set; } = [];

    /// <summary>Итог характеристики: бросок минус возрастной вычет плюс экстра-класс и улучшения ОБР, в 1–99.</summary>
    public int Value(Characteristic key, AgeBand band)
    {
        var value = Rolled.GetValueOrDefault(key);
        value -= AgePenaltyDistribution.GetValueOrDefault(key);
        value += ExtraClassBonus.GetValueOrDefault(key);

        if (key is Characteristic.APP)
            value -= band.AppearancePenalty;

        if (key is Characteristic.EDU)
            value += EducationChecks.Sum(c => c.Gain) - band.EducationPenalty;

        return Math.Clamp(value, 1, InvestigatorCreationRules.MaxCharacteristic);
    }

    /// <summary>Итоговые характеристики для листа.</summary>
    public Characteristics BuildCharacteristics()
    {
        var band = InvestigatorCreationRules.BandFor(Age);
        var result = new Characteristics();
        foreach (var key in Enum.GetValues<Characteristic>())
            result[key] = Value(key, band);
        return result;
    }

    /// <summary>Сколько возрастного вычета ещё не распределено.</summary>
    public int RemainingAgePenalty(AgeBand band) =>
        band.DistributedPenalty - band.PenaltyTargets.Sum(k => AgePenaltyDistribution.GetValueOrDefault(k));

    public int RemainingExtraClass() => (ExtraClassPool ?? 0) - ExtraClassBonus.Values.Sum();

    /// <summary>Все восемь бросков сделаны.</summary>
    [JsonIgnore]
    public bool CharacteristicsFilled => Enum.GetValues<Characteristic>().All(k => Rolled.GetValueOrDefault(k) > 0);

    /// <summary>Вложено очков профессии, включая Средства.</summary>
    [JsonIgnore]
    public int SpentOccupationPoints => OccupationPoints.Values.Sum() + CreditRating;

    [JsonIgnore]
    public int SpentPersonalPoints => PersonalPoints.Values.Sum();

    /// <summary>Значение навыка: база плюс вложенное.</summary>
    public int SkillTotal(string skillKey, int baseValue) =>
        baseValue + OccupationPoints.GetValueOrDefault(skillKey) + PersonalPoints.GetValueOrDefault(skillKey);

    // ── Правки шагов «Способ» и «Характеристики» ────────────────────────────

    /// <summary>Сменили способ — прежние броски к нему не относятся, шаг характеристик начинается заново.</summary>
    public void SetMethod(CreationMethod method)
    {
        if (Method == method)
            return;

        Method = method;
        ResetCharacteristics();
        if (method is CreationMethod.Blitz)
            Pool = [.. InvestigatorCreationRules.BlitzCharacteristics];
        else if (method is CreationMethod.AssignRolls)
            Pool = [0, 0, 0, 0, 0, 0, 0, 0];
    }

    /// <summary>
    /// Возраст (15–90, стр. 30). Строка возраста меняет и вычеты, и число проверок ОБР и бросков Удачи, поэтому
    /// прежние распределения к ней уже не относятся.
    /// </summary>
    public void SetAge(int age)
    {
        var clamped = Math.Clamp(age, InvestigatorCreationRules.MinAge, InvestigatorCreationRules.MaxAge);
        if (clamped == Age)
            return;

        Age = clamped;
        AgePenaltyDistribution.Clear();
        EducationChecks.Clear();
        LuckRolls.Clear();
        Luck = 0;
    }

    public void SetExtraClass(bool enabled)
    {
        ExtraClass = enabled;
        ExtraClassPool = null;
        ExtraClassBonus.Clear();
    }

    /// <summary>Выпало на 1d10 варианта 6 — в пул идёт 1d10 − 1, от 0 до 9 (стр. 46); null — сбросить.</summary>
    public void SetExtraClassRoll(int? d10)
    {
        ExtraClassPool = d10 is { } roll ? Math.Clamp(roll - 1, 0, InvestigatorCreationRules.ExtraClassMaxBonus) : null;
        ExtraClassBonus.Clear();
    }

    /// <summary>
    /// Значение характеристики (бросок, вписанное со стола, покупка, выбор из набора). Проверки ОБР бросаются
    /// против текущего ОБР, поэтому правка ОБР делает сделанные проверки недействительными.
    /// </summary>
    public void SetCharacteristic(Characteristic key, int value, string? rollText = null)
    {
        var clamped = Method is CreationMethod.PointBuy
            ? Math.Clamp(value, InvestigatorCreationRules.PointBuyMin, InvestigatorCreationRules.PointBuyMax)
            : Math.Clamp(value, 0, InvestigatorCreationRules.MaxCharacteristic);

        if (clamped <= 0)
            Rolled.Remove(key);
        else
            Rolled[key] = clamped;

        if (rollText is null)
            RollText.Remove(key);
        else
            RollText[key] = rollText;

        if (key is Characteristic.EDU)
            EducationChecks.Clear();
    }

    /// <summary>Стандартный способ: все восемь своими формулами (стр. 28–29).</summary>
    public void RollAll(IDiceRoller dice)
    {
        foreach (var info in InvestigatorCreationRules.Characteristics)
        {
            var roll = InvestigatorCreationRules.Roll(info.Key, dice);
            SetCharacteristic(info.Key, roll.Value, roll.Text);
        }
    }

    /// <summary>
    /// Вариант 3: восемь бросков в набор — первые пять 3d6 × 5, последние три (2d6 + 6) × 5 (стр. 46); раскладка
    /// начинается заново. Порядок не сортируется: по месту в наборе помощник знает, какими костями вписывать.
    /// </summary>
    public void RollPool(IDiceRoller dice)
    {
        Pool = [.. Enumerable.Range(0, 8).Select(i => (i < PoolThreeD6 ? InvestigatorCreationRules.Roll3d6(dice) : InvestigatorCreationRules.Roll2d6Plus6(dice)).Value)];
        Rolled.Clear();
        EducationChecks.Clear();
    }

    /// <summary>
    /// Правка значения набора: если прежнее значение уже разложено по характеристике, раскладка снимается — иначе
    /// на листе осталось бы число, которого в наборе больше нет.
    /// </summary>
    public void SetPoolValue(int index, int value)
    {
        if (index < 0 || index >= Pool.Count)
            return;

        var previous = Pool[index];
        Pool[index] = Math.Clamp(value, 0, InvestigatorCreationRules.MaxCharacteristic);

        if (previous <= 0 || previous == Pool[index])
            return;

        foreach (var (key, assigned) in Rolled.ToList())
        {
            if (assigned != previous)
                continue;

            Rolled.Remove(key);
            if (key is Characteristic.EDU)
                EducationChecks.Clear();
            break;
        }
    }

    /// <summary>Вариант 3: сколько бросков набора — 3d6 (остальные — 2d6 + 6).</summary>
    public const int PoolThreeD6 = 5;

    /// <summary>Значение места набора по сумме костей: первые пять — 3d6 × 5, остальные — (2d6 + 6) × 5.</summary>
    public static int PoolValueFromDice(int index, int sum) => index < PoolThreeD6 ? sum * 5 : (sum + 6) * 5;

    /// <summary>Значения набора (вариант 3 или блиц), которые ещё можно дать характеристике.</summary>
    public List<int> AvailableFor(Characteristic key) =>
        InvestigatorCreationRules.Available(
            Method is CreationMethod.Blitz ? InvestigatorCreationRules.BlitzCharacteristics : Pool,
            Rolled.Where(kv => kv.Key != key).Select(kv => kv.Value));

    /// <summary>Вычет за возраст: ±1 к снятому с характеристики, в пределах строки (стр. 30).</summary>
    public void ChangeAgePenalty(Characteristic key, int delta)
    {
        var band = InvestigatorCreationRules.BandFor(Age);
        if (!band.PenaltyTargets.Contains(key))
            return;

        var next = AgePenaltyDistribution.GetValueOrDefault(key) + delta;
        if (next < 0 || (delta > 0 && (RemainingAgePenalty(band) <= 0 || Value(key, band) <= 1)))
            return;

        AgePenaltyDistribution[key] = next;
    }

    /// <summary>Пункты экстра-класса: ±1 к характеристике, в пределах выпавшего и не выше 99 (стр. 46).</summary>
    public void ChangeExtraClass(Characteristic key, int delta)
    {
        var band = InvestigatorCreationRules.BandFor(Age);
        var next = ExtraClassBonus.GetValueOrDefault(key) + delta;
        if (next < 0 || (delta > 0 && (RemainingExtraClass() <= 0 || Value(key, band) >= InvestigatorCreationRules.MaxCharacteristic)))
            return;

        ExtraClassBonus[key] = next;
    }

    /// <summary>Можно ли бросать следующую проверку улучшения ОБР: все характеристики есть, проверок меньше положенного.</summary>
    [JsonIgnore]
    public bool CanAddEducationCheck =>
        CharacteristicsFilled && EducationChecks.Count < InvestigatorCreationRules.BandFor(Age).EducationChecks;

    /// <summary>
    /// Проверка улучшения ОБР против текущего ОБР (стр. 30) — правилом <see cref="InvestigatorCreationRules.RollEducationCheck"/>.
    /// Вписанные со стола d100 и 1d10 приходят через <see cref="EnteredDiceRoller"/>, невписанное бросает генератор.
    /// </summary>
    public EducationCheck? AddEducationCheck(IDiceRoller dice)
    {
        if (!CanAddEducationCheck)
            return null;

        var check = InvestigatorCreationRules.RollEducationCheck(Value(Characteristic.EDU, InvestigatorCreationRules.BandFor(Age)), dice);
        EducationChecks.Add(check);
        return check;
    }

    /// <summary>Удача из бросков 3d6 × 5: берётся лучший (стр. 30).</summary>
    public void SetLuckRolls(IReadOnlyList<int> rolls)
    {
        LuckRolls = [.. rolls.Where(r => r > 0)];
        Luck = Math.Min(DerivedAttributeRules.MaxLuck, InvestigatorCreationRules.BestLuck(LuckRolls));
    }

    /// <summary>Столько бросков Удачи, сколько положено возрасту (у «Юного» — два).</summary>
    public void RollLuck(IDiceRoller dice) =>
        SetLuckRolls(InvestigatorCreationRules.RollLuck(InvestigatorCreationRules.BandFor(Age), dice));

    private void ResetCharacteristics()
    {
        Rolled.Clear();
        RollText.Clear();
        Pool.Clear();
        AgePenaltyDistribution.Clear();
        ExtraClassBonus.Clear();
        EducationChecks.Clear();
        LuckRolls.Clear();
        Luck = 0;
    }
}
