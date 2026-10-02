using CampaignManager.Core.Dice;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Encounters.Chase;
using CampaignManager.Core.Tests.Infrastructure;
using static CampaignManager.Core.Tests.Encounters.Chase.ChaseScene;

namespace CampaignManager.Core.Tests.Encounters.Chase;

/// <summary>Проверка скорости, начало погони, расстановка, действия перемещения и долг (перенос <c>SpeedAndActionsTests</c> T0.2).</summary>
public sealed class ChaseSpeedTests
{
    // ── Проверка скорости (стр. 130) ─────────────────────────────────

    [Theory]
    [Trait("page", "130")]
    [InlineData(1, SuccessLevel.Critical, 1)]
    [InlineData(10, SuccessLevel.Extreme, 1)]
    [InlineData(11, SuccessLevel.Hard, 0)]
    [InlineData(25, SuccessLevel.Hard, 0)]
    [InlineData(50, SuccessLevel.Regular, 0)]
    [InlineData(51, SuccessLevel.Failure, -1)]
    [InlineData(100, SuccessLevel.Fumble, -1)]
    public void SpeedCheck_written_roll_sets_modifier_by_level(int roll, SuccessLevel level, int modifier)
    {
        var arthur = Runner("Артур", ChaseRole.Prey, con: 50);
        var state = Track(6, arthur, Runner("Вампир", ChaseRole.Pursuer));
        ChaseRules.BeginSpeedChecks(state);

        var outcome = ChaseActions.SpeedCheck(state, arthur.Id, roll, NoDice);
        Assert.False(state.R(arthur).SpeedChecked); // разрешение ничего не меняет
        state.Apply(outcome);

        Assert.Equal(level, outcome.Level);
        Assert.Equal(modifier, state.R(arthur).SpeedModifier);
        Assert.Equal(8 + modifier, ChaseRules.Move(arthur, state.R(arthur)));
        Assert.True(state.R(arthur).SpeedChecked);
        Assert.Equal(EncounterLogKind.SpeedCheck, state.Log[^1].Kind);
        Assert.StartsWith("ВЫН 50", outcome.Resolution.Lines[0]);
    }

    [Fact]
    [Trait("page", "130")]
    public void SpeedCheck_without_roll_rolls_d100()
    {
        var arthur = Runner("Артур", ChaseRole.Prey, con: 50);
        var state = Track(6, arthur, Runner("Вампир", ChaseRole.Pursuer));
        var dice = ScriptedDice.Of(7, 0);

        var outcome = ChaseActions.SpeedCheck(state, arthur.Id, null, dice);

        Assert.Equal(7, outcome.Roll!.Result);
        Assert.Equal(1, outcome.Amount(EncounterEffectKind.ChaseSpeed));
        Assert.Equal(0, dice.Remaining);
    }

    [Theory]
    [Trait("page", "130")]
    [Trait("page", "142")]
    [InlineData(false, null, 50, "ВЫН", 60)]
    [InlineData(true, 40, 50, "Вождение", 40)]
    // навыка вождения нет — половина ЛВК…
    [InlineData(true, null, 50, "Вождение", 25)]
    // …но не меньше единицы
    [InlineData(true, null, 1, "Вождение", 1)]
    public void SpeedSkill_is_con_or_driving_or_half_dex(bool inVehicle, int? driving, int dex, string label, int value)
    {
        var arthur = Runner("Артур", ChaseRole.Prey, dex: dex, con: 60);
        var state = Track(6, arthur);
        if (inVehicle)
            state.InVehicle(arthur, 5, skill: driving);

        var skill = ChaseRules.SpeedSkill(arthur, state.R(arthur));

        Assert.Equal((label, value), (skill.Label, skill.Value));
    }

    [Fact]
    [Trait("page", "143")]
    public void SpeedSkill_broken_down_vehicle_adds_penalty_die()
    {
        var arthur = Runner("Артур", ChaseRole.Prey);
        var state = Track(6, arthur).InVehicle(arthur, 5, skill: 50);
        state.R(arthur).Vehicle!.BuildLeft = 2; // половина от 5 с округлением вниз

        Assert.Equal(1, ChaseRules.SpeedSkill(arthur, state.R(arthur)).PenaltyDice);
    }

    [Theory]
    [Trait("page", "142")]
    [InlineData(SkillAptitude.Capable, 50)]
    [InlineData(SkillAptitude.Uncertain, 25)]
    [InlineData(SkillAptitude.Unlikely, 10)]
    public void Substitute_skill_from_dex(SkillAptitude aptitude, int expected) =>
        Assert.Equal(expected, ChaseRules.SubstituteFromDex(50, aptitude));

    [Fact]
    [Trait("page", "139")]
    public void BeginSpeedChecks_passenger_is_already_done()
    {
        var driver = Runner("Водитель", ChaseRole.Prey);
        var passenger = Runner("Пассажир", ChaseRole.Prey);
        var state = Track(6, driver, passenger, Runner("Вампир", ChaseRole.Pursuer)).InVehicle(driver, 5);
        ChaseRules.SetPassenger(state, passenger.Id, driver.Id);

        Assert.True(ChaseRules.BeginSpeedChecks(state));

        Assert.Equal(ChasePhase.SpeedCheck, state.Chase!.Phase);
        Assert.True(state.R(passenger).SpeedChecked);
        Assert.False(state.R(driver).SpeedChecked);
    }

    [Fact]
    [Trait("page", "130")]
    public void BeginSpeedChecks_needs_two_runners_and_someone_to_flee()
    {
        Assert.NotNull(ChaseRules.SpeedChecksRejection(Track(6, Runner("Артур", ChaseRole.Prey))));
        Assert.NotNull(ChaseRules.SpeedChecksRejection(Track(6, Runner("A", ChaseRole.Pursuer), Runner("B", ChaseRole.Pursuer))));
        Assert.Null(ChaseRules.SpeedChecksRejection(Track(6, Runner("A", ChaseRole.Prey), Runner("B", ChaseRole.Pursuer))));
        Assert.Equal(ChaseRules.MinLocations, Track(1, Runner("A", ChaseRole.Prey)).Chase!.LastLocation);
    }

    [Fact]
    [Trait("page", "130")]
    public void Start_needs_every_speed_check()
    {
        var arthur = Runner("Артур", ChaseRole.Prey);
        var state = Track(6, arthur, Runner("Вампир", ChaseRole.Pursuer));
        ChaseRules.BeginSpeedChecks(state);

        Assert.NotNull(ChaseRules.StartRejection(state));
        Assert.Null(ChaseRules.Start(state, Now));
    }

    // ── Начнётся ли погоня (стр. 130, 145) ───────────────────────────

    [Theory]
    [Trait("page", "130")]
    [InlineData(9, 0, 8, false)]
    [InlineData(8, 0, 8, true)]
    [InlineData(8, 1, 8, false)]
    [InlineData(8, -1, 8, true)]
    public void Start_faster_prey_escapes(int preyMov, int preyModifier, int pursuerMov, bool happens)
    {
        var prey = Runner("Артур", ChaseRole.Prey, mov: preyMov);
        var state = Track(6, prey, Runner("Вампир", ChaseRole.Pursuer, mov: pursuerMov));
        ChaseRules.BeginSpeedChecks(state);
        foreach (var runner in state.Chase!.Runners)
            runner.SpeedChecked = true;
        state.R(prey).SpeedModifier = preyModifier;

        ChaseRules.Start(state, Now);

        Assert.Equal(happens ? ChasePhase.Active : ChasePhase.Ended, state.Chase.Phase);
        Assert.Equal(!happens, prey.IsOut);
        Assert.Equal(happens ? ChaseStatus.Running : ChaseStatus.Escaped, ChaseRules.StatusOf(state, prey.Id));
        Assert.Equal(happens ? 1 : 0, state.Round);
    }

    [Fact]
    [Trait("page", "145")]
    public void Start_one_of_two_prey_escapes_the_other_is_chased()
    {
        // Пример книги: Скотт (СКО 10) быстрее любого культиста — сразу отрывается, Харви (5) — нет.
        var scott = Runner("Скотт", ChaseRole.Prey, mov: 10);
        var harvey = Runner("Харви", ChaseRole.Prey, mov: 5);
        var state = Track(10, scott, harvey, Runner("Культист", ChaseRole.Pursuer, mov: 9)).Started();

        Assert.True(scott.IsOut);
        Assert.Equal(ChaseStatus.Escaped, ChaseRules.StatusOf(state, scott.Id));
        Assert.False(harvey.IsOut);
        Assert.Equal(ChasePhase.Active, state.Chase!.Phase);
    }

    [Fact]
    [Trait("page", "130")]
    public void Start_ignores_prey_already_out()
    {
        var gone = Runner("Сбежавший", ChaseRole.Prey, mov: 12);
        gone.IsOut = true;
        var state = Track(6, gone, Runner("Артур", ChaseRole.Prey, mov: 7), Runner("Вампир", ChaseRole.Pursuer, mov: 8)).Started();

        Assert.Equal(ChasePhase.Active, state.Chase!.Phase);
    }

    /// <summary>
    /// Расстановка по книге (стр. 145, пример с тремя культистами): самые медленные преследователи — на 1, быстрый — на
    /// разницу СКО впереди; самый медленный убегающий — на 2 впереди самого быстрого преследователя, остальные — на разницу.
    /// В v1 все преследователи вставали на 1, все убегающие — на 3 (F-P14).
    /// </summary>
    [Fact]
    [Trait("page", "145")]
    [Trait("finding", "F-P14")]
    public void Start_places_runners_by_move_like_the_book_example()
    {
        var harvey = Runner("Харви", ChaseRole.Prey, mov: 5);
        var brian = Runner("Брайан", ChaseRole.Prey, mov: 8);
        var a = Runner("Культист А", ChaseRole.Pursuer, mov: 7);
        var b = Runner("Культист Б", ChaseRole.Pursuer, mov: 7);
        var c = Runner("Культист В", ChaseRole.Pursuer, mov: 9);
        var state = Track(6, harvey, brian, a, b, c).Started();

        Assert.Equal((1, 1, 3), (state.R(a).Location, state.R(b).Location, state.R(c).Location));
        Assert.Equal((5, 8), (state.R(harvey).Location, state.R(brian).Location));
        // Брайан не поместился бы в 6 локаций — трасса удлинилась
        Assert.Equal(9, state.Chase!.LastLocation);

        // Действия (стр. 146): Харви 1, культисты А и Б по 3, Брайан 4, культист В 5.
        Assert.Equal((1, 4, 3, 3, 5), (state.R(harvey).Actions, state.R(brian).Actions, state.R(a).Actions, state.R(b).Actions, state.R(c).Actions));
    }

    [Fact]
    [Trait("page", "131")]
    public void Start_gap_one_puts_prey_next_to_fastest_pursuer()
    {
        var prey = Runner("Артур", ChaseRole.Prey);
        var state = Track(6, prey, Runner("Вампир", ChaseRole.Pursuer));
        ChaseRules.Begin(state, 6, startGap: 1);
        state.Started();

        Assert.Equal(2, state.R(prey).Location);
    }

    // ── Скорость погони (стр. 132, 140) ──────────────────────────────

    [Fact]
    [Trait("page", "132")]
    public void Actions_are_one_plus_difference_to_slowest()
    {
        var prey = Runner("Артур", ChaseRole.Prey, mov: 8);
        var pursuer = Runner("Вампир", ChaseRole.Pursuer, mov: 10);
        var state = Track(6, prey, pursuer).Started();

        Assert.Equal(8, state.Chase!.SlowestMove);
        Assert.Equal((1, 1), (state.R(prey).Actions, state.R(prey).ActionsLeft));
        Assert.Equal((3, 3), (state.R(pursuer).Actions, state.R(pursuer).ActionsLeft));
    }

    [Fact]
    [Trait("page", "140")]
    [Trait("page", "145")]
    public void Pursuer_slower_than_slowest_prey_drops_out()
    {
        var prey = Runner("Артур", ChaseRole.Prey, mov: 8);
        var fast = Runner("Вампир", ChaseRole.Pursuer, mov: 10);
        var slow = Runner("Зомби", ChaseRole.Pursuer, mov: 7);
        var state = Track(6, prey, fast, slow).Started();

        Assert.True(slow.IsOut);
        Assert.Equal(ChaseStatus.TooSlow, ChaseRules.StatusOf(state, slow.Id));
        Assert.False(fast.IsOut);
        // выбывший в самую медленную СКО не входит и действий не получает
        Assert.Equal(8, state.Chase!.SlowestMove);
        Assert.Equal(0, state.R(slow).Actions);
    }

    [Fact]
    [Trait("page", "139")]
    [Trait("page", "140")]
    public void Passenger_ignored_in_slowest_move_and_gets_no_actions()
    {
        var driver = Runner("Водитель", ChaseRole.Prey, mov: 8);
        var passenger = Runner("Пассажир", ChaseRole.Prey, mov: 3);
        var pursuer = Runner("Вампир", ChaseRole.Pursuer, mov: 9);
        var state = Track(6, driver, passenger, pursuer).InVehicle(driver, 5, speed: 8);
        ChaseRules.SetPassenger(state, passenger.Id, driver.Id);
        state.R(passenger).Debt = 2;
        state.Started();

        Assert.Equal(8, state.Chase!.SlowestMove);
        Assert.False(pursuer.IsOut);
        Assert.Equal((0, 0, 0), (state.R(passenger).Actions, state.R(passenger).ActionsLeft, state.R(passenger).Debt));
        Assert.False(passenger.IsOut); // ходит в общем порядке ЛВК
        Assert.Equal(state.R(driver).Location, state.R(passenger).Location);
    }

    // ── Долг действий (стр. 133) ─────────────────────────────────────

    [Fact]
    [Trait("page", "133")]
    public void Debt_eats_earned_actions_over_two_rounds()
    {
        var prey = Runner("Артур", ChaseRole.Prey, mov: 8);
        var pursuer = Runner("Вампир", ChaseRole.Pursuer, mov: 9);
        var state = Track(6, prey, pursuer).Started();
        state.R(pursuer).Debt = 3;

        ChaseRules.OnRoundStarted(state);
        Assert.Equal((0, 1), (state.R(pursuer).Actions, state.R(pursuer).Debt));

        ChaseRules.OnRoundStarted(state);
        Assert.Equal((1, 0), (state.R(pursuer).Actions, state.R(pursuer).Debt));
    }

    [Fact]
    [Trait("page", "133")]
    public void Lost_actions_beyond_remaining_become_debt_for_next_round()
    {
        var prey = Runner("Артур", ChaseRole.Prey, mov: 8);
        var pursuer = Runner("Вампир", ChaseRole.Pursuer, mov: 9);
        var state = Track(6, prey, pursuer).Started();
        state.PutHazard(2);
        Assert.Equal(2, state.R(pursuer).ActionsLeft);

        // Провал помехи: одно действие на саму помеху, потом минус 3
        state.Apply(ChaseActions.Hazard(state, pursuer.Id, 2, Skill("Ловкость", 50, 90), new ChaseMishap(LostActionsRoll: 3), NoDice));

        Assert.Equal((0, 2), (state.R(pursuer).ActionsLeft, state.R(pursuer).Debt));

        EncounterQueue.Next(state, Now);
        EncounterQueue.Next(state, Now); // новый раунд
        Assert.Equal(2, state.Round);
        Assert.Equal((0, 0), (state.R(pursuer).Actions, state.R(pursuer).Debt));
    }

    // ── Присоединение и смена СКО (стр. 140–141) ─────────────────────

    [Fact]
    [Trait("page", "140")]
    public void Joining_runner_needs_speed_check_then_gets_actions()
    {
        var prey = Runner("Артур", ChaseRole.Prey, mov: 8);
        var state = Track(6, prey, Runner("Вампир", ChaseRole.Pursuer, mov: 9)).Started();
        var dog = Runner("Собака", ChaseRole.Pursuer, mov: 10);
        EncounterEngine.Add(state, dog, Now);

        Assert.False(state.R(dog).SpeedChecked);
        Assert.Equal(0, state.R(dog).Actions);

        state.Apply(ChaseActions.SpeedCheck(state, dog.Id, 40, NoDice));

        Assert.Equal((3, 3), (state.R(dog).Actions, state.R(dog).ActionsLeft));
    }

    [Fact]
    [Trait("page", "140")]
    public void Joining_pursuer_slower_than_slowest_prey_is_not_counted()
    {
        var prey = Runner("Артур", ChaseRole.Prey, mov: 8);
        var state = Track(6, prey, Runner("Вампир", ChaseRole.Pursuer, mov: 9)).Started();
        var zombie = Runner("Зомби", ChaseRole.Pursuer, mov: 6);
        EncounterEngine.Add(state, zombie, Now);

        state.Apply(ChaseActions.SpeedCheck(state, zombie.Id, 40, NoDice));

        Assert.True(zombie.IsOut);
        Assert.Equal(ChaseStatus.TooSlow, ChaseRules.StatusOf(state, zombie.Id));
        Assert.Equal(8, state.Chase!.SlowestMove);
    }

    [Fact]
    [Trait("page", "140")]
    public void Joining_slower_prey_recounts_everyones_actions()
    {
        var prey = Runner("Артур", ChaseRole.Prey, mov: 8);
        var pursuer = Runner("Вампир", ChaseRole.Pursuer, mov: 9);
        var state = Track(6, prey, pursuer).Started();
        var slowpoke = Runner("Билл", ChaseRole.Prey, mov: 6);
        EncounterEngine.Add(state, slowpoke, Now);

        state.Apply(ChaseActions.SpeedCheck(state, slowpoke.Id, 40, NoDice));

        Assert.Equal(6, state.Chase!.SlowestMove);
        Assert.Equal((4, 4), (state.R(pursuer).Actions, state.R(pursuer).ActionsLeft));
        Assert.Equal(1, state.R(slowpoke).Actions);
    }

    [Fact]
    [Trait("page", "142")]
    public void Locations_covered_in_rounds()
    {
        var prey = Runner("Артур", ChaseRole.Prey, mov: 8);
        var pursuer = Runner("Вампир", ChaseRole.Pursuer, mov: 10);
        var state = Track(6, prey, pursuer).Started();

        Assert.Equal(9, ChaseRules.LocationsCoveredIn(state, pursuer.Id, 3));
        Assert.Equal(0, ChaseRules.LocationsCoveredIn(state, pursuer.Id, 0));
    }
}
