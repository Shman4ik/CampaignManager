using CampaignManager.Core.Catalogs;

namespace CampaignManager.Core.Characters;

/// <summary>Профессия справочника — то, что нужно помощнику (<c>cm.occupations</c> со слотами).</summary>
public sealed record OccupationDefinition(Guid Id, string Name, SkillPointsFormula Formula)
{
    public int CreditRatingMin { get; init; }

    public int CreditRatingMax { get; init; }

    /// <summary>Слоты в порядке <c>ord</c>.</summary>
    public IReadOnlyList<OccupationSlotDefinition> Slots { get; init; } = [];
}

/// <summary>
/// Слот профессии, как он лежит в <c>cm.occupation_slots</c>: навык, названная специализация, любая
/// специализация, выбор N из вариантов, социальный, любой.
/// </summary>
public sealed record OccupationSlotDefinition(OccupationSlotKind Kind)
{
    /// <summary>Skill — навык; Specialization и AnySpecialization — родитель.</summary>
    public Guid? SkillId { get; init; }

    /// <summary>Specialization: «латынь», «черчение».</summary>
    public string? Specialization { get; init; }

    /// <summary>Choice: сколько навыков берёт игрок.</summary>
    public int ChooseCount { get; init; } = 1;

    /// <summary>Choice: варианты; родитель означает любую его специализацию.</summary>
    public IReadOnlyList<Guid> Options { get; init; } = [];
}

/// <summary>Что слот предлагает игроку в помощнике.</summary>
public enum OccupationSlotViewKind
{
    /// <summary>Навык известен заранее: «Внимание» или названная книгой специализация.</summary>
    Fixed,

    /// <summary>Любая специализация навыка с широким спектром (стр. 52).</summary>
    Specialization,

    /// <summary>Выбор из перечисленного книгой.</summary>
    Choice,

    /// <summary>Один из четырёх социальных навыков (стр. 38).</summary>
    Social,

    /// <summary>«И ещё один любой навык».</summary>
    Any,

    /// <summary>Средства — всегда последним, пункты из той же суммы в пределах диапазона профессии.</summary>
    CreditRating,
}

/// <summary>
/// Слот профессии для помощника. Навык — <see cref="SkillId"/>; у названной специализации, которой нет в
/// справочнике, — <see cref="ParentSkillId"/> и <see cref="Specialization"/>: на лист она попадает новой
/// строкой (<see cref="OccupationRules.NewSpecialization"/>), иначе профессиональный навык пропадёт.
/// </summary>
public sealed record OccupationSlotView(OccupationSlotViewKind Kind, string Label)
{
    public Guid? SkillId { get; init; }

    public Guid? ParentSkillId { get; init; }

    public string? Specialization { get; init; }

    /// <summary>Из чего выбирать (Id справочника); пусто у «любого навыка» — выбор из всех.</summary>
    public IReadOnlyList<Guid> Options { get; init; } = [];

    /// <summary>Родители, для которых игрок вправе вписать свою специализацию (стр. 52).</summary>
    public IReadOnlyList<Guid> CustomParents { get; init; } = [];

    /// <summary>Пояснение под первым слотом группы: «Выбор из: …».</summary>
    public string? Hint { get; init; }

    /// <summary>
    /// Номер группы выбора, из которой вырос слот, или −1. Соседи по группе делят пул: уже выбранное в
    /// одном слоте убирается из других, иначе повтор сгорел бы (книга просит «четыре специализации»).
    /// </summary>
    public int ChoiceGroup { get; init; } = -1;

    public bool NeedsChoice => Kind is not (OccupationSlotViewKind.Fixed or OccupationSlotViewKind.CreditRating);

    public bool AllowsCustomName => CustomParents.Count > 0;

    public bool ChoosesFromAllSkills => Kind is OccupationSlotViewKind.Any;
}

/// <summary>
/// Профессия в помощнике (стр. 31, 34, 37–39): очки навыков по формуле и слоты. В v1 слоты собирались
/// из строковых имён, и вариант, которого не было в справочнике, молча выпадал (rules-findings F-S07);
/// в 2.0 варианты — FK на справочник, а строки v1 раскладывает перенос (T1.3).
/// </summary>
public static class OccupationRules
{
    /// <summary>Профессиональных навыков всегда ровно столько, плюс Средства (стр. 37).</summary>
    public const int RequiredSkillCount = 8;

    /// <summary>
    /// Очки профессии. Если формула даёт выбор, берётся выбранная игроком характеристика — книга
    /// предлагает выбор, а не максимум (стр. 38–39); без выбора — большая из вариантов.
    /// </summary>
    public static int SkillPoints(SkillPointsFormula formula, Characteristics c, Characteristic? choice = null)
    {
        if (choice is { } key)
            return c.Edu * 2 + c[key] * 2;

        return formula switch
        {
            SkillPointsFormula.Edu2Dex2 => c.Edu * 2 + c.Dex * 2,
            SkillPointsFormula.Edu2App2 => c.Edu * 2 + c.App * 2,
            SkillPointsFormula.Edu2Str2 => c.Edu * 2 + c.Str * 2,
            SkillPointsFormula.Edu2Pow2 => c.Edu * 2 + c.Pow * 2,
            SkillPointsFormula.Edu2DexOrStr2 => c.Edu * 2 + Math.Max(c.Dex, c.Str) * 2,
            SkillPointsFormula.Edu2AppOrPow2 => c.Edu * 2 + Math.Max(c.App, c.Pow) * 2,
            SkillPointsFormula.Edu2DexOrPow2 => c.Edu * 2 + Math.Max(c.Dex, c.Pow) * 2,
            SkillPointsFormula.Edu2AppOrDexOrStr2 => c.Edu * 2 + Math.Max(c.App, Math.Max(c.Dex, c.Str)) * 2,
            _ => c.Edu * 4,
        };
    }

    /// <summary>Между чем формула предлагает выбрать (стр. 38–39).</summary>
    public static IReadOnlyList<Characteristic> FormulaChoices(SkillPointsFormula formula) => formula switch
    {
        SkillPointsFormula.Edu2DexOrStr2 => [Characteristic.DEX, Characteristic.STR],
        SkillPointsFormula.Edu2AppOrPow2 => [Characteristic.APP, Characteristic.POW],
        SkillPointsFormula.Edu2DexOrPow2 => [Characteristic.DEX, Characteristic.POW],
        SkillPointsFormula.Edu2AppOrDexOrStr2 => [Characteristic.APP, Characteristic.DEX, Characteristic.STR],
        _ => [],
    };

    /// <summary>Очки личного интереса: ИНТ × 2 (стр. 34).</summary>
    public static int PersonalPoints(Characteristics c) => c.Int * 2;

    /// <summary>
    /// Сколько профессиональных навыков даёт профессия: названные (кроме Средств и Мифов) плюс все
    /// выборы, социальные и свободные слоты. По книге — ровно <see cref="RequiredSkillCount"/>.
    /// </summary>
    public static int ProfessionalSkillCount(OccupationDefinition occupation, SkillCatalog catalog) =>
        occupation.Slots.Sum(slot => slot.Kind switch
        {
            OccupationSlotKind.Choice => slot.ChooseCount,
            OccupationSlotKind.Skill when IsNotProfessional(catalog.CodeOf(slot.SkillId)) => 0,
            _ => 1,
        });

    /// <summary>
    /// Слоты в порядке показа: названные навыки, выборы, социальные, свободные; Средства — всегда
    /// последними и один раз. Мифы пунктами навыков не покупают (стр. 34) и в слоты не попадают.
    /// </summary>
    public static List<OccupationSlotView> BuildSlots(OccupationDefinition occupation, SkillCatalog catalog)
    {
        List<OccupationSlotView> named = [];
        List<OccupationSlotView> choices = [];
        List<OccupationSlotView> social = [];
        List<OccupationSlotView> free = [];
        var choiceGroup = 0;

        foreach (var slot in occupation.Slots)
        {
            switch (slot.Kind)
            {
                case OccupationSlotKind.Skill:
                    if (catalog.Find(slot.SkillId) is not { } skill || IsNotProfessional(skill.Code))
                        break;
                    named.Add(catalog.IsParent(skill.Id) ? AnySpecialization(skill, catalog) : FixedSlot(skill));
                    break;

                case OccupationSlotKind.AnySpecialization when catalog.Find(slot.SkillId) is { } parent:
                    named.Add(AnySpecialization(parent, catalog));
                    break;

                case OccupationSlotKind.Specialization when catalog.Find(slot.SkillId) is { } parent:
                    named.Add(NamedSpecialization(parent, slot.Specialization ?? "", catalog));
                    break;

                case OccupationSlotKind.Choice:
                    choices.AddRange(ChoiceSlots(slot, choiceGroup++, catalog));
                    break;

                case OccupationSlotKind.Social:
                    social.Add(new OccupationSlotView(OccupationSlotViewKind.Social, "Социальный навык")
                    {
                        Options = SocialSkillIds(catalog),
                    });
                    break;

                case OccupationSlotKind.Free:
                    free.Add(new OccupationSlotView(OccupationSlotViewKind.Any, "Любой навык на выбор"));
                    break;
            }
        }

        var creditRating = catalog.FindByCode(SkillCodes.CreditRating);
        var credit = new OccupationSlotView(OccupationSlotViewKind.CreditRating, creditRating?.Name ?? "Средства")
        {
            SkillId = creditRating?.Id,
        };

        return [.. named, .. choices, .. social, .. free, credit];
    }

    /// <summary>
    /// Новая специализация для листа, которой ещё нет: база — <see cref="SkillCatalog.SpecializationBase"/> (общая у соседних,
    /// иначе у первой соседней; соседних нет — 1%, стр. 52).
    /// </summary>
    public static SheetSkill NewSpecialization(Guid parentId, string specialization, SkillCatalog catalog) => new()
    {
        ParentSkillId = parentId,
        Name = specialization.Trim(),
        Value = catalog.SpecializationBase(parentId).Value,
    };

    private static bool IsNotProfessional(string? code) => code is SkillCodes.CreditRating or SkillCodes.Mythos;

    private static OccupationSlotView FixedSlot(SkillDefinition skill) =>
        new(OccupationSlotViewKind.Fixed, skill.Name) { SkillId = skill.Id };

    private static OccupationSlotView AnySpecialization(SkillDefinition parent, SkillCatalog catalog) =>
        new(OccupationSlotViewKind.Specialization, $"{parent.Name} (любая специализация)")
        {
            ParentSkillId = parent.Id,
            Options = SortedChildren(parent.Id, catalog),
            CustomParents = [parent.Id],
        };

    /// <summary>
    /// «Язык, иностранный (латынь)» у Врача: специализация названа прямо. Есть в справочнике — обычный
    /// навык, нет — навык с родителем, который помощник добавит на лист.
    /// </summary>
    private static OccupationSlotView NamedSpecialization(SkillDefinition parent, string specialization, SkillCatalog catalog)
    {
        var fullName = $"{parent.Name} ({specialization.Trim()})";
        if (catalog.Children(parent.Id).FirstOrDefault(s =>
                string.Equals(s.Name, fullName, StringComparison.OrdinalIgnoreCase)) is { } known)
            return FixedSlot(known);

        return new OccupationSlotView(OccupationSlotViewKind.Fixed, fullName)
        {
            ParentSkillId = parent.Id,
            Specialization = specialization.Trim(),
        };
    }

    /// <summary>
    /// «Выбрать N из перечисленного». Вариант-родитель означает любую его специализацию: он
    /// разворачивается в специализации и даёт право вписать свою. Подсказка — у первого слота группы.
    /// </summary>
    private static IEnumerable<OccupationSlotView> ChoiceSlots(OccupationSlotDefinition slot, int group, SkillCatalog catalog)
    {
        List<Guid> options = [];
        List<Guid> customParents = [];
        List<string> names = [];

        foreach (var option in slot.Options.Select(id => catalog.Find(id)).OfType<SkillDefinition>())
        {
            names.Add(option.Name);
            if (catalog.IsParent(option.Id))
            {
                customParents.Add(option.Id);
                options.AddRange(SortedChildren(option.Id, catalog).Where(id => !options.Contains(id)));
            }
            else if (!options.Contains(option.Id))
            {
                options.Add(option.Id);
            }
        }

        var count = Math.Max(1, slot.ChooseCount);
        for (var i = 0; i < count; i++)
        {
            var label = count == 1 ? string.Join(" либо ", names) : $"Навык {i + 1} из {count} по выбору";
            yield return new OccupationSlotView(OccupationSlotViewKind.Choice, label)
            {
                Options = options,
                CustomParents = customParents,
                ChoiceGroup = group,
                Hint = count > 1 && i == 0 ? $"Выбор из: {string.Join(" / ", names)}" : null,
            };
        }
    }

    private static List<Guid> SortedChildren(Guid parentId, SkillCatalog catalog) =>
        catalog.Children(parentId).OrderBy(s => s.Name, StringComparer.Ordinal).Select(s => s.Id).ToList();

    private static List<Guid> SocialSkillIds(SkillCatalog catalog) =>
        SkillCodes.Social.Select(catalog.FindByCode).OfType<SkillDefinition>().Select(s => s.Id).ToList();
}
