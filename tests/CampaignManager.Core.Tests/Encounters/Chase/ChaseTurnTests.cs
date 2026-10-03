using CampaignManager.Core.Documents;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Encounters.Chase;
using CampaignManager.Core.KeeperScreen;
using CampaignManager.Core.Tests.Infrastructure;
using static CampaignManager.Core.Tests.Encounters.Chase.ChaseScene;

namespace CampaignManager.Core.Tests.Encounters.Chase;

/// <summary>
/// Порядок ходов, ничья по ЛВК, что можно сделать в ход, поимка, побег и конец погони (перенос <c>TurnOrderTests</c> и
/// <c>OutcomeTests</c> T0.2; ошибки очереди F-P03…F-P05 закрыты ядром — <c>EncounterQueueTests</c>).
/// </summary>
public sealed class ChaseTurnTests
{
    /// <summary>Трое по убыванию ЛВК: преследователь A (80), жертва B (60), преследователь C (40).</summary>
    private static (EncounterState State, EncounterParticipant A, EncounterParticipant B, EncounterParticipant C) ThreeRunners()
    {
        var c = Runner("C", ChaseRole.Pursuer, dex: 40);
        var a = Runner("A", ChaseRole.Pursuer, dex: 80);
        var b = Runner("B", ChaseRole.Prey, dex: 60);
        return (Track(6, c, a, b).Started(), a, b, c);
    }

    private static (EncounterState State, EncounterParticipant Prey, EncounterParticipant Pursuer) Pair(int locations = 6)
    {
        var prey = Runner("Артур", ChaseRole.Prey);
        var pursuer = Runner("Вампир", ChaseRole.Pursuer);
        return (Track(locations, prey, pursuer).Started(), prey, pursuer);
    }

    [Fact]
    [Trait("page", "132")]
    public void Start_orders_by_dex_and_starts_round_one()
    {
        var (state, a, b, c) = ThreeRunners();

        Assert.Equal([a.Id, b.Id, c.Id], state.TurnOrder);
        Assert.Equal(ChasePhase.Active, state.Chase!.Phase);
        Assert.Equal(1, state.Round);
        Assert.Equal(a.Id, state.ActiveParticipantId);
    }

    [Fact]
    [Trait("page", "131")]
    public void Start_equal_moves_pursuers_on_one_prey_two_ahead()
    {
        var (state, a, b, c) = ThreeRunners();

        Assert.Equal((1, 3, 1), (state.R(a).Location, state.R(b).Location, state.R(c).Location));
    }

    [Fact]
    [Trait("page", "132")]
    public void New_round_recounts_actions_and_attacks()
    {
        var (state, a, _, _) = ThreeRunners();
        state.R(a).ActionsLeft = 0;
        state.R(a).Attacks = 1;

        EncounterQueue.Next(state, Now);
        EncounterQueue.Next(state, Now);
        EncounterQueue.Next(state, Now);

        Assert.Equal(2, state.Round);
        Assert.Equal(a.Id, state.ActiveParticipantId);
        Assert.Equal((1, 0), (state.R(a).ActionsLeft, state.R(a).Attacks));
    }

    // ── Ничья по ЛВК (стр. 132) ──────────────────────────────────────

    [Theory]
    [Trait("page", "132")]
    // уровень выше — ходит первым
    [InlineData(10, 30, true)]
    [InlineData(40, 20, false)]
    // уровни равны — меньший бросок
    [InlineData(30, 28, false)]
    [InlineData(28, 30, true)]
    [InlineData(30, 30, true)]
    public void Dex_tie_written_rolls_winner_goes_first(int rollA, int rollB, bool firstWins)
    {
        var a = Runner("A", ChaseRole.Prey, dex: 50);
        var b = Runner("B", ChaseRole.Pursuer, dex: 50);
        var state = Track(6, a, b);
        Assert.Single(ChaseRules.DexTies(state));

        var result = ChaseRules.ResolveDexTie(state, a.Id, b.Id, rollA, rollB, NoDice, Now);
        Assert.Empty(ChaseRules.DexTies(state)); // решённую пару больше не спрашиваем
        state.Started();

        Assert.Equal(firstWins, result);
        Guid[] expected = firstWins ? [a.Id, b.Id] : [b.Id, a.Id];
        Assert.Equal(expected, state.TurnOrder);
    }

    /// <summary>F-P09: встречную ЛВК можно вписать, а не только бросить (в v1 кнопка всегда бросала сама).</summary>
    [Fact]
    [Trait("page", "132")]
    [Trait("finding", "F-P09")]
    public void Dex_tie_without_rolls_rolls_both_in_order()
    {
        var a = Runner("A", ChaseRole.Prey, dex: 50);
        var b = Runner("B", ChaseRole.Pursuer, dex: 50);
        var state = Track(6, a, b);

        // A: единицы 0, десятки 9 → 90; B: 5, 0 → 5
        var firstWins = ChaseRules.ResolveDexTie(state, a.Id, b.Id, null, null, ScriptedDice.Of(0, 9, 5, 0), Now);

        Assert.False(firstWins);
        Assert.Equal([b.Id, a.Id], state.Participants.Select(p => p.Id));
    }

    // ── Что можно сделать в ход ──────────────────────────────────────

    [Fact]
    [Trait("page", "136")]
    public void Close_combat_only_in_same_location_firearms_anywhere()
    {
        var (state, prey, pursuer) = Pair();

        var apart = ChaseRules.AvailableActions(state, pursuer.Id);
        Assert.DoesNotContain(ChaseActionKind.Melee, apart);
        Assert.DoesNotContain(ChaseActionKind.Maneuver, apart);
        Assert.Contains(ChaseActionKind.Ranged, apart);

        ChaseRules.SetPosition(state, pursuer.Id, state.R(prey).Location);
        var together = ChaseRules.AvailableActions(state, pursuer.Id);
        Assert.Contains(ChaseActionKind.Melee, together);
        Assert.Contains(ChaseActionKind.Maneuver, together);
        Assert.Equal([prey.Id], ChaseRules.CloseTargets(state, pursuer.Id).Select(p => p.Id));
    }

    [Fact]
    [Trait("page", "136")]
    [Trait("finding", "F-P20")]
    public void Attacks_per_round_like_in_combat()
    {
        var prey = Runner("Артур", ChaseRole.Prey);
        var pursuer = Runner("Вампир", ChaseRole.Pursuer, mov: 10);
        var state = Track(6, prey, pursuer).Started();
        ChaseRules.SetPosition(state, pursuer.Id, state.R(prey).Location);

        state.Apply(ChaseActions.Melee(state, pursuer.Id, prey.Id, Skill("Драка", 50, 90), null, null, NoDice));

        var actions = ChaseRules.AvailableActions(state, pursuer.Id);
        Assert.DoesNotContain(ChaseActionKind.Melee, actions);
        Assert.DoesNotContain(ChaseActionKind.Ranged, actions);
        Assert.Contains(ChaseActionKind.Track, actions);
    }

    [Fact]
    [Trait("page", "139")]
    public void Hide_for_prey_track_for_pursuer()
    {
        var (state, prey, pursuer) = Pair();

        Assert.Contains(ChaseActionKind.Hide, ChaseRules.AvailableActions(state, prey.Id));
        Assert.DoesNotContain(ChaseActionKind.Track, ChaseRules.AvailableActions(state, prey.Id));
        Assert.Contains(ChaseActionKind.Track, ChaseRules.AvailableActions(state, pursuer.Id));
        Assert.DoesNotContain(ChaseActionKind.Hide, ChaseRules.AvailableActions(state, pursuer.Id));
    }

    [Fact]
    [Trait("page", "139")]
    public void Passenger_shoots_navigates_or_creates_obstacles_but_does_not_move()
    {
        var driver = Runner("Водитель", ChaseRole.Prey);
        var passenger = Runner("Штурман", ChaseRole.Prey);
        var state = Track(6, driver, passenger, Runner("Вампир", ChaseRole.Pursuer)).InVehicle(driver, 5, speed: 8);
        ChaseRules.SetPassenger(state, passenger.Id, driver.Id);
        state.Started();

        Assert.Equal([ChaseActionKind.Ranged, ChaseActionKind.Navigate, ChaseActionKind.CreateObstacle],
            ChaseRules.AvailableActions(state, passenger.Id));
        Assert.Contains(ChaseActionKind.DriverControl, ChaseRules.AvailableActions(state, driver.Id));
    }

    [Fact]
    [Trait("page", "139")]
    [Trait("finding", "F-P18")]
    public void Passengers_move_with_their_driver()
    {
        var driver = Runner("Водитель", ChaseRole.Prey);
        var passenger = Runner("Пассажир", ChaseRole.Prey);
        var state = Track(8, driver, passenger, Runner("Вампир", ChaseRole.Pursuer)).InVehicle(driver, 5, speed: 8);
        ChaseRules.SetPassenger(state, passenger.Id, driver.Id);
        state.Started();

        state.Apply(ChaseActions.Move(state, driver.Id));

        Assert.Equal(state.R(driver).Location, state.R(passenger).Location);
    }

    [Fact]
    [Trait("page", "142")]
    public void Flyer_may_pass_over_ground_obstacles()
    {
        var byakhee = Runner("Бьякхи", ChaseRole.Pursuer, mov: 5);
        byakhee.Stats.Fly = 16;
        var state = Track(8, Runner("Артур", ChaseRole.Prey, mov: 16), byakhee);
        ChaseRules.ChangeMode(state, byakhee.Id, MovementMode.Flying, null, Now);
        state.Started();
        state.PutBarrier(state.R(byakhee).Location + 1);

        var actions = ChaseRules.AvailableActions(state, byakhee.Id);
        Assert.Contains(ChaseActionKind.Move, actions);
        Assert.Contains(ChaseActionKind.Barrier, actions);
        Assert.Contains("По воздуху", ChaseActions.Move(state, byakhee.Id).Resolution.Lines.Single());
    }

    [Fact]
    [Trait("page", "139")]
    public void Nobody_attacks_their_own_car()
    {
        var driver = Runner("Водитель", ChaseRole.Prey);
        var passenger = Runner("Пассажир", ChaseRole.Prey);
        var vampire = Runner("Вампир", ChaseRole.Pursuer);
        var state = Track(6, driver, passenger, vampire).InVehicle(driver, 5, speed: 8);
        ChaseRules.SetPassenger(state, passenger.Id, driver.Id);
        state.Started();
        ChaseRules.SetPosition(state, vampire.Id, state.R(driver).Location);

        Assert.Equal([vampire.Id], ChaseRules.CloseTargets(state, driver.Id).Select(p => p.Id));
        Assert.Equal([vampire.Id], ChaseRules.RangedTargets(state, passenger.Id).Select(p => p.Id));
        Assert.Equal([driver.Id, passenger.Id], ChaseRules.CloseTargets(state, vampire.Id).Select(p => p.Id));
    }

    [Fact]
    public void No_actions_out_of_turn_order_or_chase()
    {
        var (state, prey, _) = Pair();
        prey.IsOut = true;

        Assert.Empty(ChaseRules.AvailableActions(state, prey.Id));
        Assert.Empty(ChaseRules.AvailableActions(Track(6, Runner("Артур", ChaseRole.Prey)), Guid.NewGuid()));
    }

    // ── Поимка, побег, конец (стр. 135) ──────────────────────────────

    [Fact]
    [Trait("page", "135")]
    public void Catch_last_prey_ends_chase()
    {
        var (state, prey, pursuer) = Pair();

        var caught = ChaseRules.Catch(state, prey.Id, pursuer.Id)!;
        EncounterEngine.Apply(state, caught, Now);

        Assert.True(prey.IsOut);
        Assert.Equal(ChaseStatus.Caught, ChaseRules.StatusOf(state, prey.Id));
        Assert.True(ChaseRules.IsOver(state));
        Assert.Equal(ChasePhase.Ended, state.Chase!.Phase);
        Assert.Contains(state.Log, e => e.Kind == EncounterLogKind.Caught && e.ActorId == pursuer.Id);
        Assert.Null(ChaseRules.Catch(state, prey.Id, null)); // уже пойман
    }

    [Fact]
    public void Chase_end_entry_names_the_outcomes_in_groups_with_capitals()
    {
        var (state, prey, pursuer) = Pair();

        EncounterEngine.Apply(state, ChaseRules.Catch(state, prey.Id, pursuer.Id)!, Now);

        var end = state.Log.Last(e => e.Text.StartsWith("Погоня окончена", StringComparison.Ordinal));
        Assert.Equal($"Погоня окончена. Пойманы: {prey.Name}.", end.Text);
    }

    [Fact]
    [Trait("page", "135")]
    public void Catch_one_of_two_prey_chase_goes_on()
    {
        var first = Runner("Артур", ChaseRole.Prey);
        var second = Runner("Билл", ChaseRole.Prey);
        var state = Track(6, first, second, Runner("Вампир", ChaseRole.Pursuer)).Started();

        EncounterEngine.Apply(state, ChaseRules.Catch(state, first.Id, null)!, Now);

        Assert.False(ChaseRules.IsOver(state));
        Assert.Equal(ChasePhase.Active, state.Chase!.Phase);
    }

    [Fact]
    [Trait("page", "135")]
    public void Reaching_prey_is_contact_not_capture()
    {
        var (state, prey, pursuer) = Pair();
        ChaseRules.SetPosition(state, pursuer.Id, 2);

        state.Apply(ChaseActions.Move(state, pursuer.Id));

        Assert.Equal(state.R(prey).Location, state.R(pursuer).Location);
        Assert.False(prey.IsOut);
        var contact = Assert.Single(ChaseRules.Contacts(state));
        Assert.Equal((prey.Id, pursuer.Id), (contact.Prey.Id, contact.Pursuers.Single().Id));
    }

    [Fact]
    [Trait("page", "135")]
    public void Prey_reaching_last_location_ahead_escapes_automatically()
    {
        var (state, prey, _) = Pair(locations: 4);

        state.Apply(ChaseActions.Move(state, prey.Id));

        Assert.Equal(4, state.R(prey).Location);
        Assert.True(prey.IsOut);
        Assert.Equal(ChaseStatus.Escaped, ChaseRules.StatusOf(state, prey.Id));
        Assert.Equal(ChasePhase.Ended, state.Chase!.Phase);
        Assert.Contains(state.Log, e => e.Kind == EncounterLogKind.Escaped);
    }

    [Fact]
    [Trait("page", "135")]
    public void Prey_on_last_location_with_pursuer_does_not_escape()
    {
        var (state, prey, pursuer) = Pair(locations: 4);
        ChaseRules.SetPosition(state, pursuer.Id, 4);

        state.Apply(ChaseActions.Move(state, prey.Id));

        Assert.False(prey.IsOut);
        Assert.Equal(ChasePhase.Active, state.Chase!.Phase);
    }

    [Fact]
    [Trait("page", "139")]
    public void Last_pursuer_gone_prey_escape()
    {
        var (state, prey, pursuer) = Pair();

        state.Apply(ChaseActions.Track(state, pursuer.Id, Skill("Чтение следов", 50, 90), NoDice));

        Assert.Equal(ChaseStatus.Escaped, ChaseRules.StatusOf(state, prey.Id));
        Assert.Equal(ChasePhase.Ended, state.Chase!.Phase);
        Assert.Contains(state.Log, e => e.Kind == EncounterLogKind.Escaped && e.Text.Contains("некому", StringComparison.Ordinal));
    }

    [Fact]
    public void Returned_runner_is_back_in_the_chase()
    {
        var prey = Runner("Артур", ChaseRole.Prey);
        var pursuer = Runner("Вампир", ChaseRole.Pursuer);
        var state = Track(6, prey, pursuer, Runner("Упырь", ChaseRole.Pursuer)).Started();
        EncounterEngine.Apply(state, ChaseActions.Track(state, pursuer.Id, Skill("Чтение следов", 50, 90), NoDice).Resolution, Now);
        Assert.Equal(ChaseStatus.LostTrail, ChaseRules.StatusOf(state, pursuer.Id));

        EncounterEngine.Apply(state, new EncounterResolution
        {
            Effects = [new EncounterEffect { Kind = EncounterEffectKind.Out, ParticipantId = pursuer.Id, Flag = false }],
        }, Now);

        Assert.Equal(ChaseStatus.Running, ChaseRules.StatusOf(state, pursuer.Id));
        Assert.NotEmpty(ChaseRules.AvailableActions(state, pursuer.Id));
        Assert.False(prey.IsOut);
    }

    // ── Документ и предпросмотр ──────────────────────────────────────

    [Fact]
    public void Preview_changes_nothing_apply_changes_the_chase()
    {
        var (state, _, pursuer) = Pair();

        var move = ChaseActions.Move(state, pursuer.Id).Resolution;
        EncounterEngine.Propose(state, move);
        var preview = EncounterEngine.Preview(state, move);

        Assert.Contains(preview, l => l.Label == "Локация" && l.Before == "1" && l.After == "2");
        Assert.Equal(1, state.R(pursuer).Location);

        EncounterEngine.Apply(state, Now);
        Assert.Equal(2, state.R(pursuer).Location);
        Assert.Null(state.Pending);
    }

    [Fact]
    public void Chase_survives_json_round_trip()
    {
        var (state, prey, pursuer) = Pair();
        state.PutHazard(4, damage: "1D6");
        state.PutBarrier(5, hitPoints: 10);
        ChaseRules.SetVehicle(state, pursuer.Id, VehicleReference.Vehicles[0], 40);

        var copy = CmJson.DeserializeEncounterState(CmJson.Serialize(state))!;

        Assert.Equal(CmJson.Serialize(state), CmJson.Serialize(copy));
        Assert.Equal(("Лужа", 10), (copy.Chase!.Location(4)!.Hazard!.Name, copy.Chase.Location(5)!.Barrier!.HitPointsLeft));
        Assert.Equal(40, copy.R(pursuer).Vehicle!.Skill);
        Assert.Equal(state.R(prey).Location, copy.R(prey).Location);
    }

    [Fact]
    public void Every_chase_enum_member_has_a_label()
    {
        Assert.All(Enum.GetValues<ChasePhase>(), v => Assert.NotEqual(v.ToString(), ChaseText.Of(v)));
        Assert.All(Enum.GetValues<ChaseRole>(), v => Assert.NotEqual(v.ToString(), ChaseText.Of(v)));
        Assert.All(Enum.GetValues<MovementMode>(), v => Assert.NotEqual(v.ToString(), ChaseText.Of(v)));
        Assert.All(Enum.GetValues<ChaseStatus>(), v => Assert.NotEqual(v.ToString(), ChaseText.Of(v)));
        Assert.All(Enum.GetValues<ChaseActionKind>(), v => Assert.NotEqual(v.ToString(), ChaseText.Of(v)));
        Assert.All(Enum.GetValues<ChaseActionKind>(), v => Assert.NotEqual("fa-circle", ChaseText.Icon(v)));
    }

    [Theory]
    [InlineData(1, "1 действие")]
    [InlineData(3, "3 действия")]
    [InlineData(5, "5 действий")]
    [InlineData(11, "11 действий")]
    [InlineData(21, "21 действие")]
    public void Russian_plural(int count, string text) => Assert.Equal(text, ChaseText.Actions(count));
}
