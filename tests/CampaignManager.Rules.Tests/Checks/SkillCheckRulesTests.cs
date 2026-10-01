using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Checks.Model;
using CampaignManager.Web.Components.Features.Checks.Services;
using CampaignManager.Web.Components.Features.Combat.Model;

namespace CampaignManager.Rules.Tests.Checks;

/// <summary>Что разрешено после броска проверки: повтор, Удача, отметка развития.</summary>
public sealed class SkillCheckRulesTests
{
    private static readonly CheckTarget Library = new(CheckTargetKind.Skill, "Работа в библиотеке", 60);
    private static readonly CheckTarget Strength = new(CheckTargetKind.Characteristic, "СИЛ", 50);
    private static readonly CheckTarget Luck = new(CheckTargetKind.Luck, "Удача", 50);
    private static readonly CheckTarget Manual = new(CheckTargetKind.Other, "Значение", 50);

    private static CheckOutcome Failed(int roll = 70, int value = 60) =>
        SkillCheckRules.Evaluate(roll, value, SuccessLevel.RegularSuccess);

    [Theory]
    [Trait("page", "88")]
    [InlineData(30, 60, SuccessLevel.HardSuccess, SuccessLevel.HardSuccess, true)]
    [InlineData(31, 60, SuccessLevel.HardSuccess, SuccessLevel.RegularSuccess, false)]
    [InlineData(97, 60, SuccessLevel.HardSuccess, SuccessLevel.Fumble, false)]
    [InlineData(97, 60, SuccessLevel.RegularSuccess, SuccessLevel.Failure, false)]
    [InlineData(1, 60, SuccessLevel.ExtremeSuccess, SuccessLevel.CriticalSuccess, true)]
    [InlineData(12, 60, SuccessLevel.ExtremeSuccess, SuccessLevel.ExtremeSuccess, true)]
    public void Evaluate_LevelFromFullValue_PassedAgainstDifficulty(
        int roll, int value, SuccessLevel difficulty, SuccessLevel level, bool passed)
    {
        Assert.Equal(new CheckOutcome(roll, level, passed), SkillCheckRules.Evaluate(roll, value, difficulty));
    }

    [Theory]
    [Trait("page", "80")]
    [InlineData(60, SuccessLevel.RegularSuccess, 60)]
    [InlineData(55, SuccessLevel.HardSuccess, 27)]
    [InlineData(55, SuccessLevel.ExtremeSuccess, 11)]
    public void TargetNumber_SameAsCombat(int value, SuccessLevel difficulty, int expected)
    {
        Assert.Equal(expected, SkillCheckRules.TargetNumber(value, difficulty));
    }

    [Theory]
    [Trait("page", "89")]
    [InlineData(3, 2, 0, 3)] // больше двух костей не бывает
    [InlineData(-5, 0, 2, 3)]
    [InlineData(0, 0, 0, 1)]
    public void Roll_ClampsToTwoExtraDice(int netDice, int bonus, int penalty, int tensDice)
    {
        using var dice = ScriptedRandom.Use([5, .. Enumerable.Repeat(4, tensDice)]);

        var roll = SkillCheckRules.Roll(netDice);

        Assert.Equal(45, roll.Result);
        Assert.Equal(bonus, roll.BonusDice);
        Assert.Equal(penalty, roll.PenaltyDice);
        Assert.Equal(tensDice, roll.Candidates.Count);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    [Trait("page", "83")]
    public void PushBlockReason_Passed()
    {
        Assert.Equal("Проверка пройдена — повторять нечего.",
            SkillCheckRules.PushBlockReason(Library, SkillCheckRules.Evaluate(30, 60, SuccessLevel.RegularSuccess), false));
    }

    [Fact]
    [Trait("page", "102")]
    public void PushBlockReason_InCombat()
    {
        Assert.StartsWith("В бою повторных проверок не бывает", SkillCheckRules.PushBlockReason(Library, Failed(), true));
    }

    [Fact]
    [Trait("page", "83")]
    public void PushBlockReason_Luck()
    {
        Assert.Equal("Проверку Удачи повторить нельзя (стр. 83).", SkillCheckRules.PushBlockReason(Luck, Failed(), false));
    }

    [Theory]
    [Trait("page", "102")]
    [InlineData("Ближний бой (драка)")]
    [InlineData("Стрельба (пистолет)")]
    [InlineData("стрельба (винт./дроб.)")]
    public void PushBlockReason_FightingAndFirearms(string skill)
    {
        var target = new CheckTarget(CheckTargetKind.Skill, skill, 60);

        Assert.StartsWith("Ближний бой и Стрельбу повторно не проверяют", SkillCheckRules.PushBlockReason(target, Failed(), false));
    }

    /// <summary>
    ///     «Автомат» — строка группы «Сражение (Огнестрельное)» стандартного листа, но
    ///     <see cref="SkillCheckRules.IsCombatSkill" /> узнаёт огнестрел только по «Стрельба…», и
    ///     провал по автомату диалог предлагает повторить.
    /// </summary>
    [Fact]
    [Trait("page", "102")]
    [Trait("finding", "F-C09")]
    public void PushBlockReason_SubmachineGunSkill_CanBePushed()
    {
        var target = new CheckTarget(CheckTargetKind.Skill, "Автомат", 60);

        Assert.False(SkillCheckRules.IsCombatSkill("Автомат"));
        Assert.Null(SkillCheckRules.PushBlockReason(target, Failed(), false));
    }

    [Fact]
    [Trait("page", "87")]
    public void PushBlockReason_Fumble()
    {
        var fumble = SkillCheckRules.Evaluate(97, 60, SuccessLevel.HardSuccess);

        Assert.Equal("Крах наступает сразу, повторной проверкой его не отменить (стр. 87).",
            SkillCheckRules.PushBlockReason(Library, fumble, false));
    }

    [Fact]
    [Trait("page", "83")]
    public void PushBlockReason_FailedSkillCharacteristicOrManual_Allowed()
    {
        Assert.Null(SkillCheckRules.PushBlockReason(Library, Failed(), false));
        Assert.Null(SkillCheckRules.PushBlockReason(Strength, Failed(), false));
        Assert.Null(SkillCheckRules.PushBlockReason(Manual, Failed(), false));
    }

    [Fact]
    [Trait("page", "97")]
    public void LuckBlockReason_Order()
    {
        var passed = SkillCheckRules.Evaluate(30, 60, SuccessLevel.RegularSuccess);
        var fumble = SkillCheckRules.Evaluate(97, 60, SuccessLevel.HardSuccess);

        Assert.Null(SkillCheckRules.LuckBlockReason(Luck, passed, isPushed: true));
        Assert.Equal("На проверку Удачи пункты Удачи не тратят (стр. 97).",
            SkillCheckRules.LuckBlockReason(Luck, Failed(), false));
        Assert.Equal("На повторную проверку Удачу не тратят: либо повтор, либо Удача (стр. 97).",
            SkillCheckRules.LuckBlockReason(Library, Failed(), true));
        Assert.Equal("Крах вступает в силу в любом случае — выкупить его нельзя (стр. 97).",
            SkillCheckRules.LuckBlockReason(Library, fumble, false));
        Assert.Null(SkillCheckRules.LuckBlockReason(Library, Failed(), false));
        Assert.Null(SkillCheckRules.LuckBlockReason(Strength, Failed(), false));
    }

    [Fact]
    [Trait("page", "97")]
    public void LuckOptions_FromRegular_AllLevelsWithCostAndAffordability()
    {
        var outcome = SkillCheckRules.Evaluate(45, 40, SuccessLevel.RegularSuccess);

        var options = SkillCheckRules.LuckOptions(outcome, 40, SuccessLevel.RegularSuccess, currentLuck: 10);

        Assert.Equal(
            [
                (SuccessLevel.RegularSuccess, 5, 40, true),
                (SuccessLevel.HardSuccess, 25, 20, false),
                (SuccessLevel.ExtremeSuccess, 37, 8, false)
            ],
            options.Select(o => (o.Level, o.Cost, o.ResultingRoll, o.Affordable)));
    }

    [Fact]
    [Trait("page", "97")]
    public void LuckOptions_HardDifficulty_DropsLevelsBelow()
    {
        var outcome = SkillCheckRules.Evaluate(35, 60, SuccessLevel.HardSuccess);

        var options = SkillCheckRules.LuckOptions(outcome, 60, SuccessLevel.HardSuccess, currentLuck: 50);

        Assert.Equal([SuccessLevel.HardSuccess, SuccessLevel.ExtremeSuccess], options.Select(o => o.Level));
        Assert.Equal(5, options[0].Cost);
    }

    [Fact]
    [Trait("page", "97")]
    public void LuckOptions_HardFumble_StillListsOptions_BlockedOnlyByLuckBlockReason()
    {
        // 97 на трудной проверке навыка 60 — крах, но LuckRules видит лишь обычную сложность (провал)
        var outcome = SkillCheckRules.Evaluate(97, 60, SuccessLevel.HardSuccess);

        var options = SkillCheckRules.LuckOptions(outcome, 60, SuccessLevel.HardSuccess, currentLuck: 99);

        Assert.Equal(SuccessLevel.Fumble, outcome.Level);
        Assert.Equal([SuccessLevel.HardSuccess, SuccessLevel.ExtremeSuccess], options.Select(o => o.Level));
        Assert.NotNull(SkillCheckRules.LuckBlockReason(Library, outcome, false));
    }

    [Theory]
    [Trait("page", "97")]
    [InlineData(100, 60)]
    [InlineData(98, 40)]
    [InlineData(1, 60)]
    public void LuckOptions_FumbleOrCritical_Empty(int roll, int value)
    {
        var outcome = SkillCheckRules.Evaluate(roll, value, SuccessLevel.RegularSuccess);

        Assert.Empty(SkillCheckRules.LuckOptions(outcome, value, SuccessLevel.RegularSuccess, 99));
    }

    [Fact]
    [Trait("page", "92")]
    [Trait("page", "97")]
    public void MarkBlockReason_Order()
    {
        Assert.StartsWith("Отметку для развития ставят только навыкам",
            SkillCheckRules.MarkBlockReason(Strength, 0, false));
        Assert.StartsWith("Отметку для развития ставят только навыкам",
            SkillCheckRules.MarkBlockReason(Luck, 0, false));
        Assert.Equal("Мифы Ктулху и Средства никогда не отмечают (стр. 92).",
            SkillCheckRules.MarkBlockReason(new CheckTarget(CheckTargetKind.Skill, "Мифы Ктулху", 5), 0, false));
        Assert.Equal("Мифы Ктулху и Средства никогда не отмечают (стр. 92).",
            SkillCheckRules.MarkBlockReason(new CheckTarget(CheckTargetKind.Skill, "Средства", 30), 0, false));
        Assert.Equal("Проверка шла с бонусной костью — навык не отмечают (стр. 92).",
            SkillCheckRules.MarkBlockReason(Library, 1, false));
        Assert.Equal("Успех куплен Удачей — отметки за него нет (стр. 97).",
            SkillCheckRules.MarkBlockReason(Library, 0, true));
        Assert.StartsWith("Значение вписано вручную", SkillCheckRules.MarkBlockReason(Manual, 0, false));
        Assert.Null(SkillCheckRules.MarkBlockReason(Library, 0, false));
        Assert.Null(SkillCheckRules.MarkBlockReason(Library, -2, false)); // штрафная кость отметке не мешает
    }

    [Theory]
    [Trait("page", "80")]
    [InlineData("Hard", SuccessLevel.HardSuccess)]
    [InlineData("Extreme", SuccessLevel.ExtremeSuccess)]
    [InlineData("Regular", SuccessLevel.RegularSuccess)]
    [InlineData("hard", SuccessLevel.RegularSuccess)] // регистр важен
    [InlineData("", SuccessLevel.RegularSuccess)]
    [InlineData(null, SuccessLevel.RegularSuccess)]
    public void ParseScenarioDifficulty_ExactStrings(string? difficulty, SuccessLevel expected)
    {
        Assert.Equal(expected, SkillCheckRules.ParseScenarioDifficulty(difficulty));
    }

    [Fact]
    [Trait("page", "80")]
    public void Labels()
    {
        Assert.Equal("Трудная", SkillCheckRules.DifficultyLabel(SuccessLevel.HardSuccess));
        Assert.Equal("Обычная", SkillCheckRules.DifficultyLabel(SuccessLevel.CriticalSuccess));
        Assert.Equal("две штрафные кости", SkillCheckRules.ExtraDiceLabel(-5));
        Assert.Equal("без дополнительных костей", SkillCheckRules.ExtraDiceLabel(0));
        Assert.Equal("Трудный успех", SkillCheckRules.LevelTitle(SuccessLevel.HardSuccess));
        Assert.Equal([SuccessLevel.RegularSuccess, SuccessLevel.HardSuccess, SuccessLevel.ExtremeSuccess],
            SkillCheckRules.Difficulties);
    }
}
