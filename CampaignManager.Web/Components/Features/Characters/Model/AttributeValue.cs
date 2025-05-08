namespace CampaignManager.Web.Components.Features.Characters.Model;

public class AttributeValue
{
    public AttributeValue(int regular)
    {
        Regular = regular;
        UpdateDerived();
    }

    public int Regular { get; set; }
    public int Half { get; set; }
    public int Fifth { get; set; }

    public void UpdateDerived()
    {
        Half = Regular / 2;
        Fifth = Regular / 5;
    }
}