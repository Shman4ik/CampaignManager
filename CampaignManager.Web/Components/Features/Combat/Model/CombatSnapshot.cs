namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
/// Полное состояние боя для сохранения между подключениями.
/// <para>
/// <c>CombatService</c> живёт в circuit, поэтому обрыв связи, сон планшета или обновление
/// сервера иначе стирают бой целиком. Снапшот отдаётся Blazor через
/// <c>CombatService.PersistedState</c> и восстанавливается при возобновлении circuit.
/// </para>
/// </summary>
public class CombatSnapshot
{
    public List<Combatant> Combatants { get; set; } = [];
    public int CurrentRound { get; set; } = 1;
    public int CurrentTurnIndex { get; set; }
    public List<CombatActionResult> CombatLog { get; set; } = [];
    public Guid? SelectedCampaignId { get; set; }

    // Необязательные правила (стр. 122–123)
    public bool UseInitiativeRolls { get; set; }
    public bool UseCinematicKnockout { get; set; }
    public bool UseLuckToStayConscious { get; set; }

    /// <summary>Порядок инициативы уже определён броском и зафиксирован до конца боя.</summary>
    public bool InitiativeRolled { get; set; }
}
