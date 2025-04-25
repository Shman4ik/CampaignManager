using CampaignManager.Web.Weapons;

namespace CampaignManager.Web.Model;

public class RangedCombatDetails
{
    /// <summary>
    ///     Оружие дальнего боя
    /// </summary>
    public List<Weapon> Weapons { get; set; } = new();
}