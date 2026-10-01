using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Bestiary.Services;
using CampaignManager.Web.Components.Features.Combat.Services;
using CampaignManager.Web.Model;

namespace CampaignManager.Rules.Tests.Dice;

/// <summary>
///     Разбор формул костей в бою: <see cref="CombatService.RollDiceFormula" />,
///     <see cref="CombatService.MaximizeDiceFormula" />, бонус к урону и структурированная формула.
///     Страницы у самого разбора в коде v1 нет.
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
    public void RollDiceFormula_SumsDiceAndConstants(string formula, int[] faces, int expected)
    {
        using var dice = ScriptedRandom.Use(faces);

        Assert.Equal(expected, CombatService.RollDiceFormula(formula));
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
    public void MaximizeDiceFormula_AllDiceAtMax(string formula, int expected)
    {
        Assert.Equal(expected, CombatService.MaximizeDiceFormula(formula));
    }

    /// <summary>
    ///     Русская «д» и типографский минус не разбираются: «1д6» бросается как 0, хотя
    ///     <c>DamageFormulaParser</c> и <see cref="SanityLossFormula" /> понимают «д».
    /// </summary>
    [Theory]
    [Trait("finding", "F-C01")]
    [InlineData("1д6")]
    [InlineData("1Д6")]
    [InlineData("1D6−1")]
    public void RollDiceFormula_CyrillicDOrUnicodeMinus_ReturnsZero(string formula)
    {
        // Ни одной кости не брошено: ScriptedRandom без чисел упал бы на первом броске
        using var _ = ScriptedRandom.Use();

        Assert.Equal(0, CombatService.RollDiceFormula(formula));
        Assert.Equal(0, CombatService.MaximizeDiceFormula(formula));
    }

    [Fact]
    [Trait("finding", "F-C01")]
    public void SanityLossFormula_SameCyrillicFormula_GivesSix()
    {
        // Предел привыкания из той же строки считается как 6, а потеря — как 0
        Assert.Equal(6, SanityLossFormula.MaxLoss("1д6"));
        Assert.Equal(0, CombatService.MaximizeDiceFormula("1д6"));
    }

    [Theory]
    [Trait("page", "106")]
    [InlineData("0", new int[0], 0)]
    [InlineData("", new int[0], 0)]
    [InlineData(" 0 ", new int[0], 0)]
    [InlineData("-1", new int[0], -1)]
    [InlineData("+1D4", new[] { 3 }, 3)]
    [InlineData("+1D6", new[] { 6 }, 6)]
    [InlineData("+2D6", new[] { 1, 2 }, 3)]
    public void RollDamageBonus_ParsesDamageBonusForms(string damageBonus, int[] faces, int expected)
    {
        using var dice = ScriptedRandom.Use(faces);

        Assert.Equal(expected, CombatService.RollDamageBonus(damageBonus));
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    public void DamageExpression_RollAndMaximize_IncludeNegativeDiceAndFlat()
    {
        var expr = new DamageExpression
        {
            Dice = [new DiceTerm(2, 6), new DiceTerm(1, 4, IsNegative: true)],
            FlatModifier = 2,
            IsParsed = true
        };

        using (var _ = ScriptedRandom.Use(3, 5, 4))
            Assert.Equal(3 + 5 - 4 + 2, CombatService.RollDamageExpression(expr));

        Assert.Equal(12 - 4 + 2, CombatService.MaximizeDamageExpression(expr));
    }
}
