namespace CampaignManager.Web.Components.Features.Bestiary.Model;

public class CreatureCharacteristicModel
{
    /// <summary>
    ///     Numeric value of the characteristic
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    ///     Dice roll description (e.g. "2d6+5")
    /// </summary>
    public string? DiceRoll { get; set; }
}