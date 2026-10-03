using CampaignManager.Core.Dice;
using CampaignManager.Core.Tests.Infrastructure;

namespace CampaignManager.Core.Tests.Dice;

/// <summary>
/// d100 с бонусными и штрафными костями (стр. 89). Сырые кости в <see cref="ScriptedDice"/>: сначала
/// единицы, потом каждая кость десятков. Перенесено из T0.2 без правки ожиданий.
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
    public void Roll_NoExtraDice_TensPlusUnits(int units, int tens, int expected)
    {
        var dice = ScriptedDice.Of(units, tens);

        var roll = D100.Roll(dice);

        Assert.Equal(expected, roll.Result);
        Assert.Equal(units, roll.Units);
        Assert.Equal([expected], roll.Candidates);
        Assert.False(roll.HasExtraDice);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Roll_BonusDie_TakesLowestResult_NotLowestTens()
    {
        // Десятки 0 и 3 при единицах 0: варианты 100 и 30 — берётся 30, хотя кость десятков 0 меньше
        var roll = D100.Roll(ScriptedDice.Of(0, 0, 3), bonusDice: 1);

        Assert.Equal(30, roll.Result);
        Assert.Equal([100, 30], roll.Candidates);
        Assert.Equal(1, roll.BonusDice);
        Assert.Equal(0, roll.PenaltyDice);
        Assert.True(roll.HasExtraDice);
    }

    [Fact]
    public void Roll_PenaltyDie_TakesHighestResult_ZeroZeroIsHundred()
    {
        var roll = D100.Roll(ScriptedDice.Of(0, 3, 0), penaltyDice: 1);

        Assert.Equal(100, roll.Result);
        Assert.Equal([30, 100], roll.Candidates);
        Assert.Equal(0, roll.BonusDice);
        Assert.Equal(1, roll.PenaltyDice);
    }

    [Fact]
    public void Roll_TwoBonusDice_ThreeTensDice_Lowest()
    {
        var dice = ScriptedDice.Of(5, 9, 2, 0);

        var roll = D100.Roll(dice, bonusDice: 2);

        Assert.Equal(5, roll.Result);
        Assert.Equal([95, 25, 5], roll.Candidates);
        Assert.Equal(2, roll.BonusDice);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Roll_TwoPenaltyDice_ThreeTensDice_Highest()
    {
        var roll = D100.Roll(ScriptedDice.Of(1, 0, 2, 9), penaltyDice: 2);

        Assert.Equal(91, roll.Result);
        Assert.Equal([1, 21, 91], roll.Candidates);
        Assert.Equal(2, roll.PenaltyDice);
    }

    [Fact]
    public void Roll_BonusAndPenaltyCancel_OneTensDie()
    {
        // Одна бонусная гасит одну штрафную: бросается одна кость десятков
        var dice = ScriptedDice.Of(7, 4);

        var roll = D100.Roll(dice, 1, 1);

        Assert.Equal(47, roll.Result);
        Assert.Equal(0, roll.BonusDice);
        Assert.Equal(0, roll.PenaltyDice);
        Assert.False(roll.HasExtraDice);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Roll_TwoBonusOnePenalty_NetOneBonus()
    {
        var dice = ScriptedDice.Of(3, 8, 1);

        var roll = D100.Roll(dice, 2, 1);

        Assert.Equal(13, roll.Result);
        Assert.Equal(1, roll.BonusDice);
        Assert.Equal(0, roll.PenaltyDice);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Roll_NegativeCounts_TreatedAsZero()
    {
        var dice = ScriptedDice.Of(2, 6);

        var roll = D100.Roll(dice, -3, -1);

        Assert.Equal(62, roll.Result);
        Assert.False(roll.HasExtraDice);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void Entered_KeepsRollAsSingleCandidate()
    {
        var plain = D100Roll.Entered(47);

        Assert.Equal(47, plain.Result);
        Assert.Equal(7, plain.Units);
        Assert.Equal([47], plain.Candidates);
        Assert.False(plain.HasExtraDice);
    }

    [Fact]
    public void RollOrEntered_EnteredWins_NoDiceRolled()
    {
        var dice = ScriptedDice.Of();

        Assert.Equal(83, D100.RollOrEntered(dice, 83, bonusDice: 2).Result);
        Assert.Equal(47, D100.RollOrEntered(ScriptedDice.Of(7, 4), null).Result);
    }

    [Fact]
    public void RollDetail_DescribesExtraDice()
    {
        var roll = D100.Roll(ScriptedDice.Of(4, 2, 4), bonusDice: 1);

        Assert.Equal(" (бонусная кость: 24, 44; взято 24)", RulesText.RollDetail(roll));
        Assert.Equal(string.Empty, RulesText.RollDetail(D100Roll.Entered(24)));
        Assert.Equal(string.Empty, RulesText.RollDetail(null));
        Assert.Equal("2 штрафные кости", RulesText.DescribeDice(2, isBonus: false));
    }

    /// <summary>Короткая запись листа v1 (<c>Sheet/DiceTests</c>): NdM и d100 через тот же бросок.</summary>
    [Fact]
    public void RollerExtensions_SumFaces_AndPercentileUsesD100()
    {
        var dice = ScriptedDice.Of(2, 5, 6);
        Assert.Equal(13, dice.Roll(3, 6));
        Assert.Equal(0, dice.Remaining);

        var none = ScriptedDice.Of(4);
        Assert.Equal(0, none.Roll(0, 6));
        Assert.Equal(1, none.Remaining);

        Assert.Equal(47, ScriptedDice.Of(7, 4).Percentile());
        Assert.Equal(100, ScriptedDice.Of(0, 0).Percentile());
        Assert.Equal(23, ScriptedDice.Of(3, 8, 2).Percentile(bonusDice: 1));
        Assert.Equal(50, ScriptedDice.Of(0, 0, 5, 9).Percentile(bonusDice: 2)); // 100, 50, 90 — меньший
    }

    [Fact]
    public void SeededRoller_SameSeed_SameRolls()
    {
        var a = new SeededDiceRoller(42);
        var b = new SeededDiceRoller(42);

        Assert.Equal(
            Enumerable.Range(0, 20).Select(_ => a.Die(20)),
            Enumerable.Range(0, 20).Select(_ => b.Die(20)));
    }

    [Fact]
    public void SharedRoller_StaysInRange()
    {
        for (var i = 0; i < 200; i++)
            Assert.InRange(D100.Roll(DiceRoller.Shared, 1, 0).Result, 1, 100);
    }
}
