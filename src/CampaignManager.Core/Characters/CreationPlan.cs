using CampaignManager.Core.Catalogs;

namespace CampaignManager.Core.Characters;

/// <summary>Шаги помощника создания (глава 3) — номер шага в <see cref="InvestigatorDraft.StepIndex"/>.</summary>
public enum CreationStep
{
    Method = 0,
    Characteristics = 1,
    Occupation = 2,
    Skills = 3,
    Biography = 4,
    Gear = 5,
    Summary = 6,
}

/// <summary>
/// Помощник над черновиком: профессия и её слоты, профессиональные и личные навыки, бюджеты очков, база и итог
/// навыка, проверки шагов. Слоты собирает <b>он</b> (<see cref="OccupationRules.BuildSlots"/>), а шаги только
/// показывают: выбор игрока лежит в <see cref="InvestigatorDraft.SlotChoices"/> по индексу слота, и шаг
/// «Навыки» читает тот же список (знание v1: разъедутся индексы — навыки профессии уедут не туда).
/// <para>
/// Ключ навыка помощника (<see cref="KeyOf"/>) — <c>Id</c> справочника строкой; у своей специализации — её
/// уточнение («латынь»), родитель — в <see cref="InvestigatorDraft.AddedSpecializations"/>. Специализация, которую
/// книга называет прямо («Язык, иностранный (латынь)» у Врача) и которой нет в справочнике, попадает туда сама
/// (<see cref="SyncSlots"/>): забыть её — молча потерять профессиональный навык.
/// </para>
/// </summary>
public sealed class CreationPlan
{
    public CreationPlan(InvestigatorDraft draft, SkillCatalog catalog, OccupationDefinition? occupation)
    {
        Draft = draft;
        Catalog = catalog;
        Occupation = occupation;
        Slots = occupation is null ? [] : OccupationRules.BuildSlots(occupation, catalog);
    }

    public InvestigatorDraft Draft { get; }

    public SkillCatalog Catalog { get; }

    public OccupationDefinition? Occupation { get; }

    public IReadOnlyList<OccupationSlotView> Slots { get; }

    public AgeBand Band => InvestigatorCreationRules.BandFor(Draft.Age);

    public Characteristics Characteristics => Draft.BuildCharacteristics();

    public static string KeyOf(Guid skillId) => skillId.ToString("D");

    public static Guid? SkillIdOf(string key) => Guid.TryParse(key, out var id) ? id : null;

    public SkillDefinition? CreditRating => Catalog.FindByCode(SkillCodes.CreditRating);

    public SkillDefinition? Mythos => Catalog.FindByCode(SkillCodes.Mythos);

    // ── Профессия ────────────────────────────────────────────────────────────

    /// <summary>Выбрана профессия: прежние выборы, специализации и пункты к ней не относятся; Средства — минимум профессии.</summary>
    public static void SelectOccupation(InvestigatorDraft draft, OccupationDefinition occupation)
    {
        if (draft.OccupationId == occupation.Id)
            return;

        draft.OccupationId = occupation.Id;
        draft.OccupationName = occupation.Name;
        draft.FormulaChoice = null;
        draft.SlotChoices.Clear();
        draft.AddedSpecializations.Clear();
        draft.OccupationPoints.Clear();
        draft.BlitzValues.Clear();
        draft.CreditRating = occupation.CreditRatingMin;
    }

    /// <summary>
    /// Выборы — по числу слотов; названные книгой специализации вне справочника — в свои специализации черновика.
    /// Зовётся после каждой смены профессии или справочника.
    /// </summary>
    public void SyncSlots()
    {
        while (Draft.SlotChoices.Count < Slots.Count)
            Draft.SlotChoices.Add("");
        if (Draft.SlotChoices.Count > Slots.Count)
            Draft.SlotChoices.RemoveRange(Slots.Count, Draft.SlotChoices.Count - Slots.Count);

        foreach (var slot in Slots)
        {
            if (slot is { Kind: OccupationSlotViewKind.Fixed, SkillId: null, ParentSkillId: { } parent, Specialization: { } name })
                Draft.AddedSpecializations[name] = parent;
        }
    }

    /// <summary>Между чем выбирать в формуле очков профессии (стр. 38–39); пусто — выбора нет.</summary>
    public IReadOnlyList<Characteristic> FormulaChoices =>
        Occupation is null ? [] : OccupationRules.FormulaChoices(Occupation.Formula);

    public int OccupationBudget => Occupation is null
        ? 0
        : OccupationRules.SkillPoints(Occupation.Formula, Characteristics, Draft.FormulaChoice);

    public int PersonalBudget => OccupationRules.PersonalPoints(Characteristics);

    public int CreditMin => Occupation?.CreditRatingMin ?? 0;

    public int CreditMax => Occupation?.CreditRatingMax ?? 99;

    /// <summary>Ключ навыка, закреплённого за слотом: названный навык или выбор игрока; пусто — выбора нет.</summary>
    public string SlotKey(int index)
    {
        if (index < 0 || index >= Slots.Count)
            return "";

        var slot = Slots[index];
        if (slot.SkillId is { } id)
            return KeyOf(id);
        if (slot is { ParentSkillId: not null, Specialization: { } name })
            return name;
        return index < Draft.SlotChoices.Count ? Draft.SlotChoices[index] : "";
    }

    /// <summary>
    /// Профессиональные навыки, уже закреплённые за слотами, без повторов и без Средств (у них свой слот с
    /// диапазоном профессии).
    /// </summary>
    public IReadOnlyList<string> OccupationSkillKeys
    {
        get
        {
            var credit = CreditRating is { } c ? KeyOf(c.Id) : null;
            List<string> keys = [];
            for (var i = 0; i < Slots.Count; i++)
            {
                var key = SlotKey(i);
                if (key.Length > 0 && key != credit && !keys.Contains(key, StringComparer.Ordinal))
                    keys.Add(key);
            }

            return keys;
        }
    }

    public bool IsOccupationSkill(string key) => OccupationSkillKeys.Contains(key, StringComparer.Ordinal);

    /// <summary>
    /// Варианты слота (ключи). «Любой навык» — весь справочник, кроме родителей, Мифов (их пунктами не покупают,
    /// стр. 34) и Средств (свой слот); у соседних слотов одной группы выбора общий пул: уже взятое в соседнем
    /// убирается, иначе повтор сгорел бы — книга просит N разных навыков.
    /// </summary>
    public IReadOnlyList<string> OptionsFor(int index)
    {
        if (index < 0 || index >= Slots.Count)
            return [];

        var slot = Slots[index];
        IEnumerable<string> options = slot.ChoosesFromAllSkills
            ? SkillKeys.Where(CanTakePoints)
            : slot.Options.Select(KeyOf);

        if (slot.ChoiceGroup < 0)
            return [.. options];

        var current = Choice(index);
        var taken = Slots
            .Select((s, i) => (Slot: s, Index: i))
            .Where(p => p.Slot.ChoiceGroup == slot.ChoiceGroup && p.Index != index)
            .Select(p => Choice(p.Index))
            .Where(k => k.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        List<string> result = [.. options.Where(o => !taken.Contains(o) || o == current)];
        // Своя специализация, вписанная в этот слот, — тоже вариант (иначе выбор не показать).
        if (current.Length > 0 && !result.Contains(current, StringComparer.Ordinal))
            result.Add(current);
        return result;
    }

    public string Choice(int index) => index >= 0 && index < Draft.SlotChoices.Count ? Draft.SlotChoices[index] : "";

    /// <summary>Выбор игрока в слоте. Своя специализация, которую больше никто не держит, уходит из черновика.</summary>
    public void SetChoice(int index, string key)
    {
        SyncSlots();
        if (index < 0 || index >= Draft.SlotChoices.Count)
            return;

        var previous = Draft.SlotChoices[index];
        Draft.SlotChoices[index] = key;
        DropUnusedSpecialization(previous);
    }

    /// <summary>
    /// Своя специализация в слоте (стр. 52: навык с широким спектром — любая специализация). Есть в справочнике под
    /// этим родителем — берётся навык справочника. Возвращает ключ; пустое имя — null.
    /// </summary>
    public string? AddSpecialization(int index, Guid parentId, string specialization)
    {
        var name = specialization.Trim();
        if (name.Length == 0 || Catalog.Find(parentId) is not { } parent)
            return null;

        var fullName = $"{parent.Name} ({name})";
        var known = Catalog.Children(parentId)
            .FirstOrDefault(s => string.Equals(s.Name, fullName, StringComparison.OrdinalIgnoreCase));

        string key;
        if (known is not null)
        {
            key = KeyOf(known.Id);
        }
        else
        {
            key = name.ToLowerInvariant();
            Draft.AddedSpecializations[key] = parentId;
        }

        SetChoice(index, key);
        return key;
    }

    // ── Навыки ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Все навыки, куда можно что-то вложить: справочник без родителей (проверяют и развивают специализацию,
    /// стр. 52) и свои специализации черновика. По имени.
    /// </summary>
    public IReadOnlyList<string> SkillKeys =>
    [
        .. Catalog.Skills.Where(s => !Catalog.IsParent(s.Id)).Select(s => KeyOf(s.Id))
            .Concat(Draft.AddedSpecializations.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(NameOf, StringComparer.CurrentCulture),
    ];

    /// <summary>Мифы пунктами навыков не покупают (стр. 34), Средства — из своего диапазона профессии.</summary>
    public bool CanTakePoints(string key) => SkillIdOf(key) is not { } id
                                             || Catalog.CodeOf(id) is not (SkillCodes.Mythos or SkillCodes.CreditRating);

    public string NameOf(string key)
    {
        if (SkillIdOf(key) is { } id)
            return Catalog.Find(id)?.Name ?? key;

        return Draft.AddedSpecializations.TryGetValue(key, out var parent) && Catalog.Find(parent) is { } p
            ? $"{p.Name} ({key})"
            : key;
    }

    /// <summary>
    /// База навыка: справочник (с формулой — <c>DEX/2</c> у Уклонения, <c>EDU</c> у родного языка, стр. 57, 77); у своей
    /// специализации — база первой соседней (<see cref="OccupationRules.NewSpecialization"/>).
    /// </summary>
    public int BaseOf(string key)
    {
        if (SkillIdOf(key) is { } id)
            return Catalog.Find(id) is { } skill ? SkillCatalog.BaseValueOf(skill, Characteristics) : 0;

        return Draft.AddedSpecializations.TryGetValue(key, out var parent)
            ? OccupationRules.NewSpecialization(parent, key, Catalog).Value
            : 0;
    }

    /// <summary>Итог навыка: база плюс вложенное, не выше 99; Средства — значение шага «Навыки».</summary>
    public int Total(string key) => CreditRating is { } credit && key == KeyOf(credit.Id)
        ? Draft.CreditRating
        : Math.Min(InvestigatorCreationRules.MaxCharacteristic, Draft.SkillTotal(key, BaseOf(key)));

    /// <summary>
    /// Пункты в навык: не в минус и не выше лимита начальных навыков, если он включён (стр. 46), — с учётом базы и
    /// пунктов другого вида.
    /// </summary>
    public int ClampPoints(string key, int value, int otherPoints)
    {
        var ceiling = Draft.SkillCap ?? InvestigatorCreationRules.MaxCharacteristic;
        var headroom = ceiling - BaseOf(key) - otherPoints;
        return Math.Max(0, Math.Min(Math.Max(0, value), headroom));
    }

    public void SetOccupationPoints(string key, int value)
    {
        if (!IsOccupationSkill(key))
            return;

        var points = ClampPoints(key, value, Draft.PersonalPoints.GetValueOrDefault(key));
        if (points == 0)
            Draft.OccupationPoints.Remove(key);
        else
            Draft.OccupationPoints[key] = points;
    }

    public void SetPersonalPoints(string key, int value)
    {
        if (!CanTakePoints(key))
            return;

        var points = ClampPoints(key, value, Draft.OccupationPoints.GetValueOrDefault(key));
        if (points == 0)
            Draft.PersonalPoints.Remove(key);
        else
            Draft.PersonalPoints[key] = points;
    }

    public void SetCreditRating(int value) => Draft.CreditRating = Math.Clamp(value, CreditMin, CreditMax);

    public void ResetPoints()
    {
        Draft.OccupationPoints.Clear();
        Draft.PersonalPoints.Clear();
        Draft.BlitzValues.Clear();
        Draft.BlitzPersonalSkills.Clear();
        Draft.CreditRating = CreditMin;
    }

    // ── Блиц (вариант 5, стр. 46) ───────────────────────────────────────────

    /// <summary>Значения блиц-набора, ещё свободные для навыка (тот же алгоритм, что у характеристик).</summary>
    public List<int> BlitzAvailableFor(string key) =>
        InvestigatorCreationRules.Available(
            InvestigatorCreationRules.BlitzSkillValues,
            Draft.BlitzValues.Where(kv => kv.Key != key).Select(kv => kv.Value));

    private string CreditKey => CreditRating is { } c ? KeyOf(c.Id) : "";

    /// <summary>
    /// Блиц: навык профессии получает ровно выбранное значение — «не обращайте внимания на базовые значения»; навык,
    /// чья база и так выше, остаётся на базе. Средства — одно из девяти значений (стр. 46), в пределах профессии.
    /// </summary>
    public void SetBlitzValue(string key, int value)
    {
        if (value <= 0)
        {
            Draft.BlitzValues.Remove(key);
            Draft.OccupationPoints.Remove(key);
            if (key == CreditKey)
                Draft.CreditRating = CreditMin;
            return;
        }

        Draft.BlitzValues[key] = value;
        if (key == CreditKey)
            Draft.CreditRating = Math.Clamp(value, CreditMin, CreditMax);
        else
            Draft.OccupationPoints[key] = Math.Max(0, value - BaseOf(key));
    }

    /// <summary>Блиц: четыре личных навыка по +20 к базе (стр. 46).</summary>
    public void SetBlitzPersonal(int index, string key)
    {
        if (index < 0 || index >= InvestigatorCreationRules.BlitzPersonalSkillCount)
            return;

        while (Draft.BlitzPersonalSkills.Count <= index)
            Draft.BlitzPersonalSkills.Add("");

        var previous = Draft.BlitzPersonalSkills[index];
        if (previous.Length > 0 && Draft.BlitzPersonalSkills.Count(k => k == previous) == 1)
            Draft.PersonalPoints.Remove(previous);

        Draft.BlitzPersonalSkills[index] = key;
        if (key.Length > 0 && CanTakePoints(key))
            Draft.PersonalPoints[key] = InvestigatorCreationRules.BlitzPersonalSkillBonus;
    }

    /// <summary>Навыки, которым блиц раздаёт значения: восемь профессиональных и Средства.</summary>
    public IReadOnlyList<string> BlitzSkillKeys =>
        CreditRating is { } credit ? [.. OccupationSkillKeys, KeyOf(credit.Id)] : OccupationSkillKeys;

    // ── Проверки шагов ───────────────────────────────────────────────────────

    /// <summary>Что мешает уйти с шага; null — шаг заполнен. Вперёд по шагам — только через эту проверку.</summary>
    public string? Validate(CreationStep step) => step switch
    {
        CreationStep.Method => InvestigatorCreationRules.IsBookAge(Draft.Age)
            ? null
            : $"Возраст сыщика — от {InvestigatorCreationRules.MinAge} до {InvestigatorCreationRules.MaxAge} лет",
        CreationStep.Characteristics => ValidateCharacteristics(),
        CreationStep.Occupation => ValidateOccupation(),
        CreationStep.Skills => ValidateSkills(),
        CreationStep.Biography => string.IsNullOrWhiteSpace(Draft.Personal.Name) ? "Впишите имя сыщика" : null,
        _ => null,
    };

    /// <summary>Первый шаг, который не проходит проверку; null — все готовы.</summary>
    public CreationStep? FirstInvalidStep() =>
        Enum.GetValues<CreationStep>().Where(s => s < CreationStep.Summary).Select(s => (CreationStep?)s)
            .FirstOrDefault(s => Validate(s!.Value) is not null);

    private string? ValidateCharacteristics()
    {
        var band = Band;
        if (!Draft.CharacteristicsFilled)
            return "Определите все восемь характеристик";

        if (Draft.Method is CreationMethod.PointBuy && Draft.Rolled.Values.Sum() is var spent
                                                     && spent != InvestigatorCreationRules.PointBuyBudget)
            return $"Распределите ровно {InvestigatorCreationRules.PointBuyBudget} пунктов (сейчас {spent})";

        if (Draft.RemainingAgePenalty(band) != 0)
            return $"Распределите вычет за возраст: осталось {Draft.RemainingAgePenalty(band)}";

        if (Draft.ExtraClass && Draft.ExtraClassPool is null)
            return "Бросьте 1d10 для сыщика экстра-класса";

        if (Draft.RemainingExtraClass() != 0)
            return $"Распределите пункты сыщика экстра-класса: осталось {Draft.RemainingExtraClass()}";

        if (Draft.EducationChecks.Count != band.EducationChecks)
            return $"Выполните проверки улучшения ОБР: {Draft.EducationChecks.Count} из {band.EducationChecks}";

        return Draft.Luck <= 0 ? "Определите Удачу (3d6 × 5)" : null;
    }

    private string? ValidateOccupation()
    {
        if (Occupation is null)
            return "Выберите род занятий";

        if (FormulaChoices.Count > 0 && (Draft.FormulaChoice is not { } choice || !FormulaChoices.Contains(choice)))
            return "Выберите характеристику в формуле очков профессии";

        for (var i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].NeedsChoice && Choice(i).Length == 0)
                return $"Выберите навык для слота «{Slots[i].Label}»";
        }

        return null;
    }

    private string? ValidateSkills()
    {
        if (Occupation is null)
            return "Выберите род занятий";

        if (Draft.CreditRating < CreditMin || Draft.CreditRating > CreditMax)
            return $"Средства должны быть в пределах профессии: {CreditMin}–{CreditMax}%";

        if (Draft.Method is CreationMethod.Blitz)
        {
            var missing = BlitzSkillKeys.Count(k => !Draft.BlitzValues.ContainsKey(k));
            if (missing > 0)
                return $"Раздайте блиц-значения профессиональным навыкам и Средствам: осталось {missing}";

            var chosen = Draft.BlitzPersonalSkills.Count(k => k.Length > 0);
            return chosen < InvestigatorCreationRules.BlitzPersonalSkillCount
                ? $"Выберите личные навыки: {chosen} из {InvestigatorCreationRules.BlitzPersonalSkillCount}"
                : null;
        }

        if (Draft.SpentOccupationPoints > OccupationBudget)
            return $"Очков профессии потрачено больше, чем есть: {Draft.SpentOccupationPoints} из {OccupationBudget}";

        return Draft.SpentPersonalPoints > PersonalBudget
            ? $"Очков личного интереса потрачено больше, чем есть: {Draft.SpentPersonalPoints} из {PersonalBudget}"
            : null;
    }

    private void DropUnusedSpecialization(string? key)
    {
        if (string.IsNullOrEmpty(key) || !Draft.AddedSpecializations.ContainsKey(key))
            return;

        // Названную книгой специализацию держит слот Fixed — в выборах её нет, но с листа она пропасть не должна.
        if (Slots.Any(s => s is { SkillId: null, Specialization: { } name } && name == key))
            return;

        if (!Draft.SlotChoices.Contains(key, StringComparer.Ordinal))
        {
            Draft.AddedSpecializations.Remove(key);
            Draft.OccupationPoints.Remove(key);
            Draft.PersonalPoints.Remove(key);
        }
    }
}
