using CampaignManager.Core.Characters;
using CampaignManager.Core.Dice;
using CampaignManager.Core.Tests.Sheet;

namespace CampaignManager.Core.Tests.Dice;

/// <summary>Вписанные с настоящих костей числа идут в правила первыми (правило листа «любой бросок можно вписать»).</summary>
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

    [Fact]
    public void Not_entered_falls_back_to_dice()
    {
        var roller = new EnteredDiceRoller(new Fixed(7)).Percentile(null).Percentile(150);

        Assert.Equal(7, roller.Die(10));
    }

    [Fact]
    [Trait("page", "92")]
    public void Development_check_uses_entered_roll_and_gain()
    {
        var sheet = Sheets.NewSheet(50, Sheets.Skill("Внимание", 60, isChecked: true));
        var skill = sheet.Skills.Single();
        var dice = new EnteredDiceRoller(new Fixed(1)).Percentile(85).Faces(7);

        var result = DevelopmentPhaseRules.RollSkillImprovement(sheet, Sheets.Catalog, skill, dice);

        Assert.Equal(85, result.Roll);
        Assert.Equal(7, result.Gain);
        Assert.Equal(67, skill.Value);
    }
}
