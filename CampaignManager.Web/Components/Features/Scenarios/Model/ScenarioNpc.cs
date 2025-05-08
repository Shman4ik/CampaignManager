using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Scenarios.Model;

/// <summary>
///     Simplified model for NPCs in scenarios
/// </summary>
public sealed class ScenarioNpc : BaseDataBaseEntity
{
    /// <summary>
    ///     The name of the NPC
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Detailed description of the NPC
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     The role of the NPC in the scenario (e.g., Ally, Villain, Informant)
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    ///     The location where the NPC is found within the scenario
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    ///     Additional notes about the NPC
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    ///     Optional reference to a full Character if this NPC has been developed into one
    /// </summary>
    public Guid? CharacterId { get; set; }

    /// <summary>
    ///     Navigation property to the full Character (if any)
    /// </summary>
    public CharacterStorageDto? Character { get; set; }

    /// <summary>
    ///     The ID of the scenario this NPC belongs to
    /// </summary>
    public Guid ScenarioId { get; set; }

    /// <summary>
    ///     Navigation property to the scenario this NPC belongs to
    /// </summary>
    public Scenario Scenario { get; set; } = null!;
}