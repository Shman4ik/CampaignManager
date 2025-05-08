using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Scenarios.Model;

/// <summary>
///     Junction entity for the many-to-many relationship between Scenarios and Creatures
/// </summary>
public sealed class ScenarioCreature : BaseDataBaseEntity
{
    /// <summary>
    ///     The ID of the scenario
    /// </summary>
    public Guid ScenarioId { get; set; }

    /// <summary>
    ///     Navigation property to the scenario
    /// </summary>
    public Scenario Scenario { get; set; } = null!;

    /// <summary>
    ///     The ID of the creature
    /// </summary>
    public Guid CreatureId { get; set; }

    /// <summary>
    ///     Navigation property to the creature
    /// </summary>
    public Creature Creature { get; set; } = null!;

    /// <summary>
    ///     Optional location where this creature appears in the scenario
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    ///     Optional notes about this creature's role in the scenario
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    ///     Optional quantity of this creature in the scenario
    /// </summary>
    public int? Quantity { get; set; }
}