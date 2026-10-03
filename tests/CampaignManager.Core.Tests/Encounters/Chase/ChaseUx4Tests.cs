using CampaignManager.Core.Encounters;
using CampaignManager.Core.Encounters.Chase;
using static CampaignManager.Core.Tests.Encounters.Chase.ChaseScene;

namespace CampaignManager.Core.Tests.Encounters.Chase;

/// <summary>UX-1 (U4): погибший не блокирует начало погони, роль идёт за стороной, исправление проверки скорости, итог заранее.</summary>
public sealed class ChaseUx4Tests
{
    [Fact]
    public void Dead_runner_does_not_block_start_and_the_reason_names_who_is_missing()
    {
        var alive = Runner("Вильгельм", ChaseRole.Prey);
        var dead = Runner("Артур", ChaseRole.Prey);
        dead.HitPoints = 0;
        dead.Dead = true;
        var state = Track(6, alive, dead, Runner("Вампир", ChaseRole.Pursuer));
        Assert.True(ChaseRules.BeginSpeedChecks(state));

        // Проверен только Вильгельм; у Вампира строки проверки ещё нет — он назван в причине, Артур — нет.
        state.R(alive).SpeedChecked = true;
        var reason = ChaseRules.StartRejection(state);

        Assert.NotNull(reason);
        Assert.Contains("Вампир", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("Артур", reason, StringComparison.Ordinal);

        foreach (var runner in state.Chase!.Runners)
            runner.SpeedChecked = true;
        Assert.Null(ChaseRules.StartRejection(state));
    }

    [Fact]
    public void Unchecked_dead_runner_is_not_awaited()
    {
        var alive = Runner("Вильгельм", ChaseRole.Prey);
        var dead = Runner("Артур", ChaseRole.Prey);
        dead.Dead = true;
        var state = Track(6, alive, dead, Runner("Вампир", ChaseRole.Pursuer));
        ChaseRules.BeginSpeedChecks(state);

        state.R(alive).SpeedChecked = true;
        state.R(state.Participants.First(p => p.Name == "Вампир")).SpeedChecked = true;

        // Погибший так и не проверен (строки у него нет) — но погоню начать можно.
        Assert.False(state.R(dead).SpeedChecked);
        Assert.Empty(ChaseRules.AwaitingSpeedCheck(state));
        Assert.Null(ChaseRules.StartRejection(state));
    }

    [Fact]
    public void Side_change_in_setup_moves_the_role_but_not_in_the_middle_of_a_chase()
    {
        var thug = Runner("Громила", ChaseRole.Prey);
        var state = Track(6, thug, Runner("Вильгельм", ChaseRole.Prey));
        Assert.Equal(ChaseRole.Prey, state.R(thug).Role);

        EncounterEngine.SetSide(state, thug.Id, EncounterSide.Enemies);
        Assert.Equal(ChaseRole.Pursuer, state.R(thug).Role);

        EncounterEngine.SetSide(state, thug.Id, EncounterSide.Neutral);
        Assert.Equal(ChaseRole.Prey, state.R(thug).Role);

        state.Chase!.Phase = ChasePhase.Active;
        EncounterEngine.SetSide(state, thug.Id, EncounterSide.Enemies);
        Assert.Equal(ChaseRole.Prey, state.R(thug).Role); // посреди погони роль меняют отдельно
    }

    [Fact]
    public void Reopen_speed_check_clears_the_modifier_only_before_the_chase_starts()
    {
        var arthur = Runner("Артур", ChaseRole.Prey);
        var state = Track(6, arthur, Runner("Вампир", ChaseRole.Pursuer));
        ChaseRules.BeginSpeedChecks(state);
        state.R(arthur).SpeedChecked = true;
        state.R(arthur).SpeedModifier = 1;

        ChaseRules.ReopenSpeedCheck(state, arthur.Id);

        Assert.False(state.R(arthur).SpeedChecked);
        Assert.Equal(0, state.R(arthur).SpeedModifier);

        state.Chase!.Phase = ChasePhase.Active;
        state.R(arthur).SpeedChecked = true;
        state.R(arthur).SpeedModifier = 1;
        ChaseRules.ReopenSpeedCheck(state, arthur.Id);
        Assert.True(state.R(arthur).SpeedChecked);
    }

    [Fact]
    public void Start_note_warns_when_everyone_escapes_at_once()
    {
        var fast = Runner("Вильгельм", ChaseRole.Prey, mov: 9);
        var state = Track(6, fast, Runner("Вампир", ChaseRole.Pursuer, mov: 7));
        ChaseRules.BeginSpeedChecks(state);

        Assert.NotNull(ChaseRules.StartNote(state));

        state.Participants.First(p => p.Name == "Вампир").Stats.Move = 9;
        Assert.Null(ChaseRules.StartNote(state));
    }

    [Fact]
    public void Adding_several_participants_writes_one_journal_entry()
    {
        var state = new EncounterState();

        var outcomes = EncounterEngine.AddRange(state,
            [Runner("Вильгельм", ChaseRole.Prey), Runner("Хелен", ChaseRole.Prey), Runner("Вампир", ChaseRole.Pursuer)], Now);

        Assert.Equal(3, state.Participants.Count);
        Assert.All(outcomes, o => Assert.Null(o.Rejection));
        var joined = Assert.Single(state.Log, e => e.Kind == EncounterLogKind.Joined);
        Assert.Equal("Вступили: Вильгельм, Хелен, Вампир.", joined.Text);
    }

    [Fact]
    public void Participants_added_one_by_one_in_a_row_merge_into_the_same_entry()
    {
        var state = new EncounterState();

        EncounterEngine.Add(state, Runner("Вильгельм", ChaseRole.Prey), Now);
        EncounterEngine.Add(state, Runner("Хелен", ChaseRole.Prey), Now.AddSeconds(20));
        EncounterEngine.Add(state, Runner("Вампир", ChaseRole.Pursuer), Now.AddSeconds(40));

        var joined = Assert.Single(state.Log, e => e.Kind == EncounterLogKind.Joined);
        Assert.Equal("Вступили: Вильгельм, Хелен, Вампир.", joined.Text);

        // Через полчаса — уже новая запись, а не дописывание в старую.
        EncounterEngine.Add(state, Runner("Громила", ChaseRole.Pursuer), Now.AddMinutes(30));
        Assert.Equal(2, state.Log.Count(e => e.Kind == EncounterLogKind.Joined));
    }
}
