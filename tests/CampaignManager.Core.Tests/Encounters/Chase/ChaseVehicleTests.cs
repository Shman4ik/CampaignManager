using CampaignManager.Core.Dice;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Encounters.Chase;
using CampaignManager.Core.KeeperScreen;
using CampaignManager.Core.Tests.Infrastructure;
using static CampaignManager.Core.Tests.Encounters.Chase.ChaseScene;

namespace CampaignManager.Core.Tests.Encounters.Chase;

/// <summary>Таран, стрельба по шинам, водитель с серьёзной раной, урон транспорту (перенос <c>VehicleTests</c> T0.2).</summary>
public sealed class ChaseVehicleTests
{
    /// <summary>Две машины в одной локации: атакующий-преследователь и цель-жертва.</summary>
    private static (EncounterState State, EncounterParticipant Attacker, EncounterParticipant Target) Cars(double attackerBuild = 5,
        double targetBuild = 5)
    {
        var attacker = Runner("Артур", ChaseRole.Pursuer, hp: 10);
        var target = Runner("Вампир", ChaseRole.Prey, hp: 10);
        var state = Track(6, target, attacker).InVehicle(attacker, attackerBuild).InVehicle(target, targetBuild);
        ChaseRules.SetPosition(state, attacker.Id, state.R(target).Location);
        return (state, attacker, target);
    }

    // ── Таран (стр. 136) ─────────────────────────────────────────────

    [Fact]
    [Trait("page", "136")]
    public void Ram_miss_no_damage()
    {
        var (state, attacker, target) = Cars();

        var outcome = ChaseActions.Ram(state, attacker.Id, target.Id, Skill("Вождение", 50, 80), null, 27, NoDice);

        Assert.False(outcome.Success);
        Assert.Empty(outcome.Of(EncounterEffectKind.VehicleBuild));
        Assert.Equal(1, outcome.Amount(EncounterEffectKind.ChaseActionsSpent));
        Assert.EndsWith("мимо", outcome.Resolution.Title);
    }

    /// <summary>
    /// Удар 27: цель теряет 2 Комплекции — полные десятки, остаток не учитывается (стр. 136, 143); отдача 13 — 1 Комплекция.
    /// v1 копил остаток (5 + 27 = 32 → 3), а находка F-P07 требовала копить его и с ударов меньше 10 — книга прямо говорит, что
    /// остаток меньше 10 не учитывается (F-P07 пересмотрена).
    /// </summary>
    [Fact]
    [Trait("page", "136")]
    [Trait("page", "143")]
    [Trait("finding", "F-P07")]
    public void Ram_hit_full_tens_remainder_ignored_recoil_half()
    {
        var (state, attacker, target) = Cars();

        var outcome = ChaseActions.Ram(state, attacker.Id, target.Id, Skill("Вождение", 50, 30), null, 27, NoDice);
        state.Apply(outcome);

        Assert.True(outcome.Success);
        Assert.Equal(2, outcome.Amount(EncounterEffectKind.VehicleBuild, target.Id));
        Assert.Equal(1, outcome.Amount(EncounterEffectKind.VehicleBuild, attacker.Id));
        Assert.Equal(3.0, state.R(target).Vehicle!.BuildLeft);
        Assert.Equal(4.0, state.R(attacker).Vehicle!.BuildLeft);
        // таран бьёт машину, а не людей
        Assert.Equal(10, target.HitPoints);
    }

    [Fact]
    [Trait("page", "136")]
    [Trait("finding", "F-P07")]
    public void Two_rams_below_ten_leave_the_car_whole()
    {
        var (state, attacker, target) = Cars();

        for (var i = 0; i < 2; i++)
            state.Apply(ChaseActions.Ram(state, attacker.Id, target.Id, Skill("Вождение", 50, 30), null, 6, NoDice));

        Assert.Equal(5.0, state.R(target).Vehicle!.BuildLeft);
    }

    [Fact]
    [Trait("page", "136")]
    public void Ram_recoil_capped_by_target_build()
    {
        // велосипед: Комплекция 0,5 — отдача не больше ⌈0,5⌉ = 1 (пример книги с мотоциклом)
        var (state, attacker, target) = Cars(targetBuild: 0.5);

        var outcome = ChaseActions.Ram(state, attacker.Id, target.Id, Skill("Вождение", 50, 30), null, 60, NoDice);

        Assert.Equal(6, outcome.Amount(EncounterEffectKind.VehicleBuild, target.Id));
        Assert.Equal(1, outcome.Amount(EncounterEffectKind.VehicleBuild, attacker.Id));
    }

    /// <summary>Кости d10 тарана — по текущей Комплекции транспорта (как в v1: <c>Math.Round</c>, но не меньше одной).</summary>
    [Theory]
    [Trait("page", "136")]
    [InlineData(0.5, 1)]
    [InlineData(1, 1)]
    [InlineData(2.5, 2)]
    [InlineData(3.5, 4)]
    [InlineData(5, 5)]
    public void Ram_without_damage_roll_rolls_d10_per_build(double build, int dice)
    {
        var (state, attacker, target) = Cars(attackerBuild: build);

        var scripted = ScriptedDice.Of([.. Enumerable.Repeat(1, dice)]);
        var outcome = ChaseActions.Ram(state, attacker.Id, target.Id, Skill("Вождение", 50, 30), null, null, scripted);

        Assert.Equal(0, scripted.Remaining);
        Assert.Contains(outcome.Resolution.Lines, l => l.Contains($"({dice}d10)", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("page", "136")]
    public void Ram_dodged_with_driving_tie_goes_to_defender()
    {
        var (state, attacker, target) = Cars();

        var outcome = ChaseActions.Ram(state, attacker.Id, target.Id, Skill("Вождение", 50, 30),
            new ChaseDefence("Вождение", 50, 40), 27, NoDice);

        Assert.False(outcome.Success);
        Assert.Empty(outcome.Of(EncounterEffectKind.VehicleBuild));
    }

    [Fact]
    [Trait("page", "136")]
    public void Ram_pedestrian_hurts_person_recoil_by_person_build()
    {
        var attacker = Runner("Артур", ChaseRole.Pursuer);
        var walker = Runner("Культист", ChaseRole.Prey, hp: 12, build: 0);
        var state = Track(6, walker, attacker).InVehicle(attacker, 5);
        ChaseRules.SetPosition(state, attacker.Id, state.R(walker).Location);

        var outcome = ChaseActions.Ram(state, attacker.Id, walker.Id, Skill("Вождение", 50, 30), null, 25, NoDice);

        Assert.Equal(25, outcome.Amount(EncounterEffectKind.Damage, walker.Id));
        Assert.Empty(outcome.Of(EncounterEffectKind.VehicleBuild)); // Комплекция цели 0 — отдачи в Комплекции нет
    }

    // ── Урон транспорту (стр. 143) ───────────────────────────────────

    [Theory]
    [Trait("page", "143")]
    [InlineData(5, 3, false)]
    [InlineData(5, 2, true)]
    [InlineData(5, 0, false)]
    [InlineData(0.5, 0.5, false)]
    public void Vehicle_breaks_down_at_half_build(double build, double left, bool broken) =>
        Assert.Equal(broken, VehicleRules.IsBrokenDown(new ChaseVehicle { Build = build, BuildLeft = left }));

    [Fact]
    [Trait("page", "143")]
    public void Crash_equal_to_full_build_wrecks_the_vehicle()
    {
        var car = new ChaseVehicle { Build = 5, BuildLeft = 5 };

        Assert.Contains("вдребезги", VehicleRules.Note(car, 5, 5));
        Assert.Contains("вышел из строя", VehicleRules.Note(car, 2, 2));
        Assert.Contains("поломка", VehicleRules.Note(car, 5, 3));
        Assert.Null(VehicleRules.Note(car, 5, 1));
    }

    [Fact]
    [Trait("page", "143")]
    [Trait("finding", "F-P15")]
    public void Broken_down_driver_gets_penalty_die_on_driving_checks()
    {
        var (state, attacker, target) = Cars();
        state.R(attacker).Vehicle!.BuildLeft = 2;

        // единицы 0, десятки 3 и 8 → 30 и 80: штрафная — большее
        var outcome = ChaseActions.Ram(state, attacker.Id, target.Id, Skill("Вождение", 50, null), null, 27, ScriptedDice.Of(0, 3, 8));

        Assert.Equal(80, outcome.Roll!.Result);
        Assert.False(outcome.Success);
    }

    [Fact]
    [Trait("page", "143")]
    public void Wrecked_vehicle_cannot_move_or_ram()
    {
        var (state, attacker, _) = Cars();
        state.Started();
        state.R(attacker).Vehicle!.BuildLeft = 0;

        var actions = ChaseRules.AvailableActions(state, attacker.Id);

        Assert.DoesNotContain(ChaseActionKind.Move, actions);
        Assert.DoesNotContain(ChaseActionKind.Ram, actions);
    }

    [Fact]
    public void Vehicle_table_and_crash_table_have_book_numbers()
    {
        Assert.Equal(27, VehicleReference.Vehicles.Count);
        Assert.Equal(["1D3-1", "1D6", "1D10", "2D10", "5D10"], VehicleReference.Crashes.Select(c => c.BuildLoss));
        Assert.Equal([5, 10, 15, 25, 50, 100], VehicleReference.Barriers.Select(b => b.HitPoints));
        Assert.All(VehicleReference.Vehicles, v => Assert.StartsWith("skill.", v.SkillCode));
        Assert.All(VehicleReference.Crashes, c => Assert.True(DiceFormula.Parse(c.BuildLoss).IsValid));
        Assert.All(Enum.GetValues<VehicleCategory>(), c => Assert.NotEqual(c.ToString(), VehicleReference.Of(c)));
    }

    // ── Стрельба по шинам (стр. 139) ─────────────────────────────────

    [Theory]
    [Trait("page", "139")]
    [InlineData(5, 1)]
    [InlineData(4, null)]
    [InlineData(2, null)]
    public void Tyres_hit_armour_three_two_points_burst(int damage, int? buildLoss)
    {
        var (state, attacker, target) = Cars();

        var outcome = ChaseActions.Tyres(state, attacker.Id, target.Id, Skill("Стрельба", 50, 30), stopped: true, damage, NoDice);

        Assert.True(outcome.Success);
        Assert.Equal(buildLoss, outcome.Amount(EncounterEffectKind.VehicleBuild, target.Id));
        Assert.Equal(1, outcome.Amount(EncounterEffectKind.ChaseActionsSpent));
    }

    /// <summary>
    /// Шина — маленькая цель (штрафная кость) плюс стрельба на ходу — ещё одна (стр. 139). v1 стрелял по шинам «на ходу» без
    /// штрафной за ход (F-P17).
    /// </summary>
    [Fact]
    [Trait("page", "139")]
    [Trait("finding", "F-P17")]
    public void Tyres_on_the_move_two_penalty_dice_no_action()
    {
        var (state, attacker, target) = Cars();

        // единицы 3, десятки 2, 8, 5 → 23, 83, 53: две штрафные — большее
        var outcome = ChaseActions.Tyres(state, attacker.Id, target.Id, Skill("Стрельба", 50, null), stopped: false, 5, ScriptedDice.Of(3, 2, 8, 5));

        Assert.Equal(83, outcome.Roll!.Result);
        Assert.False(outcome.Success);
        Assert.Empty(outcome.Of(EncounterEffectKind.ChaseActionsSpent));
    }

    [Fact]
    [Trait("page", "139")]
    public void Tyres_without_damage_roll_rolls_d10_and_apply_takes_one_build()
    {
        var (state, attacker, target) = Cars();

        state.Apply(ChaseActions.Tyres(state, attacker.Id, target.Id, Skill("Стрельба", 50, 30), stopped: true, null, ScriptedDice.Of(6)));

        Assert.Equal(4.0, state.R(target).Vehicle!.BuildLeft);
    }

    // ── Водитель с серьёзной раной (стр. 139, 144) ────────────────────

    [Theory]
    [Trait("page", "139")]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void DriverControl_is_a_hard_check(int roll, bool kept)
    {
        var (state, driver, _) = Cars();

        var outcome = ChaseActions.DriverControl(state, driver.Id, Skill("Вождение", 60, roll), unconscious: false,
            new ChaseMishap(new ChaseHarm(Roll: 3), VehicleReference.Crashes[2], CrashRoll: 4, LostActionsRoll: 2), NoDice);

        Assert.Equal(kept, outcome.Success);
        Assert.Empty(outcome.Of(EncounterEffectKind.ChaseActionsSpent));
        if (kept)
        {
            Assert.Empty(outcome.Resolution.Effects);
        }
        else
        {
            Assert.Equal(4, outcome.Amount(EncounterEffectKind.VehicleBuild));
            Assert.Equal(2, outcome.Amount(EncounterEffectKind.ChaseActionsLost));
            Assert.Equal(3, outcome.Amount(EncounterEffectKind.Damage, driver.Id));
        }
    }

    /// <summary>F-P01 исправлено: уровень водителя — от полного навыка (10 при навыке 60 — чрезвычайный, в v1 — «трудный»).</summary>
    [Fact]
    [Trait("page", "139")]
    [Trait("finding", "F-P01")]
    public void DriverControl_level_from_full_skill()
    {
        var (state, driver, _) = Cars();

        var outcome = ChaseActions.DriverControl(state, driver.Id, Skill("Вождение", 60, 10), false, null, NoDice);

        Assert.Equal(SuccessLevel.Extreme, outcome.Level);
        Assert.True(outcome.Success);
    }

    [Fact]
    [Trait("page", "139")]
    public void DriverControl_unconscious_loses_control_even_on_critical()
    {
        var (state, driver, _) = Cars();

        var outcome = ChaseActions.DriverControl(state, driver.Id, Skill("Вождение", 60, 1), unconscious: true,
            new ChaseMishap(new ChaseHarm(Roll: 1), VehicleReference.Crashes[0], CrashRoll: 1, LostActionsRoll: 1), NoDice);

        Assert.False(outcome.Success);
        Assert.Null(outcome.Roll);
        Assert.Contains("Без сознания", outcome.Resolution.Lines[0]);
    }

    /// <summary>Аварию Хранитель не выбрал — средняя, как при трудной помехе (стр. 139, 142): 1d6 Комплекции, 1d6 урона, 1d3 действия.</summary>
    [Fact]
    [Trait("page", "142")]
    public void DriverControl_default_crash_is_medium()
    {
        var (state, driver, _) = Cars();

        var dice = ScriptedDice.Of(5, 2, 1);
        var outcome = ChaseActions.DriverControl(state, driver.Id, Skill("Вождение", 60, 90), false, null, dice);

        Assert.Equal(5, outcome.Amount(EncounterEffectKind.VehicleBuild));
        Assert.Equal(2, outcome.Amount(EncounterEffectKind.Damage));
        Assert.Equal(1, outcome.Amount(EncounterEffectKind.ChaseActionsLost));
        Assert.Contains(outcome.Resolution.Lines, l => l.Contains("Средняя авария", StringComparison.Ordinal));
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    [Trait("page", "145")]
    public void Minor_crash_1d3_minus_1_can_be_zero()
    {
        var (state, driver, _) = Cars();

        var outcome = ChaseActions.DriverControl(state, driver.Id, Skill("Вождение", 60, 90), false,
            new ChaseMishap(new ChaseHarm(Roll: 0), VehicleReference.Crashes[0], LostActionsRoll: 3), ScriptedDice.Of(1));

        Assert.Equal(0, outcome.Amount(EncounterEffectKind.VehicleBuild));
        Assert.Equal(3, outcome.Amount(EncounterEffectKind.ChaseActionsLost));
    }

    [Fact]
    [Trait("page", "139")]
    public void Apply_driver_lost_control_hits_own_vehicle_and_driver()
    {
        var (state, driver, _) = Cars();

        state.Apply(ChaseActions.DriverControl(state, driver.Id, Skill("Вождение", 60, 90), false,
            new ChaseMishap(new ChaseHarm(Roll: 3), VehicleReference.Crashes[2], CrashRoll: 4, LostActionsRoll: 1), NoDice));

        Assert.Equal(1.0, state.R(driver).Vehicle!.BuildLeft);
        Assert.Equal(7, driver.HitPoints);
    }
}
