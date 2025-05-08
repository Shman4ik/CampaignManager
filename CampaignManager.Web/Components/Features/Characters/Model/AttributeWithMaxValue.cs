namespace CampaignManager.Web.Components.Features.Characters.Model;

public class AttributeWithMaxValue
{
    public AttributeWithMaxValue(int value, int maxValue)
    {
        Value = value;
        MaxValue = maxValue;
    }

    public int Value { get; set; }
    public int MaxValue { get; set; }
}