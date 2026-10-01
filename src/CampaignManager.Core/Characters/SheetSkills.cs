namespace CampaignManager.Core.Characters;

/// <summary>
/// Навыки листа через справочник: значение навыка по коду, полное имя строки, родитель. Единственная
/// замена шести функций поиска v1 (<c>FindMythosSkill</c>, <c>FindCreditRatingSkill</c>,
/// <c>FindDodgeSkill</c>…) и 25 копий <c>SelectMany(g =&gt; g.Skills)</c>.
/// </summary>
public static class SheetSkills
{
    /// <summary>Строка листа для навыка справочника с этим кодом; null — на листе её нет.</summary>
    public static SheetSkill? Entry(this CharacterSheet sheet, SkillCatalog catalog, string code) =>
        catalog.FindByCode(code) is { } skill ? sheet.Entry(skill.Id) : null;

    /// <summary>Строка листа для навыка справочника.</summary>
    public static SheetSkill? Entry(this CharacterSheet sheet, Guid skillId) =>
        sheet.Skills.FirstOrDefault(s => s.SkillId == skillId);

    /// <summary>
    /// Значение навыка с этим кодом: строка листа, а если строки нет — база из справочника (навык, в
    /// который ничего не вкладывали). Нет и в справочнике — 0.
    /// </summary>
    public static int Value(this CharacterSheet sheet, SkillCatalog catalog, string code)
    {
        var skill = catalog.FindByCode(code);
        if (skill is null)
            return 0;

        return sheet.Entry(skill.Id)?.Value ?? SkillCatalog.BaseValueOf(skill, sheet.Characteristics);
    }

    /// <summary>
    /// Строка листа для навыка с этим кодом; если её нет, а навык есть в справочнике, — заводится с базовым
    /// значением. Null — навыка нет и в справочнике: записать некуда.
    /// </summary>
    public static SheetSkill? EnsureEntry(this CharacterSheet sheet, SkillCatalog catalog, string code)
    {
        var skill = catalog.FindByCode(code);
        if (skill is null)
            return null;

        if (sheet.Entry(skill.Id) is { } existing)
            return existing;

        var created = new SheetSkill
        {
            SkillId = skill.Id,
            Value = SkillCatalog.BaseValueOf(skill, sheet.Characteristics),
        };
        sheet.Skills.Add(created);
        return created;
    }

    /// <summary>Код справочника у строки (null у своего навыка и у специализации вне справочника).</summary>
    public static string? CodeOf(this SheetSkill skill, SkillCatalog catalog) => catalog.CodeOf(skill.SkillId);

    /// <summary>Родитель строки: у специализации из справочника — его родитель, у своей — <see cref="SheetSkill.ParentSkillId"/>.</summary>
    public static Guid? ParentOf(this SheetSkill skill, SkillCatalog catalog) =>
        catalog.Find(skill.SkillId)?.ParentId ?? skill.ParentSkillId;

    /// <summary>
    /// Имя строки для показа: имя справочника; у специализации вне справочника — «Родитель (уточнение)»;
    /// у своего навыка — его имя.
    /// </summary>
    public static string DisplayName(this SheetSkill skill, SkillCatalog catalog)
    {
        if (catalog.Find(skill.SkillId) is { } known)
            return known.Name;

        if (catalog.Find(skill.ParentSkillId) is { } parent && !string.IsNullOrWhiteSpace(skill.Name))
            return $"{parent.Name} ({skill.Name.Trim()})";

        return skill.Name ?? "";
    }
}
