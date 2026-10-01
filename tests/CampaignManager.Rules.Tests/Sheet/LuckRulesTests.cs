using CampaignManager.Web.Components.Features.Characters.Services;
using CampaignManager.Web.Components.Features.Combat.Model;

namespace CampaignManager.Rules.Tests.Sheet;

/// <summary>Необязательное правило «Пункты Удачи».</summary>
[Trait("page", "97")]
public sealed class LuckRulesTests
{
    [Theory]
    [InlineData(1, 50, SuccessLevel.CriticalSuccess)]
    [InlineData(10, 50, SuccessLevel.ExtremeSuccess)]
    [InlineData(25, 50, SuccessLevel.HardSuccess)]
    [InlineData(50, 50, SuccessLevel.RegularSuccess)]
    [InlineData(51, 50, SuccessLevel.Failure)]
    [InlineData(100, 50, SuccessLevel.Fumble)]
    public void LevelOf_DelegatesToCombatThresholds(int roll, int target, SuccessLevel expected) =>
        Assert.Equal(expected, LuckRules.LevelOf(roll, target));

    [Theory]
    [InlineData(0, 50, false)]
    [InlineData(101, 50, false)]
    [InlineData(30, 0, false)]
    [InlineData(1, 50, false)] // критический успех
    [InlineData(100, 50, false)] // крах
    [InlineData(97, 40, false)] // 96+ при навыке ниже 50 — крах
    [InlineData(97, 60, true)] // при навыке 50+ это обычный провал
    [InlineData(60, 50, true)]
    [InlineData(30, 50, true)]
    public void CanSpendOn_ExcludesCriticalFumbleAndNonRolls(int roll, int target, bool expected) =>
        Assert.Equal(expected, LuckRules.CanSpendOn(roll, target));

    [Fact]
    public void Options_Failure_ListsAllLevels_WithAffordability()
    {
        var options = LuckRules.Options(60, 50, 20);

        Assert.Equal(new LuckRules.SpendOption[]
        {
            new LuckRules.SpendOption(SuccessLevel.RegularSuccess, 10, 50, true),
            new LuckRules.SpendOption(SuccessLevel.HardSuccess, 35, 25, false),
            new LuckRules.SpendOption(SuccessLevel.ExtremeSuccess, 50, 10, false)
        }, options);
    }

    [Fact]
    public void Options_RegularSuccess_OffersOnlyHigherLevels()
    {
        var options = LuckRules.Options(30, 50, 99);

        Assert.Equal(new LuckRules.SpendOption[]
        {
            new LuckRules.SpendOption(SuccessLevel.HardSuccess, 5, 25, true),
            new LuckRules.SpendOption(SuccessLevel.ExtremeSuccess, 20, 10, true)
        }, options);
    }

    [Fact]
    public void Options_CostEqualToLuck_IsAffordable()
    {
        var option = Assert.Single(LuckRules.Options(20, 50, 10));

        Assert.Equal(SuccessLevel.ExtremeSuccess, option.Level);
        Assert.Equal(10, option.Cost);
        Assert.True(option.Affordable);
        Assert.False(LuckRules.Options(20, 50, 9)[0].Affordable);
    }

    [Theory]
    [InlineData(8, 50)] // уже чрезвычайный
    [InlineData(1, 50)]
    [InlineData(100, 50)]
    [InlineData(97, 30)]
    public void Options_NothingToBuy_Empty(int roll, int target) =>
        Assert.Empty(LuckRules.Options(roll, target, 99));

    [Fact]
    public void Options_TargetBelowFive_SkipsExtremeLevel()
    {
        var options = LuckRules.Options(50, 4, 99);

        Assert.Equal(new LuckRules.SpendOption[]
        {
            new LuckRules.SpendOption(SuccessLevel.RegularSuccess, 46, 4, true),
            new LuckRules.SpendOption(SuccessLevel.HardSuccess, 48, 2, true)
        }, options);
    }

    /// <summary>
    ///     При навыке 5–9 пятая часть — 1: вариант «чрезвычайный успех» выкупает бросок до 01,
    ///     а 01 по тем же порогам — критический успех, который купить нельзя.
    /// </summary>
    [Fact]
    [Trait("finding", "F-S03")]
    public void Options_SmallTarget_ExtremeOptionLandsOnCriticalRoll()
    {
        var extreme = LuckRules.Options(30, 5, 99).Single(o => o.Level == SuccessLevel.ExtremeSuccess);

        Assert.Equal(1, extreme.ResultingRoll);
        Assert.Equal(29, extreme.Cost);
        Assert.Equal(SuccessLevel.CriticalSuccess, LuckRules.LevelOf(extreme.ResultingRoll, 5));
    }
}
