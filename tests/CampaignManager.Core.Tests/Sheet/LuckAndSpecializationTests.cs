using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Dice;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>Необязательное правило «Пункты Удачи». Перенесено из T0.2 без правки ожиданий.</summary>
[Trait("page", "97")]
public sealed class LuckRulesTests
{
    [Theory]
    [InlineData(1, 50, SuccessLevel.Critical)]
    [InlineData(10, 50, SuccessLevel.Extreme)]
    [InlineData(25, 50, SuccessLevel.Hard)]
    [InlineData(50, 50, SuccessLevel.Regular)]
    [InlineData(51, 50, SuccessLevel.Failure)]
    [InlineData(100, 50, SuccessLevel.Fumble)]
    public void LevelOf_UsesCommonThresholds(int roll, int target, SuccessLevel expected) =>
        Assert.Equal(expected, LuckRules.LevelOf(roll, target));

    [Theory]
    [InlineData(0, 50, false)]
    [InlineData(101, 50, false)]
    [InlineData(30, 0, false)]
    [InlineData(1, 50, false)] // критический успех
    [InlineData(100, 50, false)] // крах
    [InlineData(97, 40, false)] // 96+ при навыке ниже 50 — крах
    [InlineData(97, 60, true)] // при навыке 50+ это обычный провал
    [InlineData(60, 50, true)]
    [InlineData(30, 50, true)]
    public void CanSpendOn_ExcludesCriticalFumbleAndNonRolls(int roll, int target, bool expected) =>
        Assert.Equal(expected, LuckRules.CanSpendOn(roll, target));

    [Fact]
    public void Options_Failure_ListsAllLevels_WithAffordability()
    {
        Assert.Equal(
        [
            new LuckRules.SpendOption(SuccessLevel.Regular, 10, 50, true),
            new LuckRules.SpendOption(SuccessLevel.Hard, 35, 25, false),
            new LuckRules.SpendOption(SuccessLevel.Extreme, 50, 10, false),
        ], LuckRules.Options(60, 50, 20));
    }

    [Fact]
    public void Options_RegularSuccess_OffersOnlyHigherLevels()
    {
        Assert.Equal(
        [
            new LuckRules.SpendOption(SuccessLevel.Hard, 5, 25, true),
            new LuckRules.SpendOption(SuccessLevel.Extreme, 20, 10, true),
        ], LuckRules.Options(30, 50, 99));
    }

    [Fact]
    public void Options_CostEqualToLuck_IsAffordable()
    {
        var option = Assert.Single(LuckRules.Options(20, 50, 10));

        Assert.Equal(SuccessLevel.Extreme, option.Level);
        Assert.Equal(10, option.Cost);
        Assert.True(option.Affordable);
        Assert.False(LuckRules.Options(20, 50, 9)[0].Affordable);
    }

    [Theory]
    [InlineData(8, 50)] // уже чрезвычайный
    [InlineData(1, 50)]
    [InlineData(100, 50)]
    [InlineData(97, 30)]
    public void Options_NothingToBuy_Empty(int roll, int target) => Assert.Empty(LuckRules.Options(roll, target, 99));

    [Fact]
    public void Options_TargetBelowFive_SkipsExtremeLevel()
    {
        Assert.Equal(
        [
            new LuckRules.SpendOption(SuccessLevel.Regular, 46, 4, true),
            new LuckRules.SpendOption(SuccessLevel.Hard, 48, 2, true),
        ], LuckRules.Options(50, 4, 99));
    }

    /// <summary>
    /// F-S03 исправлена (T2.7, решение владельца 2026-10-02): при навыке 5–9 пятая часть — 1, и в v1
    /// «чрезвычайный успех» выкупал бросок до 01, а 01 — критический, который Удачей не покупают
    /// (стр. 97). Теперь уровень с порогом 01 не предлагается.
    /// </summary>
    [Fact]
    [Trait("finding", "F-S03")]
    public void Options_SmallTarget_NoOptionLandsOnCriticalRoll()
    {
        Assert.Equal(
        [
            new LuckRules.SpendOption(SuccessLevel.Regular, 25, 5, true),
            new LuckRules.SpendOption(SuccessLevel.Hard, 28, 2, true),
        ], LuckRules.Options(30, 5, 99));
    }

    /// <summary>F-S03: то же для трудного уровня при навыке 2–3 и для любого уровня при навыке 1.</summary>
    [Theory]
    [Trait("finding", "F-S03")]
    [InlineData(3, new[] { SuccessLevel.Regular })]
    [InlineData(2, new[] { SuccessLevel.Regular })]
    [InlineData(1, new SuccessLevel[0])]
    public void Options_ThresholdOfOne_NeverOffered(int target, SuccessLevel[] expected)
    {
        var options = LuckRules.Options(50, target, 99);

        Assert.Equal(expected, options.Select(o => o.Level));
        Assert.All(options, o => Assert.NotEqual(SuccessLevel.Critical, LuckRules.LevelOf(o.ResultingRoll, target)));
    }

    /// <summary>Ни один предложенный вариант не даёт критического успеха — при любом навыке и броске.</summary>
    [Fact]
    [Trait("finding", "F-S03")]
    public void Options_NeverBuyCritical_Exhaustive()
    {
        for (var target = 1; target <= 100; target++)
        for (var roll = 1; roll <= 100; roll++)
        {
            foreach (var option in LuckRules.Options(roll, target, 99))
            {
                Assert.True(option.ResultingRoll > 1, $"бросок {roll}, навык {target}: {option}");
                Assert.Equal(option.Level, LuckRules.LevelOf(option.ResultingRoll, target));
            }
        }
    }

    /// <summary>
    /// Одна трата на приложение: списывает Удачу и снимает отметку, поставленную за этот же бросок (купленный
    /// успех её не даёт, стр. 97).
    /// </summary>
    [Fact]
    public void Spend_DeductsLuck_AndClearsMarkOfThisRoll()
    {
        var skill = Skill("Внимание", 40, isChecked: true);
        var sheet = NewSheet(50, skill);
        sheet.Current.Luck = 30;

        Assert.False(LuckRules.Spend(sheet, 31, skill));
        Assert.True(skill.Checked);

        Assert.True(LuckRules.Spend(sheet, 10, skill));
        Assert.Equal(20, sheet.Current.Luck);
        Assert.False(skill.Checked);
    }

    /// <summary>Отметку, стоявшую до броска, трата не снимает: она заработана другим успехом.</summary>
    [Fact]
    public void Spend_WithoutMarkOfThisRoll_KeepsEarlierMark()
    {
        var skill = Skill("Внимание", 40, isChecked: true);
        var sheet = NewSheet(50, skill);
        sheet.Current.Luck = 30;

        Assert.True(LuckRules.Spend(sheet, 10));

        Assert.Equal(20, sheet.Current.Luck);
        Assert.True(skill.Checked);
    }
}

/// <summary>Бонус +10 смежным специализациям на порогах 50 и 90. Перенесено из T0.2.</summary>
[Trait("page", "76-77")]
public sealed class SpecializationRulesTests
{
    [Theory]
    [InlineData(SkillCodes.Fighting, true)]
    [InlineData(SkillCodes.Firearms, true)]
    [InlineData(SkillCodes.Survival, true)]
    [InlineData(SkillCodes.LanguageForeign, true)]
    [InlineData("skill.science", false)]
    [InlineData("skill.art-craft", false)]
    [InlineData(null, false)]
    public void ParentSharesProgress_ClosedList(string? parentCode, bool expected) =>
        Assert.Equal(expected, SpecializationRules.ParentSharesProgress(parentCode));

    /// <summary>
    /// F-S04 исправлена: в v1 список держал старое «Языки», а справочник называл родителя «Язык,
    /// иностранный», и иностранные языки бонуса не получали. Теперь сверка по коду родителя.
    /// </summary>
    [Fact]
    [Trait("finding", "F-S04")]
    public void BonusFor_ForeignLanguagesShareProgress()
    {
        var latin = Skill(Latin, 30);
        var sheet = NewSheet(50, latin, Skill("Язык, иностранный (греческий)", 60));

        Assert.Equal(10, SpecializationRules.BonusFor(sheet, Catalog, latin));
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
        var skill = Skill("Ближний бой (меч)", value);
        var sheet = NewSheet(50, skill, Skill("Ближний бой (драка)", sibling));

        Assert.Equal(expected, SpecializationRules.BonusFor(sheet, Catalog, skill));
        Assert.Equal(expected, SpecializationRules.BonusFor(value, [sibling], SkillCodes.Fighting));
    }

    [Fact]
    public void BonusFor_OwnValueNotCountedAsSibling()
    {
        var brawl = Skill("Ближний бой (драка)", 60);
        var sword = Skill("Ближний бой (меч)", 20);
        var sheet = NewSheet(50, brawl, sword);

        Assert.Equal(0, SpecializationRules.BonusFor(sheet, Catalog, brawl));
        Assert.Equal(10, SpecializationRules.BonusFor(sheet, Catalog, sword));
    }

    [Fact]
    public void BonusFor_SingleSpecialization_Zero()
    {
        var sword = Skill("Ближний бой (меч)", 20);

        Assert.Equal(0, SpecializationRules.BonusFor(NewSheet(50, sword), Catalog, sword));
    }

    [Fact]
    public void BonusFor_ParentOutsideList_Zero()
    {
        var chemistry = Skill("Наука (химия)", 20);
        var sheet = NewSheet(50, chemistry, Skill("Наука (физика)", 70));

        Assert.Equal(0, SpecializationRules.BonusFor(sheet, Catalog, chemistry));
    }

    [Fact]
    public void BonusFor_TakesBestOfSeveralSiblings_IncludingOwnSpecializations()
    {
        var rifle = Skill("Стрельба (винтовка)", 40);
        var sheet = NewSheet(50, rifle, Skill("Стрельба (пистолет)", 20), Specialization(Firearms, "гарпун", 91));

        Assert.Equal(10, SpecializationRules.BonusFor(sheet, Catalog, rifle));
    }
}
