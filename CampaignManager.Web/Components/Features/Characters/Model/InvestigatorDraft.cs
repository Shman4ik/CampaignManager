namespace CampaignManager.Web.Components.Features.Characters.Model;

/// <summary>
///     Способ создания сыщика: стандартный и варианты 3–5 из «Другие способы создания сыщиков»
///     («Зов Ктулху» 7e, стр. 45–46).
/// </summary>
public enum CreationMethod
{
    /// <summary>Стандартный: каждая характеристика бросается своей формулой.</summary>
    Standard,

    /// <summary>Вариант 3: восемь бросков раскладывают по характеристикам как захочется.</summary>
    AssignRolls,

    /// <summary>Вариант 4: 460 пунктов распределяются между восемью характеристиками.</summary>
    PointBuy,

    /// <summary>Вариант 5, блиц-метод: готовые значения характеристик и навыков.</summary>
    Blitz
}

/// <summary>Запись о проверке улучшения ОБР — показываем игроку, что именно выпало.</summary>
public sealed class EducationCheckEntry
{
    public int Roll { get; set; }
    public int Before { get; set; }
    public int Gain { get; set; }
}

/// <summary>
///     Состояние помощника создания сыщика — всё, что игрок успел выбрать и набросать.
///     Обычный POCO без ссылок на EF: черновик переживает паузу circuit'а как
///     <c>[PersistentState]</c> и потому обязан сериализоваться в JSON.
/// </summary>
public sealed class InvestigatorDraft
{
    public int StepIndex { get; set; }

    public CreationMethod Method { get; set; } = CreationMethod.Standard;

    /// <summary>Современная эпоха: от неё зависят столбец таблицы «Наличные и активы» и состав навыков.</summary>
    public bool ModernEra { get; set; }

    public int Age { get; set; } = 25;

    /// <summary>Необязательное правило «лимит начальных значений навыков» (стр. 46). null — без лимита.</summary>
    public int? SkillCap { get; set; }

    // ── Шаг 1: характеристики ───────────────────────────────────────────────

    /// <summary>Выпавшие значения до возрастных модификаторов.</summary>
    public Dictionary<CharacteristicKey, int> Rolled { get; set; } = new();

    /// <summary>Вариант 3: набор бросков, ещё не разложенный по характеристикам.</summary>
    public List<int> Pool { get; set; } = [];

    /// <summary>Текст бросков для показа игроку: «(4 + 3 + 6) × 5 = 65».</summary>
    public Dictionary<CharacteristicKey, string> RollText { get; set; } = new();

    /// <summary>Сколько пунктов возрастного вычета игрок снял с каждой характеристики.</summary>
    public Dictionary<CharacteristicKey, int> AgePenaltyDistribution { get; set; } = new();

    /// <summary>Вариант 6 «сыщики экстра-класса»: куда игрок вложил выпавшие 0–9 пунктов.</summary>
    public Dictionary<CharacteristicKey, int> ExtraClassBonus { get; set; } = new();

    /// <summary>Вариант 6 включён — бросок 1d10 делается на шаге характеристик.</summary>
    public bool ExtraClass { get; set; }

    /// <summary>Выпавшие 0–9 пунктов варианта 6; null — ещё не бросали.</summary>
    public int? ExtraClassPool { get; set; }

    public List<EducationCheckEntry> EducationChecks { get; set; } = [];

    public List<int> LuckRolls { get; set; } = [];

    public int Luck { get; set; }

    // ── Шаг 2: род занятий ──────────────────────────────────────────────────

    public Guid? OccupationId { get; set; }

    public string OccupationName { get; set; } = "";

    /// <summary>
    ///     Характеристика, выбранная в формуле очков профессии, когда формула даёт выбор
    ///     («ОБР × 2 + ЛВК × 2 или ОБР × 2 + СИЛ × 2»). null — выбора нет.
    /// </summary>
    public CharacteristicKey? FormulaChoice { get; set; }

    /// <summary>Навыки, выбранные по слотам профессии; индекс совпадает с порядком слотов.</summary>
    public List<string> SlotChoices { get; set; } = [];

    // ── Шаг 3: навыки ───────────────────────────────────────────────────────

    /// <summary>Вложено очков профессии, по названию навыка.</summary>
    public Dictionary<string, int> OccupationPoints { get; set; } = new();

    /// <summary>Вложено очков личного интереса, по названию навыка.</summary>
    public Dictionary<string, int> PersonalPoints { get; set; } = new();

    /// <summary>Значение навыка Средства: пункты в него идут из очков профессии (стр. 34).</summary>
    public int CreditRating { get; set; }

    /// <summary>
    ///     Блиц-метод: какое из готовых значений (70/60/60/50/50/50/40/40/40) получил каждый
    ///     профессиональный навык. Из этой раскладки считаются <see cref="OccupationPoints" />
    ///     и <see cref="CreditRating" /> — она же не даёт занять одно значение дважды.
    /// </summary>
    public Dictionary<string, int> BlitzValues { get; set; } = new();

    /// <summary>Блиц-метод: четыре личных навыка, получающих по +20 (стр. 46).</summary>
    public List<string> BlitzPersonalSkills { get; set; } = [];

    /// <summary>Специализации, добавленные на лист сверх справочника: имя → родительский навык.</summary>
    public Dictionary<string, string> AddedSpecializations { get; set; } = new();

    // ── Шаг 4: биография и личные данные ────────────────────────────────────

    public PersonalInfo Info { get; set; } = new();

    public BiographyInfo Biography { get; set; } = new();

    /// <summary>
    ///     Ключ графы биографии, отмеченной как ключевая связь (стр. 43). Хранится ключ, а не текст:
    ///     графу правят и после того, как её отметили, а связь при этом никуда не девается.
    /// </summary>
    public string KeyConnectionSection { get; set; } = "";

    // ── Шаг 5: снаряжение ───────────────────────────────────────────────────

    public List<EquipmentItem> Equipment { get; set; } = [];

    /// <summary>Итоговое значение характеристики: бросок минус возрастной вычет плюс улучшения ОБР.</summary>
    public int Value(CharacteristicKey key, AgeBand band)
    {
        var value = Rolled.GetValueOrDefault(key);
        value -= AgePenaltyDistribution.GetValueOrDefault(key);
        value += ExtraClassBonus.GetValueOrDefault(key);

        if (key is CharacteristicKey.Appearance)
            value -= band.AppearancePenalty;

        if (key is CharacteristicKey.Education)
            value += EducationChecks.Sum(c => c.Gain) - band.EducationPenalty;

        return Math.Clamp(value, 1, 99);
    }

    /// <summary>Сколько пунктов возрастного вычета игрок ещё не распределил.</summary>
    public int RemainingAgePenalty(AgeBand band) =>
        band.DistributedPenalty - band.PenaltyTargets.Sum(k => AgePenaltyDistribution.GetValueOrDefault(k));

    public int RemainingExtraClass() =>
        (ExtraClassPool ?? 0) - ExtraClassBonus.Values.Sum();

    /// <summary>Все броски характеристик сделаны — шаг можно считать заполненным.</summary>
    public bool CharacteristicsFilled =>
        InvestigatorCreationKeys.All(k => Rolled.GetValueOrDefault(k) > 0);

    /// <summary>Порядок характеристик на листе — держим здесь, чтобы модель не звала правила.</summary>
    private static IEnumerable<CharacteristicKey> InvestigatorCreationKeys =>
        Enum.GetValues<CharacteristicKey>();

    /// <summary>Сколько очков профессии уже вложено, включая Средства.</summary>
    public int SpentOccupationPoints => OccupationPoints.Values.Sum() + CreditRating;

    public int SpentPersonalPoints => PersonalPoints.Values.Sum();

    /// <summary>Итоговое значение навыка: база из справочника плюс вложенные пункты.</summary>
    public int SkillTotal(string skillName, int baseValue) =>
        baseValue + OccupationPoints.GetValueOrDefault(skillName) + PersonalPoints.GetValueOrDefault(skillName);
}
