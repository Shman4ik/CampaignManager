using CampaignManager.Web.Model;

namespace CampaignManager.Web.Scenarios.Models;

/// <summary>
/// Junction entity for the many-to-many relationship between Scenarios and Items
/// </summary>
public sealed class ScenarioItem : BaseDataBaseEntity
{
    /// <summary>
    /// The ID of the scenario
    /// </summary>
    public Guid ScenarioId { get; set; }
    
    /// <summary>
    /// Navigation property to the scenario
    /// </summary>
    public Scenario Scenario { get; set; } = null!;
    
    /// <summary>
    /// The ID of the item
    /// </summary>
    public Guid ItemId { get; set; }
    
    /// <summary>
    /// Navigation property to the item
    /// </summary>
    public Item Item { get; set; } = null!;
    
    /// <summary>
    /// Optional location where this item can be found in the scenario
    /// </summary>
    public string? Location { get; set; }
    
    /// <summary>
    /// Optional notes about this item's role in the scenario
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Optional quantity of this item in the scenario
    /// </summary>
    public int? Quantity { get; set; }
}
