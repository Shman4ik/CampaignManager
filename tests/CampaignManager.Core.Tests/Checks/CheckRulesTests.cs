using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Checks;
using CampaignManager.Core.Dice;
using CampaignManager.Core.Tests.Infrastructure;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Checks;

/// <summary>
/// Что разрешено после броска проверки: повтор, Удача, отметка развития. Перенесено из T0.2
/// (<c>Rules.Tests/Checks/SkillCheckRulesTests</c>) без правки ожиданий, кроме F-C09.
/// </summary>
public sealed class CheckRulesTests
{
    private static readonly CheckSubject Library = new(CheckSubjectKind.Skill, "Работа в библиотеке", 60)
    {
        SkillCode = "skill.library-use",
    };

    private static readonly CheckSubject Strength = new(CheckSubjectKind.Characteristic, "СИЛ", 50)
    {
        Characteristic = Characteristic.STR,
    };

    private static readonly CheckSubject Luck = new(CheckSubjectKind.Luck, "Удача", 50);
    private static readonly CheckSubject Manual = CheckSubject.Manual(50, "Значение");

    private static CheckOutcome Failed(int roll = 70, int value = 60) => CheckRules.Evaluate(roll, value, Difficulty.Regular);

    [Theory]
    [Trait("page", "88")]
    [InlineData(30, 60, Difficulty.Hard, SuccessLevel.Hard, true)]
    [InlineData(31, 60, Difficulty.Hard, SuccessLevel.Regular, false)]
    [InlineData(97, 60, Difficulty.Hard, SuccessLevel.Fumble, false)]
    [InlineData(97, 60, Difficulty.Regular, SuccessLevel.Failure, false)]
    [InlineData(1, 60, Difficulty.Extreme, SuccessLevel.Critical, true)]
    [InlineData(12, 60, Difficulty.Extreme, SuccessLevel.Extreme, true)]
    public void Evaluate_LevelFromFullValue_PassedAgainstDifficulty(
        int roll, int value, Difficulty difficulty, SuccessLevel level, bool passed) =>
        Assert.Equal(new CheckOutcome(roll, level, passed), CheckRules.Evaluate(roll, value, difficulty));

    [Theory]
    [Trait("page", "89")]
    [InlineData(3, 2, 0, 3)] // больше двух костей не бывает
    [InlineData(-5, 0, 2, 3)]
    [InlineData(0, 0, 0, 1)]
    public void Roll_ClampsToTwoExtraDice(int netDice, int bonus, int penalty, int tensDice)
    {
        var dice = ScriptedDice.Of([5, .. Enumerable.Repeat(4, tensDice)]);

        var roll = CheckRules.Roll(dice, netDice);

        Assert.Equal(45, roll.Result);
        Assert.Equal(bonus, roll.BonusDice);
        Assert.Equal(penalty, roll.PenaltyDice);
        Assert.Equal(tensDice, roll.Candidates.Count);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    [Trait("page", "83")]
    public void PushBlockReason_Passed() =>
        Assert.Equal("Проверка пройдена — повторять нечего.",
            CheckRules.PushBlockReason(Library, CheckRules.Evaluate(30, 60, Difficulty.Regular), false));

    [Fact]
    [Trait("page", "102")]
    public void PushBlockReason_InCombat() =>
        Assert.StartsWith("В бою повторных проверок не бывает", CheckRules.PushBlockReason(Library, Failed(), true));

    [Fact]
    [Trait("page", "83")]
    public void PushBlockReason_Luck() =>
        Assert.Equal("Проверку Удачи повторить нельзя (стр. 83).", CheckRules.PushBlockReason(Luck, Failed(), false));

    [Theory]
    [Trait("page", "102")]
    [InlineData("skill.fighting.brawl", null)]
    [InlineData("skill.firearms.handgun", null)]
    [InlineData("skill.firearms.rifle-shotgun", null)]
    [InlineData(null, SkillCodes.Firearms)] // своя специализация: «Стрельба (гарпун)»
    [InlineData(null, SkillCodes.Fighting)]
    public void PushBlockReason_FightingAndFirearms(string? code, string? parentCode)
    {
        var subject = new CheckSubject(CheckSubjectKind.Skill, "Оружие", 60) { SkillCode = code, ParentSkillCode = parentCode };

        Assert.StartsWith("Ближний бой и Стрельбу повторно не проверяют", CheckRules.PushBlockReason(subject, Failed(), false));
    }

    /// <summary>
    /// F-C09 исправлена устройством: «Автомат» v1 — это <c>skill.firearms.submachine-gun</c>, специализация
    /// Стрельбы, и узнаётся по коду, а не по началу имени.
    /// </summary>
    [Fact]
    [Trait("page", "102")]
    [Trait("finding", "F-C09")]
    public void PushBlockReason_SubmachineGun_IsFirearms()
    {
        var code = SkillCodes.FromName("Автомат");
        var subject = new CheckSubject(CheckSubjectKind.Skill, "Стрельба (пистолет-пулемёт)", 60) { SkillCode = code };

        Assert.Equal("skill.firearms.submachine-gun", code);
        Assert.True(CheckRules.IsCombatSkill(subject));
        Assert.NotNull(CheckRules.PushBlockReason(subject, Failed(), false));
    }

    [Fact]
    public void IsCombatSkill_OtherSkillsAndArtillery_False()
    {
        Assert.False(CheckRules.IsCombatSkill(Library));
        Assert.False(CheckRules.IsCombatSkill(new CheckSubject(CheckSubjectKind.Skill, "Артиллерия", 10) { SkillCode = "skill.artillery" }));
        Assert.False(CheckRules.IsCombatSkill(new CheckSubject(CheckSubjectKind.Skill, "Своё", 10)));
        Assert.False(CheckRules.IsCombatSkill(Strength));
    }

    [Fact]
    [Trait("page", "87")]
    public void PushBlockReason_Fumble()
    {
        var fumble = CheckRules.Evaluate(97, 60, Difficulty.Hard);

        Assert.Equal("Крах наступает сразу, повторной проверкой его не отменить (стр. 87).",
            CheckRules.PushBlockReason(Library, fumble, false));
    }

    [Fact]
    [Trait("page", "83")]
    public void PushBlockReason_FailedSkillCharacteristicOrManual_Allowed()
    {
        Assert.Null(CheckRules.PushBlockReason(Library, Failed(), false));
        Assert.Null(CheckRules.PushBlockReason(Strength, Failed(), false));
        Assert.Null(CheckRules.PushBlockReason(Manual, Failed(), false));
    }

    [Fact]
    [Trait("page", "97")]
    public void LuckBlockReason_Order()
    {
        var passed = CheckRules.Evaluate(30, 60, Difficulty.Regular);
        var fumble = CheckRules.Evaluate(97, 60, Difficulty.Hard);

        Assert.Null(CheckRules.LuckBlockReason(Luck, passed, isPushed: true));
        Assert.Equal("На проверку Удачи пункты Удачи не тратят (стр. 97).", CheckRules.LuckBlockReason(Luck, Failed(), false));
        Assert.Equal("На повторную проверку Удачу не тратят: либо повтор, либо Удача (стр. 97).",
            CheckRules.LuckBlockReason(Library, Failed(), true));
        Assert.Equal("Крах вступает в силу в любом случае — выкупить его нельзя (стр. 97).",
            CheckRules.LuckBlockReason(Library, fumble, false));
        Assert.Null(CheckRules.LuckBlockReason(Library, Failed(), false));
        Assert.Null(CheckRules.LuckBlockReason(Strength, Failed(), false));
    }

    [Fact]
    [Trait("page", "97")]
    public void LuckOptions_FromRegular_AllLevelsWithCostAndAffordability()
    {
        var outcome = CheckRules.Evaluate(45, 40, Difficulty.Regular);

        var options = CheckRules.LuckOptions(outcome, 40, Difficulty.Regular, currentLuck: 10);

        Assert.Equal(
            [(SuccessLevel.Regular, 5, 40, true), (SuccessLevel.Hard, 25, 20, false), (SuccessLevel.Extreme, 37, 8, false)],
            options.Select(o => (o.Level, o.Cost, o.ResultingRoll, o.Affordable)));
    }

    [Fact]
    [Trait("page", "97")]
    public void LuckOptions_HardDifficulty_DropsLevelsBelow()
    {
        var outcome = CheckRules.Evaluate(35, 60, Difficulty.Hard);

        var options = CheckRules.LuckOptions(outcome, 60, Difficulty.Hard, currentLuck: 50);

        Assert.Equal([SuccessLevel.Hard, SuccessLevel.Extreme], options.Select(o => o.Level));
        Assert.Equal(5, options[0].Cost);
    }

    [Fact]
    [Trait("page", "97")]
    public void LuckOptions_HardFumble_StillListsOptions_BlockedOnlyByLuckBlockReason()
    {
        // 97 на трудной проверке навыка 60 — крах, а LuckRules видит лишь обычную сложность (провал)
        var outcome = CheckRules.Evaluate(97, 60, Difficulty.Hard);

        var options = CheckRules.LuckOptions(outcome, 60, Difficulty.Hard, currentLuck: 99);

        Assert.Equal(SuccessLevel.Fumble, outcome.Level);
        Assert.Equal([SuccessLevel.Hard, SuccessLevel.Extreme], options.Select(o => o.Level));
        Assert.NotNull(CheckRules.LuckBlockReason(Library, outcome, false));
    }

    [Theory]
    [Trait("page", "97")]
    [InlineData(100, 60)]
    [InlineData(98, 40)]
    [InlineData(1, 60)]
    public void LuckOptions_FumbleOrCritical_Empty(int roll, int value) =>
        Assert.Empty(CheckRules.LuckOptions(CheckRules.Evaluate(roll, value, Difficulty.Regular), value, Difficulty.Regular, 99));

    /// <summary>F-S03 в диалоге: при навыке 7 чрезвычайный успех за Удачу не продаётся — его порог 01.</summary>
    [Fact]
    [Trait("page", "97")]
    [Trait("finding", "F-S03")]
    public void LuckOptions_ExtremeOnSmallSkill_NotOffered()
    {
        var outcome = CheckRules.Evaluate(40, 7, Difficulty.Extreme);

        Assert.Empty(CheckRules.LuckOptions(outcome, 7, Difficulty.Extreme, 99));
    }

    [Fact]
    [Trait("page", "92")]
    [Trait("page", "97")]
    public void MarkBlockReason_Order()
    {
        Assert.StartsWith("Отметку для развития ставят только навыкам", CheckRules.MarkBlockReason(Strength, 0, false));
        Assert.StartsWith("Отметку для развития ставят только навыкам", CheckRules.MarkBlockReason(Luck, 0, false));
        Assert.Equal("Мифы Ктулху и Средства никогда не отмечают (стр. 92).",
            CheckRules.MarkBlockReason(new CheckSubject(CheckSubjectKind.Skill, "Мифы Ктулху", 5) { SkillCode = SkillCodes.Mythos }, 0, false));
        Assert.Equal("Мифы Ктулху и Средства никогда не отмечают (стр. 92).",
            CheckRules.MarkBlockReason(new CheckSubject(CheckSubjectKind.Skill, "Средства", 30) { SkillCode = SkillCodes.CreditRating }, 0, false));
        Assert.Equal("Проверка шла с бонусной костью — навык не отмечают (стр. 92).", CheckRules.MarkBlockReason(Library, 1, false));
        Assert.Equal("Успех куплен Удачей — отметки за него нет (стр. 97).", CheckRules.MarkBlockReason(Library, 0, true));
        Assert.StartsWith("Значение вписано вручную", CheckRules.MarkBlockReason(Manual, 0, false));
        Assert.Null(CheckRules.MarkBlockReason(Library, 0, false));
        Assert.Null(CheckRules.MarkBlockReason(Library, -2, false)); // штрафная кость отметке не мешает
    }

    [Fact]
    [Trait("page", "80")]
    public void Labels()
    {
        Assert.Equal("Трудная", CheckRules.DifficultyLabel(Difficulty.Hard));
        Assert.Equal("Обычная", CheckRules.DifficultyLabel(Difficulty.Regular));
        Assert.Equal("две штрафные кости", CheckRules.ExtraDiceLabel(-5));
        Assert.Equal("без дополнительных костей", CheckRules.ExtraDiceLabel(0));
        Assert.Equal([Difficulty.Regular, Difficulty.Hard, Difficulty.Extreme], CheckRules.Difficulties);
    }

    /// <summary>Диалог лист не пишет: Удачу и отметку записывает Apply, которым пользуется владелец листа.</summary>
    [Fact]
    [Trait("page", "97")]
    public void Apply_SpendsLuck_KeepsEarlierHonestCheck()
    {
        var spot = Skill("Внимание", 40, isChecked: true);
        var sheet = NewSheet(50, spot);
        sheet.Current.Luck = 30;
        var subject = CheckSubjects.Skill(Catalog, spot);

        Assert.True(CheckRules.Apply(sheet, Catalog, new CheckSheetChange { LuckCost = 10 }));

        Assert.Equal(20, sheet.Current.Luck);
        Assert.True(spot.Checked); // отметка от прошлого успеха остаётся
        Assert.Equal("Внимание", subject.Name);
    }

    [Fact]
    [Trait("page", "97")]
    public void Apply_LuckBeyondCurrent_NothingChanges()
    {
        var sheet = NewSheet();
        sheet.Current.Luck = 5;

        Assert.False(CheckRules.Apply(sheet, Catalog, new CheckSheetChange { LuckCost = 6 }));
        Assert.Equal(5, sheet.Current.Luck);
    }

    [Fact]
    [Trait("page", "92")]
    public void Apply_Mark_ChecksSheetRow()
    {
        var listen = Skill("Слух", 45);
        var sheet = NewSheet(50, listen);

        Assert.True(CheckRules.Apply(sheet, Catalog, new CheckSheetChange { Mark = CheckSubjects.Skill(Catalog, listen) }));
        Assert.True(listen.Checked);

        // повторная отметка ничего не меняет
        Assert.False(CheckRules.Apply(sheet, Catalog, new CheckSheetChange { Mark = CheckSubjects.Skill(Catalog, listen) }));
    }

    /// <summary>Навык справочника, которого на листе нет (по базе), получает строку с отметкой.</summary>
    [Fact]
    [Trait("page", "92")]
    public void Apply_MarkCatalogSkillMissingOnSheet_AddsRowWithBase()
    {
        var sheet = NewSheet();
        var subject = CheckSubjects.CatalogSkill(sheet, Catalog, Id("Маскировка"))!;

        Assert.True(CheckRules.Apply(sheet, Catalog, new CheckSheetChange { Mark = subject }));

        var row = Assert.Single(sheet.Skills);
        Assert.Equal(Id("Маскировка"), row.SkillId);
        Assert.Equal(5, row.Value);
        Assert.True(row.Checked);
    }

    [Fact]
    [Trait("page", "92")]
    public void Apply_MarkMythos_Ignored()
    {
        var sheet = WithMythos(10);
        var mythos = CheckSubjects.Skill(Catalog, sheet.Skills[0]);

        Assert.False(CheckRules.Apply(sheet, Catalog, new CheckSheetChange { Mark = mythos }));
        Assert.False(sheet.Skills[0].Checked);
    }

    [Fact]
    public void Apply_OwnSpecialization_FoundByName()
    {
        var harpoon = Specialization(Firearms, "гарпун", 30);
        var sheet = NewSheet(50, harpoon);
        var subject = CheckSubjects.Skill(Catalog, harpoon);

        Assert.Equal("Стрельба (гарпун)", subject.Name);
        Assert.Equal(SkillCodes.Firearms, subject.ParentSkillCode);
        Assert.True(CheckRules.Apply(sheet, Catalog, new CheckSheetChange { Mark = subject }));
        Assert.True(harpoon.Checked);
    }
}
