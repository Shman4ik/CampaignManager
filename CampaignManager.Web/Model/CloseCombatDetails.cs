namespace CampaignManager.Web.Model;

public class CloseCombatDetails
{
    /// <summary>
    ///     Оружие ближнего боя
    /// </summary>
    public List<CloseCombatWeapon> Weapons { get; set; } = new();
}