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

    // Проверка навыка
    public string? SkillName { get; set; }
    public int SkillValue { get; set; }
    public int Roll { get; set; }
    public SuccessLevel SuccessLevel { get; set; }
    public bool IsSuccess { get; set; }

    // Перемещение
    public int? LocationBefore { get; set; }
    public int? LocationAfter { get; set; }

    // Урон
    public int? DamageDealt { get; set; }
    public int? HpBefore { get; set; }
    public int? HpAfter { get; set; }

    // Текст
    public string Summary { get; set; } = string.Empty;
}
