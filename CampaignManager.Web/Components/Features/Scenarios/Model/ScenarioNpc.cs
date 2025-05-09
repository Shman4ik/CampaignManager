using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Scenarios.Model;

public class ScenarioNpc: BaseDataBaseEntity
{
    public string Name { get; set; } = string.Empty;
    
    public Guid CharacterId { get; set; }
    
    public CharacterStorageDto Character { get; set; } 
    
    /// <summary>
    ///     The ID of the scenario
    /// </summary>
    public Guid ScenarioId { get; set; }

    /// <summary>
    ///     Navigation property to the scenario
    /// </summary>
    public Scenario Scenario { get; set; } 
}