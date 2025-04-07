using System.ComponentModel.DataAnnotations;

namespace CampaignManager.Web.Model;

/// <summary>
/// Базовый класс для всех видов оружия
/// </summary>
public abstract class Weapon : BaseDataBaseEntity
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string Skill { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string Damage { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string Range { get; set; } = string.Empty;
    
    [Required]
    [StringLength(20)]
    public string Attacks { get; set; } = string.Empty;
    
    [StringLength(20)]
    public string Cost { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;
    
    /// <summary>
    /// Тип оружия (ближнего или дальнего боя)
    /// </summary>
    [Required]
    public WeaponType Type { get; set; }
}

/// <summary>
/// Оружие ближнего боя
/// </summary>
public class CloseCombatWeapon : Weapon
{
    public CloseCombatWeapon()
    {
        Type = WeaponType.CloseCombat;
    }
}

/// <summary>
/// Оружие дальнего боя
/// </summary>
public class RangedCombatWeapon : Weapon
{
    [StringLength(20)]
    public string Ammo { get; set; } = string.Empty;
    
    [StringLength(10)]
    public string Malfunction { get; set; } = string.Empty;
    
    public RangedCombatWeapon()
    {
        Type = WeaponType.RangedCombat;
    }
}

/// <summary>
/// Перечисление типов оружия
/// </summary>
public enum WeaponType
{
    CloseCombat,
    RangedCombat
}