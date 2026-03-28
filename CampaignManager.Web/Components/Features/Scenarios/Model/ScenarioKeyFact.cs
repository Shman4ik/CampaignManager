namespace CampaignManager.Web.Components.Features.Scenarios.Model;

public sealed class ScenarioKeyFact
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Title { get; set; }

    public string? Content { get; set; }

    public KeyFactType Type { get; set; }

    public int Order { get; set; }
}

public enum KeyFactType
{
    Backstory,
    Truth,
    Timeline,
    Reward
}
