namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
/// DTO для настройки бегства из ближнего боя (CoC 7e стр. 106)
/// </summary>
public class FleeSetup
{
    public Guid FleeerId { get; set; }
    public string FleeerName { get; set; } = string.Empty;
}
