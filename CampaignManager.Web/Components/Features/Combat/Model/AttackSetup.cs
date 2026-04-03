using CampaignManager.Web.Components.Features.Weapons.Model;

namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
/// DTO для настройки атаки мастером перед разрешением
/// </summary>
public class AttackSetup
{
    public Guid AttackerId { get; set; }
    public Guid DefenderId { get; set; }

    /// <summary>Оружие персонажа (для игроков)</summary>
    public Weapon? SelectedWeapon { get; set; }

    /// <summary>Название атаки существа (из CombatDescriptions)</summary>
    public string? CreatureAttackName { get; set; }

    /// <summary>Урон атаки существа (свободный текст, напр. "1D6+1D4")</summary>
    public string? CreatureAttackDamage { get; set; }

    /// <summary>Навык атаки (базовое значение, до модификаторов дальности)</summary>
    public int AttackSkillValue { get; set; }

    /// <summary>Ближний бой или дальний</summary>
    public bool IsMelee { get; set; }

    /// <summary>Реакция защитника (Уклонение или Ответный удар), только для ближнего боя</summary>
    public CombatActionType DefenderReaction { get; set; } = CombatActionType.Dodge;

    /// <summary>Навык защитника (уклонение или ближний бой)</summary>
    public int DefenderSkillValue { get; set; }

    // ── Ручной ввод бросков (null = авторбросок) ────────────────────────

    /// <summary>Результат d100 атакующего (null = авторбросок)</summary>
    public int? ManualAttackerRoll { get; set; }

    /// <summary>Результат d100 защитника (null = авторбросок)</summary>
    public int? ManualDefenderRoll { get; set; }

    /// <summary>Итог броска урона оружия (null = авторбросок). Не используется при чрезвычайном/крит. успехе.</summary>
    public int? ManualWeaponDamageRoll { get; set; }

    /// <summary>Итог броска бонуса к урону (null = авторбросок)</summary>
    public int? ManualDamageBonusRoll { get; set; }

    /// <summary>Дополнительный бросок урона при проникающей ране (null = авторбросок)</summary>
    public int? ManualExtraImpalingRoll { get; set; }

    /// <summary>Урон контратаки по атакующему — итог броска оружия (null = авторбросок)</summary>
    public int? ManualCounterWeaponDamageRoll { get; set; }

    /// <summary>Урон контратаки по атакующему — итог броска БкУ (null = авторбросок)</summary>
    public int? ManualCounterDamageBonusRoll { get; set; }

    /// <summary>Формула урона контратаки защитника (если fight back)</summary>
    public string CounterAttackDamageFormula { get; set; } = "1D3";

    // ── Модификаторы (бонусные / штрафные кости) ─────────────────────────

    /// <summary>Число бонусных костей (0–2). Итоговые = max(0, BonusDice - PenaltyDice)</summary>
    public int BonusDice { get; set; }

    /// <summary>Число штрафных костей (0–2)</summary>
    public int PenaltyDice { get; set; }

    // ── Особые условия ───────────────────────────────────────────────────

    /// <summary>Внезапная атака — цель не подозревала об атаке (автоуспех или бонусная кость)</summary>
    public bool IsSurpriseAttack { get; set; }

    /// <summary>Цель не уклоняется и не контратакует (не подозревала) — атака автоуспешна кроме провала</summary>
    public bool TargetUnawareMeansAutoSuccess { get; set; }

    // ── Дальность (огнестрельное) ────────────────────────────────────────

    /// <summary>Дальность стрельбы — влияет на эффективное значение навыка</summary>
    public RangeLevel RangeLevel { get; set; } = RangeLevel.Base;
}
