namespace CampaignManager.Web.Model;

public class RangedCombatDetails
{
    /// <summary>
    ///     Оружие дальнего боя
    /// </summary>
    public List<RangedCombatWeapon> Weapons { get; set; } = new();
}