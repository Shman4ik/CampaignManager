using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Chase.Model;
using CampaignManager.Web.Components.Features.Chase.Services;
using CampaignManager.Web.Components.Features.Combat.Model;
using static CampaignManager.Rules.Tests.Chase.ChaseScene;

namespace CampaignManager.Rules.Tests.Chase;

/// <summary>Таран, стрельба по шинам, водитель с серьёзной раной.</summary>
public sealed class VehicleTests
{
    private static (ChaseService Chase, ChaseParticipant Attacker, ChaseParticipant Target) Cars(
        double attackerBuild = 5, double targetBuild = 5)
    {
        var attacker = Runner("Артур", ChaseRole.Pursuer, hp: 10).InVehicle(attackerBuild);
        var target = Runner("Вампир", ChaseRole.Prey, hp: 10).InVehicle(targetBuild);
        return (Track(6, target, attacker), attacker, target);
    }

    // ── Таран (стр. 136) ─────────────────────────────────────────────

    [Fact]
    [Trait("page", "136")]
    public void ResolveVehicleCollision_Miss_NoDamage()
    {
        var (chase, attacker, target) = Cars();

        var result = chase.ResolveVehicleCollision(attacker.Id, target.Id, "Вождение", 50, 80, 27);

        Assert.False(result.IsSuccess);
        Assert.Null(result.DamageDealt);
        Assert.Null(result.TargetBuildLoss);
        Assert.Null(result.ActorBuildLoss);
        Assert.Equal(1, result.ActorMovementActionsSpent);
        Assert.EndsWith("Мимо!", result.Summary);
    }

    [Fact]
    [Trait("page", "136")]
    public void ResolveVehicleCollision_Hit_FullTensWithCarry_RemainderCarried_RecoilHalf()
    {
        var (chase, attacker, target) = Cars();
        target.VehicleDamageCarry = 5;

        var result = chase.ResolveVehicleCollision(attacker.Id, target.Id, "Вождение", 50, 30, 27);

        Assert.True(result.IsSuccess);
        Assert.Equal(27, result.DamageDealt);
        // 5 + 27 = 32: три полных десятка, два в остаток
        Assert.Equal((3.0, 2), (result.TargetBuildLoss, result.TargetVehicleDamage));
        // отдача 27 / 2 = 13 → одна Комплекция
        Assert.Equal(1.0, result.ActorBuildLoss);
        Assert.Equal(target.Id, result.TargetId);
    }

    [Fact]
    [Trait("page", "136")]
    public void ApplyResult_Collision_TakesBuildFromBothAndKeepsCarry()
    {
        var (chase, attacker, target) = Cars();
        target.VehicleDamageCarry = 5;

        chase.ApplyResult(chase.ResolveVehicleCollision(attacker.Id, target.Id, "Вождение", 50, 30, 27));

        Assert.Equal((2.0, 2), (target.VehicleCurrentBuild, target.VehicleDamageCarry));
        Assert.Equal(4.0, attacker.VehicleCurrentBuild);
        // таран бьёт машину, а не людей
        Assert.Equal(10, target.CurrentHitPoints);
    }

    [Fact]
    [Trait("page", "136")]
    public void ResolveVehicleCollision_RecoilCappedByTargetBuild()
    {
        // велосипед: Комплекция 0,5 — отдача не больше ⌈0,5⌉ = 1
        var (chase, attacker, target) = Cars(targetBuild: 0.5);

        var result = chase.ResolveVehicleCollision(attacker.Id, target.Id, "Вождение", 50, 30, 60);

        Assert.Equal(6.0, result.TargetBuildLoss);
        Assert.Equal(1.0, result.ActorBuildLoss);
    }

    /// <summary>
    ///     Сколько костей d10 бросает таран: <c>Math.Round</c> по Комплекции машины (банковское
    ///     округление: 2,5 → 2, 0,5 → 0, но не меньше одной кости).
    /// </summary>
    [Theory]
    [Trait("page", "136")]
    [InlineData(0.5, 1)]
    [InlineData(1, 1)]
    [InlineData(2.5, 2)]
    [InlineData(3.5, 4)]
    [InlineData(5, 5)]
    public void ResolveVehicleCollision_NoDamageRoll_RollsD10PerEffectiveBuild(double build, int dice)
    {
        var (chase, attacker, target) = Cars(attackerBuild: build);

        using var scripted = ScriptedRandom.Use([.. Enumerable.Repeat(1, dice)]);
        var result = chase.ResolveVehicleCollision(attacker.Id, target.Id, "Вождение", 50, 30, null);

        Assert.Equal(dice, result.DamageDealt);
        Assert.Equal(0, scripted.Remaining);
        Assert.Contains($"({dice}d10)", result.Summary);
    }

    /// <summary>
    ///     F-P07: остаток урона машины переносится, только если удар снял хотя бы одну Комплекцию.
    ///     Два тарана по 6 должны дать 12 — минус одна Комплекция и 2 в остатке; в v1 машина цела.
    /// </summary>
    [Fact]
    [Trait("page", "136")]
    [Trait("finding", "F-P07")]
    public void ApplyResult_CollisionBelowTen_CarryIsLost()
    {
        var (chase, attacker, target) = Cars();

        for (var i = 0; i < 2; i++)
        {
            var result = chase.ResolveVehicleCollision(attacker.Id, target.Id, "Вождение", 50, 30, 6);
            Assert.Equal((0.0, 6), (result.TargetBuildLoss, result.TargetVehicleDamage));
            chase.ApplyResult(result);
        }

        Assert.Equal((5.0, 0), (target.VehicleCurrentBuild, target.VehicleDamageCarry));
    }

    /// <summary>F-P07: отдача атакующему считается без его собственного остатка и остаток не копит.</summary>
    [Fact]
    [Trait("page", "136")]
    [Trait("finding", "F-P07")]
    public void ResolveVehicleCollision_Recoil_IgnoresAttackerCarry()
    {
        var (chase, attacker, target) = Cars();
        attacker.VehicleDamageCarry = 9;

        var result = chase.ResolveVehicleCollision(attacker.Id, target.Id, "Вождение", 50, 30, 18);
        chase.ApplyResult(result);

        // отдача 9 + остаток 9 = 18 — по книге минус одна Комплекция; в v1 — ноль
        Assert.Equal(0.0, result.ActorBuildLoss);
        Assert.Equal((5.0, 9), (attacker.VehicleCurrentBuild, attacker.VehicleDamageCarry));
    }

    // ── Стрельба по шинам (стр. 139) ─────────────────────────────────

    [Theory]
    [Trait("page", "139")]
    [InlineData(5, 2, 1.0)]
    [InlineData(4, 1, 0.0)]
    [InlineData(2, 0, 0.0)]
    public void ResolveTyreShot_Hit_ArmourThree_TwoPointsBurst(int damage, int afterArmour, double buildLoss)
    {
        var (chase, attacker, target) = Cars();

        var result = chase.ResolveTyreShot(attacker.Id, target.Id, "Стрельба", 50, 30, damage);

        Assert.True(result.IsSuccess);
        Assert.Equal(afterArmour, result.DamageDealt);
        Assert.Equal(buildLoss, result.TargetBuildLoss);
        Assert.Equal((0, 1), (result.ActorMovementActionsSpent, result.PenaltyDice));
    }

    [Fact]
    [Trait("page", "139")]
    public void ResolveTyreShot_NoRoll_RollsWithPenaltyDie()
    {
        var (chase, attacker, target) = Cars();

        // единицы 3, десятки 2 и 8 → 23 и 83; штрафная — большее
        using var dice = ScriptedRandom.Use(3, 2, 8);
        var result = chase.ResolveTyreShot(attacker.Id, target.Id, "Стрельба", 50, null, 5);

        Assert.Equal(83, result.Roll);
        Assert.False(result.IsSuccess);
        Assert.Null(result.DamageDealt);
    }

    [Fact]
    [Trait("page", "139")]
    public void ResolveTyreShot_NoDamageRoll_RollsD10_AndApplyTakesOneBuild()
    {
        var (chase, attacker, target) = Cars();

        using var dice = ScriptedRandom.Use(6);
        var result = chase.ResolveTyreShot(attacker.Id, target.Id, "Стрельба", 50, 30, null);
        chase.ApplyResult(result);

        Assert.Equal(3, result.DamageDealt);
        Assert.Equal(4.0, target.VehicleCurrentBuild);
    }

    // ── Водитель с серьёзной раной (стр. 139, таблица VI стр. 145) ────

    [Theory]
    [Trait("page", "139")]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void ResolveDriverControlCheck_HardCheck_HalfSkill(int roll, bool kept)
    {
        var (chase, driver, _) = Cars();

        var result = chase.ResolveDriverControlCheck(driver.Id, "Вождение", 60, roll, false,
            "Серьёзная авария", 4, 3, 2);

        Assert.Equal(kept, result.IsSuccess);
        Assert.Equal(30, result.SkillValue);
        Assert.Equal("Управление (Вождение)", result.SkillName);
        Assert.Equal(0, result.ActorMovementActionsSpent);
        if (kept)
        {
            Assert.Null(result.TargetBuildLoss);
            Assert.Equal(0, result.MovementActionsLost);
        }
        else
        {
            Assert.Equal((4.0, 2), (result.TargetBuildLoss, result.MovementActionsLost));
            Assert.Equal((3, 7), (result.DamageDealt, result.HpAfter));
        }
    }

    /// <summary>F-P01: уровень у водителя — тоже от половины навыка: 10 при навыке 60 записан «трудным».</summary>
    [Fact]
    [Trait("page", "139")]
    [Trait("finding", "F-P01")]
    public void ResolveDriverControlCheck_LevelFromHalfSkill()
    {
        var (chase, driver, _) = Cars();

        var result = chase.ResolveDriverControlCheck(driver.Id, "Вождение", 60, 10, false, "Мелкая авария", 0, 0, null);

        Assert.Equal(SuccessLevel.HardSuccess, result.SuccessLevel);
    }

    [Fact]
    [Trait("page", "139")]
    public void ResolveDriverControlCheck_Unconscious_LosesControlEvenOnCritical()
    {
        var (chase, driver, _) = Cars();

        var result = chase.ResolveDriverControlCheck(driver.Id, "Вождение", 60, 1, true,
            "Мелкая авария", 1, 1, null);

        Assert.False(result.IsSuccess);
        // бросок в результат не пишется
        Assert.Equal(0, result.Roll);
        Assert.Contains("без сознания", result.Summary);
    }

    [Fact]
    [Trait("page", "145")]
    public void ResolveDriverControlCheck_UnknownTier_FallsBackToMediumCrash_RollsItsFormula()
    {
        var (chase, driver, _) = Cars();

        // 1D6 средней аварии, затем 1D3 потерянных действий
        using var dice = ScriptedRandom.Use(5, 1);
        var result = chase.ResolveDriverControlCheck(driver.Id, "Вождение", 60, 90, false, "Нет такой", null, null, null);

        Assert.Equal((5.0, 1), (result.TargetBuildLoss, result.MovementActionsLost));
        Assert.Contains("Средняя авария", result.Summary);
    }

    [Fact]
    [Trait("page", "145")]
    public void ResolveDriverControlCheck_MinorCrash_OneD3MinusOne_CanBeZero()
    {
        var (chase, driver, _) = Cars();

        using var dice = ScriptedRandom.Use(1, 3);
        var result = chase.ResolveDriverControlCheck(driver.Id, "Вождение", 60, 90, false, "Мелкая авария", null, null, null);

        Assert.Equal((0.0, 3), (result.TargetBuildLoss, result.MovementActionsLost));
    }

    [Fact]
    [Trait("page", "139")]
    public void ApplyResult_DriverLostControl_HitsOwnVehicleAndDriver()
    {
        var (chase, driver, _) = Cars();

        chase.ApplyResult(chase.ResolveDriverControlCheck(driver.Id, "Вождение", 60, 90, false,
            "Серьёзная авария", buildLossRoll: 4, damageRoll: 3, lostActionsRoll: 1));

        Assert.Equal(1.0, driver.VehicleCurrentBuild);
        Assert.Equal(7, driver.CurrentHitPoints);
    }
}
