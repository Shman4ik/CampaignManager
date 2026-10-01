using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Rules.Tests.Infrastructure;

/// <summary>Сам шов: подмена работает и снимается.</summary>
public sealed class ScriptedRandomTests
{
    [Fact]
    public void RollDice_ReturnsScriptedFace_AndRestoresRandomAfterDispose()
    {
        using (var dice = ScriptedRandom.Use(4, 6))
        {
            Assert.Equal(4, CombatService.RollDice(6));
            Assert.Equal(6, CombatService.RollDice(6));
            Assert.Equal(0, dice.Remaining);
        }

        Assert.Null(CombatService.RandomOverride.Value);
        Assert.InRange(CombatService.RollDice(6), 1, 6);
    }

    [Fact]
    public void ValueOutsideRange_Throws()
    {
        using var _ = ScriptedRandom.Use(7);
        Assert.Throws<InvalidOperationException>(() => CombatService.RollDice(6));
    }
}
