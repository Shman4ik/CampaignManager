using CampaignManager.Web.Components.Features.Combat.Model;
using Microsoft.AspNetCore.Components;

namespace CampaignManager.Web.Components.Features.Combat.Services;

/// <summary>
/// Сохранение и восстановление боя между подключениями.
/// </summary>
public sealed partial class CombatService
{
    /// <summary>
    /// Точка, за которую Blazor держит состояние боя (см. корневой CLAUDE.md, «Circuit State Persistence»).
    /// Геттер вызывается при постановке circuit на паузу, сеттер — при возобновлении.
    /// Сервис зарегистрирован в <c>Program.cs</c> через <c>RegisterPersistentService</c>.
    /// </summary>
    [PersistentState]
    public CombatSnapshot? PersistedState
    {
        get => HasContent() ? CreateSnapshot() : null;
        set
        {
            if (value is not null)
                RestoreSnapshot(value);
        }
    }

    /// <summary>Есть ли что восстанавливать — пустой помощник боем не считается.</summary>
    public bool HasContent() => Combatants.Count > 0;

    public CombatSnapshot CreateSnapshot() => new()
    {
        Combatants = Combatants,
        CurrentRound = CurrentRound,
        CurrentTurnIndex = CurrentTurnIndex,
        CombatLog = CombatLog,
        SelectedCampaignId = SelectedCampaignId,
        UseInitiativeRolls = UseInitiativeRolls,
        UseCinematicKnockout = UseCinematicKnockout,
        UseLuckToStayConscious = UseLuckToStayConscious,
        InitiativeRolled = InitiativeRolled
    };

    public void RestoreSnapshot(CombatSnapshot snapshot)
    {
        Combatants = snapshot.Combatants;
        CurrentRound = snapshot.CurrentRound;
        CurrentTurnIndex = snapshot.CurrentTurnIndex;
        CombatLog = snapshot.CombatLog;
        SelectedCampaignId = snapshot.SelectedCampaignId;
        UseInitiativeRolls = snapshot.UseInitiativeRolls;
        UseCinematicKnockout = snapshot.UseCinematicKnockout;
        UseLuckToStayConscious = snapshot.UseLuckToStayConscious;
        InitiativeRolled = snapshot.InitiativeRolled;

        // Предпросмотр результата — состояние страницы, а не боя: после восстановления
        // подтверждать нечего.
        PendingResult = null;
        NotifyStateChanged();
    }
}
