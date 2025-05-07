using CampaignManager.Web.Model;

namespace CampaignManager.Web.Scenarios.Models;

/// <summary>
/// Model for significant items and artifacts in scenarios
/// </summary>
public sealed class Item : BaseDataBaseEntity
{
    /// <summary>
    /// The name of the item
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// The type of item (e.g., Weapon, Artifact, Tool)
    /// </summary>
    public string? Type { get; set; }
    
    /// <summary>
    /// Detailed description of the item
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Special effects or properties of the item
    /// </summary>
    public string? Effects { get; set; }
    
    /// <summary>
    /// The rarity of the item (e.g., Common, Rare, Unique)
    /// </summary>
    public string? Rarity { get; set; }
    
    /// <summary>
    /// Optional URL to an image of the item
    /// </summary>
    public string? ImageUrl { get; set; }
    
    /// <summary>
    /// Categories this item belongs to (comma-separated)
    /// </summary>
    public string? Categories { get; set; }
    
    /// <summary>
    /// Collection of scenario-item relationships
    /// </summary>
    public ICollection<ScenarioItem> ScenarioItems { get; set; } = [];
}
