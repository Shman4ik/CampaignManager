namespace CampaignManager.Web.Components.Features.Scenarios.Model;

public sealed class ScenarioHandout
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public string? Description { get; set; }

    public string? FileUrl { get; set; }

    public int Order { get; set; }
}
