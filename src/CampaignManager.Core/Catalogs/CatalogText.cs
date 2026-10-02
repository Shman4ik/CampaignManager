namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Русские подписи перечислений справочников — одна таблица на приложение. В v1 у категорий навыков
/// было два словаря (<c>EnumExtensions</c> и <c>SkillService.CategoryGroupNames</c>) с разными
/// написаниями.
/// </summary>
public static class CatalogText
{
    /// <summary>Группа навыков на бланке сыщика: «Сбор информации», «Сражение (огнестрельное)».</summary>
    public static string Of(SkillCategory category) => category switch
    {
        SkillCategory.ProblemSolving => "Решение проблем",
        SkillCategory.InformationGathering => "Сбор информации",
        SkillCategory.Special => "Специальные",
        SkillCategory.Social => "Социальные",
        SkillCategory.Healing => "Лечение",
        SkillCategory.CombatGeneral => "Сражение (общее)",
        SkillCategory.Knowledge => "Знания",
        SkillCategory.CombatFirearms => "Сражение (огнестрельное)",
        SkillCategory.Actions => "Действия",
        _ => category.ToString(),
    };
}
