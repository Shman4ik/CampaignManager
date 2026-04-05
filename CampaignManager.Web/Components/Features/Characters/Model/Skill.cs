namespace CampaignManager.Web.Components.Features.Characters.Model;

public class Skill
{
    public required string Name { get; set; }
    public required AttributeValue Value { get; set; } = new AttributeValue(0);
    public required string BaseValue { get; set; } = "0";
    public bool IsUsed { get; set; } = false;

    /// <summary>
    /// Optional reference to SkillModel entity for wiki functionality
    /// </summary>
    public Guid? SkillModelId { get; set; }

    /// <summary>
    /// Name of the parent skill for specializations (e.g. "Ближний бой" for "Ближний бой (драка)").
    /// Stored in JSONB as a denormalized string to avoid FK lookups at render time.
    /// Null if the skill is not a specialization.
    /// </summary>
    public string? ParentSkillName { get; set; }
}