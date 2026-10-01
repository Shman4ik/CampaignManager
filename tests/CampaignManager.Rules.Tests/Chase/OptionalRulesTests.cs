using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Chase.Model;
using CampaignManager.Web.Components.Features.Chase.Services;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;
using static CampaignManager.Rules.Tests.Chase.ChaseScene;

namespace CampaignManager.Rules.Tests.Chase;

/// <summary>Часть 5: разгон, штурман, случайные помехи, укрыться и потерять след.</summary>
public sealed class OptionalRulesTests
{
    // ── «Педаль в пол» (стр. 137) ────────────────────────────────────

    [Theory]
    [Trait("page", "137")]
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
    public void GetBoostPenaltyDice_ByLocations_NavigatorTakesOne(int locations, bool navigator, int expected) =>
        Assert.Equal(expected, ChaseService.GetBoostPenaltyDice(locations, navigator));

    [Theory]
    [Trait("page", "137")]
    [InlineData(1, 2, 3)]
    [InlineData(3, 3, 4)]
    [InlineData(9, 5, 6)]
    public void ResolveFloorIt_LocationsClampedTwoToFive(int asked, int travelled, int destination)
    {
        var driver = Runner("Водитель", ChaseRole.Pursuer).InVehicle(5);
        var chase = Track(10, Runner("Артур", ChaseRole.Prey), driver);

        var result = chase.ResolveFloorIt(driver.Id, asked);

        Assert.Equal((1, destination), (result.LocationBefore, result.LocationAfter));
        Assert.Equal(1, result.ActorMovementActionsSpent);
        Assert.Equal(ChaseService.GetBoostPenaltyDice(travelled, false), result.PenaltyDice);
        Assert.Equal(ChaseActionType.FloorIt, result.ActionType);
    }

    [Fact]
    [Trait("page", "137")]
    public void ResolveFloorIt_StopsAtTrackEnd()
    {
        var driver = Runner("Водитель", ChaseRole.Pursuer).InVehicle(5);
        var chase = Track(4, Runner("Артур", ChaseRole.Prey), driver);

        Assert.Equal(4, chase.ResolveFloorIt(driver.Id, 5).LocationAfter);
    }

    [Fact]
    [Trait("page", "139")]
    public void ResolveNavigatorAssist_Success_AppliedAtOnce_ThenFloorItSpendsIt()
    {
        var driver = Runner("Водитель", ChaseRole.Pursuer).InVehicle(5);
        var navigator = Runner("Штурман", ChaseRole.Pursuer);
        var chase = Track(10, Runner("Артур", ChaseRole.Prey), driver, navigator);
        chase.SetPassenger(navigator.Id, true, driver.Id);

        var assist = chase.ResolveNavigatorAssist(navigator.Id, driver.Id, "Ориентирование", 50, 40);

        Assert.True(assist.IsApplied);
        Assert.True(driver.HasNavigatorAssist);

        var boost = chase.ResolveFloorIt(driver.Id, 2);
        Assert.Equal(0, boost.PenaltyDice);
        Assert.Contains("Штурман снял", boost.Summary);
        // Resolve* ещё ничего не тратит
        Assert.True(driver.HasNavigatorAssist);

        chase.ApplyResult(boost);
        Assert.False(driver.HasNavigatorAssist);
        Assert.Equal(3, driver.CurrentLocation);
    }

    [Fact]
    [Trait("page", "139")]
    public void ResolveNavigatorAssist_Failure_NoAssist()
    {
        var driver = Runner("Водитель", ChaseRole.Pursuer).InVehicle(5);
        var navigator = Runner("Штурман", ChaseRole.Pursuer);
        var chase = Track(10, Runner("Артур", ChaseRole.Prey), driver, navigator);

        chase.ResolveNavigatorAssist(navigator.Id, driver.Id, "Ориентирование", 50, 51);

        Assert.False(driver.HasNavigatorAssist);
    }

    // ── Случайные помехи (стр. 137) ──────────────────────────────────

    [Theory]
    [Trait("page", "137")]
    [InlineData(1, "чисто")]
    [InlineData(59, "чисто")]
    [InlineData(60, "обычная помеха")]
    [InlineData(84, "обычная помеха")]
    [InlineData(85, "трудная помеха")]
    [InlineData(95, "трудная помеха")]
    [InlineData(96, "чрезвычайная помеха")]
    [InlineData(100, "чрезвычайная помеха")]
    public void RollRandomHazard_Boundaries_60_85_96(int roll, string text)
    {
        var chase = Track(6, Runner("Артур", ChaseRole.Prey));

        var result = chase.RollRandomHazard(roll, 0, 0, 4);

        Assert.Equal(roll, result.Roll);
        Assert.Equal(roll < 60, result.IsSuccess);
        Assert.Contains(text, result.Summary);
        Assert.Equal(4, result.ObstacleLocation);
        Assert.True(result.IsApplied);
    }

    [Theory]
    [Trait("page", "137")]
    // тяжёлая дорога: штрафная кость, 00+0 = 100
    [InlineData(0, 1, new[] { 0, 0, 9 }, 100)]
    // автострада: бонусная, меньшее из 95 и 05
    [InlineData(1, 0, new[] { 5, 9, 0 }, 5)]
    public void RollRandomHazard_NoRoll_RollsWithRoadDice(int bonus, int penalty, int[] dice, int expected)
    {
        var chase = Track(6, Runner("Артур", ChaseRole.Prey));

        using var scripted = ScriptedRandom.Use(dice);
        var result = chase.RollRandomHazard(null, bonus, penalty, 4);

        Assert.Equal(expected, result.Roll);
        Assert.Equal((bonus, penalty), (result.BonusDiceUsed, result.PenaltyDice));
    }

    [Fact]
    [Trait("page", "137")]
    public void RollRandomHazard_WrittenRoll_IgnoresRoadDice()
    {
        var chase = Track(6, Runner("Артур", ChaseRole.Prey));

        using var scripted = ScriptedRandom.Use();
        var result = chase.RollRandomHazard(70, 0, 2, 4);

        Assert.Equal(70, result.Roll);
    }

    // ── Спрятаться (стр. 139) ────────────────────────────────────────

    [Theory]
    [Trait("page", "139")]
    [InlineData(1, 60, true, 60)]
    [InlineData(1, 61, false, 60)]
    [InlineData(2, 30, true, 30)]
    [InlineData(3, 13, false, 12)]
    public void ResolveHide_OnFoot_StealthAgainstDifficulty(int difficulty, int roll, bool hidden, int threshold)
    {
        var prey = Runner("Артур", ChaseRole.Prey);
        var chase = Track(6, prey, Runner("Вампир", ChaseRole.Pursuer));

        var result = chase.ResolveHide(prey.Id, 60, null, difficulty, 0, roll);

        Assert.Equal(hidden, result.IsSuccess);
        Assert.Equal(hidden, result.RemovesActorFromChase);
        Assert.Equal(threshold, result.SkillValue);
        Assert.Equal(1, result.ActorMovementActionsSpent);
    }

    [Theory]
    [Trait("page", "139")]
    [InlineData(40, true, "укрылся")]
    [InlineData(50, false, "Вождения не хватило")]
    [InlineData(61, false, "укрытие не найдено")]
    public void ResolveHide_InVehicle_RollMustPassStealthAndDriving(int roll, bool hidden, string text)
    {
        var prey = Runner("Артур", ChaseRole.Prey).InVehicle(5);
        var chase = Track(6, prey, Runner("Вампир", ChaseRole.Pursuer));

        var result = chase.ResolveHide(prey.Id, 60, 40, 1, 0, roll);

        Assert.Equal(hidden, result.IsSuccess);
        Assert.Contains(text, result.Summary);
        Assert.Equal("Скрытность + Вождение", result.SkillName);
    }

    [Fact]
    [Trait("page", "139")]
    public void ResolveHide_NoRoll_BonusDiceGoIntoRoll()
    {
        var prey = Runner("Артур", ChaseRole.Prey);
        var chase = Track(6, prey, Runner("Вампир", ChaseRole.Pursuer));

        using var dice = ScriptedRandom.Use(5, 9, 2);
        var result = chase.ResolveHide(prey.Id, 60, null, 1, 1, null);

        Assert.Equal(25, result.Roll);
        Assert.True(result.IsSuccess);
    }

    /// <summary>
    ///     F-P08: Вождение в совместной проверке сравнивается инлайном <c>roll &lt;= порог</c>, а не
    ///     через общую функцию. Расходится на 01: по общей функции это всегда критический успех,
    ///     а при пороге Вождения 0 инлайн-сравнение его проваливает.
    /// </summary>
    [Theory]
    [Trait("page", "139")]
    [Trait("finding", "F-P08")]
    [InlineData(0, 1)]
    [InlineData(4, 3)]
    public void ResolveHide_InVehicle_CriticalFailsDrivingWithZeroThreshold(int driving, int difficulty)
    {
        var prey = Runner("Артур", ChaseRole.Prey).InVehicle(5);
        var chase = Track(6, prey, Runner("Вампир", ChaseRole.Pursuer));

        var result = chase.ResolveHide(prey.Id, 60, driving, difficulty, 0, 1);

        Assert.False(result.IsSuccess);
        Assert.Equal(SuccessLevel.CriticalSuccess, result.SuccessLevel);
        var drivingThreshold = ChaseService.GetDifficultyThreshold(driving, difficulty);
        Assert.Equal(0, drivingThreshold);
        Assert.Equal(SuccessLevel.CriticalSuccess, CombatService.CalculateSuccessLevel(1, drivingThreshold));
    }

    [Fact]
    [Trait("page", "139")]
    public void ApplyResult_PreyHidden_EscapesAndChaseEnds()
    {
        var prey = Runner("Артур", ChaseRole.Prey);
        var chase = Track(6, prey, Runner("Вампир", ChaseRole.Pursuer));
        chase.StartChase();

        chase.ApplyResult(chase.ResolveHide(prey.Id, 60, null, 1, 0, 10));

        Assert.True(prey.HasEscaped);
        Assert.True(chase.IsChaseOver());
        // фазу закрывает только автоматический побег по трассе и MarkCaught — укрытие её не трогает
        Assert.Equal(ChasePhase.Active, chase.Phase);
    }

    // ── Потерять след (стр. 139) ─────────────────────────────────────

    [Theory]
    [Trait("page", "139")]
    [InlineData(1, 50, true, 50)]
    [InlineData(1, 51, false, 50)]
    [InlineData(3, 10, true, 10)]
    [InlineData(3, 11, false, 10)]
    public void ResolveTrackingCheck_FailureDropsPursuer(int difficulty, int roll, bool found, int threshold)
    {
        var pursuer = Runner("Вампир", ChaseRole.Pursuer);
        var chase = Track(6, Runner("Артур", ChaseRole.Prey), pursuer);

        var result = chase.ResolveTrackingCheck(pursuer.Id, "Чтение следов", 50, difficulty, roll);

        Assert.Equal(found, result.IsSuccess);
        Assert.Equal(!found, result.RemovesActorFromChase);
        Assert.Equal(threshold, result.SkillValue);

        chase.ApplyResult(result);
        Assert.Equal(!found, pursuer.IsOutOfChase);
        Assert.Equal(found, pursuer.IsActive);
    }

    /// <summary>
    ///     F-P01: и здесь уровень от урезанного порога — 10 при навыке 50 и трудной сложности
    ///     записан «трудным» вместо «чрезвычайного».
    /// </summary>
    [Fact]
    [Trait("page", "139")]
    [Trait("finding", "F-P01")]
    public void ResolveTrackingCheck_LevelFromCutThreshold()
    {
        var pursuer = Runner("Вампир", ChaseRole.Pursuer);
        var chase = Track(6, Runner("Артур", ChaseRole.Prey), pursuer);

        var result = chase.ResolveTrackingCheck(pursuer.Id, "Чтение следов", 50, 2, 10);

        Assert.Equal(SuccessLevel.HardSuccess, result.SuccessLevel);
        Assert.Equal(SuccessLevel.ExtremeSuccess,
            CombatService.CalculateSuccessLevel(10, 50, SuccessLevel.HardSuccess));
    }
}
