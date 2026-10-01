using CampaignManager.Web.Components.Features.Chase.Model;
using CampaignManager.Web.Components.Features.Chase.Services;
using static CampaignManager.Rules.Tests.Chase.ChaseScene;

namespace CampaignManager.Rules.Tests.Chase;

/// <summary>СКО участника по способу передвижения и Комплекция в погоне.</summary>
public sealed class ParticipantTests
{
    [Theory]
    [Trait("page", "141")]
    [InlineData(false, MovementMode.OnFoot, 0, null, null, 8)]
    // транспорт — своя СКО, способ передвижения не важен
    [InlineData(true, MovementMode.Flying, 0, null, null, 14)]
    // введённая Хранителем СКО важнее статблока
    [InlineData(false, MovementMode.Swimming, 5, 3, null, 5)]
    [InlineData(false, MovementMode.Swimming, 0, 3, null, 3)]
    [InlineData(false, MovementMode.Flying, 0, null, 20, 20)]
    // своей СКО нет — половина обычной
    [InlineData(false, MovementMode.Flying, 0, null, null, 4)]
    [InlineData(false, MovementMode.Swimming, 0, null, 20, 4)]
    public void BaseMov_EnteredThenStatBlockThenHalf(bool inVehicle, MovementMode mode, int native, int? swim,
        int? fly, int expected)
    {
        var p = Runner("Бьякхи", ChaseRole.Pursuer, mov: 8);
        if (inVehicle) p.InVehicle(5);
        p.Mode = mode;
        p.NativeModeSpeed = native;
        p.SwimSpeed = swim;
        p.FlySpeed = fly;

        Assert.Equal(expected, p.BaseMov);
    }

    [Fact]
    [Trait("page", "141")]
    public void BaseMov_OddMovHalved_RoundsDown()
    {
        var p = Runner("Артур", ChaseRole.Prey, mov: 7);
        p.Mode = MovementMode.Swimming;

        Assert.Equal(3, p.BaseMov);
    }

    [Theory]
    [Trait("page", "130")]
    [InlineData(8, 1, 9)]
    [InlineData(8, -1, 7)]
    [InlineData(0, -1, 0)]
    public void AdjustedMov_AddsSpeedCheckModifier_NotBelowZero(int mov, int modifier, int expected)
    {
        var p = Runner("Артур", ChaseRole.Prey, mov: mov);
        p.MovModifier = modifier;

        Assert.Equal(expected, p.AdjustedMov);
    }

    [Fact]
    [Trait("page", "136")]
    public void EffectiveBuild_OnFootIsCharacterBuild_InVehicleIsCurrentVehicleBuild()
    {
        var p = Runner("Артур", ChaseRole.Prey, build: 2);
        Assert.Equal(2.0, p.EffectiveBuild);

        p.InVehicle(5);
        p.VehicleCurrentBuild = 3.5;
        Assert.Equal(3.5, p.EffectiveBuild);
        Assert.False(p.IsVehicleWrecked);

        p.VehicleCurrentBuild = 0;
        Assert.True(p.IsVehicleWrecked);
    }

    [Theory]
    [Trait("page", "136")]
    [InlineData(2, 2, 0, false)]
    [InlineData(2, 3, 1, false)]
    [InlineData(2, 4.5, 2, false)]
    [InlineData(2, 5, 0, true)]
    [InlineData(4, 1, 0, false)]
    public void GetManeuverBuildPenalty_ByBuildDifference(double attacker, double target, int penalty,
        bool impossible)
    {
        var a = Runner("A", ChaseRole.Pursuer).InVehicle(attacker);
        var t = Runner("T", ChaseRole.Prey).InVehicle(target);

        Assert.Equal((penalty, impossible), ChaseService.GetManeuverBuildPenalty(a, t));
    }

    [Fact]
    [Trait("page", "143")]
    public void SetVehicleFromTemplate_CopiesTableVRow()
    {
        var p = Runner("Артур", ChaseRole.Prey);
        var chase = Track(6, p);
        var car = ChaseReference.FindVehicle("Стандартный автомобиль")!;

        chase.SetVehicleFromTemplate(p.Id, car);

        Assert.Equal((14, 5.0, 5.0, 2), (p.BaseMov, p.VehicleBuild, p.EffectiveBuild, p.VehicleArmor));
        Assert.Equal("Вождение автомобиля", p.VehicleSkillName);
    }

    /// <summary>
    ///     F-P10: Хранитель не вписал СКО — берётся полёт из статблока (20), но запись в журнале
    ///     говорит «своей СКО нет — половина обычной».
    /// </summary>
    [Fact]
    [Trait("page", "141")]
    [Trait("finding", "F-P10")]
    public void ChangeMovementMode_StatBlockSpeed_LogSaysHalf()
    {
        var byakhee = Runner("Бьякхи", ChaseRole.Pursuer, mov: 5);
        byakhee.FlySpeed = 20;
        var chase = Track(6, Runner("Артур", ChaseRole.Prey), byakhee);

        var result = chase.ChangeMovementMode(byakhee.Id, MovementMode.Flying, 0);

        Assert.Equal((20, 20), (byakhee.NativeModeSpeed, byakhee.AdjustedMov));
        Assert.Contains("СКО 5 → 20", result.Summary);
        Assert.Contains("половина обычной", result.Summary);
        Assert.True(result.IsApplied);
    }

    [Fact]
    [Trait("page", "141")]
    public void ChangeMovementMode_EnteredSpeed_LogSaysOwn()
    {
        var p = Runner("Артур", ChaseRole.Prey, mov: 8);
        var chase = Track(6, p, Runner("Вампир", ChaseRole.Pursuer));

        var result = chase.ChangeMovementMode(p.Id, MovementMode.Swimming, 6);

        Assert.Equal(6, p.AdjustedMov);
        Assert.Contains("собственная СКО", result.Summary);
    }
}
