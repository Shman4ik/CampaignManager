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

    /// <summary>
    /// Действия перемещения, которые исполнитель тратит на само действие
    /// (перемещение, атака, бонусные кости к проверке помехи).
    /// </summary>
    public int ActorMovementActionsSpent { get; set; }

    /// <summary>
    /// Действия перемещения, потерянные пострадавшим (целью, если она есть, иначе исполнителем)
    /// из-за провала помехи или удачного манёвра противника.
    /// </summary>
    public int MovementActionsLost { get; set; }

    // Преграды — разрушение
    public int? BarrierLocation { get; set; }
    public int? BarrierDamageDealt { get; set; }
    public int? BarrierHpAfter { get; set; }

    // Транспорт (стр. 136, 139)
    /// <summary>Потеря Комплекции цели.</summary>
    public double? TargetBuildLoss { get; set; }

    /// <summary>Потеря Комплекции самого исполнителя — отдача от тарана или столкновения.</summary>
    public double? ActorBuildLoss { get; set; }

    /// <summary>Урон, «сдачу» с которого транспорт цели копит до следующего полного десятка.</summary>
    public int? TargetVehicleDamage { get; set; }

    /// <summary>Штрафные кости, которые Хранитель должен учесть в броске (разгон, размер цели, шины).</summary>
    public int PenaltyDice { get; set; }

    /// <summary>Действие вывело исполнителя из погони: спрятался или преследователь потерял след.</summary>
    public bool RemovesActorFromChase { get; set; }

    /// <summary>Помеха или преграда, которую действие создаёт в локации (стр. 141).</summary>
    public ChaseLocation? ObstacleToPlace { get; set; }
    public int? ObstacleLocation { get; set; }

    /// <summary>Результат уже применён к состоянию погони — защита от повторного применения.</summary>
    public bool IsApplied { get; set; }

    // Текст
    public string Summary { get; set; } = string.Empty;
}
