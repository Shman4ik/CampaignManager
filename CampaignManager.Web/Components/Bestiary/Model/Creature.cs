using CampaignManager.Web.Model;
using CampaignManager.Web.Scenarios.Models;

namespace CampaignManager.Web.Components.Bestiary.Model;

/// <summary>
///     Model for monsters and supernatural entities
/// </summary>
public sealed class Creature : BaseDataBaseEntity
{
    public required string Name { get; set; }


    public CreatureType Type { get; set; }

    /// <summary>
    ///     Detailed description of the creature
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Simplified statistics for the creature (stored as JSON)
    /// </summary>
    public CreatureCharacteristics CreatureCharacteristics { get; set; }

    /// <summary>
    ///     Бой
    /// </summary>
    public Dictionary<string, string> CombatDescriptions { get; set; }

    /// <summary>
    ///     Особые умения
    /// </summary>
    public Dictionary<string, string> SpecialAbilities { get; set; }

    /// <summary>
    ///     Optional URL to an image of the creature
    /// </summary>
    public string? ImageUrl { get; set; }


    /// <summary>
    ///     Collection of scenario-creature relationships
    /// </summary>
    public ICollection<ScenarioCreature> ScenarioCreatures { get; set; } = [];
}