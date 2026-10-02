using CampaignManager.Core.Encounters;
using CampaignManager.Core.Encounters.Chase;
using CampaignManager.Core.KeeperScreen;
using static CampaignManager.Core.Tests.Encounters.Chase.ChaseScene;

namespace CampaignManager.Core.Tests.Encounters.Chase;

/// <summary>СКО по способу передвижения, Комплекция в погоне, транспорт таблицы V (перенос <c>ParticipantTests</c> T0.2).</summary>
public sealed class ChaseParticipantTests
{
    [Theory]
    [Trait("page", "141")]
    [InlineData(false, MovementMode.OnFoot, null, null, null, 8)]
    // транспорт — своя СКО, способ передвижения не важен
    [InlineData(true, MovementMode.OnFoot, null, null, null, 14)]
    // вписанная Хранителем СКО важнее статблока
    [InlineData(false, MovementMode.Swimming, 5, 3, null, 5)]
    [InlineData(false, MovementMode.Swimming, null, 3, null, 3)]
    [InlineData(false, MovementMode.Flying, null, null, 20, 20)]
    // своей СКО нет — половина обычной
    [InlineData(false, MovementMode.Flying, null, null, null, 4)]
    [InlineData(false, MovementMode.Swimming, null, null, 20, 4)]
    public void BaseMove_entered_then_statblock_then_half(bool inVehicle, MovementMode mode, int? entered, int? swim, int? fly, int expected)
    {
        var byakhee = Runner("Бьякхи", ChaseRole.Pursuer, mov: 8);
        byakhee.Stats.Swim = swim;
        byakhee.Stats.Fly = fly;
        var state = Track(6, byakhee);
        if (inVehicle)
            state.InVehicle(byakhee, 5);
        state.R(byakhee).Mode = mode;
        state.R(byakhee).ModeSpeed = entered;

        Assert.Equal(expected, ChaseRules.BaseMove(byakhee, state.R(byakhee)));
    }

    [Fact]
    [Trait("page", "141")]
    public void Odd_move_halved_rounds_down()
    {
        var arthur = Runner("Артур", ChaseRole.Prey, mov: 7);
        var state = Track(6, arthur);
        state.R(arthur).Mode = MovementMode.Swimming;

        Assert.Equal(3, ChaseRules.BaseMove(arthur, state.R(arthur)));
    }

    [Theory]
    [Trait("page", "130")]
    [InlineData(8, 1, 9)]
    [InlineData(8, -1, 7)]
    [InlineData(0, -1, 0)]
    public void Move_adds_speed_check_modifier_not_below_zero(int mov, int modifier, int expected)
    {
        var arthur = Runner("Артур", ChaseRole.Prey, mov: mov);
        var state = Track(6, arthur);
        state.R(arthur).SpeedModifier = modifier;

        Assert.Equal(expected, ChaseRules.Move(arthur, state.R(arthur)));
    }

    [Fact]
    [Trait("page", "136")]
    public void Build_on_foot_is_own_in_vehicle_is_vehicle_and_passenger_shares_it()
    {
        var driver = Runner("Артур", ChaseRole.Prey, build: 2);
        var passenger = Runner("Билл", ChaseRole.Prey, build: 1);
        var state = Track(6, driver, passenger);
        Assert.Equal(2.0, ChaseRules.Build(state, driver.Id));

        state.InVehicle(driver, 5);
        state.R(driver).Vehicle!.BuildLeft = 3.5;
        ChaseRules.SetPassenger(state, passenger.Id, driver.Id);

        Assert.Equal(3.5, ChaseRules.Build(state, driver.Id));
        Assert.Equal(3.5, ChaseRules.Build(state, passenger.Id));
        Assert.False(VehicleRules.IsWrecked(state.R(driver).Vehicle!));
        state.R(driver).Vehicle!.BuildLeft = 0;
        Assert.True(VehicleRules.IsWrecked(state.R(driver).Vehicle!));
    }

    [Theory]
    [Trait("page", "136")]
    [InlineData(2, 2, 0, false)]
    [InlineData(2, 3, 1, false)]
    [InlineData(2, 4.5, 2, false)]
    [InlineData(2, 5, 0, true)]
    [InlineData(4, 1, 0, false)]
    public void Maneuver_build_penalty_by_difference(double attacker, double target, int penalty, bool impossible) =>
        Assert.Equal((penalty, impossible), ChaseActions.ManeuverBuildPenalty(attacker, target));

    [Fact]
    [Trait("page", "143")]
    public void SetVehicle_copies_table_V_row_and_skill()
    {
        var arthur = Runner("Артур", ChaseRole.Prey);
        var state = Track(6, arthur);

        ChaseRules.SetVehicle(state, arthur.Id, VehicleReference.Find("Стандартный автомобиль"), driverSkill: 40);

        var vehicle = state.R(arthur).Vehicle!;
        Assert.Equal((14, 5.0, 5.0, 2), (ChaseRules.BaseMove(arthur, state.R(arthur)), vehicle.Build, ChaseRules.Build(state, arthur.Id), vehicle.Armor));
        Assert.Equal((VehicleReference.Drive, 40), (vehicle.SkillCode, vehicle.Skill));
    }

    /// <summary>
    /// F-P10 исправлено: Хранитель не вписал СКО — берётся полёт из статблока (20), и журнал говорит «из статблока», а не
    /// «своей СКО нет — половина обычной», как в v1.
    /// </summary>
    [Fact]
    [Trait("page", "141")]
    [Trait("finding", "F-P10")]
    public void ChangeMode_statblock_speed_log_says_statblock()
    {
        var byakhee = Runner("Бьякхи", ChaseRole.Pursuer, mov: 5);
        byakhee.Stats.Fly = 20;
        var state = Track(6, Runner("Артур", ChaseRole.Prey), byakhee);

        var text = ChaseRules.ChangeMode(state, byakhee.Id, MovementMode.Flying, null, Now);

        Assert.Equal(20, ChaseRules.Move(byakhee, state.R(byakhee)));
        Assert.Contains("СКО 5 → 20", text);
        Assert.Contains("из статблока", text);
        Assert.DoesNotContain("половина обычной", text);
        Assert.Equal(EncounterLogKind.ModeChange, state.Log[^1].Kind);
    }

    [Fact]
    [Trait("page", "141")]
    public void ChangeMode_entered_speed_log_says_keeper_and_no_own_speed_says_half()
    {
        var arthur = Runner("Артур", ChaseRole.Prey, mov: 8);
        var state = Track(6, arthur, Runner("Вампир", ChaseRole.Pursuer));

        var entered = ChaseRules.ChangeMode(state, arthur.Id, MovementMode.Swimming, 6, Now);
        Assert.Equal(6, ChaseRules.Move(arthur, state.R(arthur)));
        Assert.Contains("вписанная Хранителем", entered);

        var half = ChaseRules.ChangeMode(state, arthur.Id, MovementMode.Swimming, null, Now);
        Assert.Equal(4, ChaseRules.Move(arthur, state.R(arthur)));
        Assert.Contains("половина обычной", half);
    }

    [Fact]
    [Trait("page", "139")]
    public void Passenger_rides_with_driver_without_speed_check_or_actions()
    {
        var driver = Runner("Водитель", ChaseRole.Prey);
        var passenger = Runner("Пассажир", ChaseRole.Pursuer);
        var state = Track(6, driver, passenger).InVehicle(driver, 5);

        ChaseRules.SetPassenger(state, passenger.Id, driver.Id);
        ChaseRules.SetPosition(state, driver.Id, 4);

        var runner = state.R(passenger);
        Assert.True(runner.IsPassenger);
        Assert.True(runner.SpeedChecked);
        Assert.Equal(ChaseRole.Prey, runner.Role); // едет с водителем — та же роль
        Assert.Equal(4, runner.Location);
    }

    [Fact]
    [Trait("page", "139")]
    public void Passenger_only_of_a_driver()
    {
        var walker = Runner("Пешеход", ChaseRole.Prey);
        var passenger = Runner("Пассажир", ChaseRole.Prey);
        var state = Track(6, walker, passenger);

        ChaseRules.SetPassenger(state, passenger.Id, walker.Id);

        Assert.False(state.R(passenger).IsPassenger);
    }
}
