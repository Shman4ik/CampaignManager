using System.ComponentModel.DataAnnotations;

namespace CampaignManager.Web.Model;

/// <summary>
///     Оружие дальнего боя
/// </summary>
public class RangedCombatWeapon : Weapon
{
    public RangedCombatWeapon()
    {
        Type = WeaponType.RangedCombat;
    }

    [StringLength(20)] public string Ammo { get; set; } = string.Empty;

    [StringLength(10)] public string Malfunction { get; set; } = string.Empty;
}