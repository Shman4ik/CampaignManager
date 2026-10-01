using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Chase.Model;
using CampaignManager.Web.Components.Features.Chase.Services;
using CampaignManager.Web.Components.Features.Combat.Model;
using static CampaignManager.Rules.Tests.Chase.ChaseScene;

namespace CampaignManager.Rules.Tests.Chase;

/// <summary>Проверка скорости, самая медленная СКО и действия перемещения.</summary>
public sealed class SpeedAndActionsTests
{
    // ── Проверка скорости (стр. 130) ─────────────────────────────────

    [Theory]
    [Trait("page", "130")]
    [InlineData(1, SuccessLevel.CriticalSuccess, 1)]
    [InlineData(10, SuccessLevel.ExtremeSuccess, 1)]
    [InlineData(11, SuccessLevel.HardSuccess, 0)]
    [InlineData(25, SuccessLevel.HardSuccess, 0)]
    [InlineData(50, SuccessLevel.RegularSuccess, 0)]
    [InlineData(51, SuccessLevel.Failure, -1)]
    [InlineData(100, SuccessLevel.Fumble, -1)]
    public void ResolveSpeedCheck_WrittenRoll_SetsMovModifierByLevel(int roll, SuccessLevel level, int modifier)
    {
        var runner = Runner("Артур", ChaseRole.Prey, con: 50);
        var chase = Track(6, runner, Runner("Вампир", ChaseRole.Pursuer));

        var result = chase.ResolveSpeedCheck(runner.Id, roll);

        Assert.Equal(level, result.SuccessLevel);
        Assert.Equal(modifier, runner.MovModifier);
        Assert.Equal(8 + modifier, runner.AdjustedMov);
        Assert.True(runner.SpeedCheckCompleted);
        // Проверка скорости применяется сразу, мимо ApplyResult
        Assert.True(result.IsApplied);
        Assert.Equal(0, result.Round);
        Assert.Single(chase.ChaseLog);
        Assert.Equal(("ВЫН", 50), (result.SkillName, result.SkillValue));
    }

    [Fact]
    [Trait("page", "130")]
    public void ResolveSpeedCheck_NoRoll_RollsD100()
    {
        var runner = Runner("Артур", ChaseRole.Prey, con: 50);
        var chase = Track(6, runner, Runner("Вампир", ChaseRole.Pursuer));

        using var dice = ScriptedRandom.Use(7);
        var result = chase.ResolveSpeedCheck(runner.Id, null);

        Assert.Equal(7, result.Roll);
        Assert.Equal(1, runner.MovModifier);
        Assert.Equal(0, dice.Remaining);
    }

    [Theory]
    [Trait("page", "130")]
    [Trait("page", "142")]
    [InlineData(false, 0, null, 50, "ВЫН", 60)]
    [InlineData(true, 40, "Пилотирование", 50, "Пилотирование", 40)]
    // Навыка вождения нет — половина ЛВК
    [InlineData(true, 0, null, 50, "Вождение", 25)]
    // …но не меньше единицы
    [InlineData(true, 0, "", 1, "Вождение", 1)]
    public void GetSpeedCheckSkill_PicksConOrDrivingOrHalfDex(bool inVehicle, int driving, string? skillName,
        int dex, string expectedName, int expectedValue)
    {
        var p = Runner("Артур", ChaseRole.Prey, dex: dex, con: 60);
        p.IsInVehicle = inVehicle;
        p.DrivingSkill = driving;
        p.VehicleSkillName = skillName;

        Assert.Equal((expectedName, expectedValue), ChaseService.GetSpeedCheckSkill(p));
    }

    [Theory]
    [Trait("page", "142")]
    [InlineData(SkillAptitude.Capable, 50)]
    [InlineData(SkillAptitude.Uncertain, 25)]
    [InlineData(SkillAptitude.Unlikely, 10)]
    public void SubstituteSkillFromDexterity_FullHalfOrFifth(SkillAptitude aptitude, int expected) =>
        Assert.Equal(expected, ChaseService.SubstituteSkillFromDexterity(50, aptitude));

    [Fact]
    [Trait("page", "139")]
    public void BeginSpeedChecks_PassengerIsAlreadyDone()
    {
        var driver = Runner("Водитель", ChaseRole.Prey);
        var passenger = Runner("Пассажир", ChaseRole.Prey);
        var chase = Track(6, driver, passenger, Runner("Вампир", ChaseRole.Pursuer));
        chase.SetPassenger(passenger.Id, true, driver.Id);

        chase.BeginSpeedChecks();

        Assert.Equal(ChasePhase.SpeedCheck, chase.Phase);
        Assert.True(passenger.SpeedCheckCompleted);
        Assert.False(driver.SpeedCheckCompleted);
    }

    [Theory]
    [Trait("page", "130")]
    [InlineData(1, 6)]
    [InlineData(2, 2)]
    public void BeginSpeedChecks_NeedsTwoParticipantsAndThreeLocations(int participants, int locations)
    {
        var chase = Track(locations, [.. Enumerable.Range(0, participants).Select(i => Runner($"#{i}", ChaseRole.Prey))]);

        chase.BeginSpeedChecks();

        Assert.Equal(ChasePhase.Setup, chase.Phase);
    }

    // ── Начнётся ли погоня (стр. 130) ────────────────────────────────

    [Theory]
    [Trait("page", "130")]
    [InlineData(9, 0, 8, false)]
    [InlineData(8, 0, 8, true)]
    [InlineData(8, 1, 8, false)]
    [InlineData(8, -1, 8, true)]
    public void EvaluateChaseStart_FasterPreyEscapes(int preyMov, int preyModifier, int pursuerMov, bool happens)
    {
        var prey = Runner("Артур", ChaseRole.Prey, mov: preyMov);
        prey.MovModifier = preyModifier;
        var chase = Track(6, prey, Runner("Вампир", ChaseRole.Pursuer, mov: pursuerMov));

        Assert.Equal(happens, chase.EvaluateChaseStart().ChaseHappens);
    }

    [Fact]
    [Trait("page", "130")]
    public void EvaluateChaseStart_IgnoresInactivePrey()
    {
        var gone = Runner("Сбежавший", ChaseRole.Prey, mov: 12);
        gone.HasEscaped = true;
        var chase = Track(6, gone, Runner("Артур", ChaseRole.Prey, mov: 7), Runner("Вампир", ChaseRole.Pursuer, mov: 8));

        Assert.True(chase.EvaluateChaseStart().ChaseHappens);
    }

    [Fact]
    [Trait("page", "130")]
    public void EvaluateChaseStart_NoPursuers_PreyEscapes() =>
        Assert.False(Track(6, Runner("Артур", ChaseRole.Prey)).EvaluateChaseStart().ChaseHappens);

    // ── Скорость погони (стр. 140) ───────────────────────────────────

    [Fact]
    [Trait("page", "140")]
    public void RecalculateChaseSpeeds_ActionsAreOnePlusDifferenceToSlowest()
    {
        var prey = Runner("Артур", ChaseRole.Prey, mov: 8);
        var pursuer = Runner("Вампир", ChaseRole.Pursuer, mov: 10);
        var chase = Track(6, prey, pursuer);

        chase.RecalculateChaseSpeeds();

        Assert.Equal(8, chase.MinAdjustedMov);
        Assert.Equal((1, 1), (prey.TotalMovementActions, prey.MovementActionsRemaining));
        Assert.Equal((3, 3), (pursuer.TotalMovementActions, pursuer.MovementActionsRemaining));
    }

    [Fact]
    [Trait("page", "140")]
    public void RecalculateChaseSpeeds_PursuerSlowerThanSlowestPrey_DropsOut()
    {
        var prey = Runner("Артур", ChaseRole.Prey, mov: 8);
        var fast = Runner("Вампир", ChaseRole.Pursuer, mov: 10);
        var slow = Runner("Зомби", ChaseRole.Pursuer, mov: 7);
        var chase = Track(6, prey, fast, slow);

        chase.RecalculateChaseSpeeds();

        Assert.True(slow.IsOutOfChase);
        Assert.False(slow.IsActive);
        Assert.False(fast.IsOutOfChase);
        // Выбывший в самую медленную СКО не входит и действий не получает
        Assert.Equal(8, chase.MinAdjustedMov);
        Assert.Equal(0, slow.TotalMovementActions);
    }

    [Fact]
    [Trait("page", "140")]
    public void RecalculateChaseSpeeds_NoPrey_NobodyDropsOut()
    {
        var a = Runner("Вампир", ChaseRole.Pursuer, mov: 10);
        var b = Runner("Зомби", ChaseRole.Pursuer, mov: 6);
        var chase = Track(6, a, b);

        chase.RecalculateChaseSpeeds();

        Assert.False(b.IsOutOfChase);
        Assert.Equal(6, chase.MinAdjustedMov);
        Assert.Equal(5, a.TotalMovementActions);
    }

    [Fact]
    [Trait("page", "140")]
    [Trait("page", "139")]
    public void RecalculateChaseSpeeds_PassengerIgnoredAndGetsNoActions()
    {
        var driver = Runner("Водитель", ChaseRole.Prey, mov: 8);
        var passenger = Runner("Пассажир", ChaseRole.Prey, mov: 3);
        var pursuer = Runner("Вампир", ChaseRole.Pursuer, mov: 9);
        var chase = Track(6, driver, passenger, pursuer);
        chase.SetPassenger(passenger.Id, true, driver.Id);
        passenger.MovementActionDebt = 2;

        chase.RecalculateChaseSpeeds();

        Assert.Equal(8, chase.MinAdjustedMov);
        Assert.False(pursuer.IsOutOfChase);
        Assert.Equal((0, 0, 0),
            (passenger.TotalMovementActions, passenger.MovementActionsRemaining, passenger.MovementActionDebt));
        // Пассажир ходит в общем порядке ЛВК — он активен
        Assert.True(passenger.IsActive);
    }

    // ── Долг действий (стр. 133) ─────────────────────────────────────

    [Fact]
    [Trait("page", "133")]
    public void CalculateMovementActions_DebtEatsEarnedActions_OverTwoRounds()
    {
        var prey = Runner("Артур", ChaseRole.Prey, mov: 8);
        var pursuer = Runner("Вампир", ChaseRole.Pursuer, mov: 9);
        var chase = Track(6, prey, pursuer);
        chase.RecalculateChaseSpeeds();
        pursuer.MovementActionDebt = 3;

        chase.CalculateMovementActions();
        Assert.Equal((0, 1), (pursuer.TotalMovementActions, pursuer.MovementActionDebt));

        chase.CalculateMovementActions();
        Assert.Equal((1, 0), (pursuer.TotalMovementActions, pursuer.MovementActionDebt));
    }

    [Fact]
    [Trait("page", "133")]
    public void ApplyResult_LostActionsBeyondRemaining_BecomeDebtForNextRound()
    {
        var prey = Runner("Артур", ChaseRole.Prey, mov: 8);
        var pursuer = Runner("Вампир", ChaseRole.Pursuer, mov: 9);
        var chase = Track(6, prey, pursuer);
        chase.PutHazard(2);
        chase.StartChase();
        Assert.Equal(2, pursuer.MovementActionsRemaining);

        // Провал помехи: 1 действие на саму помеху, потом минус 3
        var result = chase.ResolveHazard(pursuer.Id, 2, "Ловкость", 50, 90, 0, null, 3);
        chase.ApplyResult(result);

        Assert.Equal((0, 2), (pursuer.MovementActionsRemaining, pursuer.MovementActionDebt));

        chase.NextRound();
        Assert.Equal((0, 0), (pursuer.TotalMovementActions, pursuer.MovementActionDebt));
    }
}
