namespace CampaignManager.Web.Components.Features.Scenarios.Model;

public sealed class ScenarioSkillCheck
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string SkillName { get; set; }

    public string? Difficulty { get; set; }

    public string? SuccessResult { get; set; }

    public string? FailureResult { get; set; }
}
