using CampaignManager.Web.Components.Features.Combat.Model;

namespace CampaignManager.Web.Components.Features.Combat.Services;

/// <summary>
/// Service for managing combat encounters and turn order
/// </summary>
public sealed class CombatService
{
    public List<Combatant> Combatants { get; private set; } = new();
    public int CurrentRound { get; private set; } = 1;
    public int CurrentTurnIndex { get; private set; } = 0;

    public event Action? OnChange;

    public void AddCombatant(Combatant combatant)
    {
        Combatants.Add(combatant);
        NotifyStateChanged();
    }

    public void RemoveCombatant(Combatant combatant)
    {
        Combatants.Remove(combatant);
        NotifyStateChanged();
    }

    public void SortByInitiative()
    {
        Combatants = Combatants.OrderByDescending(c => c.Initiative)
                               .ThenByDescending(c => c.Dexterity)
                               .ToList();
        NotifyStateChanged();
    }

    public void NextTurn()
    {
        if (Combatants.Count == 0) return;

        CurrentTurnIndex++;
        if (CurrentTurnIndex >= Combatants.Count)
        {
            CurrentTurnIndex = 0;
            CurrentRound++;
        }
        NotifyStateChanged();
    }

    public void NextRound()
    {
        CurrentRound++;
        CurrentTurnIndex = 0;
        NotifyStateChanged();
    }

    public Combatant? GetActiveCombatant()
    {
        if (Combatants.Count == 0) return null;
        if (CurrentTurnIndex >= Combatants.Count) return Combatants[0];
        return Combatants[CurrentTurnIndex];
    }
    
    public void ResetCombat()
    {
        Combatants.Clear();
        CurrentRound = 1;
        CurrentTurnIndex = 0;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
