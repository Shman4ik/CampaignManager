using CampaignManager.Web.Model;
using System.ComponentModel.DataAnnotations;

namespace CampaignManager.Web.Weapons;

/// <summary>
///     Класс для всех видов оружия
/// </summary>
public class Weapon : BaseDataBaseEntity
{
    public Weapon()
    {
        Type = WeaponType.Melee;
    }
    /// <summary>
    ///     Тип оружия (ближнего или дальнего боя)
    /// </summary>
    [Required]
    public WeaponType Type { get; set; }
    /// <summary>
    ///     Название оружия
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Навык, используемый для владения оружием
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Skill { get; set; } = string.Empty;

    /// <summary>
    ///     Признак оружия эпохи 1920-х годов
    /// </summary>
    public bool Is1920 { get; set; }

    /// <summary>
    ///     Признак современного оружия
    /// </summary>
    public bool IsModern { get; set; }

    /// <summary>
    ///     Урон, наносимый оружием
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Damage { get; set; } = string.Empty;

    /// <summary>
    ///     Дальность действия оружия
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Range { get; set; } = string.Empty;

    /// <summary>
    ///     Количество атак, совершаемых оружием
    /// </summary>
    [Required]
    [StringLength(40)]
    public string Attacks { get; set; } = string.Empty;

    /// <summary>
    ///     Стоимость оружия
    /// </summary>
    [StringLength(20)]
    public string Cost { get; set; } = string.Empty;

    /// <summary>
    ///     Дополнительные примечания к оружию
    /// </summary>
    [StringLength(500)]
    public string Notes { get; set; } = string.Empty;



    /// <summary>
    ///     Тип боеприпасов (для оружия дальнего боя)
    /// </summary>
    [StringLength(20)]
    public string Ammo { get; set; } = string.Empty;

    /// <summary>
    ///     Шанс осечки (для оружия дальнего боя)
    /// </summary>
    [StringLength(10)]
    public string Malfunction { get; set; } = string.Empty;
}