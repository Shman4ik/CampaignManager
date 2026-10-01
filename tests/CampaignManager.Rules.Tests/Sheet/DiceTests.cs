using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Characters.Services;

namespace CampaignManager.Rules.Tests.Sheet;

/// <summary>Короткая запись бросков листа идёт через общий генератор боя.</summary>
[Trait("page", "89")]
public sealed class DiceTests
{
    [Fact]
    public void Roll_SumsFaces()
    {
        using var dice = ScriptedRandom.Use(2, 5, 6);

        Assert.Equal(13, Dice.Roll(3, 6));
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Roll_ZeroDice_Zero_NoDraws()
    {
        using var dice = ScriptedRandom.Use(4);

        Assert.Equal(0, Dice.Roll(0, 6));
        Assert.Equal(1, dice.Remaining);
    }

    [Theory]
    [InlineData(new[] { 7, 4 }, 47)]
    [InlineData(new[] { 1, 0 }, 1)]
    [InlineData(new[] { 0, 0 }, 100)] // «00» + «0»
    [InlineData(new[] { 0, 5 }, 50)]
    public void Percentile_UnitsThenTens(int[] values, int expected)
    {
        using var dice = ScriptedRandom.Use(values);

        Assert.Equal(expected, Dice.Percentile());
    }

    [Theory]
    [InlineData(1, new[] { 3, 8, 2 }, 23)]
    [InlineData(2, new[] { 0, 0, 5, 9 }, 50)] // 100, 50, 90 — меньший
    public void Percentile_BonusDice_TakesLowest(int bonusDice, int[] values, int expected)
    {
        using var dice = ScriptedRandom.Use(values);

        Assert.Equal(expected, Dice.Percentile(bonusDice));
        Assert.Equal(0, dice.Remaining);
    }
}
