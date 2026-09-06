namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
/// DTO для настройки боевого манёвра (CoC 7e, стр. 103)
/// </summary>
public class ManeuverSetup
{
    public Guid AttackerId { get; set; }
    public Guid DefenderId { get; set; }

    public ManeuverType ManeuverType { get; set; }

    /// <summary>Навык атакующего для манёвра (обычно Ближний бой)</summary>
    public int AttackSkillValue { get; set; }

    /// <summary>Реакция цели: Уклонение или Контратака</summary>
    public CombatActionType DefenderReaction { get; set; } = CombatActionType.Dodge;

    /// <summary>Навык защитника (уклонение или ближний бой)</summary>
    public int DefenderSkillValue { get; set; }

    /// <summary>Комплекция атакующего (для расчёта штрафных костей)</summary>
    public int AttackerBuild { get; set; }

    /// <summary>Комплекция цели</summary>
    public int DefenderBuild { get; set; }

    /// <summary>Ручной бросок атакующего (null = авторбросок)</summary>
    public int? ManualAttackerRoll { get; set; }

    /// <summary>Ручной бросок защитника (null = авторбросок)</summary>
    public int? ManualDefenderRoll { get; set; }

    /// <summary>
    /// Бонусные кости атакующего, назначенные Хранителем вручную.
    /// Штрафные кости за разницу Комплекции добавляются автоматически.
    /// </summary>
    public int BonusDice { get; set; }

    /// <summary>Штрафные кости атакующего, назначенные Хранителем вручную.</summary>
    public int PenaltyDice { get; set; }

    /// <summary>Бонусные кости защитника, назначаются вручную.</summary>
    public int DefenderBonusDice { get; set; }

    /// <summary>Штрафные кости защитника, назначаются вручную.</summary>
    public int DefenderPenaltyDice { get; set; }

    /// <summary>Уже брошенные кости атакующего (если панель бросила их заранее).</summary>
    public DiceRollResult? AttackerRollDetail { get; set; }

    /// <summary>Уже брошенные кости защитника.</summary>
    public DiceRollResult? DefenderRollDetail { get; set; }
}
