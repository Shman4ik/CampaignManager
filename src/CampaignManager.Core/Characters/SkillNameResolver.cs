using System.Text.RegularExpressions;
using CampaignManager.Core.Catalogs;

namespace CampaignManager.Core.Characters;

/// <summary>Чем оказался навык, записанный строкой (лист v1, импорт сценария, профессия v1).</summary>
public abstract record SkillMatch
{
    /// <summary>Навык справочника.</summary>
    public sealed record Catalog(SkillDefinition Skill) : SkillMatch;

    /// <summary>Специализация, которой нет в справочнике: «Язык, иностранный» + «латынь».</summary>
    public sealed record Specialization(SkillDefinition Parent, string Name) : SkillMatch;
}

/// <summary>
/// Навык по имени (лист v1, тварь, проверка в локации, профессия; импорт сценария — <see cref="SheetBuilder.FromImport"/>)
/// — порядок SCHEMA, «Персонажи»: точное
/// имя справочника или старое написание (<see cref="SkillCodes.FromName"/>), затем «родитель (специализация)»
/// по имени в скобках, затем языки, записанные без родителя («Латынь», «Язык (французский)»). Не нашлось —
/// null: это самодельный навык. Ничего не угадывает по похожести.
/// </summary>
public sealed partial class SkillNameResolver(SkillCatalog catalog)
{
    public SkillMatch? Resolve(string? rawName)
    {
        var name = rawName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (Exact(name) is { } skill)
        {
            return new SkillMatch.Catalog(skill);
        }

        // «Родитель (специализация)», которой нет в справочнике
        if (SpecializationPattern().Match(name) is { Success: true } m
            && Exact(m.Groups["parent"].Value) is { } parent
            && catalog.Find(parent.Id) is not null)
        {
            var specialization = m.Groups["spec"].Value.Trim();
            return IsLanguageParent(parent)
                ? Language(specialization)
                : new SkillMatch.Specialization(parent, specialization.ToLowerInvariant());
        }

        // Язык, записанный листом v1 без «иностранный»: «Язык (латынь)», «Языки (французский)»
        if (LanguagePattern().Match(name) is { Success: true } language)
        {
            return Language(language.Groups["spec"].Value);
        }

        // Голое название языка на листе — «Латынь» в группе «Социальные» у пяти листов
        if (BareLanguages.Contains(CatalogCodeTable.NormalizeName(name)))
        {
            return Language(name);
        }

        return null;
    }

    /// <summary>Id навыка справочника по имени; специализацию без справочника не считает навыком.</summary>
    public Guid? CatalogId(string? name) => Resolve(name) is SkillMatch.Catalog c ? c.Skill.Id : null;

    private static readonly HashSet<string> BareLanguages = new(StringComparer.Ordinal)
    {
        "латынь", "греческий", "древнегреческий", "французский", "немецкий", "английский", "арабский", "иврит",
    };

    private SkillDefinition? Exact(string name) =>
        (SkillCodes.FromName(name) is { } code ? catalog.FindByCode(code) : null)
        ?? catalog.FindByName(name);

    private static bool IsLanguageParent(SkillDefinition skill) => skill.Code == SkillCodes.LanguageForeign;

    private SkillMatch? Language(string specialization)
    {
        var parent = catalog.FindByCode(SkillCodes.LanguageForeign);
        if (parent is null)
        {
            return null;
        }

        var spec = specialization.Trim().ToLowerInvariant();
        var known = catalog.Children(parent.Id).FirstOrDefault(child =>
            string.Equals(child.Name, $"{parent.Name} ({spec})", StringComparison.OrdinalIgnoreCase));
        return known is not null ? new SkillMatch.Catalog(known) : new SkillMatch.Specialization(parent, spec);
    }

    [GeneratedRegex(@"^(?<parent>.+?)\s*\((?<spec>[^()]+)\)$")]
    private static partial Regex SpecializationPattern();

    [GeneratedRegex(@"^(язык|языки)\s*\((?<spec>[^()]+)\)$", RegexOptions.IgnoreCase)]
    private static partial Regex LanguagePattern();
}
