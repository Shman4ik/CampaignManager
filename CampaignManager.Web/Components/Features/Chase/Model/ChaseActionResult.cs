using CampaignManager.Web.Components.Features.Combat.Model;

namespace CampaignManager.Web.Components.Features.Chase.Model;

public class ChaseActionResult
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int Round { get; set; }
    public ChaseActionType ActionType { get; set; }

    // Участник
    public Guid ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;

    // Цель (для боевых действий)
    public Guid? TargetId { get; set; }
    public string? TargetName { get; set; }

    // Проверка навыка
    public string? SkillName { get; set; }
    public int SkillValue { get; set; }
    public int Roll { get; set; }
    public SuccessLevel SuccessLevel { get; set; }
    public bool IsSuccess { get; set; }

    // Перемещение
    public int? LocationBefore { get; set; }
    public int? LocationAfter { get; set; }

    // Урон персонажу
    public int? DamageDealt { get; set; }
    public int? HpBefore { get; set; }
    public int? HpAfter { get; set; }

    // Помехи — бонусные кости и потерянные действия
    public int BonusDiceUsed { get; set; }
    public int MovementActionsLost { get; set; }

    // Преграды — разрушение
    public int? BarrierDamageDealt { get; set; }
    public int? BarrierHpAfter { get; set; }

    // Текст
    public string Summary { get; set; } = string.Empty;
}
