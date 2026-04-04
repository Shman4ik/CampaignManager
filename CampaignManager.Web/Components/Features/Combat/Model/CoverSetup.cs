namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
/// DTO для настройки укрытия от огня (CoC 7e стр. 111)
/// </summary>
public class CoverSetup
{
    public Guid CombatantId { get; set; }
    public int DodgeSkill { get; set; }

    /// <summary>Ручной бросок d100 (null = авторбросок)</summary>
    public int? ManualRoll { get; set; }
}
