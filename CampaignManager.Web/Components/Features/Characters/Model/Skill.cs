namespace CampaignManager.Web.Components.Features.Characters.Model;

public class Skill
{
    public required string Name { get; set; }
    public required AttributeValue Value { get; set; } = new AttributeValue(0);
    public required string BaseValue { get; set; } = "0";
    public bool IsUsed { get; set; } = false;
}