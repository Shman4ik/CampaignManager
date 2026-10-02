using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Dice;
using CampaignManager.Core.KeeperScreen;

namespace CampaignManager.Core.Tests.KeeperScreen;

/// <summary>Справочные таблицы ширмы: формулы в них — те, что понимают кости приложения.</summary>
public sealed class ReferenceTablesTests
{
    [Fact]
    [Trait("page", "122")]
    public void OtherDamage_SixTiersOfGrowingDamage()
    {
        var tiers = OtherDamageReference.Tiers;

        Assert.Equal(["Мелкое", "Среднее", "Тяжёлое", "Губительное", "Фатальное", "Безнадёжное"], tiers.Select(t => t.Name));
        Assert.Equal([3, 6, 10, 20, 40, 80], tiers.Select(t => DiceFormula.Parse(t.Formula).Max));
        Assert.All(tiers, t => Assert.True(DiceFormula.Parse(t.Formula).IsValid));
    }

    [Fact]
    [Trait("page", "122")]
    public void Falls_FormulasFromTableThree()
    {
        var tierFormulas = OtherDamageReference.Tiers.Select(t => t.Formula).ToHashSet();

        Assert.Equal(3, OtherDamageReference.Falls.Count);
        Assert.All(OtherDamageReference.Falls, fall => Assert.Contains(fall.Formula, tierFormulas));
    }

    [Fact]
    [Trait("page", "126-127")]
    public void Poisons_DamageIsDiceOrNone()
    {
        Assert.Equal(4, OtherDamageReference.Poisons.Count);
        Assert.Equal([0, 10, 20, 40], OtherDamageReference.Poisons.Select(p => DiceFormula.Parse(p.Damage).IsValid ? DiceFormula.Parse(p.Damage).Max : 0));
    }

    [Fact]
    [Trait("page", "153")]
    public void SanityExamples_ParseAsLossFormulas()
    {
        Assert.Equal(12, SanityReference.LossExamples.Count);
        Assert.All(SanityReference.LossExamples, example =>
        {
            var formula = SanityLossFormula.Parse(example.Loss);
            Assert.True(formula.OnSuccess.IsValid, example.Loss);
            Assert.True(formula.OnFailure.IsValid, example.Loss);
            Assert.True(formula.OnFailure.HasDice, example.Loss);
        });
        Assert.Equal(100, SanityLossFormula.MaxLoss("1D10/1D100"));
    }

    [Fact]
    [Trait("page", "152")]
    public void InvoluntaryActions_Five() => Assert.Equal(5, SanityReference.InvoluntaryActions.Count);
}
