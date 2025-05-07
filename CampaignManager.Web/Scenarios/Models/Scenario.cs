using CampaignManager.Web.Model;

namespace CampaignManager.Web.Scenarios.Models;

/// <summary>
/// Base model for adventure scenarios that can be used as templates or added to campaigns
/// </summary>
public sealed class Scenario : BaseDataBaseEntity
{
    /// <summary>
    /// The name of the scenario
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Detailed description of the scenario
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// The location where the scenario takes place
    /// </summary>
    public string? Location { get; set; }
    
    /// <summary>
    /// The era in which the scenario is set (e.g., 1920s, Modern)
    /// </summary>
    public string? Era { get; set; }
    
    /// <summary>
    /// Markdown content for the scenario journal/notes
    /// </summary>
    public string? Journal { get; set; }
    
    /// <summary>
    /// Indicates if this scenario is a template (true) or an active campaign scenario (false)
    /// </summary>
    public bool IsTemplate { get; set; }
    
    /// <summary>
    /// Email of the user who created this scenario
    /// </summary>
    public string? CreatorEmail { get; set; }
    
    /// <summary>
    /// Optional reference to a campaign if this scenario is part of one
    /// </summary>
    public Guid? CampaignId { get; set; }
    
    /// <summary>
    /// Navigation property to the campaign this scenario belongs to (if any)
    /// </summary>
    public Companies.Models.Campaign? Campaign { get; set; }
    
    /// <summary>
    /// Collection of NPCs in this scenario
    /// </summary>
    public ICollection<ScenarioNpc> Npcs { get; set; } = [];
    
    /// <summary>
    /// Collection of creatures in this scenario
    /// </summary>
    public ICollection<ScenarioCreature> ScenarioCreatures { get; set; } = [];
    
    /// <summary>
    /// Collection of items in this scenario
    /// </summary>
    public ICollection<ScenarioItem> ScenarioItems { get; set; } = [];
}
