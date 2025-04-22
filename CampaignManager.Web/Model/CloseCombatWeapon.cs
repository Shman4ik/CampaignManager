namespace CampaignManager.Web.Model;

/// <summary>
///     Оружие ближнего боя
/// </summary>
public class CloseCombatWeapon : Weapon
{
    public CloseCombatWeapon()
    {
        Type = WeaponType.CloseCombat;
    }
}