namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
/// Полный результат одного разрешённого боевого действия
/// </summary>
public class CombatActionResult
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int Round { get; set; }

    // --- Атакующий ---
    public Guid AttackerId { get; set; }
    public string AttackerName { get; set; } = string.Empty;
    public CombatActionType ActionType { get; set; }
    public string WeaponName { get; set; } = string.Empty;
    public int AttackerSkillValue { get; set; }
    public int AttackerRoll { get; set; }
    public SuccessLevel AttackerSuccessLevel { get; set; }

    // --- Защитник (для ближнего боя / opposed roll) ---
    public Guid? DefenderId { get; set; }
    public string DefenderName { get; set; } = string.Empty;
    public CombatActionType DefenderReaction { get; set; }
    public int DefenderSkillValue { get; set; }
    public int DefenderRoll { get; set; }
    public SuccessLevel DefenderSuccessLevel { get; set; }

    // --- Исход ---
    public bool AttackerWins { get; set; }
    public string DamageFormula { get; set; } = string.Empty;
    public int DamageRolled { get; set; }
    public int BonusDamage { get; set; }
    public int TotalDamage { get; set; }
    public bool IsCritical { get; set; }
    public bool IsExtreme { get; set; }
    public bool IsImpalingWeapon { get; set; }
    public int ExtraDamage { get; set; }

    // --- Эффекты на защитника ---
    public int DefenderHpBefore { get; set; }
    public int DefenderHpAfter { get; set; }
    public bool TriggeredMajorWound { get; set; }
    public int? MajorWoundConRoll { get; set; }
    public bool MajorWoundConRollSuccess { get; set; }
    public bool DefenderKnockedUnconscious { get; set; }
    public bool DefenderDying { get; set; }
    public bool DefenderDead { get; set; }

    // --- Рассудок ---
    public int? SanityBefore { get; set; }
    public int? SanityAfter { get; set; }
    public int? SanityLoss { get; set; }
    public bool? TriggeredTemporaryInsanity { get; set; }

    // --- Описание результата ---
    public string Summary { get; set; } = string.Empty;
}
