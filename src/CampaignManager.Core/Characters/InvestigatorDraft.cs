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
/// Навыки в словарях — по <b>ключу навыка помощника</b>: <c>Id</c> справочника строкой, а для своей
/// специализации — её имя. Как именно помощник раскладывает слоты, решает T2.4; здесь — арифметика
/// главы 3, которую v1 держал в этом же классе.
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

    /// <summary>Свои специализации сверх справочника: имя → Id родителя.</summary>
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
    public bool CharacteristicsFilled => Enum.GetValues<Characteristic>().All(k => Rolled.GetValueOrDefault(k) > 0);

    /// <summary>Вложено очков профессии, включая Средства.</summary>
    public int SpentOccupationPoints => OccupationPoints.Values.Sum() + CreditRating;

    public int SpentPersonalPoints => PersonalPoints.Values.Sum();

    /// <summary>Значение навыка: база плюс вложенное.</summary>
    public int SkillTotal(string skillKey, int baseValue) =>
        baseValue + OccupationPoints.GetValueOrDefault(skillKey) + PersonalPoints.GetValueOrDefault(skillKey);
}
