using CampaignManager.Core.Characters;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>Выбывание сыщика (гл. 10): погиб или Рассудок 0; эпилог — абзацем в хронику встречи.</summary>
public sealed class FateRulesTests
{
    [Fact]
    [Trait("page", "210")]
    public void Fate_DeadOrPermanentlyInsane()
    {
        var alive = NewSheet(sanity: 30);
        var dead = NewSheet(sanity: 30);
        dead.Condition.Dead = true;
        var insane = NewSheet(sanity: 0);

        Assert.Equal(InvestigatorFate.InPlay, FateRules.Of(alive));
        Assert.Equal(InvestigatorFate.Dead, FateRules.Of(dead));
        Assert.Equal(InvestigatorFate.Insane, FateRules.Of(insane));
    }

    [Theory]
    [Trait("page", "211")]
    [InlineData(null, "**Эпилог: Харви.** Ушёл в запой.")]
    [InlineData("Сыщики спустились в подвал.\n", "Сыщики спустились в подвал.\n\n**Эпилог: Харви.** Ушёл в запой.")]
    public void Epilogue_AppendedAsParagraph(string? chronicle, string expected) =>
        Assert.Equal(expected, FateRules.AppendEpilogue(chronicle, " Харви ", " Ушёл в запой. "));

    [Fact]
    public void EmptyEpilogue_LeavesChronicle() =>
        Assert.Equal("Было.", FateRules.AppendEpilogue("Было.", "Харви", "  "));
}
