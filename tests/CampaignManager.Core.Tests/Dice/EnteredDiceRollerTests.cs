using CampaignManager.Core.Characters;
using CampaignManager.Core.Dice;
using CampaignManager.Core.Tests.Sheet;

namespace CampaignManager.Core.Tests.Dice;

/// <summary>
/// Вписанные с настоящих костей числа идут в правила первыми (правило листа «любой бросок можно вписать»): d100,
/// суммы костей, пустые слоты на месте невписанного. Плюс фаза развития целиком на вписанных костях (стр. 92–94, 165).
/// </summary>
public sealed class EnteredDiceRollerTests
{
    private sealed class Fixed(int value) : IDiceRoller
    {
        public int Next(int minInclusive, int maxExclusive) => Math.Clamp(value, minInclusive, maxExclusive - 1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(37)]
    [InlineData(90)]
    [InlineData(100)]
    public void Entered_percentile_is_the_result(int entered)
    {
        var roller = new EnteredDiceRoller(new Fixed(5)).Percentile(entered);

        Assert.Equal(entered, roller.Percentile());
    }

    [Fact]
    public void Entered_percentile_with_bonus_die_keeps_the_result()
    {
        var roller = new EnteredDiceRoller(new Fixed(0)).Percentile(42, tensDice: 2);

        Assert.Equal(42, roller.Percentile(bonusDice: 1));
    }

    [Theory]
    [InlineData(2, 6, 2)]
    [InlineData(2, 6, 7)]
    [InlineData(2, 6, 12)]
    [InlineData(2, 10, 15)]
    [InlineData(1, 100, 63)]
    public void Entered_total_is_rolled_back(int count, int sides, int total)
    {
        var roller = new EnteredDiceRoller(new Fixed(1)).Total(total, count, sides);

        Assert.Equal(total, roller.Roll(count, sides));
    }

    [Theory]
    [InlineData(1)] // меньше двух костей
    [InlineData(13)] // больше 2d6
    public void Impossible_total_falls_back_to_dice(int total)
    {
        var roller = new EnteredDiceRoller(new Fixed(3)).Total(total, 2, 6);

        Assert.Equal(6, roller.Roll(2, 6));
    }

    [Fact]
    public void Not_entered_roll_keeps_its_slot()
    {
        // d100 не вписан, прирост вписан — прирост не должен уехать в кость единиц d100
        var roller = new EnteredDiceRoller(new Fixed(4)).Percentile(null).Total(9, 1, 10);

        Assert.Equal(44, roller.Percentile());
        Assert.Equal(9, roller.Die(10));
    }

    [Fact]
    [Trait("page", "92")]
    public void Development_check_uses_entered_roll_gain_and_mastery_sanity()
    {
        var sheet = Sheets.NewSheet(50, Sheets.Skill("Внимание", 85, isChecked: true));
        var skill = sheet.Skills.Single();
        var spec = DevelopmentPhaseRules.MasterySanity;
        var dice = new EnteredDiceRoller(new Fixed(1))
            .Percentile(97)
            .Total(8, DevelopmentPhaseRules.SkillGain.Count, DevelopmentPhaseRules.SkillGain.Sides)
            .Total(11, spec.Count, spec.Sides);

        var result = DevelopmentPhaseRules.RollSkillImprovement(sheet, Sheets.Catalog, skill, dice);

        // Пример книги: 85% меча, выпало 97, +8 — мастерство; +2d6 Рассудка (вписано 11)
        Assert.Equal(97, result.Roll);
        Assert.Equal(8, result.Gain);
        Assert.Equal(93, skill.Value);
        Assert.True(result.ReachedMastery);
        Assert.Equal(11, result.SanityGain);
        Assert.Equal(61, sheet.Current.Sanity);
    }

    [Fact]
    [Trait("page", "93")]
    public void Luck_recovery_uses_entered_roll_and_gain()
    {
        var sheet = Sheets.NewSheet();
        sheet.Current.Luck = 40;
        var dice = new EnteredDiceRoller(new Fixed(1)).Percentile(55).Total(6, 1, 10);

        var result = DevelopmentPhaseRules.RollLuckRecovery(sheet, dice);

        Assert.Equal(55, result.Roll);
        Assert.Equal(46, result.NewValue);
    }

    [Fact]
    [Trait("page", "165")]
    public void Self_healing_uses_entered_check_and_gain()
    {
        var sheet = Sheets.NewSheet(50);
        var dice = new EnteredDiceRoller(new Fixed(9)).Percentile(30).Total(4, 1, 6);

        var result = DevelopmentPhaseRules.RollSelfHealing(sheet, Sheets.Catalog, useKeyConnection: false, dice);

        Assert.True(result.Success);
        Assert.Equal(4, result.SanityDelta);
        Assert.Equal(54, sheet.Current.Sanity);
    }

    [Theory]
    [Trait("page", "94")]
    [InlineData(CreditRatingChange.Rich, 4, 45)] // пример книги: 41 + 4
    [InlineData(CreditRatingChange.RoughPatch, 13, 28)]
    [InlineData(CreditRatingChange.Bankrupt, 63, 0)]
    public void Credit_rating_change_uses_entered_total(CreditRatingChange change, int entered, int expected)
    {
        var sheet = Sheets.NewSheet();
        sheet.Skills.Add(new SheetSkill { SkillId = Sheets.Id(Sheets.CreditRating), Value = 41 });
        var spec = DevelopmentPhaseRules.CreditRatingDice(change)!;
        var dice = new EnteredDiceRoller(new Fixed(1)).Total(entered, spec.Count, spec.Sides);

        var result = DevelopmentPhaseRules.ApplyCreditRatingChange(sheet, Sheets.Catalog, change, dice);

        Assert.Equal(entered, result.Roll);
        Assert.Equal(expected, result.NewValue);
    }
}
