using CampaignManager.Core.Catalogs;

namespace CampaignManager.Core.Characters;

/// <summary>
/// Бонус смежным специализациям (стр. 76–77): улучшив одну специализацию до 50% (затем до 90%), сыщик
/// поднимает смежные на 10 — но не выше того же порога.
/// </summary>
public static class SpecializationRules
{
    /// <summary>
    /// Навыки, чьи специализации «имеют много общего» и делятся прогрессом. Список закрытый: книга прямо
    /// противопоставляет им Науку. Сверяется по коду родителя, а не по префиксу имени — в v1 в списке
    /// стояло старое «Языки», а справочник называл родителя «Язык, иностранный», и иностранные языки бонуса
    /// не получали никогда (rules-findings F-S04).
    /// </summary>
    public static IReadOnlyList<string> SharedProgressParents { get; } =
        [SkillCodes.Fighting, SkillCodes.Firearms, SkillCodes.LanguageForeign, SkillCodes.Survival];

    public static bool ParentSharesProgress(string? parentCode) =>
        parentCode is not null && SharedProgressParents.Contains(parentCode, StringComparer.Ordinal);

    /// <summary>
    /// Прибавка, которую специализация получает от лучшей соседней, или 0. Пороги 50 и 90, каждый даёт
    /// +10, но не выше себя: специализация на 45 при соседе 50+ поднимается только до 50.
    /// </summary>
    public static int BonusFor(int value, IEnumerable<int> siblingValues, string? parentCode)
    {
        if (!ParentSharesProgress(parentCode))
            return 0;

        var best = siblingValues.DefaultIfEmpty(0).Max();
        var cap = best >= 90 ? 90 : best >= 50 ? 50 : 0;
        if (cap == 0 || value >= cap)
            return 0;

        return Math.Min(10, cap - value);
    }

    /// <summary>
    /// Прибавка строке листа от остальных специализаций того же родителя на этом же листе.
    /// </summary>
    public static int BonusFor(CharacterSheet sheet, SkillCatalog catalog, SheetSkill skill)
    {
        var parent = skill.ParentOf(catalog);
        if (parent is null)
            return 0;

        var siblings = sheet.Skills
            .Where(s => !ReferenceEquals(s, skill) && s.ParentOf(catalog) == parent)
            .Select(s => s.Value);

        return BonusFor(skill.Value, siblings, catalog.CodeOf(parent));
    }
}
