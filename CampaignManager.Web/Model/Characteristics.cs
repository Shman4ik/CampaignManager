namespace CampaignManager.Web.Model;

public class Characteristics
{
    public AttributeValue Strength { get; set; } = new(50);
    public AttributeValue Dexterity { get; set; } = new(45);
    public AttributeValue Intelligence { get; set; } = new(80);
    public AttributeValue Constitution { get; set; } = new(80);
    public AttributeValue Appearance { get; set; } = new(40);
    public AttributeValue Power { get; set; } = new(70);
    public AttributeValue Size { get; set; } = new(60);
    public AttributeValue Education { get; set; } = new(75);
}