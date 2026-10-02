using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Scenarios;

namespace CampaignManager.Core.Checks;

/// <summary>Навыки одной группы бланка для выбора цели: «Сбор информации» — Внимание, Слух…</summary>
public sealed record CheckSubjectGroup(string Title, IReadOnlyList<CheckSubject> Subjects);

/// <summary>
/// Цели проверки, снятые с листа сыщика: восемь характеристик, Удача и навыки. Рассудка здесь нет
/// намеренно — у его проверки свои правила (без костей, без Удачи, без повтора) и своя панель листа.
/// <para>
/// Навыки — строки листа плюс навыки справочника, которых на листе нет: в них ничего не вкладывали, и
/// проверяются они по базе (<see cref="SkillCatalog.BaseValueOf"/>), как на бумажном бланке. Навык-родитель
/// («Стрельба») целью не бывает — проверяют специализацию (стр. 52).
/// </para>
/// </summary>
public static class CheckSubjects
{
    public const string LuckName = "Удача";

    /// <summary>Подпись группы своих навыков — тех, что Хранитель или игрок завёл сам.</summary>
    public const string OwnSkillsTitle = "Свои навыки";

    /// <summary>Восемь характеристик в порядке бланка; подпись — сокращение («СИЛ»).</summary>
    public static IReadOnlyList<CheckSubject> Characteristics(CharacterSheet sheet) =>
        InvestigatorCreationRules.Characteristics.Select(info => Characteristic(sheet, info.Key)).ToList();

    public static CheckSubject Characteristic(CharacterSheet sheet, Characteristic characteristic) =>
        new(CheckSubjectKind.Characteristic, InvestigatorCreationRules.Info(characteristic).Abbreviation,
            sheet.Characteristics[characteristic])
        {
            Characteristic = characteristic,
        };

    public static CheckSubject Luck(CharacterSheet sheet) => new(CheckSubjectKind.Luck, LuckName, sheet.Current.Luck);

    /// <summary>Цель по строке листа.</summary>
    public static CheckSubject Skill(SkillCatalog catalog, SheetSkill skill)
    {
        var definition = catalog.Find(skill.SkillId);
        var parentId = skill.ParentOf(catalog);
        return new CheckSubject(CheckSubjectKind.Skill, skill.DisplayName(catalog), skill.Value)
        {
            SkillId = definition?.Id,
            SkillCode = definition?.Code,
            ParentSkillCode = catalog.CodeOf(parentId),
        };
    }

    /// <summary>
    /// Навык справочника на этом листе: строка листа, а если её нет — база справочника. Null — навыка нет в
    /// справочнике.
    /// </summary>
    public static CheckSubject? CatalogSkill(CharacterSheet sheet, SkillCatalog catalog, Guid skillId)
    {
        if (sheet.Entry(skillId) is { } entry)
            return Skill(catalog, entry);

        if (catalog.Find(skillId) is not { } definition)
            return null;

        return new CheckSubject(CheckSubjectKind.Skill, definition.Name,
            SkillCatalog.BaseValueOf(definition, sheet.Characteristics))
        {
            SkillId = definition.Id,
            SkillCode = definition.Code,
            ParentSkillCode = catalog.CodeOf(definition.ParentId),
        };
    }

    /// <summary>
    /// Все навыки листа для выбора, по группам бланка в порядке справочника; свои навыки — последней
    /// группой. Внутри группы — по имени.
    /// </summary>
    public static IReadOnlyList<CheckSubjectGroup> SkillGroups(CharacterSheet sheet, SkillCatalog catalog)
    {
        List<(SkillCategory? Category, CheckSubject Subject)> all = [];

        foreach (var definition in catalog.Skills)
        {
            if (catalog.IsParent(definition.Id))
                continue;

            if (CatalogSkill(sheet, catalog, definition.Id) is { } subject)
                all.Add((definition.Category, subject));
        }

        foreach (var row in sheet.Skills.Where(s => catalog.Find(s.SkillId) is null))
        {
            var subject = Skill(catalog, row);
            if (subject.Name.Length == 0)
                continue;

            all.Add((catalog.Find(row.ParentSkillId)?.Category, subject));
        }

        var categoryOrder = catalog.Skills.Select(s => s.Category).Distinct().ToList();

        return all
            .GroupBy(item => item.Category)
            .OrderBy(group => group.Key is { } category ? categoryOrder.IndexOf(category) : int.MaxValue)
            .Select(group => new CheckSubjectGroup(
                group.Key is { } category ? CatalogText.Of(category) : OwnSkillsTitle,
                group.Select(item => item.Subject).OrderBy(s => s.Name, StringComparer.CurrentCulture).ToList()))
            .ToList();
    }

    /// <summary>Цель по ключу выпадающего списка (<see cref="CheckSubject.Key"/>).</summary>
    public static CheckSubject? FindByKey(CharacterSheet sheet, SkillCatalog catalog, string key)
    {
        if (key == "luck")
            return Luck(sheet);

        if (key.StartsWith("char:", StringComparison.Ordinal))
            return Enum.TryParse<Characteristic>(key["char:".Length..], out var characteristic)
                ? Characteristic(sheet, characteristic)
                : null;

        if (key.StartsWith("skill:", StringComparison.Ordinal))
            return Guid.TryParse(key["skill:".Length..], out var skillId) ? CatalogSkill(sheet, catalog, skillId) : null;

        if (key.StartsWith("own:", StringComparison.Ordinal))
        {
            var name = key["own:".Length..];
            return sheet.Skills
                .Where(s => catalog.Find(s.SkillId) is null)
                .Select(s => Skill(catalog, s))
                .FirstOrDefault(s => s.Name == name);
        }

        return null;
    }

    /// <summary>
    /// Цель проверки локации сценария (<c>cm.scenario_checks</c>: вид, навык, характеристика) на листе
    /// сыщика. В v1 проверка хранила имя строкой, и его приходилось угадывать («СИЛ», «Сила», «Стрельба (П)»);
    /// в 2.0 ссылку разрешил перенос (T1.3), угадывать нечего. Null — навыка нет в справочнике.
    /// </summary>
    public static CheckSubject? Resolve(
        CharacterSheet sheet, SkillCatalog catalog, CheckTarget target, Guid? skillId, Characteristic? characteristic) =>
        target switch
        {
            CheckTarget.Luck => Luck(sheet),
            CheckTarget.Characteristic when characteristic is { } key => Characteristic(sheet, key),
            CheckTarget.Skill when skillId is { } id => CatalogSkill(sheet, catalog, id),
            _ => null,
        };

    /// <summary>
    /// Ключ цели проверки локации — для <c>InitialKey</c> диалога: одинаков на листе любого сыщика, потому что
    /// у навыка это id справочника. Null — проверка без навыка или характеристики.
    /// </summary>
    public static string? KeyFor(CheckTarget target, Guid? skillId, Characteristic? characteristic) => target switch
    {
        CheckTarget.Luck => "luck",
        CheckTarget.Characteristic when characteristic is { } key => $"char:{key}",
        CheckTarget.Skill when skillId is { } id => $"skill:{id}",
        _ => null,
    };

    /// <summary>
    /// Строка листа, которой ставят отметку развития. У навыка справочника, которого на листе нет, строки
    /// тоже нет — её заводит <see cref="CheckRules.Apply"/>.
    /// </summary>
    public static SheetSkill? FindSkill(CharacterSheet sheet, SkillCatalog catalog, CheckSubject subject)
    {
        if (subject.Kind is not CheckSubjectKind.Skill)
            return null;

        if (subject.SkillId is { } id)
            return sheet.Entry(id);

        return sheet.Skills.FirstOrDefault(s => catalog.Find(s.SkillId) is null && s.DisplayName(catalog) == subject.Name);
    }
}
