using CampaignManager.Core.Dice;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Encounters.Chase;
using CampaignManager.Core.KeeperScreen;
using CampaignManager.Core.Tests.Infrastructure;
using static CampaignManager.Core.Tests.Encounters.Chase.ChaseScene;

namespace CampaignManager.Core.Tests.Encounters.Chase;

/// <summary>Помехи, преграды и их разрушение — препятствие лежит в СЛЕДУЮЩЕЙ локации (перенос <c>ObstacleTests</c> T0.2).</summary>
public sealed class ChaseObstacleTests
{
    /// <summary>Трасса из 6 локаций: преследователь на 1, жертва на 3, впереди преследователя — локация 2.</summary>
    private static (EncounterState State, EncounterParticipant Pursuer) Scene(int hp = 10, bool started = false)
    {
        var pursuer = Runner("Вампир", ChaseRole.Pursuer, hp: hp, mov: 9);
        var state = Track(6, Runner("Артур", ChaseRole.Prey), pursuer);
        if (started)
            state.Started();
        return (state, pursuer);
    }

    // ── Помеха (стр. 133) ────────────────────────────────────────────

    [Fact]
    [Trait("page", "133")]
    public void Hazard_success_enters_location_costs_one_plus_bonus_dice_and_changes_nothing_yet()
    {
        var (state, pursuer) = Scene();
        state.PutHazard(2);

        var outcome = ChaseActions.Hazard(state, pursuer.Id, 2, Skill("Ловкость", 50, 30, bonus: 1), null, NoDice);

        Assert.True(outcome.Success);
        Assert.Equal(2, outcome.Amount(EncounterEffectKind.ChaseMove));
        Assert.Equal(2, outcome.Amount(EncounterEffectKind.ChaseActionsSpent));
        Assert.Empty(outcome.Of(EncounterEffectKind.Damage));
        Assert.Empty(outcome.Of(EncounterEffectKind.ChaseActionsLost));
        Assert.Contains("Лужа", outcome.Resolution.Title);
        // разрешение ничего не меняет
        Assert.Equal(1, state.R(pursuer).Location);
        Assert.DoesNotContain(state.Log, e => e.Kind == EncounterLogKind.Hazard);
    }

    [Fact]
    [Trait("page", "133")]
    public void Hazard_failure_still_enters_location_with_damage_and_lost_actions()
    {
        var (state, pursuer) = Scene();
        state.PutHazard(2);

        var outcome = ChaseActions.Hazard(state, pursuer.Id, 2, Skill("Ловкость", 50, 80), new ChaseMishap(new ChaseHarm(Roll: 4), LostActionsRoll: 2), NoDice);

        Assert.False(outcome.Success);
        Assert.Equal(2, outcome.Amount(EncounterEffectKind.ChaseMove));
        Assert.Equal(4, outcome.Amount(EncounterEffectKind.Damage));
        Assert.Equal(2, outcome.Amount(EncounterEffectKind.ChaseActionsLost));
    }

    [Fact]
    [Trait("page", "133")]
    public void Apply_hazard_failure_moves_hurts_and_logs()
    {
        var (state, pursuer) = Scene(started: true);
        state.PutHazard(2);

        state.Apply(ChaseActions.Hazard(state, pursuer.Id, 2, Skill("Ловкость", 50, 80), new ChaseMishap(new ChaseHarm(Roll: 4), LostActionsRoll: 2), NoDice));

        Assert.Equal((2, 6), (state.R(pursuer).Location, pursuer.HitPoints));
        Assert.Equal((0, 1), (state.R(pursuer).ActionsLeft, state.R(pursuer).Debt));
        Assert.Equal(EncounterLogKind.Hazard, state.Log[^1].Kind);
    }

    [Theory]
    [Trait("page", "133")]
    [InlineData(5, 2)]
    [InlineData(-1, 0)]
    public void Hazard_bonus_dice_clamped_to_two(int asked, int used)
    {
        var (state, pursuer) = Scene();
        state.PutHazard(2);

        var outcome = ChaseActions.Hazard(state, pursuer.Id, 2, Skill("Ловкость", 50, 30, bonus: asked), null, NoDice);

        Assert.Equal(1 + used, outcome.Amount(EncounterEffectKind.ChaseActionsSpent));
    }

    [Fact]
    [Trait("page", "133")]
    public void Hazard_without_roll_paid_bonus_dice_go_into_roll()
    {
        var (state, pursuer) = Scene();
        state.PutHazard(2);

        // единицы 5, десятки 9, 2, 7 → 95, 25, 75; две бонусные — меньшее
        var dice = ScriptedDice.Of(5, 9, 2, 7);
        var outcome = ChaseActions.Hazard(state, pursuer.Id, 2, Skill("Ловкость", 50, null, bonus: 2), null, dice);

        Assert.Equal(25, outcome.Roll!.Result);
        Assert.True(outcome.Success);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    [Trait("page", "133")]
    public void Hazard_failure_without_rolls_rolls_formula_then_1d3()
    {
        var (state, pursuer) = Scene();
        state.PutHazard(2, damage: "1D6");

        var dice = ScriptedDice.Of(4, 2);
        var outcome = ChaseActions.Hazard(state, pursuer.Id, 2, Skill("Ловкость", 50, 80), new ChaseMishap(new ChaseHarm("1D6")), dice);

        Assert.Equal(4, outcome.Amount(EncounterEffectKind.Damage));
        Assert.Equal(2, outcome.Amount(EncounterEffectKind.ChaseActionsLost));
        Assert.Equal(0, dice.Remaining);
    }

    /// <summary>F-P02 исправлено: «1д6» с русской «д» бросается как 1d6 (в v1 — ноль урона, кости не бросались).</summary>
    [Theory]
    [Trait("page", "133")]
    [Trait("finding", "F-P02")]
    [InlineData("1д6")]
    [InlineData("1Д6")]
    public void Hazard_cyrillic_dice_formula_rolls(string formula)
    {
        var (state, pursuer) = Scene();
        state.PutHazard(2, damage: formula);

        var outcome = ChaseActions.Hazard(state, pursuer.Id, 2, Skill("Ловкость", 50, 80), new ChaseMishap(new ChaseHarm(formula), LostActionsRoll: 1),
            ScriptedDice.Of(5));

        Assert.Equal(5, outcome.Amount(EncounterEffectKind.Damage));
    }

    [Fact]
    [Trait("page", "133")]
    public void Hazard_damage_to_zero_hp_takes_runner_out_of_chase()
    {
        var (state, pursuer) = Scene(hp: 3, started: true);
        state.PutHazard(2);

        state.Apply(ChaseActions.Hazard(state, pursuer.Id, 2, Skill("Ловкость", 50, 80), new ChaseMishap(new ChaseHarm(Roll: 4), LostActionsRoll: 1), NoDice));

        Assert.Equal(0, pursuer.HitPoints);
        Assert.True(pursuer.IsOut);
        Assert.Empty(ChaseRules.AvailableActions(state, pursuer.Id));
    }

    /// <summary>
    /// F-P01 исправлено: уровень — от полного навыка, сложность решает только, пройдена ли проверка (стр. 87–89). В v1 уровень
    /// считался от урезанного порога: навык 60, трудная, бросок 10 → «трудный» вместо «чрезвычайного».
    /// </summary>
    [Theory]
    [Trait("page", "87-89")]
    [Trait("page", "133")]
    [Trait("finding", "F-P01")]
    [InlineData(Difficulty.Regular, 60, 10, SuccessLevel.Extreme, true)]
    [InlineData(Difficulty.Hard, 60, 10, SuccessLevel.Extreme, true)]
    [InlineData(Difficulty.Hard, 60, 30, SuccessLevel.Hard, true)]
    [InlineData(Difficulty.Extreme, 100, 20, SuccessLevel.Extreme, true)]
    // трудная, 31: обычный успех, которого мало
    [InlineData(Difficulty.Hard, 60, 31, SuccessLevel.Regular, false)]
    public void Hazard_level_from_full_skill(Difficulty difficulty, int skill, int roll, SuccessLevel level, bool success)
    {
        var (state, pursuer) = Scene();
        state.PutHazard(2, difficulty);

        var outcome = ChaseActions.Hazard(state, pursuer.Id, 2, Skill("Ловкость", skill, roll), new ChaseMishap(LostActionsRoll: 1), NoDice);

        Assert.Equal(level, outcome.Level);
        Assert.Equal(success, outcome.Success);
        Assert.Contains(RulesText.Of(level), outcome.Resolution.Lines[0]);
    }

    /// <summary>
    /// Провал помехи на транспорте — авария по таблице VI (стр. 142–144): Комплекция и тот же урон каждому внутри; без аварии
    /// (Хранитель решил, что обошлось) транспорт цел.
    /// </summary>
    [Fact]
    [Trait("page", "133")]
    [Trait("page", "144")]
    public void Hazard_vehicle_failure_crash_takes_build_and_hurts_everyone_inside()
    {
        var driver = Runner("Водитель", ChaseRole.Pursuer);
        var passenger = Runner("Пассажир", ChaseRole.Pursuer);
        var state = Track(6, Runner("Артур", ChaseRole.Prey), driver, passenger).InVehicle(driver, 5);
        ChaseRules.SetPassenger(state, passenger.Id, driver.Id);
        state.PutHazard(2);

        var crash = ChaseActions.Hazard(state, driver.Id, 2, Skill("Вождение", 50, 80),
            new ChaseMishap(Crash: VehicleReference.Crashes[1], CrashRoll: 2, LostActionsRoll: 1), ScriptedDice.Of(3, 5));
        var spared = ChaseActions.Hazard(state, driver.Id, 2, Skill("Вождение", 50, 80), new ChaseMishap(LostActionsRoll: 1), NoDice);

        Assert.Equal(2, crash.Amount(EncounterEffectKind.VehicleBuild, driver.Id));
        Assert.Equal(3, crash.Amount(EncounterEffectKind.Damage, driver.Id));
        Assert.Equal(5, crash.Amount(EncounterEffectKind.Damage, passenger.Id));
        Assert.Empty(spared.Of(EncounterEffectKind.VehicleBuild));
        Assert.Empty(spared.Of(EncounterEffectKind.Damage));
    }

    [Theory]
    [Trait("page", "142")]
    [InlineData(Difficulty.Regular, "Мелкая авария")]
    [InlineData(Difficulty.Hard, "Средняя авария")]
    [InlineData(Difficulty.Extreme, "Серьёзная авария")]
    public void Default_crash_by_hazard_difficulty(Difficulty difficulty, string crash) =>
        Assert.Equal(crash, VehicleReference.DefaultCrash(difficulty).Name);

    [Fact]
    [Trait("page", "133")]
    public void Hazard_on_foot_ignores_crash()
    {
        var (state, pursuer) = Scene();
        state.PutHazard(2);

        var outcome = ChaseActions.Hazard(state, pursuer.Id, 2, Skill("Ловкость", 50, 80),
            new ChaseMishap(Crash: VehicleReference.Crashes[2], CrashRoll: 2, LostActionsRoll: 1), NoDice);

        Assert.Empty(outcome.Of(EncounterEffectKind.VehicleBuild));
    }

    [Fact]
    [Trait("page", "133")]
    public void Hazard_unknown_location_regular_difficulty_clamped_to_track()
    {
        var (state, pursuer) = Scene();

        var outcome = ChaseActions.Hazard(state, pursuer.Id, 99, Skill("Ловкость", 50, 50), null, NoDice);

        Assert.True(outcome.Success);
        Assert.Equal(6, outcome.Amount(EncounterEffectKind.ChaseMove));
        Assert.Contains("Помеха", outcome.Resolution.Title);
    }

    // ── Преграда (стр. 134) ──────────────────────────────────────────

    [Theory]
    [Trait("page", "134")]
    [InlineData(Difficulty.Regular, 50, true)]
    [InlineData(Difficulty.Regular, 51, false)]
    [InlineData(Difficulty.Hard, 25, true)]
    [InlineData(Difficulty.Hard, 26, false)]
    [InlineData(Difficulty.Extreme, 10, true)]
    public void Barrier_success_enters_failure_stays(Difficulty difficulty, int roll, bool success)
    {
        var (state, pursuer) = Scene();
        state.PutBarrier(2, difficulty: difficulty);

        var outcome = ChaseActions.Barrier(state, pursuer.Id, 2, Skill("Лазание", 50, roll), null, NoDice);

        Assert.Equal(success, outcome.Success);
        Assert.Equal(success ? (int?)2 : null, outcome.Amount(EncounterEffectKind.ChaseMove));
        Assert.Equal(1, outcome.Amount(EncounterEffectKind.ChaseActionsSpent));
        Assert.Empty(outcome.Of(EncounterEffectKind.ChaseActionsLost));
        Assert.Contains("Забор", outcome.Resolution.Title);
    }

    [Fact]
    [Trait("page", "134")]
    public void Barrier_failure_consequences_only_when_keeper_sets_them()
    {
        var (state, pursuer) = Scene();
        state.PutBarrier(2);

        var outcome = ChaseActions.Barrier(state, pursuer.Id, 2, Skill("Лазание", 50, 90),
            new ChaseMishap(new ChaseHarm(Roll: 2), LostActionsRoll: 1), NoDice);

        Assert.Equal(2, outcome.Amount(EncounterEffectKind.Damage));
        Assert.Equal(1, outcome.Amount(EncounterEffectKind.ChaseActionsLost));
    }

    [Fact]
    [Trait("page", "134")]
    public void Barrier_without_roll_rolls_plain_d100()
    {
        var (state, pursuer) = Scene();
        state.PutBarrier(2);

        var outcome = ChaseActions.Barrier(state, pursuer.Id, 2, Skill("Лазание", 50, null), null, ScriptedDice.Of(2, 4));

        Assert.Equal(42, outcome.Roll!.Result);
    }

    // ── Разрушение преграды (стр. 135–136) ───────────────────────────

    [Fact]
    [Trait("page", "135")]
    public void BreakBarrier_on_foot_damages_without_breaking()
    {
        var (state, pursuer) = Scene();
        state.PutBarrier(2, hitPoints: 10);

        var outcome = ChaseActions.BreakBarrier(state, pursuer.Id, 2, 4, NoDice);

        Assert.False(outcome.Success);
        var hit = Assert.Single(outcome.Of(EncounterEffectKind.BarrierDamage));
        Assert.Equal((2, 4), (hit.Location, hit.Amount));
        Assert.Equal(1, outcome.Amount(EncounterEffectKind.ChaseActionsSpent));
        Assert.Empty(outcome.Of(EncounterEffectKind.ChaseMove));
        Assert.Empty(outcome.Of(EncounterEffectKind.VehicleBuild));
    }

    [Fact]
    [Trait("page", "135")]
    public void BreakBarrier_on_foot_without_roll_rolls_1d3()
    {
        var (state, pursuer) = Scene();
        state.PutBarrier(2, hitPoints: 10);

        var outcome = ChaseActions.BreakBarrier(state, pursuer.Id, 2, null, ScriptedDice.Of(3));

        Assert.Equal(3, outcome.Amount(EncounterEffectKind.BarrierDamage));
    }

    [Fact]
    [Trait("page", "136")]
    public void Broken_barrier_becomes_debris_hazard_breaker_stays()
    {
        var (state, pursuer) = Scene(started: true);
        state.PutBarrier(2, hitPoints: 10);

        var outcome = ChaseActions.BreakBarrier(state, pursuer.Id, 2, 15, NoDice);
        Assert.True(outcome.Success);
        state.Apply(outcome);

        var location = state.Chase!.Location(2)!;
        Assert.Null(location.Barrier);
        Assert.Equal(("Обломки: Забор", Difficulty.Regular, "1D3"), (location.Hazard!.Name, location.Hazard.Difficulty, location.Hazard.Damage));
        Assert.Equal(1, state.R(pursuer).Location);
        Assert.Contains(ChaseActionKind.Hazard, ChaseRules.AvailableActions(state, pursuer.Id));
    }

    [Theory]
    [Trait("page", "135")]
    // пробил: отдача — половина ПЗ преграды
    [InlineData(50, 60, true, 2)]
    // не пробил: отдача — половина своего урона
    [InlineData(100, 30, false, 1)]
    [InlineData(100, 10, false, null)]
    public void BreakBarrier_vehicle_recoil(int barrierHp, int damage, bool destroyed, int? buildLoss)
    {
        var driver = Runner("Водитель", ChaseRole.Pursuer);
        var state = Track(6, Runner("Артур", ChaseRole.Prey), driver).InVehicle(driver, 5).Started();
        state.PutBarrier(2, hitPoints: barrierHp);

        var outcome = ChaseActions.BreakBarrier(state, driver.Id, 2, damage, NoDice);

        Assert.Equal(destroyed, outcome.Success);
        Assert.Equal(buildLoss, outcome.Amount(EncounterEffectKind.VehicleBuild));
        state.Apply(outcome);
        Assert.Equal(5.0 - (buildLoss ?? 0), state.R(driver).Vehicle!.BuildLeft);
    }

    /// <summary>
    /// F-P06 исправлено: транспорт бьёт преграду 1d10 за каждый пункт <b>своей</b> Комплекции (стр. 135), а не Комплекции
    /// водителя, как в v1.
    /// </summary>
    [Fact]
    [Trait("page", "135")]
    [Trait("finding", "F-P06")]
    public void BreakBarrier_vehicle_rolls_d10_per_vehicle_build()
    {
        var driver = Runner("Водитель", ChaseRole.Pursuer, build: 1);
        var state = Track(6, Runner("Артур", ChaseRole.Prey), driver).InVehicle(driver, 5);
        state.PutBarrier(2, hitPoints: 50);

        var dice = ScriptedDice.Of(7, 1, 1, 1, 1);
        var outcome = ChaseActions.BreakBarrier(state, driver.Id, 2, null, dice);

        Assert.Equal(11, outcome.Amount(EncounterEffectKind.BarrierDamage));
        Assert.Equal(0, dice.Remaining);
    }

    // ── Что можно сделать перед препятствием ─────────────────────────

    [Fact]
    [Trait("page", "133")]
    [Trait("page", "134")]
    public void Ahead_hazard_offers_hazard_not_plain_move_barrier_offers_climb_and_break()
    {
        var (state, pursuer) = Scene(started: true);
        Assert.Contains(ChaseActionKind.Move, ChaseRules.AvailableActions(state, pursuer.Id));

        state.PutHazard(2);
        var hazardActions = ChaseRules.AvailableActions(state, pursuer.Id);
        Assert.Contains(ChaseActionKind.Hazard, hazardActions);
        Assert.DoesNotContain(ChaseActionKind.Move, hazardActions);

        state.PutBarrier(2);
        var barrierActions = ChaseRules.AvailableActions(state, pursuer.Id);
        Assert.Contains(ChaseActionKind.Barrier, barrierActions);
        Assert.DoesNotContain(ChaseActionKind.Hazard, barrierActions);
        Assert.DoesNotContain(ChaseActionKind.BreakBarrier, barrierActions); // 0 ПЗ — не сломать

        state.PutBarrier(2, hitPoints: 10);
        Assert.Contains(ChaseActionKind.BreakBarrier, ChaseRules.AvailableActions(state, pursuer.Id));
    }

    [Fact]
    [Trait("page", "141")]
    public void Created_obstacle_is_placed_on_apply()
    {
        var (state, pursuer) = Scene(started: true);
        var shelf = new ChaseLocation { Barrier = new ChaseBarrier { Name = "Шкаф у двери", HitPoints = 10 } };

        var outcome = ChaseActions.CreateObstacle(state, pursuer.Id, 2, shelf, "придвигает шкаф", 1, Skill("СИЛ", 60, 30), NoDice);
        Assert.Null(state.Chase!.Location(2)!.Barrier);
        state.Apply(outcome);

        Assert.Equal(("Шкаф у двери", 10), (state.Chase.Location(2)!.Barrier!.Name, state.Chase.Location(2)!.Barrier!.HitPointsLeft));
        Assert.Equal(1, state.R(pursuer).ActionsLeft);
    }

    [Fact]
    [Trait("page", "141")]
    public void Created_obstacle_without_check_always_works_failed_check_places_nothing()
    {
        var (state, pursuer) = Scene();
        var lockDoor = new ChaseLocation { Barrier = new ChaseBarrier { Name = "Запертая дверь" } };

        var free = ChaseActions.CreateObstacle(state, pursuer.Id, 2, lockDoor, "запирает дверь", 1, null, NoDice);
        var failed = ChaseActions.CreateObstacle(state, pursuer.Id, 2, lockDoor, "запирает дверь", 1, Skill("Взлом", 40, 90), NoDice);

        Assert.Single(free.Of(EncounterEffectKind.PlaceObstacle));
        Assert.Empty(failed.Of(EncounterEffectKind.PlaceObstacle));
        Assert.Equal(1, failed.Amount(EncounterEffectKind.ChaseActionsSpent));
    }
}
