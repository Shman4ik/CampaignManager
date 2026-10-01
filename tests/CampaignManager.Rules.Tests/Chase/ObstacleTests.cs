using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Chase.Model;
using CampaignManager.Web.Components.Features.Chase.Services;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;
using static CampaignManager.Rules.Tests.Chase.ChaseScene;

namespace CampaignManager.Rules.Tests.Chase;

/// <summary>Помехи, преграды и их разрушение. Препятствие лежит в СЛЕДУЮЩЕЙ локации.</summary>
public sealed class ObstacleTests
{
    /// <summary>Трасса из 6 локаций: преследователь на 1, жертва на 3, впереди преследователя — локация 2.</summary>
    private static (ChaseService Chase, ChaseParticipant Pursuer) Scene(ChaseParticipant? pursuer = null)
    {
        pursuer ??= Runner("Вампир", ChaseRole.Pursuer, hp: 10);
        var chase = Track(6, Runner("Артур", ChaseRole.Prey), pursuer);
        return (chase, pursuer);
    }

    // ── Помеха (стр. 133) ────────────────────────────────────────────

    [Fact]
    [Trait("page", "133")]
    public void ResolveHazard_Success_EntersLocation_CostsOnePlusBonusDice()
    {
        var (chase, pursuer) = Scene();
        chase.PutHazard(2);

        var result = chase.ResolveHazard(pursuer.Id, 2, "Ловкость", 50, 30, 1, null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal((1, 2), (result.LocationBefore, result.LocationAfter));
        Assert.Equal((1, 2), (result.BonusDiceUsed, result.ActorMovementActionsSpent));
        Assert.Equal("Лужа (Ловкость)", result.SkillName);
        Assert.Null(result.DamageDealt);
        Assert.Equal(0, result.MovementActionsLost);
        // Resolve* — чистые: ни участник, ни журнал не тронуты
        Assert.Equal(1, pursuer.CurrentLocation);
        Assert.Empty(chase.ChaseLog);
    }

    [Fact]
    [Trait("page", "133")]
    public void ResolveHazard_Failure_StillEntersLocation_WithDamageAndLostActions()
    {
        var (chase, pursuer) = Scene();
        chase.PutHazard(2);

        var result = chase.ResolveHazard(pursuer.Id, 2, "Ловкость", 50, 80, 0, 4, 2);

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.LocationAfter);
        Assert.Equal((4, 10, 6), (result.DamageDealt, result.HpBefore, result.HpAfter));
        Assert.Equal(2, result.MovementActionsLost);
    }

    [Fact]
    [Trait("page", "133")]
    public void ApplyResult_HazardFailure_MovesHurtsAndLogs()
    {
        var (chase, pursuer) = Scene();
        chase.PutHazard(2);
        chase.StartChase();

        chase.ApplyResult(chase.ResolveHazard(pursuer.Id, 2, "Ловкость", 50, 80, 0, 4, 2));

        Assert.Equal((2, 6), (pursuer.CurrentLocation, pursuer.CurrentHitPoints));
        Assert.Equal((0, 2), (pursuer.MovementActionsRemaining, pursuer.MovementActionDebt));
        Assert.Single(chase.ChaseLog);
        Assert.True(chase.ChaseLog[0].IsApplied);
    }

    [Theory]
    [Trait("page", "133")]
    [InlineData(5, 2)]
    [InlineData(-1, 0)]
    public void ResolveHazard_BonusDice_ClampedToTwo(int asked, int used)
    {
        var (chase, pursuer) = Scene();
        chase.PutHazard(2);

        var result = chase.ResolveHazard(pursuer.Id, 2, "Ловкость", 50, 30, asked, null, null);

        Assert.Equal((used, 1 + used), (result.BonusDiceUsed, result.ActorMovementActionsSpent));
    }

    [Fact]
    [Trait("page", "133")]
    public void ResolveHazard_NoRoll_PaidBonusDiceGoIntoRoll()
    {
        var (chase, pursuer) = Scene();
        chase.PutHazard(2);

        // единицы 5, десятки 9, 2, 7 → 95, 25, 75; две бонусные — берётся меньшее
        using var dice = ScriptedRandom.Use(5, 9, 2, 7);
        var result = chase.ResolveHazard(pursuer.Id, 2, "Ловкость", 50, null, 2, null, null);

        Assert.Equal(25, result.Roll);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    [Trait("page", "133")]
    public void ResolveHazard_FailureWithoutRolls_RollsFormulaThenOneD3()
    {
        var (chase, pursuer) = Scene();
        chase.PutHazard(2, damageFormula: "1D6");

        using var dice = ScriptedRandom.Use(4, 2);
        var result = chase.ResolveHazard(pursuer.Id, 2, "Ловкость", 50, 80, 0, null, null);

        Assert.Equal((4, 6), (result.DamageDealt, result.HpAfter));
        Assert.Equal(2, result.MovementActionsLost);
    }

    /// <summary>F-P02: «1д6» с русской «д» — бросок ноль, кости не бросаются вовсе.</summary>
    [Theory]
    [Trait("page", "133")]
    [Trait("finding", "F-P02")]
    [InlineData("1д6")]
    [InlineData("1Д6")]
    public void ResolveHazard_CyrillicDiceFormula_DealsZero(string formula)
    {
        var (chase, pursuer) = Scene();
        chase.PutHazard(2, damageFormula: formula);

        using var dice = ScriptedRandom.Use();
        var result = chase.ResolveHazard(pursuer.Id, 2, "Ловкость", 50, 80, 0, null, 1);

        Assert.Equal(0, result.DamageDealt);
        Assert.Null(result.HpAfter);
        Assert.DoesNotContain("Урон", result.Summary);
    }

    /// <summary>
    ///     F-P01: уровень успеха считается от урезанного порога, а не от полного навыка.
    ///     Прошёл ли участник — верно; какой уровень записан в результат и журнал — нет.
    /// </summary>
    [Theory]
    [Trait("page", "87-89")]
    [Trait("page", "133")]
    [Trait("finding", "F-P01")]
    // обычная сложность — расхождения нет
    [InlineData(1, 60, 10, 60, SuccessLevel.ExtremeSuccess, SuccessLevel.ExtremeSuccess, true)]
    // трудная: 10 ≤ 60/5 — по книге чрезвычайный
    [InlineData(2, 60, 10, 30, SuccessLevel.HardSuccess, SuccessLevel.ExtremeSuccess, true)]
    // трудная: 30 ≤ 60/2 — по книге трудный
    [InlineData(2, 60, 30, 30, SuccessLevel.RegularSuccess, SuccessLevel.HardSuccess, true)]
    // чрезвычайная: 20 ≤ 100/5 — по книге чрезвычайный
    [InlineData(3, 100, 20, 20, SuccessLevel.RegularSuccess, SuccessLevel.ExtremeSuccess, true)]
    // трудная, 31: по книге обычный успех, которого мало, — проверка провалена в обоих случаях
    [InlineData(2, 60, 31, 30, SuccessLevel.Failure, SuccessLevel.RegularSuccess, false)]
    public void ResolveHazard_HarderDifficulty_LevelFromCutThreshold(int difficulty, int skill, int roll,
        int threshold, SuccessLevel v1Level, SuccessLevel bookLevel, bool success)
    {
        var (chase, pursuer) = Scene();
        chase.PutHazard(2, difficulty);

        var result = chase.ResolveHazard(pursuer.Id, 2, "Ловкость", skill, roll, 0, 1, 1);

        Assert.Equal(threshold, result.SkillValue);
        Assert.Equal(v1Level, result.SuccessLevel);
        Assert.Contains(CombatService.GetSuccessLevelText(v1Level), result.Summary);
        Assert.Equal(success, result.IsSuccess);
        // общая функция порогов: уровень от полного навыка, сложность — отдельно
        Assert.Equal(bookLevel, CombatService.CalculateSuccessLevel(roll, skill, Required(difficulty)));
    }

    private static SuccessLevel Required(int difficulty) => difficulty switch
    {
        2 => SuccessLevel.HardSuccess,
        3 => SuccessLevel.ExtremeSuccess,
        _ => SuccessLevel.RegularSuccess
    };

    [Fact]
    [Trait("page", "133")]
    [Trait("page", "145")]
    public void ResolveHazard_VehicleFailure_BuildLossOnlyWhenKeeperEnteredIt()
    {
        var driver = Runner("Водитель", ChaseRole.Pursuer).InVehicle(5);
        var (chase, _) = Scene(driver);
        chase.PutHazard(2);

        var withLoss = chase.ResolveHazard(driver.Id, 2, "Вождение", 50, 80, 0, null, 1, buildLossRoll: 2);
        var withoutLoss = chase.ResolveHazard(driver.Id, 2, "Вождение", 50, 80, 0, null, 1);

        Assert.Equal(2, withLoss.TargetBuildLoss);
        Assert.Null(withoutLoss.TargetBuildLoss);
    }

    [Fact]
    [Trait("page", "133")]
    public void ResolveHazard_OnFoot_BuildLossIgnored()
    {
        var (chase, pursuer) = Scene();
        chase.PutHazard(2);

        var result = chase.ResolveHazard(pursuer.Id, 2, "Ловкость", 50, 80, 0, null, 1, buildLossRoll: 2);

        Assert.Null(result.TargetBuildLoss);
    }

    [Fact]
    [Trait("page", "133")]
    public void ResolveHazard_NoSuchLocation_RegularDifficulty_ClampedToTrack()
    {
        var (chase, pursuer) = Scene();

        var result = chase.ResolveHazard(pursuer.Id, 99, "Ловкость", 50, 50, 0, null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.SkillValue);
        Assert.Equal(6, result.LocationAfter);
        Assert.StartsWith("Помеха", result.SkillName);
    }

    // ── Преграда (стр. 134) ──────────────────────────────────────────

    [Theory]
    [Trait("page", "134")]
    [InlineData(1, 50, true, 50, 2)]
    [InlineData(1, 51, false, 50, 1)]
    [InlineData(2, 25, true, 25, 2)]
    [InlineData(2, 26, false, 25, 1)]
    [InlineData(3, 10, true, 10, 2)]
    public void ResolveBarrier_SuccessEnters_FailureStays(int difficulty, int roll, bool success, int threshold,
        int location)
    {
        var (chase, pursuer) = Scene();
        chase.PutBarrier(2, difficulty: difficulty);

        var result = chase.ResolveBarrier(pursuer.Id, 2, "Лазание", 50, roll);

        Assert.Equal(success, result.IsSuccess);
        Assert.Equal(threshold, result.SkillValue);
        Assert.Equal(location, result.LocationAfter);
        Assert.Equal(1, result.ActorMovementActionsSpent);
        Assert.Equal("Забор (Лазание)", result.SkillName);
    }

    [Fact]
    [Trait("page", "134")]
    public void ResolveBarrier_NoRoll_RollsPlainD100()
    {
        var (chase, pursuer) = Scene();
        chase.PutBarrier(2);

        using var dice = ScriptedRandom.Use(42);
        var result = chase.ResolveBarrier(pursuer.Id, 2, "Лазание", 50, null);

        Assert.Equal(42, result.Roll);
    }

    // ── Разрушение преграды (стр. 135–136) ───────────────────────────

    [Fact]
    [Trait("page", "135")]
    public void AttemptDestroyBarrier_OnFoot_DamagesWithoutBreaking()
    {
        var (chase, pursuer) = Scene();
        chase.PutBarrier(2, hitPoints: 10);

        var result = chase.AttemptDestroyBarrier(pursuer.Id, 2, 4);

        Assert.False(result.IsSuccess);
        Assert.Equal((2, 4, 6), (result.BarrierLocation, result.BarrierDamageDealt, result.BarrierHpAfter));
        Assert.Equal(1, result.ActorMovementActionsSpent);
        Assert.Equal((1, 1), (result.LocationBefore, result.LocationAfter));
        Assert.Null(result.ActorBuildLoss);
    }

    [Fact]
    [Trait("page", "135")]
    public void AttemptDestroyBarrier_OnFootNoRoll_RollsOneD3()
    {
        var (chase, pursuer) = Scene();
        chase.PutBarrier(2, hitPoints: 10);

        using var dice = ScriptedRandom.Use(3);
        var result = chase.AttemptDestroyBarrier(pursuer.Id, 2, null);

        Assert.Equal(3, result.BarrierDamageDealt);
    }

    [Fact]
    [Trait("page", "136")]
    public void ApplyResult_BarrierBroken_BecomesDebrisHazard()
    {
        var (chase, pursuer) = Scene();
        chase.PutBarrier(2, hitPoints: 10);

        var result = chase.AttemptDestroyBarrier(pursuer.Id, 2, 15);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.BarrierHpAfter);

        chase.ApplyResult(result);

        var location = chase.GetLocation(2)!;
        Assert.False(location.HasBarrier);
        Assert.True(location.IsBarrierDestroyed);
        Assert.True(location.HasHazard);
        Assert.Equal("Обломки: Забор", location.HazardName);
        Assert.Equal((1, "1D3"), (location.HazardDifficulty, location.HazardDamageFormula));
        // Пробивший преграду остаётся на месте
        Assert.Equal(1, pursuer.CurrentLocation);
    }

    [Theory]
    [Trait("page", "135")]
    // пробил: отдача — половина ПЗ преграды
    [InlineData(50, 60, true, 2)]
    // не пробил: отдача — половина своего урона
    [InlineData(100, 30, false, 1)]
    [InlineData(100, 10, false, null)]
    public void AttemptDestroyBarrier_Vehicle_Recoil(int barrierHp, int damage, bool destroyed, int? buildLoss)
    {
        var driver = Runner("Водитель", ChaseRole.Pursuer).InVehicle(5);
        var (chase, _) = Scene(driver);
        chase.PutBarrier(2, hitPoints: barrierHp);

        var result = chase.AttemptDestroyBarrier(driver.Id, 2, damage);

        Assert.Equal(destroyed, result.IsSuccess);
        Assert.Equal(buildLoss, (int?)result.ActorBuildLoss);

        chase.ApplyResult(result);
        Assert.Equal(5.0 - (buildLoss ?? 0), driver.VehicleCurrentBuild);
    }

    /// <summary>
    ///     F-P06: транспорт бьёт преграду 1d10 за каждый пункт Комплекции, но берёт Комплекцию
    ///     водителя (<c>BuildValue</c>), а не машины. Таран в том же файле берёт <c>EffectiveBuild</c>.
    /// </summary>
    [Fact]
    [Trait("page", "135")]
    [Trait("finding", "F-P06")]
    public void AttemptDestroyBarrier_VehicleNoRoll_RollsDriverBuildNotVehicleBuild()
    {
        var driver = Runner("Водитель", ChaseRole.Pursuer, build: 1).InVehicle(5);
        var (chase, _) = Scene(driver);
        chase.PutBarrier(2, hitPoints: 50);

        // Одна кость d10 — по Комплекции водителя; у машины их было бы пять
        using var dice = ScriptedRandom.Use(7);
        var result = chase.AttemptDestroyBarrier(driver.Id, 2, null);

        Assert.Equal(7, result.BarrierDamageDealt);
        Assert.Equal(0, dice.Remaining);
        Assert.Equal(5, driver.EffectiveBuild);
    }

    [Fact]
    [Trait("page", "135")]
    [Trait("finding", "F-P06")]
    public void AttemptDestroyBarrier_VehicleWithoutDriverBuild_RollsOneDie()
    {
        var driver = Runner("Водитель", ChaseRole.Pursuer, build: 0).InVehicle(9);
        var (chase, _) = Scene(driver);
        chase.PutBarrier(2, hitPoints: 50);

        using var dice = ScriptedRandom.Use(10);
        var result = chase.AttemptDestroyBarrier(driver.Id, 2, null);

        Assert.Equal(10, result.BarrierDamageDealt);
    }
}
