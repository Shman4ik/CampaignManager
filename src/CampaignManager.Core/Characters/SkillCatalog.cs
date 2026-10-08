using CampaignManager.Core.Catalogs;

namespace CampaignManager.Core.Characters;

/// <summary>
/// Навык справочника — то, что правилам нужно знать о строке <c>cm.skills</c>: имя, родитель, база и
/// стабильный код. Сервер и клиент собирают его из DTO справочника.
/// </summary>
public sealed record SkillDefinition(Guid Id, string Name)
{
    /// <summary>Код книжного навыка (<see cref="SkillCodes"/>); null у самодельного.</summary>
    public string? Code { get; init; }

    /// <summary>Родитель специализации («Стрельба» у «Стрельба (пистолет)»).</summary>
    public Guid? ParentId { get; init; }

    public int BaseValue { get; init; }

    /// <summary>Когда база не число: <c>DEX/2</c> у Уклонения, <c>EDU</c> у родного языка (стр. 57, 77).</summary>
    public string? BaseFormula { get; init; }

    public SkillCategory Category { get; init; }
}

/// <summary>
/// Справочник навыков, по которому читается лист: имена, коды, родители и базовые значения.
/// <para>
/// Лист хранит только ссылки (<see cref="SheetSkill.SkillId"/>), поэтому всё, что правилам надо знать
/// о навыке «Мифы Ктулху» или «Средства», они спрашивают здесь по коду, а не ищут строкой по листу.
/// </para>
/// </summary>
public sealed class SkillCatalog
{
    private readonly Dictionary<Guid, SkillDefinition> _byId;
    private readonly Dictionary<string, SkillDefinition> _byCode;

    public SkillCatalog(IEnumerable<SkillDefinition> skills)
    {
        Skills = skills.ToList();
        _byId = Skills.ToDictionary(s => s.Id);
        _byCode = Skills.Where(s => s.Code is not null).ToDictionary(s => s.Code!, StringComparer.Ordinal);
    }

    public IReadOnlyList<SkillDefinition> Skills { get; }

    public SkillDefinition? Find(Guid? id) => id is { } key ? _byId.GetValueOrDefault(key) : null;

    public SkillDefinition? FindByCode(string code) => _byCode.GetValueOrDefault(code);

    public SkillDefinition? FindByName(string name) =>
        Skills.FirstOrDefault(s => string.Equals(s.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Специализации навыка-родителя.</summary>
    public IEnumerable<SkillDefinition> Children(Guid parentId) => Skills.Where(s => s.ParentId == parentId);

    /// <summary>
    /// База специализации, которой нет в справочнике: та, что у большинства соседних (Наука — 1% кроме математики, Искусство —
    /// 5%). У Ближнего боя и Стрельбы базы разные (стр. 56), своей книга не даёт — тогда база первой соседней, но
    /// <c>Known</c> ложно, и лист числа не показывает. Соседних нет — 1%, тоже без показа.
    /// </summary>
    public (int Value, bool Known) SpecializationBase(Guid parentId)
    {
        var bases = Children(parentId).Select(c => c.BaseValue).ToList();
        if (bases.Count == 0)
            return (1, false);

        var common = bases.GroupBy(b => b).MaxBy(g => g.Count())!;
        return common.Count() * 2 > bases.Count ? (common.Key, true) : (bases[0], false);
    }

    /// <summary>У навыка есть специализации: его нельзя взять «вообще», только конкретную (стр. 52).</summary>
    public bool IsParent(Guid id) => Skills.Any(s => s.ParentId == id);

    public string? CodeOf(Guid? id) => Find(id)?.Code;

    /// <summary>
    /// Нужна ли навыку справочника строка листа: только если значение <b>выше</b> базы (<see cref="BaseValueOf"/>).
    /// Навык на базе лист показывает и без строки, как пустую графу бланка; строка ниже базы перекрыла бы базу —
    /// в v1 0 у навыка значил «не заполнено», и перенесённый буквально он делал родной язык 0 вместо ОБР.
    /// Одна копия правила: конструктор листа (<see cref="SheetBuilder"/>), экспорт сценария и перенос v1.
    /// </summary>
    public static bool IsAboveBase(SkillDefinition skill, int value, Characteristics characteristics) =>
        value > BaseValueOf(skill, characteristics);

    /// <summary>
    /// Базовое значение (стр. 30–31, 57, 77): число справочника либо формула от характеристики —
    /// <c>DEX/2</c>, <c>EDU</c>, <c>POW*2</c>.
    /// </summary>
    public static int BaseValueOf(SkillDefinition skill, Characteristics characteristics)
    {
        if (string.IsNullOrWhiteSpace(skill.BaseFormula))
            return skill.BaseValue;

        var formula = skill.BaseFormula.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        var operatorIndex = formula.IndexOfAny(['/', '*']);
        var name = operatorIndex < 0 ? formula : formula[..operatorIndex];

        if (!Enum.TryParse<Characteristic>(name, out var characteristic))
            return skill.BaseValue;

        var value = characteristics[characteristic];
        if (operatorIndex < 0 || !int.TryParse(formula[(operatorIndex + 1)..], out var operand) || operand == 0)
            return value;

        return formula[operatorIndex] == '/' ? value / operand : value * operand;
    }
}
