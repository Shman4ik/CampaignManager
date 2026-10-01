using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Characters.Services;
using static CampaignManager.Rules.Tests.Sheet.Sheets;

namespace CampaignManager.Rules.Tests.Sheet;

/// <summary>Бонус +10 смежным специализациям на порогах 50 и 90.</summary>
[Trait("page", "76-77")]
public sealed class SpecializationRulesTests
{
    private const string Melee = "Ближний бой";

    [Theory]
    [InlineData("Ближний бой", true)]
    [InlineData("ближний бой", true)]
    [InlineData("Стрельба", true)]
    [InlineData("Выживание", true)]
    [InlineData("Языки", true)]
    [InlineData("Наука", false)]
    [InlineData("Искусство/ремесло", false)]
    [InlineData(null, false)]
    public void ParentSharesProgress_ClosedList(string? parent, bool expected) =>
        Assert.Equal(expected, SpecializationRules.ParentSharesProgress(parent));

    /// <summary>
    ///     В списке — старое «Языки», а справочник и профессии называют родителя
    ///     «Язык, иностранный»: иностранные языки бонуса не получают.
    /// </summary>
    [Fact]
    [Trait("finding", "F-S04")]
    public void BonusFor_ForeignLanguageParentFromCatalog_GetsNoBonus()
    {
        const string parent = "Язык, иностранный";
        var latin = Skill("Язык, иностранный (латынь)", 30, parent);
        var greek = Skill("Язык, иностранный (греческий)", 60, parent);

        Assert.False(SpecializationRules.ParentSharesProgress(parent));
        Assert.Equal(0, SpecializationRules.BonusFor(latin, [latin, greek], parent));
    }

    [Theory]
    [InlineData(45, 50, 5)] // не выше порога 50
    [InlineData(30, 55, 10)]
    [InlineData(40, 50, 10)]
    [InlineData(50, 60, 0)] // уже на пороге
    [InlineData(30, 49, 0)] // сосед порог не перешёл
    [InlineData(85, 90, 5)]
    [InlineData(70, 95, 10)] // порог 90: +10, до 80
    [InlineData(89, 99, 1)]
    [InlineData(90, 99, 0)]
    public void BonusFor_ByBestSibling_TenUpToThreshold(int value, int sibling, int expected)
    {
        var skill = Skill("Ближний бой (меч)", value, Melee);
        var other = Skill("Ближний бой (драка)", sibling, Melee);

        Assert.Equal(expected, SpecializationRules.BonusFor(skill, [skill, other], Melee));
    }

    [Fact]
    public void BonusFor_OwnValueNotCountedAsSibling()
    {
        var skill = Skill("Ближний бой (драка)", 60, Melee);
        var other = Skill("Ближний бой (меч)", 20, Melee);

        Assert.Equal(0, SpecializationRules.BonusFor(skill, [skill, other], Melee));
        Assert.Equal(10, SpecializationRules.BonusFor(other, [skill, other], Melee));
    }

    [Fact]
    public void BonusFor_SingleSkillGroup_Zero()
    {
        var skill = Skill("Ближний бой (меч)", 20, Melee);

        Assert.Equal(0, SpecializationRules.BonusFor(skill, [skill], Melee));
    }

    [Fact]
    public void BonusFor_ParentOutsideList_Zero()
    {
        var chemistry = Skill("Наука (химия)", 20, "Наука");
        var physics = Skill("Наука (физика)", 70, "Наука");

        Assert.Equal(0, SpecializationRules.BonusFor(chemistry, [chemistry, physics], "Наука"));
    }

    [Fact]
    public void BonusFor_TakesBestOfSeveralSiblings()
    {
        var skill = Skill("Стрельба (винтовка)", 40, "Стрельба");
        IReadOnlyCollection<Skill> group =
        [
            skill,
            Skill("Стрельба (пистолет)", 20, "Стрельба"),
            Skill("Стрельба (дробовик)", 91, "Стрельба")
        ];

        Assert.Equal(10, SpecializationRules.BonusFor(skill, group, "Стрельба"));
    }
}
