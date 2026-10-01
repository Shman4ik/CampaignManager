using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Rules.Tests.Dice;

/// <summary>
///     d100 с бонусными и штрафными костями (стр. 89). Сырые кости в <see cref="ScriptedRandom" />:
///     сначала единицы, потом каждая кость десятков.
/// </summary>
[Trait("page", "89")]
public sealed class RollD100Tests
{
    [Theory]
    [InlineData(0, 0, 100)] // «00» + «0» = 100, а не 0
    [InlineData(1, 0, 1)]
    [InlineData(5, 0, 5)]
    [InlineData(0, 3, 30)]
    [InlineData(7, 4, 47)]
    [InlineData(9, 9, 99)]
    public void RollD100_NoExtraDice_TensPlusUnits(int units, int tens, int expected)
    {
        using var dice = ScriptedRandom.Use(units, tens);

        var roll = CombatService.RollD100(0, 0);

        Assert.Equal(expected, roll.Result);
        Assert.Equal(units, roll.Units);
        Assert.Equal([expected], roll.Candidates);
        Assert.False(roll.HasExtraDice);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void RollD100_BonusDie_TakesLowestResult_NotLowestTens()
    {
        // Десятки 0 и 3 при единицах 0: варианты 100 и 30 — берётся 30, хотя кость десятков 0 меньше
        using var _ = ScriptedRandom.Use(0, 0, 3);

        var roll = CombatService.RollD100(1, 0);

        Assert.Equal(30, roll.Result);
        Assert.Equal([100, 30], roll.Candidates);
        Assert.Equal(1, roll.BonusDice);
        Assert.Equal(0, roll.PenaltyDice);
        Assert.True(roll.HasExtraDice);
    }

    [Fact]
    public void RollD100_PenaltyDie_TakesHighestResult_ZeroZeroIsHundred()
    {
        using var _ = ScriptedRandom.Use(0, 3, 0);

        var roll = CombatService.RollD100(0, 1);

        Assert.Equal(100, roll.Result);
        Assert.Equal([30, 100], roll.Candidates);
        Assert.Equal(0, roll.BonusDice);
        Assert.Equal(1, roll.PenaltyDice);
    }

    [Fact]
    public void RollD100_TwoBonusDice_ThreeTensDice_Lowest()
    {
        using var dice = ScriptedRandom.Use(5, 9, 2, 0);

        var roll = CombatService.RollD100(2, 0);

        Assert.Equal(5, roll.Result);
        Assert.Equal([95, 25, 5], roll.Candidates);
        Assert.Equal(2, roll.BonusDice);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void RollD100_TwoPenaltyDice_ThreeTensDice_Highest()
    {
        using var _ = ScriptedRandom.Use(1, 0, 2, 9);

        var roll = CombatService.RollD100(0, 2);

        Assert.Equal(91, roll.Result);
        Assert.Equal([1, 21, 91], roll.Candidates);
        Assert.Equal(2, roll.PenaltyDice);
    }

    [Fact]
    public void RollD100_BonusAndPenaltyCancel_OneTensDie()
    {
        // Одна бонусная гасит одну штрафную: бросается одна кость десятков
        using var dice = ScriptedRandom.Use(7, 4);

        var roll = CombatService.RollD100(1, 1);

        Assert.Equal(47, roll.Result);
        Assert.Equal(0, roll.BonusDice);
        Assert.Equal(0, roll.PenaltyDice);
        Assert.False(roll.HasExtraDice);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void RollD100_TwoBonusOnePenalty_NetOneBonus()
    {
        using var dice = ScriptedRandom.Use(3, 8, 1);

        var roll = CombatService.RollD100(2, 1);

        Assert.Equal(13, roll.Result);
        Assert.Equal(1, roll.BonusDice);
        Assert.Equal(0, roll.PenaltyDice);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void RollD100_NegativeCounts_TreatedAsZero()
    {
        using var dice = ScriptedRandom.Use(2, 6);

        var roll = CombatService.RollD100(-3, -1);

        Assert.Equal(62, roll.Result);
        Assert.False(roll.HasExtraDice);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void RollD100_Plain_IsOneToHundred()
    {
        using var _ = ScriptedRandom.Use(1, 100);

        Assert.Equal(1, CombatService.RollD100());
        Assert.Equal(100, CombatService.RollD100());
    }

    [Fact]
    public void DiceRollResultPlain_KeepsRollAsSingleCandidate()
    {
        var plain = DiceRollResult.Plain(47);

        Assert.Equal(47, plain.Result);
        Assert.Equal(7, plain.Units);
        Assert.Equal([47], plain.Candidates);
        Assert.False(plain.HasExtraDice);
    }

    [Fact]
    public void FormatRollDetail_DescribesExtraDice()
    {
        using var _ = ScriptedRandom.Use(4, 2, 4);
        var roll = CombatService.RollD100(1, 0);

        Assert.Equal(" [кости 24, 44 — бонусная кость]", CombatService.FormatRollDetail(roll));
        Assert.Equal(string.Empty, CombatService.FormatRollDetail(DiceRollResult.Plain(24)));
        Assert.Equal(string.Empty, CombatService.FormatRollDetail(null));
        Assert.Equal("2 штрафные кости", CombatService.DescribeDice(2, isBonus: false));
    }
}
