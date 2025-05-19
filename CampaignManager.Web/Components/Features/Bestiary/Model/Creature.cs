using CampaignManager.Web.Components.Features.Scenarios.Model;
using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Bestiary.Model;

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
    ///     Характеристики существа
    /// </summary>
    public required CreatureCharacteristics CreatureCharacteristics { get; set; }

    /// <summary>
    ///     Описание боевых действий
    /// </summary>
    public Dictionary<string, string> CombatDescriptions { get; set; } = new();

    /// <summary>
    ///     Особые умения
    /// </summary>
    public Dictionary<string, string> SpecialAbilities { get; set; } = new();

    /// <summary>
    ///     Optional URL to an image of the creature
    /// </summary>
    public string? ImageUrl { get; set; }


    /// <summary>
    ///     Collection of scenario-creature relationships
    /// </summary>
    public ICollection<ScenarioCreature> ScenarioCreatures { get; set; } = [];
}