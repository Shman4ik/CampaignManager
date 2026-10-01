using CampaignManager.Core.Dice;
using CampaignManager.Core.Tests.Infrastructure;

namespace CampaignManager.Core.Tests.Dice;

/// <summary>
/// Один разбор формул костей — <see cref="DiceFormula"/>. Перенесено из T0.2
/// (<c>RollDiceFormula</c>/<c>MaximizeDiceFormula</c>/<c>RollDamageBonus</c> v1) без правки ожиданий,
/// кроме находки F-C01: русская «д» и типографский минус теперь разбираются.
/// </summary>
[Trait("page", "?")]
public sealed class DiceFormulaTests
{
    [Theory]
    [InlineData("1D6", new[] { 4 }, 4)]
    [InlineData("1d6", new[] { 4 }, 4)]
    [InlineData("D6", new[] { 6 }, 6)]
    [InlineData("2D6+2", new[] { 3, 5 }, 10)]
    [InlineData("2D6 + 2", new[] { 3, 5 }, 10)]
    [InlineData("1D8+1D6", new[] { 8, 1 }, 9)]
    [InlineData("1D10-2", new[] { 1 }, -1)]
    [InlineData("+1D4", new[] { 2 }, 2)]
    [InlineData("-1D4", new[] { 3 }, -3)]
    [InlineData("3", new int[0], 3)]
    [InlineData("-1", new int[0], -1)]
    [InlineData("0", new int[0], 0)]
    [InlineData("", new int[0], 0)]
    [InlineData("   ", new int[0], 0)]
    [InlineData("1D6+", new[] { 5 }, 5)]
    [InlineData("мусор", new int[0], 0)]
    [InlineData("1D6+БкУ", new[] { 2 }, 2)]
    // Бонус к урону (стр. 106) — та же формула
    [InlineData(" 0 ", new int[0], 0)]
    [InlineData("+1D6", new[] { 6 }, 6)]
    [InlineData("+2D6", new[] { 1, 2 }, 3)]
    public void Roll_SumsDiceAndConstants(string formula, int[] faces, int expected)
    {
        var dice = ScriptedDice.Of(faces);

        Assert.Equal(expected, DiceFormula.Roll(formula, dice));
        Assert.Equal(0, dice.Remaining);
    }

    [Theory]
    [InlineData("1D6", 6)]
    [InlineData("2D6+2", 14)]
    [InlineData("1D8+1D6", 14)]
    [InlineData("1D10-2", 8)]
    [InlineData("+1D4", 4)]
    [InlineData("-1D4", -4)]
    [InlineData("+1D6", 6)]
    [InlineData("-1", -1)]
    [InlineData("0", 0)]
    [InlineData("", 0)]
    [InlineData("мусор", 0)]
    public void Max_AllDiceAtMax(string formula, int expected)
    {
        Assert.Equal(expected, DiceFormula.MaxOf(formula));
    }

    /// <summary>
    /// F-C01 исправлена: в v1 «1д6», «1Д6» и «1D6−1» бросались как 0 (ни одной кости), хотя разборщик
    /// урона и предел привыкания понимали «д». Теперь разбор один и понимает всё.
    /// </summary>
    [Theory]
    [Trait("finding", "F-C01")]
    [InlineData("1д6", new[] { 4 }, 4, 6)]
    [InlineData("1Д6", new[] { 4 }, 4, 6)]
    [InlineData("1D6−1", new[] { 4 }, 3, 5)]
    [InlineData("2д4 – 1", new[] { 1, 3 }, 3, 7)]
    [InlineData("1 d 6", new[] { 2 }, 2, 6)]
    public void CyrillicDAndTypographicMinus_AreDice(string formula, int[] faces, int rolled, int max)
    {
        var dice = ScriptedDice.Of(faces);

        Assert.Equal(rolled, DiceFormula.Roll(formula, dice));
        Assert.Equal(max, DiceFormula.MaxOf(formula));
        Assert.Equal(0, dice.Remaining);
    }

    [Theory]
    [InlineData("1d6", true)]
    [InlineData("2D6 + 1д4 - 2", true)]
    [InlineData("", true)]
    [InlineData("0", true)]
    [InlineData("1D6+БкУ", false)]
    [InlineData("мусор", false)]
    [InlineData("1d", false)]
    public void IsValid_FalseWhenSomethingWasSkipped(string formula, bool expected)
    {
        Assert.Equal(expected, DiceFormula.Parse(formula).IsValid);
    }

    [Fact]
    public void Terms_MinMaxAndRoll_IncludeNegativeDiceAndFlat()
    {
        // Бывший DamageExpression_RollAndMaximize: 2d6 − 1d4 + 2
        var formula = DiceFormula.Parse("2d6-1d4+2");

        Assert.Equal([new DiceTerm(2, 6), new DiceTerm(1, 4, IsNegative: true)], formula.Dice);
        Assert.Equal(2, formula.Constant);
        Assert.Equal(3 + 5 - 4 + 2, formula.Roll(ScriptedDice.Of(3, 5, 4)));
        Assert.Equal(12 - 4 + 2, formula.Max);
        Assert.Equal(2 - 1 + 2, formula.Min); // каждая кость на единице, как Max — каждая на максимуме
    }
}
