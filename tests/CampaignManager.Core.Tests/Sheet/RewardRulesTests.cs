using CampaignManager.Core.Characters;
using CampaignManager.Core.Dice;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>Итоги сценария для партии: прибавка и проверка Рассудка, деньги — одной записью в лист.</summary>
public sealed class RewardRulesTests
{
    [Fact]
    [Trait("page", "165")]
    public void SanityGain_CappedByMax()
    {
        var sheet = WithMythos(mythos: 10, sanity: 86); // максимум 99 − 10 = 89

        Assert.Equal("Рассудок 86 → 89", RewardRules.Apply(sheet, Catalog, RewardKind.SanityGain, 6));
        Assert.Equal(89, sheet.Current.Sanity);
    }

    /// <summary>Потеря по итогам — одна причина: 5+ требует проверки ИНТ, считается в потерях за день.</summary>
    [Fact]
    [Trait("page", "359")]
    public void SanityCheckLoss_IsOneCause()
    {
        var sheet = NewSheet(sanity: 40);

        Assert.Equal("Рассудок 40 → 34", RewardRules.Apply(sheet, Catalog, RewardKind.SanityCheck, 6));
        Assert.Equal(6, sheet.Condition.LastSanityLoss);
        Assert.Equal(6, sheet.Condition.SanityLostToday);
        Assert.True(SanityRules.Status(sheet, Catalog).NeedsIntCheck);
    }

    [Fact]
    [Trait("page", "94")]
    public void Money_ToCash_EvenWithoutCash()
    {
        var poor = NewSheet();
        var rich = NewSheet();
        rich.Finances.Cash = 30;

        Assert.Equal("наличные $0 → $250", RewardRules.Apply(poor, Catalog, RewardKind.Money, 250));
        RewardRules.Apply(rich, Catalog, RewardKind.Money, 250);
        Assert.Equal(280m, rich.Finances.Cash);
    }

    [Theory]
    [Trait("page", "153")]
    [InlineData(SuccessLevel.Regular, "1", false)]
    [InlineData(SuccessLevel.Failure, "1d6", false)]
    [InlineData(SuccessLevel.Fumble, "1d6", true)]
    public void LossFor_SuccessOrFailurePart_FumbleIsMaximum(SuccessLevel level, string formula, bool maximum)
    {
        var (loss, max) = SanityRules.LossFor(level, "1", "1d6");

        Assert.Equal(formula, loss.Text);
        Assert.Equal(maximum, max);
    }
}
