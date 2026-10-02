using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Tests.Infrastructure;

namespace CampaignManager.Core.Tests.Parsers;

/// <summary>
/// Предел привыкания — максимум провальной части записи «успех/провал». Перенесено из T0.2 без правки
/// ожиданий, кроме находки F-P11: бросок той же записи теперь тоже понимает «д».
/// </summary>
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
    /// F-P11 исправлена: в v1 из «1/1д6» предел привыкания был 6, а потеря при провале — 0 (бросок не
    /// понимал «д»). Теперь обе стороны читают запись одним разбором.
    /// </summary>
    [Theory]
    [Trait("finding", "F-P11")]
    [InlineData("1/1д6", 6, 4)]
    [InlineData("0/1Д10", 10, 7)]
    public void CyrillicDice_LimitAndRollAgree(string formula, int max, int face)
    {
        var parsed = SanityLossFormula.Parse(formula);

        Assert.Equal(max, SanityLossFormula.MaxLoss(formula));
        Assert.Equal(max, parsed.OnFailure.Max);
        Assert.Equal(face, parsed.OnFailure.Roll(ScriptedDice.Of(face)));
    }

    [Fact]
    public void Parse_SuccessAndFailureParts()
    {
        var parsed = SanityLossFormula.Parse("1/1d6");

        Assert.Equal(1, parsed.OnSuccess.Max);
        Assert.Equal(6, parsed.OnFailure.Max);
        Assert.Equal(0, SanityLossFormula.Parse("1d8").OnSuccess.Max);
    }
}
