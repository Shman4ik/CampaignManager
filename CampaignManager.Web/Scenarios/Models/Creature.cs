using CampaignManager.Web.Model;

namespace CampaignManager.Web.Scenarios.Models;

/// <summary>
/// Model for monsters and supernatural entities
/// </summary>
public sealed class Creature : BaseDataBaseEntity
{
    /// <summary>
    /// The name of the creature
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// The type of creature (e.g., Undead, Beast, Aberration)
    /// </summary>
    public string? Type { get; set; }
    
    /// <summary>
    /// Detailed description of the creature
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Simplified statistics for the creature (stored as JSON)
    /// </summary>
    public string? Stats { get; set; }
    
    /// <summary>
    /// Special abilities of the creature
    /// </summary>
    public string? Abilities { get; set; }
    
    /// <summary>
    /// Optional URL to an image of the creature
    /// </summary>
    public string? ImageUrl { get; set; }
    
    /// <summary>
    /// Categories this creature belongs to (comma-separated)
    /// </summary>
    public string? Categories { get; set; }
    
    /// <summary>
    /// Collection of scenario-creature relationships
    /// </summary>
    public ICollection<ScenarioCreature> ScenarioCreatures { get; set; } = [];
}
