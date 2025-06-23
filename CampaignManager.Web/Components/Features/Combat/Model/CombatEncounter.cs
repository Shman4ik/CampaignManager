namespace CampaignManager.Web.Components.Features.Combat.Model;

public enum CombatState
{
    Setup = 0,
    RollingInitiative = 1,
    Active = 2,
    Paused = 3,
    Finished = 4
}

public class CombatEncounter
{
    public Guid? CampaignId { get; set; }
    public Guid? ScenarioId { get; set; }

    // Combat State
    public CombatState State { get; set; } = CombatState.Setup;
    public int CurrentRound { get; set; } = 0;
    public int CurrentTurnIndex { get; set; } = 0;

    // Participants
    public List<CombatParticipant> Participants { get; set; } = new();
    public List<CombatAction> CombatLog { get; set; } = new();

    // Initiative Order (sorted by total initiative, descending)
    public List<Guid> InitiativeOrder { get; set; } = new();

    // Combat Settings
    public bool UsePositioning { get; set; } = false;
    public bool AutoRollInitiative { get; set; } = true;
    public bool AllowDelayedActions { get; set; } = true;

    public CombatParticipant? CurrentParticipant
    {
        get
        {
            if (InitiativeOrder.Count == 0 || CurrentTurnIndex >= InitiativeOrder.Count)
                return null;

            var participantId = InitiativeOrder[CurrentTurnIndex];
            return Participants.FirstOrDefault(p => p.Id == participantId);
        }
    }

    public List<CombatParticipant> AliveParticipants =>
        Participants.Where(p => !p.HasCondition(CombatConditionType.Dead)).ToList();

    public List<CombatParticipant> ConsciousParticipants =>
        Participants.Where(p => p.IsConscious).ToList();

    public bool IsCombatOver
    {
        get
        {
            var aliveCharacters = Participants.Where(p => p.Type == "Character" && !p.HasCondition(CombatConditionType.Dead));
            var aliveEnemies = Participants.Where(p => p.Type != "Character" && !p.HasCondition(CombatConditionType.Dead));

            return !aliveCharacters.Any() || !aliveEnemies.Any();
        }
    }

    public void AddParticipant(CombatParticipant participant)
    {
        Participants.Add(participant);

        // Recalculate initiative order if combat has started
        if (State != CombatState.Setup)
        {
            RecalculateInitiativeOrder();
        }
    }

    public void RemoveParticipant(Guid participantId)
    {
        Participants.RemoveAll(p => p.Id == participantId);
        InitiativeOrder.RemoveAll(id => id == participantId);

        // Adjust current turn index if needed
        if (CurrentTurnIndex >= InitiativeOrder.Count)
        {
            CurrentTurnIndex = 0;
        }
    }

    public void RollInitiative(Random random)
    {
        foreach (var participant in Participants)
        {
            participant.InitiativeRoll = random.Next(1, 101); // 1d100
            participant.InitiativeModifier = participant.CombatStats.GetInitiativeWithFirearm();
        }

        RecalculateInitiativeOrder();
        State = CombatState.Active;
    }

    private void RecalculateInitiativeOrder()
    {
        InitiativeOrder = Participants
            .OrderByDescending(p => p.TotalInitiative)
            .ThenByDescending(p => p.CombatStats.Initiative) // Tie-breaker: higher DEX goes first
            .Select(p => p.Id)
            .ToList();
    }

    public void StartCombat(Random random)
    {
        if (State != CombatState.Setup)
            return;

        if (AutoRollInitiative)
        {
            RollInitiative(random);
        }
        else
        {
            State = CombatState.RollingInitiative;
        }

        CurrentRound = 1;
        CurrentTurnIndex = 0;
    }

    public void NextTurn()
    {
        if (State != CombatState.Active)
            return;

        CurrentTurnIndex++;

        // Check if round is complete
        if (CurrentTurnIndex >= InitiativeOrder.Count)
        {
            NextRound();
        }
    }

    public void NextRound()
    {
        CurrentRound++;
        CurrentTurnIndex = 0;

        // Reset participant round counters
        foreach (var participant in Participants)
        {
            participant.ResetRoundCounters();
        }

        // Check for dying participants
        foreach (var participant in Participants.Where(p => p.HasCondition(CombatConditionType.Dying)))
        {
            // This would need a CON roll - handled by combat engine
        }

        // Check if combat is over
        if (IsCombatOver)
        {
            EndCombat();
        }
    }

    public void EndCombat()
    {
        State = CombatState.Finished;
    }

    public void AddToCombatLog(CombatAction action)
    {
        action.Round = CurrentRound;
        CombatLog.Add(action);
    }

    public List<CombatAction> GetRoundLog(int round)
    {
        return CombatLog.Where(a => a.Round == round).ToList();
    }

    public List<CombatAction> GetRecentLog(int actionCount = 10)
    {
        return CombatLog.TakeLast(actionCount).ToList();
    }
}