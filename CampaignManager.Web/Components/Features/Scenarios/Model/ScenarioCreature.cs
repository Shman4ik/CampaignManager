using CampaignManager.Web.Components.Features.Bestiary.Model;

namespace CampaignManager.Web.Components.Features.Scenarios.Model;

/// <summary>
///     Junction entity for the many-to-many relationship between Scenarios and Creatures
/// </summary>
public sealed class ScenarioCreature : Creature
{
    /// <summary>
    ///     Optional location of this creature in the scenario
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    ///     Optional notes about this creature's role in the scenario
    /// </summary>
    public string? Notes { get; set; }

    public required Guid ScenarioId { get; set; }
}