using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Tests.Infrastructure;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>
/// Шаги 1–2 главы 3: таблица возраста, броски характеристик, проверка ОБР, черновик помощника, очки
/// навыков. Перенесено из T0.2 без правки ожиданий; кости — параметром, а не шов генератора.
/// </summary>
public sealed class InvestigatorCreationRulesTests
{
    [Fact]
    [Trait("page", "30")]
    public void AgeBands_ContiguousFrom15To89()
    {
        var bands = InvestigatorCreationRules.AgeBands;

        Assert.Equal(7, bands.Count);
        Assert.Equal(InvestigatorCreationRules.MinAge, bands[0].MinAge);
        Assert.Equal(InvestigatorCreationRules.MaxAge, bands[^1].MaxAge);
        for (var i = 1; i < bands.Count; i++)
            Assert.Equal(bands[i - 1].MaxAge + 1, bands[i].MinAge);
    }

    [Theory]
    [Trait("page", "30")]
    [InlineData(15, "Юный", 0, 5, 0, 5, 0, 2, 0)]
    [InlineData(20, "Молодой", 1, 0, 0, 0, 0, 1, 0)]
    [InlineData(40, "Средний", 2, 0, 5, 0, 5, 1, 1)]
    [InlineData(50, "Зрелый", 3, 0, 10, 0, 10, 1, 2)]
    [InlineData(60, "Пожилой", 4, 0, 15, 0, 20, 1, 3)]
    [InlineData(70, "Старый", 4, 0, 20, 0, 40, 1, 4)]
    [InlineData(80, "Престарелый", 4, 0, 25, 0, 80, 1, 5)]
    public void AgeBand_Row_MatchesTable(
        int minAge, string name, int eduChecks, int eduPenalty, int appPenalty,
        int strSizPenalty, int physicalPenalty, int luckRolls, int movePenalty)
    {
        var band = InvestigatorCreationRules.AgeBands.Single(b => b.MinAge == minAge);

        Assert.Equal(name, band.Name);
        Assert.Equal(eduChecks, band.EducationChecks);
        Assert.Equal(eduPenalty, band.EducationPenalty);
        Assert.Equal(appPenalty, band.AppearancePenalty);
        Assert.Equal(strSizPenalty, band.StrengthSizePenalty);
        Assert.Equal(physicalPenalty, band.PhysicalPenalty);
        Assert.Equal(luckRolls, band.LuckRolls);
        Assert.Equal(movePenalty, band.MovePenalty);
    }

    [Theory]
    [Trait("page", "30")]
    [InlineData(15, "Юный")]
    [InlineData(19, "Юный")]
    [InlineData(20, "Молодой")]
    [InlineData(39, "Молодой")]
    [InlineData(40, "Средний")]
    [InlineData(49, "Средний")]
    [InlineData(50, "Зрелый")]
    [InlineData(59, "Зрелый")]
    [InlineData(60, "Пожилой")]
    [InlineData(69, "Пожилой")]
    [InlineData(70, "Старый")]
    [InlineData(79, "Старый")]
    [InlineData(80, "Престарелый")]
    [InlineData(89, "Престарелый")]
    public void BandFor_EveryBoundary(int age, string expected) =>
        Assert.Equal(expected, InvestigatorCreationRules.BandFor(age).Name);

    /// <summary>Вне 15–89 возраст молча считается «молодым». Поведение v1 сохранено, вопрос — в rules-findings.</summary>
    [Theory]
    [Trait("page", "30")]
    [Trait("finding", "F-S01")]
    [InlineData(0)]
    [InlineData(14)]
    [InlineData(90)]
    [InlineData(120)]
    public void BandFor_OutsideTable_FallsBackToYoung(int age) =>
        Assert.Equal("Молодой", InvestigatorCreationRules.BandFor(age).Name);

    [Fact]
    [Trait("page", "30")]
    public void Ages_MinIs15_MaxIs89()
    {
        Assert.Equal(15, InvestigatorCreationRules.MinAge);
        Assert.Equal(89, InvestigatorCreationRules.MaxAge);
    }

    [Theory]
    [Trait("page", "30")]
    [InlineData(17, 5, new[] { Characteristic.STR, Characteristic.SIZ })]
    [InlineData(30, 0, new Characteristic[0])]
    [InlineData(45, 5, new[] { Characteristic.STR, Characteristic.CON, Characteristic.DEX })]
    [InlineData(85, 80, new[] { Characteristic.STR, Characteristic.CON, Characteristic.DEX })]
    public void AgeBand_PenaltyTargets_AndDistributedPenalty(int age, int penalty, Characteristic[] targets)
    {
        var band = InvestigatorCreationRules.BandFor(age);

        Assert.Equal(penalty, band.DistributedPenalty);
        Assert.Equal(targets, band.PenaltyTargets);
    }

    [Fact]
    [Trait("page", "28-29")]
    public void Characteristics_DiceFormulas()
    {
        Characteristic[] twoD6Plus6 = [Characteristic.SIZ, Characteristic.INT, Characteristic.EDU];

        Assert.Equal(8, InvestigatorCreationRules.Characteristics.Count);
        foreach (var info in InvestigatorCreationRules.Characteristics)
            Assert.Equal(twoD6Plus6.Contains(info.Key) ? CharacteristicDice.TwoD6Plus6 : CharacteristicDice.ThreeD6, info.Dice);

        Assert.Equal("СИЛ", InvestigatorCreationRules.Info(Characteristic.STR).Abbreviation);
        Assert.Equal("(2d6 + 6) × 5", InvestigatorCreationRules.Info(Characteristic.EDU).DiceText);
    }

    [Fact]
    [Trait("page", "46")]
    public void AlternativeMethods_Constants()
    {
        Assert.Equal([80, 70, 60, 60, 50, 50, 50, 40], InvestigatorCreationRules.BlitzCharacteristics);
        Assert.Equal([70, 60, 60, 50, 50, 50, 40, 40, 40], InvestigatorCreationRules.BlitzSkillValues);
        Assert.Equal(460, InvestigatorCreationRules.PointBuyBudget);
        Assert.Equal(9, InvestigatorCreationRules.ExtraClassMaxBonus);
        Assert.Equal(75, InvestigatorCreationRules.OptionalSkillCap);
    }

    // ── Броски ──────────────────────────────────────────────────────────────

    [Fact]
    [Trait("page", "28")]
    public void Roll3d6_SumTimesFive()
    {
        var roll = InvestigatorCreationRules.Roll3d6(ScriptedDice.Of(4, 3, 6));

        Assert.Equal([4, 3, 6], roll.Dice);
        Assert.Equal(65, roll.Value);
        Assert.Equal("(4 + 3 + 6) × 5 = 65", roll.Text);
    }

    [Fact]
    [Trait("page", "29")]
    public void Roll_SizeUses2d6Plus6()
    {
        var dice = ScriptedDice.Of(2, 5);

        var roll = InvestigatorCreationRules.Roll(Characteristic.SIZ, dice);

        Assert.Equal(65, roll.Value);
        Assert.Equal(6, roll.Bonus);
        Assert.Equal("(2 + 5 + 6) × 5 = 65", roll.Text);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    [Trait("page", "46")]
    public void RollPool_FiveThreeD6AndThree2d6Plus6_SortedDescending()
    {
        var dice = ScriptedDice.Of(
            1, 1, 1, 6, 6, 6, 2, 2, 2, 3, 3, 3, 4, 4, 4, // пять 3d6: 15, 90, 30, 45, 60
            1, 1, 6, 6, 3, 3); // три 2d6+6: 40, 90, 60

        Assert.Equal([90, 90, 60, 60, 45, 40, 30, 15], InvestigatorCreationRules.RollPool(dice));
    }

    [Theory]
    [Trait("page", "30")]
    [InlineData(70, 1, 7, 4, 4)] // 71 > 70 → +4
    [InlineData(70, 0, 7, 0, 0)] // 70 — не больше ОБР
    [InlineData(95, 9, 9, 10, 4)] // не выше 99
    [InlineData(99, 0, 0, 3, 0)] // 100 > 99, но выше 99 некуда
    public void RollEducationCheck_ImprovesOnlyAboveCurrent_CappedAt99(
        int education, int units, int tens, int d10, int expectedGain)
    {
        var dice = d10 > 0 ? ScriptedDice.Of(units, tens, d10) : ScriptedDice.Of(units, tens);

        var check = InvestigatorCreationRules.RollEducationCheck(education, dice);

        Assert.Equal(expectedGain, check.Gain);
        Assert.Equal(education + expectedGain, check.After);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    [Trait("page", "30")]
    public void EducationCheck_Text()
    {
        Assert.Equal("1d100 = 71 > 70 → +4 (ОБР 70 → 74)", new EducationCheck(71, 70, 4).Text);
        Assert.Equal("1d100 = 40 ≤ 70 → без изменений", new EducationCheck(40, 70, 0).Text);
    }

    [Theory]
    [Trait("page", "46")]
    [InlineData(1, 0)]
    [InlineData(10, 9)]
    public void RollExtraClass_D10MinusOne(int face, int expected) =>
        Assert.Equal(expected, InvestigatorCreationRules.RollExtraClass(ScriptedDice.Of(face)));

    // ── Черновик помощника ──────────────────────────────────────────────────

    [Fact]
    [Trait("page", "30")]
    public void DraftValue_SubtractsDistributedPenalty_AddsExtraClass()
    {
        var draft = new InvestigatorDraft
        {
            Rolled = { [Characteristic.STR] = 60 },
            AgePenaltyDistribution = { [Characteristic.STR] = 5 },
            ExtraClassBonus = { [Characteristic.STR] = 3 },
        };

        Assert.Equal(58, draft.Value(Characteristic.STR, InvestigatorCreationRules.BandFor(45)));
    }

    [Theory]
    [Trait("page", "30")]
    [InlineData(25, 50)]
    [InlineData(45, 45)]
    [InlineData(85, 25)]
    public void DraftValue_Appearance_FixedAgePenalty(int age, int expected)
    {
        var draft = new InvestigatorDraft { Rolled = { [Characteristic.APP] = 50 } };

        Assert.Equal(expected, draft.Value(Characteristic.APP, InvestigatorCreationRules.BandFor(age)));
    }

    [Fact]
    [Trait("page", "30")]
    public void DraftValue_Education_AddsChecksAndSubtractsYouthPenalty()
    {
        var draft = new InvestigatorDraft
        {
            Rolled = { [Characteristic.EDU] = 70 },
            EducationChecks = [new(80, 70, 5), new(10, 75, 0), new(90, 75, 3)],
        };

        Assert.Equal(73, draft.Value(Characteristic.EDU, InvestigatorCreationRules.BandFor(17)));
        Assert.Equal(78, draft.Value(Characteristic.EDU, InvestigatorCreationRules.BandFor(30)));
    }

    [Theory]
    [Trait("page", "30")]
    [InlineData(0, 0, 1)] // не брошено — 1, а не 0
    [InlineData(10, 20, 1)]
    [InlineData(99, 0, 99)]
    public void DraftValue_ClampedTo1And99(int rolled, int penalty, int expected)
    {
        var draft = new InvestigatorDraft
        {
            Rolled = { [Characteristic.POW] = rolled },
            AgePenaltyDistribution = { [Characteristic.POW] = penalty },
            ExtraClassBonus = { [Characteristic.POW] = rolled == 99 ? 5 : 0 },
        };

        Assert.Equal(expected, draft.Value(Characteristic.POW, InvestigatorCreationRules.BandFor(30)));
    }

    [Theory]
    [Trait("page", "30")]
    [InlineData(45, 2, 0, 0, 3)]
    [InlineData(45, 2, 3, 0, 0)]
    [InlineData(45, 7, 0, 0, -2)] // перераспределено — уходит в минус
    [InlineData(45, 0, 0, 5, 5)] // НАР не цель вычета — не засчитывается
    [InlineData(65, 10, 5, 0, 5)]
    public void RemainingAgePenalty_CountsOnlyTargets(int age, int str, int con, int app, int expected)
    {
        var draft = new InvestigatorDraft
        {
            AgePenaltyDistribution =
            {
                [Characteristic.STR] = str,
                [Characteristic.CON] = con,
                [Characteristic.APP] = app,
            },
        };

        Assert.Equal(expected, draft.RemainingAgePenalty(InvestigatorCreationRules.BandFor(age)));
    }

    [Fact]
    [Trait("page", "30")]
    public void RemainingAgePenalty_Youth_SizeCounts()
    {
        var draft = new InvestigatorDraft { AgePenaltyDistribution = { [Characteristic.SIZ] = 5 } };

        Assert.Equal(0, draft.RemainingAgePenalty(InvestigatorCreationRules.BandFor(16)));

        // У «молодого» целей нет — остаток 0, но Value вычитает распределённое при любой строке таблицы:
        // поэтому смена возраста в помощнике обнуляет распределение.
        draft.Rolled[Characteristic.SIZ] = 60;
        Assert.Equal(0, draft.RemainingAgePenalty(InvestigatorCreationRules.BandFor(30)));
        Assert.Equal(55, draft.Value(Characteristic.SIZ, InvestigatorCreationRules.BandFor(30)));
    }

    [Fact]
    [Trait("page", "46")]
    public void Draft_ExtraClassAndSpentPoints()
    {
        var draft = new InvestigatorDraft
        {
            ExtraClassPool = 7,
            ExtraClassBonus = { [Characteristic.STR] = 3 },
            OccupationPoints = { ["Внимание"] = 10, ["Психология"] = 20 },
            PersonalPoints = { ["Внимание"] = 5 },
            CreditRating = 30,
        };

        Assert.Equal(4, draft.RemainingExtraClass());
        Assert.Equal(60, draft.SpentOccupationPoints);
        Assert.Equal(5, draft.SpentPersonalPoints);
        Assert.Equal(40, draft.SkillTotal("Внимание", 25));
        Assert.Equal(-3, new InvestigatorDraft { ExtraClassBonus = { [Characteristic.POW] = 3 } }.RemainingExtraClass());
    }

    [Fact]
    [Trait("page", "28-29")]
    public void CharacteristicsFilled_AllEightRolled()
    {
        var draft = new InvestigatorDraft();
        foreach (var key in Enum.GetValues<Characteristic>())
            draft.Rolled[key] = 50;

        Assert.True(draft.CharacteristicsFilled);
        Assert.Equal(50, draft.BuildCharacteristics().Edu);

        draft.Rolled[Characteristic.EDU] = 0;
        Assert.False(draft.CharacteristicsFilled);
    }

    // ── Очки навыков ────────────────────────────────────────────────────────

    /// <summary>ОБР 75, ЛВК 45, НАР 40, СИЛ 50, МОЩ 70.</summary>
    [Theory]
    [Trait("page", "38-39")]
    [InlineData(SkillPointsFormula.Edu4, 300)]
    [InlineData(SkillPointsFormula.Edu2Dex2, 240)]
    [InlineData(SkillPointsFormula.Edu2App2, 230)]
    [InlineData(SkillPointsFormula.Edu2Str2, 250)]
    [InlineData(SkillPointsFormula.Edu2Pow2, 290)]
    [InlineData(SkillPointsFormula.Edu2DexOrStr2, 250)] // без выбора — максимум
    [InlineData(SkillPointsFormula.Edu2AppOrPow2, 290)]
    [InlineData(SkillPointsFormula.Edu2DexOrPow2, 290)]
    [InlineData(SkillPointsFormula.Edu2AppOrDexOrStr2, 250)]
    public void SkillPoints_EveryFormula(SkillPointsFormula formula, int expected) =>
        Assert.Equal(expected, OccupationRules.SkillPoints(formula, Chars()));

    [Theory]
    [Trait("page", "38-39")]
    [InlineData(SkillPointsFormula.Edu2DexOrStr2, Characteristic.DEX, 240)]
    [InlineData(SkillPointsFormula.Edu2DexOrStr2, Characteristic.STR, 250)]
    [InlineData(SkillPointsFormula.Edu2AppOrPow2, Characteristic.APP, 230)]
    [InlineData(SkillPointsFormula.Edu2AppOrDexOrStr2, Characteristic.APP, 230)]
    [InlineData(SkillPointsFormula.Edu4, Characteristic.POW, 290)] // выбор перекрывает формулу
    public void SkillPoints_WithChoice_UsesChosenNotMax(SkillPointsFormula formula, Characteristic choice, int expected) =>
        Assert.Equal(expected, OccupationRules.SkillPoints(formula, Chars(), choice));

    [Theory]
    [Trait("page", "38-39")]
    [InlineData(SkillPointsFormula.Edu4, new Characteristic[0])]
    [InlineData(SkillPointsFormula.Edu2Dex2, new Characteristic[0])]
    [InlineData(SkillPointsFormula.Edu2DexOrStr2, new[] { Characteristic.DEX, Characteristic.STR })]
    [InlineData(SkillPointsFormula.Edu2AppOrPow2, new[] { Characteristic.APP, Characteristic.POW })]
    [InlineData(SkillPointsFormula.Edu2DexOrPow2, new[] { Characteristic.DEX, Characteristic.POW })]
    [InlineData(SkillPointsFormula.Edu2AppOrDexOrStr2, new[] { Characteristic.APP, Characteristic.DEX, Characteristic.STR })]
    public void FormulaChoices_ListsAlternatives(SkillPointsFormula formula, Characteristic[] expected) =>
        Assert.Equal(expected, OccupationRules.FormulaChoices(formula));

    [Fact]
    [Trait("page", "34")]
    public void PersonalPoints_IntTimesTwo() => Assert.Equal(160, OccupationRules.PersonalPoints(Chars(@int: 80)));

    [Theory]
    [Trait("page", "57, 77")]
    [InlineData(Dodge, 22)] // половина ЛВК 45
    [InlineData(OwnLanguage, 75)] // ОБР
    [InlineData("Внимание", 25)]
    public void BaseValue_DodgeAndOwnLanguageFromCharacteristics(string name, int expected) =>
        Assert.Equal(expected, SkillCatalog.BaseValueOf(Def(name), Chars()));

    [Theory]
    [InlineData("POW*2", 140)]
    [InlineData("pow * 2", 140)]
    [InlineData("XYZ/2", 7)] // непонятная формула — число справочника
    [InlineData("DEX/0", 45)]
    public void BaseValue_FormulaForms(string formula, int expected) =>
        Assert.Equal(expected, SkillCatalog.BaseValueOf(new SkillDefinition(Guid.NewGuid(), "Т") { BaseValue = 7, BaseFormula = formula }, Chars()));
}
