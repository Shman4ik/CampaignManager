namespace CampaignManager.Web.Components.Features.Scenarios.Model;

public sealed class ScenarioLocation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public string? Address { get; set; }

    public string? Description { get; set; }

    public int Order { get; set; }

    public Guid? ParentLocationId { get; set; }

    public List<ScenarioSkillCheck> SkillChecks { get; set; } = [];

    public List<Guid> CreatureIds { get; set; } = [];

    public List<Guid> ItemIds { get; set; } = [];

    public List<Guid> NpcIds { get; set; } = [];

    public List<Guid> HandoutIds { get; set; } = [];
}
