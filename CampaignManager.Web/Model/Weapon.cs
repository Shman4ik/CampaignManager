using System.ComponentModel.DataAnnotations;

namespace CampaignManager.Web.Model;

/// <summary>
///     Базовый класс для всех видов оружия
/// </summary>
public abstract class Weapon : BaseDataBaseEntity
{
    [Required] [StringLength(100)] public string Name { get; set; } = string.Empty;

    [Required] [StringLength(50)] public string Skill { get; set; } = string.Empty;

    [Required] [StringLength(50)] public string Damage { get; set; } = string.Empty;

    [Required] [StringLength(50)] public string Range { get; set; } = string.Empty;

    [Required] [StringLength(20)] public string Attacks { get; set; } = string.Empty;

    [StringLength(20)] public string Cost { get; set; } = string.Empty;

    [StringLength(500)] public string Notes { get; set; } = string.Empty;

    /// <summary>
    ///     Тип оружия (ближнего или дальнего боя)
    /// </summary>
    [Required]
    public WeaponType Type { get; set; }
}