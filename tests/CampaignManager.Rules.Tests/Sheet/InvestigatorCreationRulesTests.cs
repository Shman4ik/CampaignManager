using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Characters.Services;
using static CampaignManager.Rules.Tests.Sheet.Sheets;

namespace CampaignManager.Rules.Tests.Sheet;

/// <summary>Шаги 1–2 главы 3: таблица возраста, броски характеристик, проверка ОБР.</summary>
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

    /// <summary>Строка таблицы: проверки ОБР, штрафы, броски Удачи и Скорость.</summary>
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

    /// <summary>
    ///     Вне 15–89 возраст молча считается «молодым» — без штрафов. Комментарий к
    ///     <c>MinAge</c> цитирует книгу «от 15 до 90 лет», а <c>MaxAge</c> = 89, и 90 лет
    ///     попадают в строку 20–39.
    /// </summary>
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
    [InlineData(17, 5, new[] { CharacteristicKey.Strength, CharacteristicKey.Size })]
    [InlineData(30, 0, new CharacteristicKey[0])]
    [InlineData(45, 5, new[] { CharacteristicKey.Strength, CharacteristicKey.Constitution, CharacteristicKey.Dexterity })]
    [InlineData(85, 80, new[] { CharacteristicKey.Strength, CharacteristicKey.Constitution, CharacteristicKey.Dexterity })]
    public void AgeBand_PenaltyTargets_AndDistributedPenalty(int age, int penalty, CharacteristicKey[] targets)
    {
        var band = InvestigatorCreationRules.BandFor(age);

        Assert.Equal(penalty, band.DistributedPenalty);
        Assert.Equal(targets, band.PenaltyTargets);
    }

    [Fact]
    [Trait("page", "28-29")]
    public void Characteristics_DiceFormulas()
    {
        CharacteristicKey[] twoD6Plus6 = [CharacteristicKey.Size, CharacteristicKey.Intelligence, CharacteristicKey.Education];

        Assert.Equal(8, InvestigatorCreationRules.Characteristics.Count);
        foreach (var info in InvestigatorCreationRules.Characteristics)
            Assert.Equal(twoD6Plus6.Contains(info.Key) ? CharacteristicDice.TwoD6Plus6 : CharacteristicDice.ThreeD6, info.Dice);

        Assert.Equal("СИЛ", InvestigatorCreationRules.Info(CharacteristicKey.Strength).Abbreviation);
        Assert.Equal("(2d6 + 6) × 5", InvestigatorCreationRules.Info(CharacteristicKey.Education).DiceText);
    }

    [Fact]
    [Trait("page", "46")]
    public void AlternativeMethods_Constants()
    {
        Assert.Equal(new[] { 80, 70, 60, 60, 50, 50, 50, 40 }, InvestigatorCreationRules.BlitzCharacteristics);
        Assert.Equal(new[] { 70, 60, 60, 50, 50, 50, 40, 40, 40 }, InvestigatorCreationRules.BlitzSkillValues);
        Assert.Equal(460, InvestigatorCreationRules.PointBuyBudget);
        Assert.Equal(9, InvestigatorCreationRules.ExtraClassMaxBonus);
        Assert.Equal(75, InvestigatorCreationRules.OptionalSkillCap);
    }

    // ── Броски ──────────────────────────────────────────────────────────────

    [Fact]
    [Trait("page", "28")]
    public void Roll3d6_SumTimesFive()
    {
        using var dice = ScriptedRandom.Use(4, 3, 6);

        var roll = InvestigatorCreationRules.Roll3d6();

        Assert.Equal(new[] { 4, 3, 6 }, roll.Dice);
        Assert.Equal(65, roll.Value);
        Assert.Equal("(4 + 3 + 6) × 5 = 65", roll.Text);
    }

    [Fact]
    [Trait("page", "29")]
    public void Roll_SizeUses2d6Plus6()
    {
        using var dice = ScriptedRandom.Use(2, 5);

        var roll = InvestigatorCreationRules.Roll(CharacteristicKey.Size);

        Assert.Equal(65, roll.Value);
        Assert.Equal(6, roll.Bonus);
        Assert.Equal("(2 + 5 + 6) × 5 = 65", roll.Text);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    [Trait("page", "46")]
    public void RollPool_FiveThreeD6AndThree2d6Plus6_SortedDescending()
    {
        using var dice = ScriptedRandom.Use(
            1, 1, 1, 6, 6, 6, 2, 2, 2, 3, 3, 3, 4, 4, 4, // пять 3d6: 15, 90, 30, 45, 60
            1, 1, 6, 6, 3, 3); // три 2d6+6: 40, 90, 60

        var pool = InvestigatorCreationRules.RollPool();

        Assert.Equal(new[] { 90, 90, 60, 60, 45, 40, 30, 15 }, pool);
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
        int[] values = d10 > 0 ? [units, tens, d10] : [units, tens];
        using var dice = ScriptedRandom.Use(values);

        var check = InvestigatorCreationRules.RollEducationCheck(education);

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

    // ── Черновик помощника ──────────────────────────────────────────────────

    [Fact]
    [Trait("page", "30")]
    public void DraftValue_SubtractsDistributedPenalty_AddsExtraClass()
    {
        var draft = new InvestigatorDraft
        {
            Rolled = { [CharacteristicKey.Strength] = 60 },
            AgePenaltyDistribution = { [CharacteristicKey.Strength] = 5 },
            ExtraClassBonus = { [CharacteristicKey.Strength] = 3 }
        };

        Assert.Equal(58, draft.Value(CharacteristicKey.Strength, InvestigatorCreationRules.BandFor(45)));
    }

    [Theory]
    [Trait("page", "30")]
    [InlineData(25, 50)]
    [InlineData(45, 45)]
    [InlineData(85, 25)]
    public void DraftValue_Appearance_FixedAgePenalty(int age, int expected)
    {
        var draft = new InvestigatorDraft { Rolled = { [CharacteristicKey.Appearance] = 50 } };

        Assert.Equal(expected, draft.Value(CharacteristicKey.Appearance, InvestigatorCreationRules.BandFor(age)));
    }

    [Fact]
    [Trait("page", "30")]
    public void DraftValue_Education_AddsChecksAndSubtractsYouthPenalty()
    {
        var draft = new InvestigatorDraft
        {
            Rolled = { [CharacteristicKey.Education] = 70 },
            EducationChecks = [new() { Gain = 5 }, new() { Gain = 0 }, new() { Gain = 3 }]
        };

        Assert.Equal(73, draft.Value(CharacteristicKey.Education, InvestigatorCreationRules.BandFor(17)));
        Assert.Equal(78, draft.Value(CharacteristicKey.Education, InvestigatorCreationRules.BandFor(30)));
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
            Rolled = { [CharacteristicKey.Power] = rolled },
            AgePenaltyDistribution = { [CharacteristicKey.Power] = penalty },
            ExtraClassBonus = { [CharacteristicKey.Power] = rolled == 99 ? 5 : 0 }
        };

        Assert.Equal(expected, draft.Value(CharacteristicKey.Power, InvestigatorCreationRules.BandFor(30)));
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
                [CharacteristicKey.Strength] = str,
                [CharacteristicKey.Constitution] = con,
                [CharacteristicKey.Appearance] = app
            }
        };

        Assert.Equal(expected, draft.RemainingAgePenalty(InvestigatorCreationRules.BandFor(age)));
    }

    [Fact]
    [Trait("page", "30")]
    public void RemainingAgePenalty_Youth_SizeCounts()
    {
        var draft = new InvestigatorDraft { AgePenaltyDistribution = { [CharacteristicKey.Size] = 5 } };

        Assert.Equal(0, draft.RemainingAgePenalty(InvestigatorCreationRules.BandFor(16)));

        // У «молодого» целей нет — остаток 0, но Value вычитает распределённое при любой полосе:
        // поэтому смена возраста в помощнике обнуляет распределение (WizardMethodStep.SetAge).
        draft.Rolled[CharacteristicKey.Size] = 60;
        Assert.Equal(0, draft.RemainingAgePenalty(InvestigatorCreationRules.BandFor(30)));
        Assert.Equal(55, draft.Value(CharacteristicKey.Size, InvestigatorCreationRules.BandFor(30)));
    }

    [Fact]
    [Trait("page", "46")]
    public void Draft_ExtraClassAndSpentPoints()
    {
        var draft = new InvestigatorDraft
        {
            ExtraClassPool = 7,
            ExtraClassBonus = { [CharacteristicKey.Strength] = 3 },
            OccupationPoints = { ["Внимание"] = 10, ["Психология"] = 20 },
            PersonalPoints = { ["Внимание"] = 5 },
            CreditRating = 30
        };

        Assert.Equal(4, draft.RemainingExtraClass());
        Assert.Equal(60, draft.SpentOccupationPoints);
        Assert.Equal(5, draft.SpentPersonalPoints);
        Assert.Equal(40, draft.SkillTotal("Внимание", 25));
        Assert.Equal(-3, new InvestigatorDraft { ExtraClassBonus = { [CharacteristicKey.Power] = 3 } }.RemainingExtraClass());
    }

    [Fact]
    [Trait("page", "28-29")]
    public void CharacteristicsFilled_AllEightRolled()
    {
        var draft = new InvestigatorDraft();
        foreach (var key in Enum.GetValues<CharacteristicKey>())
            draft.Rolled[key] = 50;

        Assert.True(draft.CharacteristicsFilled);

        draft.Rolled[CharacteristicKey.Education] = 0;
        Assert.False(draft.CharacteristicsFilled);
    }

    // ── Очки навыков ────────────────────────────────────────────────────────

    /// <summary>ОБР 75, ЛВК 45, НАР 40, СИЛ 50, МОЩ 70.</summary>
    [Theory]
    [Trait("page", "38-39")]
    [InlineData(OccupationSkillPointFormula.Edu4, 300)]
    [InlineData(OccupationSkillPointFormula.Edu2Dex2, 240)]
    [InlineData(OccupationSkillPointFormula.Edu2App2, 230)]
    [InlineData(OccupationSkillPointFormula.Edu2Str2, 250)]
    [InlineData(OccupationSkillPointFormula.Edu2Pow2, 290)]
    [InlineData(OccupationSkillPointFormula.Edu2DexOrStr2, 250)] // без выбора — максимум
    [InlineData(OccupationSkillPointFormula.Edu2AppOrPow2, 290)]
    [InlineData(OccupationSkillPointFormula.Edu2DexOrPow2, 290)]
    [InlineData(OccupationSkillPointFormula.Edu2AppOrDexOrStr2, 250)]
    public void CalculateSkillPoints_EveryFormula(OccupationSkillPointFormula formula, int expected)
    {
        var occupation = Occupation(formula);

        Assert.Equal(expected, occupation.CalculateSkillPoints(Chars()));
        Assert.Equal(expected, InvestigatorFactory.OccupationPointsFor(occupation, Chars(), null));
    }

    [Theory]
    [Trait("page", "38-39")]
    [InlineData(OccupationSkillPointFormula.Edu2DexOrStr2, CharacteristicKey.Dexterity, 240)]
    [InlineData(OccupationSkillPointFormula.Edu2DexOrStr2, CharacteristicKey.Strength, 250)]
    [InlineData(OccupationSkillPointFormula.Edu2AppOrPow2, CharacteristicKey.Appearance, 230)]
    [InlineData(OccupationSkillPointFormula.Edu2AppOrDexOrStr2, CharacteristicKey.Appearance, 230)]
    [InlineData(OccupationSkillPointFormula.Edu4, CharacteristicKey.Power, 290)] // выбор перекрывает формулу
    public void OccupationPointsFor_WithChoice_UsesChosenNotMax(
        OccupationSkillPointFormula formula, CharacteristicKey choice, int expected) =>
        Assert.Equal(expected, InvestigatorFactory.OccupationPointsFor(Occupation(formula), Chars(), choice));

    [Fact]
    [Trait("page", "38-39")]
    public void OccupationPointsFor_NoOccupation_Zero() =>
        Assert.Equal(0, InvestigatorFactory.OccupationPointsFor(null, Chars(), CharacteristicKey.Strength));

    [Theory]
    [Trait("page", "38-39")]
    [InlineData(OccupationSkillPointFormula.Edu4, new CharacteristicKey[0])]
    [InlineData(OccupationSkillPointFormula.Edu2Dex2, new CharacteristicKey[0])]
    [InlineData(OccupationSkillPointFormula.Edu2DexOrStr2, new[] { CharacteristicKey.Dexterity, CharacteristicKey.Strength })]
    [InlineData(OccupationSkillPointFormula.Edu2AppOrPow2, new[] { CharacteristicKey.Appearance, CharacteristicKey.Power })]
    [InlineData(OccupationSkillPointFormula.Edu2DexOrPow2, new[] { CharacteristicKey.Dexterity, CharacteristicKey.Power })]
    [InlineData(OccupationSkillPointFormula.Edu2AppOrDexOrStr2,
        new[] { CharacteristicKey.Appearance, CharacteristicKey.Dexterity, CharacteristicKey.Strength })]
    public void FormulaChoices_ListsAlternatives(OccupationSkillPointFormula formula, CharacteristicKey[] expected) =>
        Assert.Equal(expected, InvestigatorFactory.FormulaChoices(formula));

    [Fact]
    [Trait("page", "34")]
    public void PersonalPointsFor_IntTimesTwo() =>
        Assert.Equal(160, InvestigatorFactory.PersonalPointsFor(Chars(@int: 80)));

    [Theory]
    [Trait("page", "57, 77")]
    [InlineData("Уклонение", 22)] // половина ЛВК 45
    [InlineData("Язык, родной", 75)] // ОБР
    [InlineData("Внимание", 25)]
    public void BaseValue_DodgeAndOwnLanguageFromCharacteristics(string name, int expected) =>
        Assert.Equal(expected, InvestigatorFactory.BaseValue(Skill(name, 25), Chars()));

    private static Occupation Occupation(OccupationSkillPointFormula formula) =>
        new() { Name = "Тест", SkillPointFormula = formula };
}
