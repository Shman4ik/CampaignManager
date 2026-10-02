using CampaignManager.Contracts.Catalogs;
using CampaignManager.Core.Characters;

namespace CampaignManager.UI.Characters;

/// <summary>Справочник навыков из API как <see cref="SkillCatalog"/> Core — по нему читается любой лист.</summary>
public static class SkillCatalogs
{
    public static SkillCatalog From(IEnumerable<SkillDto> skills) =>
        new(skills.Select(s => new SkillDefinition(s.Id, s.Name)
        {
            Code = s.Code,
            ParentId = s.ParentId,
            BaseValue = s.BaseValue,
            BaseFormula = s.BaseFormula,
            Category = s.Category,
        }));
}
