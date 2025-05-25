using CampaignManager.Web.Components.Features.Items.Model;

namespace CampaignManager.Web.Components.Features.Scenarios.Model;

/// <summary>
///     Junction entity for the many-to-many relationship between Scenarios and Items
/// </summary>
public sealed class ScenarioItem : Item
{
    /// <summary>
    ///     Optional notes about this item's role in the scenario
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    ///     Optional location of this item in the scenario
    /// </summary>
    public string? Location { get; set; }

    public required Guid ScenarioId { get; set; }
}