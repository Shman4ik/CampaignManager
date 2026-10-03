using CampaignManager.Core.Catalogs;

namespace CampaignManager.Core.Characters;

/// <summary>
/// Строка навыка на листе — то, что рисует один <c>SkillRow</c>: строка документа (<see cref="Entry"/>) или навык
/// справочника, которого на листе нет и который стоит на базе (<see cref="Entry"/> null — как пустая графа
/// бумажного бланка). Значение такой строки правится, и строка документа заводится в этот момент.
/// </summary>
public sealed record SkillLine(string Key, string Name, int Value, int BaseValue)
{
    public SkillDefinition? Definition { get; init; }

    public SheetSkill? Entry { get; init; }

    /// <summary>Родитель специализации (из справочника или своей строки).</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Мифы и Средства не отмечают (стр. 92).</summary>
    public bool CanBeChecked { get; init; }

    public bool Checked => Entry?.Checked ?? false;

    /// <summary>+10 от смежной специализации, перешедшей порог (стр. 76–77); 0 — нет.</summary>
    public int SpecializationBonus { get; init; }

    /// <summary>Своя строка (самодельный навык или специализация вне справочника) — её можно удалить целиком.</summary>
    public bool IsOwn => Definition is null;
}

/// <summary>Свёрнутые специализации одного родителя на базовом значении: «Стрельба — ещё 4».</summary>
public sealed record SpecializationFold(Guid ParentId, string ParentName, IReadOnlyList<SkillLine> Lines);

/// <summary>Группа бланка («Сбор информации») или «Свои навыки».</summary>
public sealed record SkillLineGroup(string Title, IReadOnlyList<SkillLine> Lines, IReadOnlyList<SpecializationFold> Folds);

/// <summary>
/// Навыки листа в порядке «Частые → остальные по алфавиту» (решение владельца 2026-10-03, вариант A):
/// <see cref="Frequent"/> — всегда один набор в порядке <see cref="SheetSkillLayout.FrequentCodes"/>;
/// <see cref="Others"/> — остальные строки одним списком; <see cref="Folds"/> — специализации на базе, свёрнутые
/// под родителем (кроме «Частых»).
/// </summary>
public sealed record SkillSections(
    IReadOnlyList<SkillLine> Frequent,
    IReadOnlyList<SkillLine> Others,
    IReadOnlyList<SpecializationFold> Folds);

/// <summary>
/// Раскладка навыков листа по группам бланка и правки строк. Одна на лист, диалог проверки и фазу развития:
/// в v1 строка навыка была размечена трижды, группы зеркалили словарь имён, а навык из справочника получал
/// значение 0 вместо базы (AUDIT, «Персонажи и НПС → Ошибки», 4).
/// <para>
/// На листе видны все базовые навыки справочника (как на бланке) и строки документа. Специализации
/// справочника, которых на листе нет или которые стоят на базе, свёрнуты под родителем — их десятки.
/// Навык-родитель («Стрельба») строкой не бывает: проверяют и развивают специализацию (стр. 52).
/// </para>
/// </summary>
public static class SheetSkillLayout
{
    public const string OwnSkillsTitle = "Свои навыки";

    /// <summary>
    /// Блок «Частые» на листе: коды справочника в порядке показа (решение владельца 2026-10-03). Единственное
    /// место набора; по имени навыки не ищутся.
    /// </summary>
    public static IReadOnlyList<string> FrequentCodes { get; } =
    [
        "skill.spot-hidden",
        "skill.listen",
        "skill.library-use",
        "skill.psychology",
        "skill.stealth",
        SkillCodes.Dodge,
        "skill.fighting.brawl",
        "skill.firearms.handgun",
        "skill.first-aid",
        SkillCodes.Persuade,
        SkillCodes.Charm,
        SkillCodes.FastTalk,
        SkillCodes.Intimidate,
    ];

    /// <summary>
    /// «Частые» сверху, остальное — одним списком по алфавиту без групп. Навык «Частых» без строки в листе берётся
    /// из справочника на базе, как остальные; дублей между блоками нет.
    /// </summary>
    public static SkillSections Sections(CharacterSheet sheet, SkillCatalog catalog)
    {
        var groups = Groups(sheet, catalog);
        var visible = groups.SelectMany(g => g.Lines).ToList();
        var all = visible.Concat(groups.SelectMany(g => g.Folds).SelectMany(f => f.Lines)).ToList();

        var frequent = new List<SkillLine>();
        foreach (var code in FrequentCodes)
        {
            var line = all.FirstOrDefault(l => l.Definition?.Code == code);
            if (line is not null)
                frequent.Add(line);
        }

        var taken = frequent.Select(l => l.Key).ToHashSet(StringComparer.Ordinal);
        var others = visible.Where(l => !taken.Contains(l.Key)).OrderBy(l => AlphabeticKey(l.Name), StringComparer.Ordinal).ToList();
        var folds = groups.SelectMany(g => g.Folds)
            .Select(f => f with { Lines = [.. f.Lines.Where(l => !taken.Contains(l.Key))] })
            .Where(f => f.Lines.Count > 0)
            .OrderBy(f => AlphabeticKey(f.ParentName), StringComparer.Ordinal)
            .ToList();

        return new SkillSections(frequent, others, folds);
    }

    /// <summary>
    /// Навыки режима «Игра» (решение владельца 2026-10-03): только развитые — значение выше базы или отметка развития,
    /// одним списком по алфавиту. Навык на базе за столом находит поиск по <see cref="All"/>.
    /// </summary>
    public static IReadOnlyList<SkillLine> Developed(CharacterSheet sheet, SkillCatalog catalog) =>
        [.. All(sheet, catalog).Where(l => l.Value > l.BaseValue || l.Checked)];

    /// <summary>Все строки листа — «Частые», остальные и свёрнутые специализации — одним списком по алфавиту, без дублей.</summary>
    public static IReadOnlyList<SkillLine> All(CharacterSheet sheet, SkillCatalog catalog)
    {
        var sections = Sections(sheet, catalog);
        return [.. sections.Frequent.Concat(sections.Others).Concat(sections.Folds.SelectMany(f => f.Lines))
            .OrderBy(l => AlphabeticKey(l.Name), StringComparer.Ordinal)];
    }

    /// <summary>Ключ русской сортировки: регистр не важен, «ё» = «е» (ординально «ё» ушла бы за «я»).</summary>
    public static string AlphabeticKey(string name) => name.ToLowerInvariant().Replace('ё', 'е');

    /// <summary>Делит список ровно пополам: нечётное число — лишний в левой (первой) половине.</summary>
    public static (IReadOnlyList<T> Left, IReadOnlyList<T> Right) SplitInHalf<T>(IReadOnlyList<T> items)
    {
        var left = (items.Count + 1) / 2;
        return ([.. items.Take(left)], [.. items.Skip(left)]);
    }

    /// <summary>
    /// Делит список на <paramref name="parts"/> колонок подряд (режим «Игра» — три колонки навыков): первая колонка
    /// сверху вниз, затем вторая; лишние — в первые колонки. Пустых колонок в конце не бывает.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<T>> SplitInto<T>(IReadOnlyList<T> items, int parts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(parts, 1);
        var columns = new List<IReadOnlyList<T>>();
        var taken = 0;
        for (var i = 0; i < parts && taken < items.Count; i++)
        {
            var size = (items.Count - taken + parts - i - 1) / (parts - i);
            columns.Add([.. items.Skip(taken).Take(size)]);
            taken += size;
        }

        return columns;
    }

    public static IReadOnlyList<SkillLineGroup> Groups(CharacterSheet sheet, SkillCatalog catalog)
    {
        var lines = new List<(SkillCategory? Category, SkillLine Line, bool Folded)>();
        var onSheet = new HashSet<Guid>();

        foreach (var entry in sheet.Skills)
        {
            if (entry.SkillId is { } id)
            {
                if (!onSheet.Add(id) || catalog.IsParent(id))
                    continue;
            }

            var line = Line(sheet, catalog, entry);
            if (line.Name.Length == 0)
                continue;

            var category = catalog.Find(entry.SkillId)?.Category ?? catalog.Find(entry.ParentSkillId)?.Category;
            var folded = line.Definition?.ParentId is not null && line.Value == line.BaseValue && !line.Checked;
            lines.Add((FoldCategory(catalog, line, folded, category), line, folded));
        }

        foreach (var definition in catalog.Skills)
        {
            if (onSheet.Contains(definition.Id) || catalog.IsParent(definition.Id))
                continue;

            var line = Line(sheet, catalog, definition);
            var folded = definition.ParentId is not null;
            lines.Add((FoldCategory(catalog, line, folded, definition.Category), line, folded));
        }

        var categoryOrder = catalog.Skills.Select(s => s.Category).Distinct().ToList();

        return lines
            .GroupBy(item => item.Category)
            .OrderBy(group => group.Key is { } category ? categoryOrder.IndexOf(category) : int.MaxValue)
            .Select(group => new SkillLineGroup(
                group.Key is { } category ? CatalogText.Of(category) : OwnSkillsTitle,
                group.Where(item => !item.Folded).Select(item => item.Line).OrderBy(l => l.Name, StringComparer.CurrentCulture).ToList(),
                group.Where(item => item.Folded)
                    .GroupBy(item => item.Line.ParentId!.Value)
                    .Select(fold => new SpecializationFold(fold.Key, catalog.Find(fold.Key)?.Name ?? "",
                        fold.Select(item => item.Line).OrderBy(l => l.Name, StringComparer.CurrentCulture).ToList()))
                    .OrderBy(fold => fold.ParentName, StringComparer.CurrentCulture)
                    .ToList()))
            .ToList();
    }

    /// <summary>
    /// Свёрнутая специализация живёт в группе родителя, а не своей: категории специализаций одного родителя
    /// различаются («Наука (фармакология)» — лечение, «Наука (химия)» — знания), и свёртка по категориям давала
    /// «Науку» в нескольких группах сразу — два соседних <c>&lt;details&gt;</c> с одним ключом.
    /// </summary>
    private static SkillCategory? FoldCategory(SkillCatalog catalog, SkillLine line, bool folded, SkillCategory? own) =>
        folded && catalog.Find(line.ParentId) is { } parent ? parent.Category : own;

    /// <summary>Строка документа как строка листа.</summary>
    public static SkillLine Line(CharacterSheet sheet, SkillCatalog catalog, SheetSkill entry)
    {
        var definition = catalog.Find(entry.SkillId);
        var parentId = entry.ParentOf(catalog);
        var baseValue = definition is not null
            ? SkillCatalog.BaseValueOf(definition, sheet.Characteristics)
            : parentId is { } parent ? catalog.Children(parent).FirstOrDefault()?.BaseValue ?? 1 : 0;

        return new SkillLine(definition is not null ? $"skill:{definition.Id}" : $"own:{entry.DisplayName(catalog)}",
            entry.DisplayName(catalog), entry.Value, baseValue)
        {
            Definition = definition,
            Entry = entry,
            ParentId = parentId,
            CanBeChecked = DevelopmentPhaseRules.CanBeChecked(entry, catalog),
            SpecializationBonus = SpecializationRules.BonusFor(sheet, catalog, entry),
        };
    }

    /// <summary>Навык справочника, которого на листе нет: значение — база.</summary>
    public static SkillLine Line(CharacterSheet sheet, SkillCatalog catalog, SkillDefinition definition)
    {
        var baseValue = SkillCatalog.BaseValueOf(definition, sheet.Characteristics);
        return new SkillLine($"skill:{definition.Id}", definition.Name, baseValue, baseValue)
        {
            Definition = definition,
            ParentId = definition.ParentId,
            CanBeChecked = DevelopmentPhaseRules.CanBeChecked(definition.Code),
        };
    }

    /// <summary>
    /// Новое значение строки. У навыка справочника без строки документа она заводится (с этим значением);
    /// значение не уходит ниже нуля.
    /// </summary>
    public static SheetSkill? SetValue(CharacterSheet sheet, SkillCatalog catalog, SkillLine line, int value)
    {
        var entry = EnsureEntry(sheet, line);
        if (entry is null)
            return null;

        entry.Value = Math.Max(0, value);
        return entry;
    }

    /// <summary>Отметка развития (стр. 92). Мифам и Средствам — нет.</summary>
    public static void SetChecked(CharacterSheet sheet, SkillLine line, bool isChecked)
    {
        if (!line.CanBeChecked)
            return;

        if (EnsureEntry(sheet, line) is { } entry)
            entry.Checked = isChecked;
    }

    /// <summary>
    /// Убрать строку: своя исчезает, навык справочника возвращается к базе (как стёртая графа бланка).
    /// </summary>
    public static void Remove(CharacterSheet sheet, SkillLine line)
    {
        if (line.Entry is { } entry)
            sheet.Skills.Remove(entry);
    }

    /// <summary>
    /// Навык справочника на лист — с базовым значением, а не с нулём (в v1 — ошибка: «навык из справочника
    /// получал 0»). Уже есть — возвращается существующая строка. Родителя на лист не кладут.
    /// </summary>
    public static SheetSkill? AddFromCatalog(CharacterSheet sheet, SkillCatalog catalog, Guid skillId)
    {
        if (catalog.Find(skillId) is not { } definition || catalog.IsParent(skillId))
            return null;

        if (sheet.Entry(skillId) is { } existing)
            return existing;

        var created = new SheetSkill { SkillId = skillId, Value = SkillCatalog.BaseValueOf(definition, sheet.Characteristics) };
        sheet.Skills.Add(created);
        return created;
    }

    /// <summary>
    /// Своя специализация родителя («Язык, иностранный» → «латынь»): база — у соседней специализации
    /// справочника, иначе 1% (<see cref="OccupationRules.NewSpecialization"/>). Такая же уже есть — она.
    /// Есть в справочнике с этим именем — строка справочника.
    /// </summary>
    public static SheetSkill? AddSpecialization(CharacterSheet sheet, SkillCatalog catalog, Guid parentId, string specialization)
    {
        var name = specialization.Trim();
        if (name.Length == 0 || catalog.Find(parentId) is not { } parent)
            return null;

        var fullName = $"{parent.Name} ({name})";
        if (catalog.Children(parentId).FirstOrDefault(c => string.Equals(c.Name, fullName, StringComparison.CurrentCultureIgnoreCase)) is { } known)
            return AddFromCatalog(sheet, catalog, known.Id);

        if (sheet.Skills.FirstOrDefault(s => s.SkillId is null && s.ParentSkillId == parentId
                && string.Equals(s.Name?.Trim(), name, StringComparison.CurrentCultureIgnoreCase)) is { } same)
            return same;

        var created = OccupationRules.NewSpecialization(parentId, name, catalog);
        sheet.Skills.Add(created);
        return created;
    }

    /// <summary>Самодельный навык Хранителя: имя и значение, в справочник он не попадает.</summary>
    public static SheetSkill? AddOwn(CharacterSheet sheet, string name, int value)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            return null;

        if (sheet.Skills.FirstOrDefault(s => s.SkillId is null && s.ParentSkillId is null
                && string.Equals(s.Name?.Trim(), trimmed, StringComparison.CurrentCultureIgnoreCase)) is { } same)
            return same;

        var created = new SheetSkill { Name = trimmed, Value = Math.Max(0, value) };
        sheet.Skills.Add(created);
        return created;
    }

    private static SheetSkill? EnsureEntry(CharacterSheet sheet, SkillLine line)
    {
        if (line.Entry is { } entry)
            return entry;

        if (line.Definition is not { } definition)
            return null;

        if (sheet.Entry(definition.Id) is { } existing)
            return existing;

        var created = new SheetSkill { SkillId = definition.Id, Value = line.BaseValue };
        sheet.Skills.Add(created);
        return created;
    }
}
