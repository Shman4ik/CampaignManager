namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
/// Полный результат одного разрешённого боевого действия
/// </summary>
public class CombatActionResult
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int Round { get; set; }

    // ── Атакующий ──────────────────────────────────────────────────────
    public Guid AttackerId { get; set; }
    public string AttackerName { get; set; } = string.Empty;
    public CombatActionType ActionType { get; set; }
    public string WeaponName { get; set; } = string.Empty;
    public int AttackerSkillValue { get; set; }
    public int AttackerRoll { get; set; }
    public SuccessLevel AttackerSuccessLevel { get; set; }

    // ── Защитник (для ближнего боя / opposed roll) ─────────────────────
    public Guid? DefenderId { get; set; }
    public string DefenderName { get; set; } = string.Empty;
    public CombatActionType DefenderReaction { get; set; }
    public int DefenderSkillValue { get; set; }
    public int DefenderRoll { get; set; }
    public SuccessLevel DefenderSuccessLevel { get; set; }

    // ── Исход ──────────────────────────────────────────────────────────
    public bool AttackerWins { get; set; }

    // ── Урон атакующего по защитнику ──────────────────────────────────
    public string DamageFormula { get; set; } = string.Empty;
    public int DamageRolled { get; set; }
    public int BonusDamage { get; set; }
    public int ExtraDamage { get; set; }
    public int RawDamage { get; set; }        // Урон до вычета брони
    public int ArmorReduction { get; set; }   // Сколько поглотила броня
    public int TotalDamage { get; set; }      // Итоговый урон после брони
    public bool IsCritical { get; set; }      // Критический успех (01)
    public bool IsExtreme { get; set; }       // Чрезвычайный/экстремальный (≤навык/5)
    public bool IsImpalingWeapon { get; set; }
    public bool IsInstantDeath { get; set; }  // Одним ударом ≥ макс. ПЗ
    public bool IsMalfunction { get; set; }   // Осечка / заклинило
    public string? MalfunctionMessage { get; set; }

    // ── Эффекты на защитника ──────────────────────────────────────────
    public int DefenderHpBefore { get; set; }
    public int DefenderHpAfter { get; set; }
    public bool TriggeredMajorWound { get; set; }   // Урон ≥ ½ макс. ПЗ
    public bool DefenderFallsProne { get; set; }    // Упал при серьёзной ране
    public int? MajorWoundConRoll { get; set; }
    public bool MajorWoundConRollSuccess { get; set; }
    public bool DefenderKnockedUnconscious { get; set; }
    public bool DefenderDying { get; set; }
    public bool DefenderDead { get; set; }

    // ── Контратака (fight back — урон по атакующему) ──────────────────
    public int AttackerHpBefore { get; set; }
    public int AttackerHpAfter { get; set; }
    public string CounterAttackDamageFormula { get; set; } = string.Empty;
    public int CounterDamageRolled { get; set; }
    public int CounterBonusDamage { get; set; }
    public int CounterRawDamage { get; set; }
    public int CounterArmorReduction { get; set; }
    public int CounterTotalDamage { get; set; }
    public bool AttackerTriggeredMajorWound { get; set; }
    public bool AttackerFallsProne { get; set; }
    public int? AttackerMajorWoundConRoll { get; set; }
    public bool AttackerMajorWoundConRollSuccess { get; set; }
    public bool AttackerKnockedUnconscious { get; set; }
    public bool AttackerDying { get; set; }
    public bool AttackerDead { get; set; }

    // ── Манёвры ────────────────────────────────────────────────────────
    public ManeuverType? ManeuverType { get; set; }
    public bool ManeuverSucceeded { get; set; }

    // ── Рассудок ───────────────────────────────────────────────────────
    public int? SanityBefore { get; set; }
    public int? SanityAfter { get; set; }
    public int? SanityLoss { get; set; }
    public bool? TriggeredTemporaryInsanity { get; set; }

    // ── Описание результата ────────────────────────────────────────────
    public string Summary { get; set; } = string.Empty;
}
