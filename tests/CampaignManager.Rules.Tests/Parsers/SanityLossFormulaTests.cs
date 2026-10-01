using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Bestiary.Services;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Rules.Tests.Parsers;

/// <summary>Предел привыкания к ужасному — максимум провальной части записи «успех/провал».</summary>
[Trait("page", "167")]
public sealed class SanityLossFormulaTests
{
    [Theory]
    [InlineData("0/1d6", 6)]
    [InlineData("1/1d10+2", 12)]
    [InlineData("1/1D20", 20)]
    [InlineData("1d6/1d20", 20)]
    [InlineData("1/2d10", 20)]
    [InlineData("0/1d4+1d6", 10)]
    [InlineData("1/1d6-1", 5)]
    [InlineData("1/d6", 6)]
    // без «/» — вся запись и есть провальная часть
    [InlineData("2", 2)]
    [InlineData("1d8", 8)]
    // меньше нуля не бывает
    [InlineData("0/1d3-5", 0)]
    [InlineData(" 1 / 1d6 + 1 ", 7)]
    public void MaxLoss_FailurePartMaximum(string formula, int expected) =>
        Assert.Equal(expected, SanityLossFormula.MaxLoss(formula));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("нет")]
    [InlineData("0/")]
    public void MaxLoss_EmptyOrUnreadable_Zero(string? formula) =>
        Assert.Equal(0, SanityLossFormula.MaxLoss(formula));

    /// <summary>
    ///     F-P11: своя копия разбора костей понимает русскую «д», а бросок потери рассудка идёт через
    ///     <c>CombatService.RollDiceFormula</c>, который её не понимает. Из одной записи «1/1д6»
    ///     предел привыкания — 6, а потеря при провале — 0.
    /// </summary>
    [Theory]
    [Trait("finding", "F-P11")]
    [InlineData("1/1д6", "1д6", 6)]
    [InlineData("0/1Д10", "1Д10", 10)]
    public void MaxLoss_CyrillicDice_CountsButRollIsZero(string formula, string failurePart, int max)
    {
        Assert.Equal(max, SanityLossFormula.MaxLoss(formula));

        using var dice = ScriptedRandom.Use();
        Assert.Equal(0, CombatService.RollDiceFormula(failurePart));
    }

    [Fact]
    public void MaxLoss_LatinDice_AgreesWithMaximizedRoll()
    {
        Assert.Equal(6, SanityLossFormula.MaxLoss("1/1d6"));
        Assert.Equal(6, CombatService.MaximizeDiceFormula("1d6"));
    }
}
