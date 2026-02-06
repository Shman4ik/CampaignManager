using CampaignManager.Web.Components.Features.Weapons.Model;

namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
/// DTO для настройки атаки мастером перед разрешением
/// </summary>
public class AttackSetup
{
    public Guid AttackerId { get; set; }
    public Guid DefenderId { get; set; }

    /// <summary>
    /// Оружие персонажа (для игроков)
    /// </summary>
    public Weapon? SelectedWeapon { get; set; }

    /// <summary>
    /// Название атаки существа (из CombatDescriptions)
    /// </summary>
    public string? CreatureAttackName { get; set; }

    /// <summary>
    /// Урон атаки существа (свободный текст, напр. "1D6+1D4")
    /// </summary>
    public string? CreatureAttackDamage { get; set; }

    /// <summary>
    /// Навык атаки (автозаполнение с возможностью ручной правки)
    /// </summary>
    public int AttackSkillValue { get; set; }

    /// <summary>
    /// Ближний бой или дальний
    /// </summary>
    public bool IsMelee { get; set; }

    /// <summary>
    /// Реакция защитника (Уклонение или Ответный удар), только для ближнего боя
    /// </summary>
    public CombatActionType DefenderReaction { get; set; } = CombatActionType.Dodge;

    /// <summary>
    /// Навык защитника (уклонение или ближний бой)
    /// </summary>
    public int DefenderSkillValue { get; set; }
}
