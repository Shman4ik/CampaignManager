using CampaignManager.Core.Dice;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Encounters.Chase;
using CampaignManager.Core.Tests.Infrastructure;
using static CampaignManager.Core.Tests.Encounters.Chase.ChaseScene;

namespace CampaignManager.Core.Tests.Encounters.Chase;

/// <summary>Часть 5: разгон, штурман, случайные и внезапные помехи, спрятаться и потерять след (перенос <c>OptionalRulesTests</c> T0.2).</summary>
public sealed class ChaseOptionalRulesTests
{
    private static (EncounterState State, EncounterParticipant Driver) Road(int locations = 10)
    {
        var driver = Runner("Водитель", ChaseRole.Pursuer);
        var state = Track(locations, Runner("Артур", ChaseRole.Prey), driver).InVehicle(driver, 5, speed: 8);
        state.Chase!.FloorIt = true;
        return (state, driver);
    }

    // ── «Педаль в пол» (стр. 137–139) ────────────────────────────────

    [Theory]
    [Trait("page", "138")]
    [Trait("page", "139")]
    [InlineData(1, false, 0)]
    [InlineData(2, false, 1)]
    [InlineData(3, false, 1)]
    [InlineData(4, false, 2)]
    [InlineData(5, false, 2)]
    [InlineData(6, false, 2)]
    [InlineData(1, true, 0)]
    [InlineData(2, true, 0)]
    [InlineData(4, true, 1)]
    public void Boost_penalty_dice_by_locations_navigator_takes_one(int locations, bool navigator, int expected) =>
        Assert.Equal(expected, ChaseActions.BoostPenaltyDice(locations, navigator));

    [Theory]
    [Trait("page", "137")]
    [InlineData(1, 3)]
    [InlineData(3, 4)]
    [InlineData(9, 6)]
    public void FloorIt_locations_clamped_two_to_five(int asked, int destination)
    {
        var (state, driver) = Road();

        var outcome = ChaseActions.FloorIt(state, driver.Id, asked);

        Assert.Equal(destination, outcome.Amount(EncounterEffectKind.ChaseMove));
        Assert.Equal(1, outcome.Amount(EncounterEffectKind.ChaseActionsSpent));
        Assert.Equal(EncounterLogKind.FloorIt, outcome.Resolution.Kind);
    }

    [Fact]
    [Trait("page", "137")]
    public void FloorIt_stops_at_track_end()
    {
        var (state, driver) = Road(locations: 4);

        Assert.Equal(4, ChaseActions.FloorIt(state, driver.Id, 5).Amount(EncounterEffectKind.ChaseMove));
    }

    /// <summary>
    /// Разгон проезжает только свободные локации: перед помехой встаёт, и её проходят с штрафными костями разгона и без нового
    /// действия; успех везёт дальше. В v1 разгон перепрыгивал помехи и даже преграды (F-P16).
    /// </summary>
    [Fact]
    [Trait("page", "138")]
    [Trait("finding", "F-P16")]
    public void FloorIt_stops_before_hazard_then_hazard_takes_boost_penalty_and_no_action()
    {
        var (state, driver) = Road();
        state.Started();
        var from = state.R(driver).Location;
        state.PutHazard(from + 2);

        state.Apply(ChaseActions.FloorIt(state, driver.Id, 4));
        Assert.Equal(from + 1, state.R(driver).Location);
        Assert.Equal((3, 2), (state.R(driver).Boost!.LocationsLeft, state.R(driver).Boost!.PenaltyDice));
        Assert.Equal([ChaseActionKind.Hazard], ChaseRules.AvailableActions(state, driver.Id).Where(a => a is ChaseActionKind.Hazard or ChaseActionKind.Move));

        // единицы 0, десятки 1, 2, 3 → 10, 20, 30: две штрафные — 30
        var hazard = ChaseActions.Hazard(state, driver.Id, from + 2, Skill("Вождение", 50, null), null, ScriptedDice.Of(0, 1, 2, 3));
        Assert.Equal(30, hazard.Roll!.Result);
        Assert.Empty(hazard.Of(EncounterEffectKind.ChaseActionsSpent));
        state.Apply(hazard);

        Assert.Equal(from + 2, state.R(driver).Location);
        Assert.Equal(2, state.R(driver).Boost!.LocationsLeft);
        Assert.Contains(ChaseActionKind.Move, ChaseRules.AvailableActions(state, driver.Id));
    }

    [Fact]
    [Trait("page", "138")]
    public void FloorIt_hazard_failure_ends_the_boost()
    {
        var (state, driver) = Road();
        state.Started();
        state.PutHazard(state.R(driver).Location + 1);
        state.Apply(ChaseActions.FloorIt(state, driver.Id, 3));

        state.Apply(ChaseActions.Hazard(state, driver.Id, state.R(driver).Location + 1, Skill("Вождение", 50, 90), new ChaseMishap(LostActionsRoll: 1), NoDice));

        Assert.Null(state.R(driver).Boost);
    }

    [Fact]
    [Trait("page", "138")]
    public void FloorIt_stops_before_barrier_and_ends()
    {
        var (state, driver) = Road();
        state.Started();
        var from = state.R(driver).Location;
        state.PutBarrier(from + 3);

        state.Apply(ChaseActions.FloorIt(state, driver.Id, 5));

        Assert.Equal(from + 2, state.R(driver).Location);
        Assert.Null(state.R(driver).Boost);
    }

    [Fact]
    [Trait("page", "139")]
    public void Navigator_success_then_boost_spends_it()
    {
        var (state, driver) = Road();
        var navigator = Runner("Штурман", ChaseRole.Pursuer);
        EncounterEngine.Add(state, navigator, Now);
        ChaseRules.SetPassenger(state, navigator.Id, driver.Id);

        state.Apply(ChaseActions.Navigate(state, navigator.Id, Skill("Ориентирование", 50, 40), NoDice));
        Assert.True(state.R(driver).NavigatorAssist);

        var boost = ChaseActions.FloorIt(state, driver.Id, 2);
        Assert.True(state.R(driver).NavigatorAssist); // разрешение ещё ничего не тратит
        state.Apply(boost);

        Assert.False(state.R(driver).NavigatorAssist);
        Assert.Equal(3, state.R(driver).Location);
        Assert.Contains(state.Log[^1].Lines, l => l.Contains("штурман снял", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("page", "139")]
    public void Navigator_failure_gives_nothing()
    {
        var (state, driver) = Road();
        var navigator = Runner("Штурман", ChaseRole.Pursuer);
        EncounterEngine.Add(state, navigator, Now);
        ChaseRules.SetPassenger(state, navigator.Id, driver.Id);

        state.Apply(ChaseActions.Navigate(state, navigator.Id, Skill("Ориентирование", 50, 51), NoDice));

        Assert.False(state.R(driver).NavigatorAssist);
    }

    // ── Случайные и внезапные помехи (стр. 137) ──────────────────────

    [Theory]
    [Trait("page", "137")]
    [InlineData(1, null)]
    [InlineData(59, null)]
    [InlineData(60, Difficulty.Regular)]
    [InlineData(84, Difficulty.Regular)]
    [InlineData(85, Difficulty.Hard)]
    [InlineData(95, Difficulty.Hard)]
    [InlineData(96, Difficulty.Extreme)]
    [InlineData(100, Difficulty.Extreme)]
    public void Random_hazard_boundaries_60_85_96(int roll, Difficulty? difficulty)
    {
        var state = Track(6, Runner("Артур", ChaseRole.Prey));

        var outcome = ChaseActions.RandomHazard(state, 4, roll, 0, 0, NoDice);
        state.Apply(outcome);

        Assert.Equal(roll, outcome.Roll!.Result);
        Assert.Equal(difficulty is null, outcome.Success);
        Assert.Equal(difficulty, state.Chase!.Location(4)!.Hazard?.Difficulty);
    }

    [Theory]
    [Trait("page", "137")]
    // тяжёлая дорога: штрафная кость, 00 + 0 = 100
    [InlineData(0, 1, new[] { 0, 0, 9 }, 100)]
    // автострада: бонусная, меньшее из 95 и 05
    [InlineData(1, 0, new[] { 5, 9, 0 }, 5)]
    public void Random_hazard_rolls_with_road_dice(int bonus, int penalty, int[] dice, int expected)
    {
        var state = Track(6, Runner("Артур", ChaseRole.Prey));

        var outcome = ChaseActions.RandomHazard(state, 4, null, bonus, penalty, ScriptedDice.Of(dice));

        Assert.Equal(expected, outcome.Roll!.Result);
    }

    [Fact]
    [Trait("page", "137")]
    public void Random_hazard_written_roll_ignores_road_dice() =>
        Assert.Equal(70, ChaseActions.RandomHazard(Track(6, Runner("Артур", ChaseRole.Prey)), 4, 70, 0, 2, NoDice).Roll!.Result);

    [Theory]
    [Trait("page", "137")]
    [InlineData(40, true)]
    [InlineData(60, false)]
    public void Sudden_hazard_group_luck_decides_who_places_it(int roll, bool players) =>
        Assert.Equal(players, ChaseActions.SuddenHazard(50, roll, declaredByPlayers: true, NoDice).Success);

    [Fact]
    [Trait("page", "137")]
    public void Sudden_hazard_sides_take_turns()
    {
        var chase = new ChaseState();
        Assert.Null(ChaseRules.SuddenHazardRejection(chase, byPlayers: true));
        Assert.Null(ChaseRules.SuddenHazardRejection(chase, byPlayers: false));

        ChaseRules.DeclareSuddenHazard(chase, byPlayers: true);

        Assert.NotNull(ChaseRules.SuddenHazardRejection(chase, byPlayers: true));
        Assert.Null(ChaseRules.SuddenHazardRejection(chase, byPlayers: false));
    }

    // ── Спрятаться (стр. 139) ────────────────────────────────────────

    [Theory]
    [Trait("page", "139")]
    [InlineData(Difficulty.Regular, 60, true)]
    [InlineData(Difficulty.Regular, 61, false)]
    [InlineData(Difficulty.Hard, 30, true)]
    [InlineData(Difficulty.Extreme, 13, false)]
    public void Hide_on_foot_stealth_against_difficulty(Difficulty difficulty, int roll, bool hidden)
    {
        var prey = Runner("Артур", ChaseRole.Prey);
        var state = Track(6, prey, Runner("Вампир", ChaseRole.Pursuer));

        var outcome = ChaseActions.Hide(state, prey.Id, new ChaseCheck("Скрытность", 60, roll, difficulty), null, NoDice);

        Assert.Equal(hidden, outcome.Success);
        Assert.Equal(hidden, outcome.Of(EncounterEffectKind.Escaped).Any());
        Assert.Equal(1, outcome.Amount(EncounterEffectKind.ChaseActionsSpent));
    }

    [Theory]
    [Trait("page", "139")]
    [InlineData(40, true, "укрылся")]
    [InlineData(50, false, "Вождения не хватило")]
    [InlineData(61, false, "укрытие не найдено")]
    public void Hide_in_vehicle_roll_must_pass_stealth_and_driving(int roll, bool hidden, string text)
    {
        var prey = Runner("Артур", ChaseRole.Prey);
        var state = Track(6, prey, Runner("Вампир", ChaseRole.Pursuer)).InVehicle(prey, 5);

        var outcome = ChaseActions.Hide(state, prey.Id, Skill("Скрытность", 60, roll), 40, NoDice);

        Assert.Equal(hidden, outcome.Success);
        Assert.Contains(text, outcome.Resolution.Title);
        Assert.Contains("Скрытность + Вождение", outcome.Resolution.Title);
    }

    [Fact]
    [Trait("page", "139")]
    public void Hide_without_roll_bonus_dice_go_into_roll()
    {
        var prey = Runner("Артур", ChaseRole.Prey);
        var state = Track(6, prey, Runner("Вампир", ChaseRole.Pursuer));

        var outcome = ChaseActions.Hide(state, prey.Id, Skill("Скрытность", 60, null, bonus: 1), null, ScriptedDice.Of(5, 9, 2));

        Assert.Equal(25, outcome.Roll!.Result);
        Assert.True(outcome.Success);
    }

    /// <summary>
    /// F-P08 исправлено: Вождение в совместной проверке — та же общая функция (стр. 90): 01 — критический успех и при нуле
    /// навыка. В v1 инлайн <c>roll &lt;= порог</c> проваливал его.
    /// </summary>
    [Theory]
    [Trait("page", "139")]
    [Trait("finding", "F-P08")]
    [InlineData(0, Difficulty.Regular)]
    [InlineData(4, Difficulty.Extreme)]
    public void Hide_in_vehicle_critical_passes_driving_even_with_zero_target(int driving, Difficulty difficulty)
    {
        var prey = Runner("Артур", ChaseRole.Prey);
        var state = Track(6, prey, Runner("Вампир", ChaseRole.Pursuer)).InVehicle(prey, 5);

        var outcome = ChaseActions.Hide(state, prey.Id, new ChaseCheck("Скрытность", 60, 1, difficulty), driving, NoDice);

        Assert.Equal(SuccessLevel.Critical, outcome.Level);
        Assert.True(outcome.Success);
    }

    [Fact]
    [Trait("page", "139")]
    public void Hidden_prey_escapes_and_chase_ends()
    {
        var prey = Runner("Артур", ChaseRole.Prey);
        var state = Track(6, prey, Runner("Вампир", ChaseRole.Pursuer)).Started();

        state.Apply(ChaseActions.Hide(state, prey.Id, Skill("Скрытность", 60, 10), null, NoDice));

        Assert.Equal(ChaseStatus.Escaped, ChaseRules.StatusOf(state, prey.Id));
        Assert.True(ChaseRules.IsOver(state));
        Assert.Equal(ChasePhase.Ended, state.Chase!.Phase);
    }

    // ── Потерять след (стр. 139) ─────────────────────────────────────

    [Theory]
    [Trait("page", "139")]
    [InlineData(Difficulty.Regular, 50, true)]
    [InlineData(Difficulty.Regular, 51, false)]
    [InlineData(Difficulty.Extreme, 10, true)]
    [InlineData(Difficulty.Extreme, 11, false)]
    public void Tracking_failure_drops_pursuer(Difficulty difficulty, int roll, bool found)
    {
        var pursuer = Runner("Вампир", ChaseRole.Pursuer);
        var state = Track(6, Runner("Артур", ChaseRole.Prey), pursuer).Started();

        var outcome = ChaseActions.Track(state, pursuer.Id, new ChaseCheck("Чтение следов", 50, roll, difficulty), NoDice);
        state.Apply(outcome);

        Assert.Equal(found, outcome.Success);
        Assert.Equal(!found, pursuer.IsOut);
        Assert.Equal(found ? ChaseStatus.Running : ChaseStatus.LostTrail, ChaseRules.StatusOf(state, pursuer.Id));
    }

    /// <summary>F-P01 исправлено: 10 при навыке 50 и трудной сложности — чрезвычайный успех, а не «трудный», как в v1.</summary>
    [Fact]
    [Trait("page", "139")]
    [Trait("finding", "F-P01")]
    public void Tracking_level_from_full_skill()
    {
        var pursuer = Runner("Вампир", ChaseRole.Pursuer);
        var state = Track(6, Runner("Артур", ChaseRole.Prey), pursuer);

        var outcome = ChaseActions.Track(state, pursuer.Id, new ChaseCheck("Чтение следов", 50, 10, Difficulty.Hard), NoDice);

        Assert.Equal(SuccessLevel.Extreme, outcome.Level);
    }

    [Fact]
    [Trait("page", "136")]
    public void Melee_hit_damage_minus_armour_and_dodge()
    {
        var prey = Runner("Артур", ChaseRole.Prey, hp: 10);
        prey.Stats.Armor = 1;
        var pursuer = Runner("Вампир", ChaseRole.Pursuer);
        var state = Track(6, prey, pursuer);

        var hit = ChaseActions.Melee(state, pursuer.Id, prey.Id, Skill("Драка", 50, 20), null, new ChaseHarm("1D6"), ScriptedDice.Of(4));
        var dodged = ChaseActions.Melee(state, pursuer.Id, prey.Id, Skill("Драка", 50, 20),
            new ChaseDefence("Уклонение", 50, 5), new ChaseHarm(Roll: 4), NoDice);

        Assert.Equal(3, hit.Amount(EncounterEffectKind.Damage));
        Assert.False(dodged.Success);
        Assert.Empty(dodged.Of(EncounterEffectKind.Damage));
    }

    [Fact]
    [Trait("page", "139")]
    public void Ranged_on_the_move_penalty_die_no_action_vehicle_armour_protects()
    {
        var shooter = Runner("Артур", ChaseRole.Pursuer);
        var driver = Runner("Культист", ChaseRole.Prey);
        var state = Track(6, driver, shooter).InVehicle(driver, 5);

        // единицы 0, десятки 2 и 3 → 20 и 30: штрафная — 30
        var outcome = ChaseActions.Ranged(state, shooter.Id, driver.Id, Skill("Стрельба", 50, null), stopped: false,
            new ChaseHarm(Roll: 8), ScriptedDice.Of(0, 2, 3));

        Assert.Equal(30, outcome.Roll!.Result);
        Assert.Empty(outcome.Of(EncounterEffectKind.ChaseActionsSpent));
        Assert.Equal(6, outcome.Amount(EncounterEffectKind.Damage)); // броня машины 2
    }

    [Fact]
    [Trait("page", "136")]
    public void Maneuver_success_target_loses_1d3_actions_impossible_when_much_bigger()
    {
        var prey = Runner("Артур", ChaseRole.Prey, build: 0);
        var pursuer = Runner("Фермер", ChaseRole.Pursuer, build: 0);
        var giant = Runner("Шоггот", ChaseRole.Pursuer, build: 6);
        var state = Track(6, prey, pursuer, giant);

        var thrown = ChaseActions.Maneuver(state, prey.Id, pursuer.Id, Skill("Драка", 50, 30), null, new ChaseMishap(new ChaseHarm(Roll: 3), LostActionsRoll: 1), NoDice);
        var impossible = ChaseActions.Maneuver(state, prey.Id, giant.Id, Skill("Драка", 50, 30), null, null, NoDice);

        Assert.Equal(1, thrown.Amount(EncounterEffectKind.ChaseActionsLost, pursuer.Id));
        Assert.Equal(3, thrown.Amount(EncounterEffectKind.Damage, pursuer.Id));
        Assert.False(impossible.Success);
        Assert.Empty(impossible.Resolution.Effects);
    }
}
