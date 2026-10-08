using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Tests.Infrastructure;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>
/// Фаза развития с заданными костями. d100 — сначала единицы, потом десятки: 41 = (1, 4), 100 = (0, 0).
/// Перенесено из T0.2; ожидания те же, кроме денег (F-S06): наличные — число, а не строка.
/// </summary>
public sealed class DevelopmentPhaseRulesTests
{
    // ── Отметки ─────────────────────────────────────────────────────────────

    [Theory]
    [Trait("page", "92")]
    [InlineData(SkillCodes.Mythos, false)]
    [InlineData(SkillCodes.CreditRating, false)]
    [InlineData("skill.spot-hidden", true)]
    [InlineData(null, true)] // свой навык
    public void CanBeChecked_MythosAndCreditRatingNever(string? code, bool expected) =>
        Assert.Equal(expected, DevelopmentPhaseRules.CanBeChecked(code));

    [Fact]
    [Trait("page", "92")]
    public void CheckedSkills_OnlyMarked_ExcludingMythosAndCredit_ByName()
    {
        var sheet = NewSheet(50,
            Skill("Слух", 20, true), Skill("Внимание", 25, true), Skill("Психология", 10),
            Skill(Mythos, 5, true), Skill(CreditRating, 30, true));

        Assert.Equal(["Внимание", "Слух"],
            DevelopmentPhaseRules.CheckedSkills(sheet, Catalog).Select(s => s.DisplayName(Catalog)));
    }

    [Fact]
    [Trait("page", "92")]
    public void ClearSkillChecks_UnmarksEverything()
    {
        var sheet = NewSheet(50, Skill("Слух", 20, true), Skill(Mythos, 5, true));

        DevelopmentPhaseRules.ClearSkillChecks(sheet);

        Assert.All(sheet.Skills, s => Assert.False(s.Checked));
    }

    // ── Проверка опыта ──────────────────────────────────────────────────────

    [Fact]
    [Trait("page", "92")]
    public void RollSkillImprovement_RollAboveSkill_GainsD10()
    {
        var sheet = NewSheet(50, Skill("Слух", 40));
        var dice = ScriptedDice.Of(1, 4, 6); // 41, затем 1d10 = 6

        var result = DevelopmentPhaseRules.RollSkillImprovement(sheet, Catalog, Find(sheet, "Слух"), dice);

        Assert.True(result.Improved);
        Assert.Equal("Слух", result.SkillName);
        Assert.Equal(41, result.Roll);
        Assert.Equal(6, result.Gain);
        Assert.Equal(46, result.NewValue);
        Assert.Equal(46, Find(sheet, "Слух").Value);
        Assert.False(result.ReachedMastery);
        Assert.Equal(0, dice.Remaining);
    }

    [Theory]
    [Trait("page", "92")]
    [InlineData(40, 0, 4)] // 40 — не больше навыка
    [InlineData(95, 5, 9)] // 95 — не «выше 95»
    [InlineData(97, 5, 9)] // 95 при навыке 97
    public void RollSkillImprovement_RollNotAbove_NoChange(int value, int units, int tens)
    {
        var sheet = NewSheet(50, Skill("Слух", value));

        var result = DevelopmentPhaseRules.RollSkillImprovement(sheet, Catalog, Find(sheet, "Слух"), ScriptedDice.Of(units, tens));

        Assert.False(result.Improved);
        Assert.Equal(0, result.Gain);
        Assert.Equal(value, result.NewValue);
        Assert.Equal(value, Find(sheet, "Слух").Value);
    }

    /// <summary>Выше 95 навык растёт всегда — даже если уже больше 95, и может перейти 100%.</summary>
    [Fact]
    [Trait("page", "92")]
    public void RollSkillImprovement_RollAbove95_ImprovesHighSkill_PastHundred()
    {
        var sheet = NewSheet(50, Skill("Слух", 97));

        var result = DevelopmentPhaseRules.RollSkillImprovement(sheet, Catalog, Find(sheet, "Слух"), ScriptedDice.Of(6, 9, 5));

        Assert.True(result.Improved);
        Assert.Equal(102, result.NewValue);
        Assert.False(result.ReachedMastery); // 90 уже было
    }

    [Fact]
    [Trait("page", "92")]
    public void RollSkillImprovement_Roll100_AlwaysImproves()
    {
        var sheet = NewSheet(50, Skill("Слух", 88));

        var result = DevelopmentPhaseRules.RollSkillImprovement(sheet, Catalog, Find(sheet, "Слух"), ScriptedDice.Of(0, 0, 1));

        Assert.Equal(100, result.Roll);
        Assert.Equal(89, result.NewValue);
        Assert.False(result.ReachedMastery);
    }

    [Fact]
    [Trait("page", "92")]
    public void RollSkillImprovement_ReachingNinety_Grants2d6Sanity()
    {
        var sheet = NewSheet(50, Skill("Слух", 85));
        var dice = ScriptedDice.Of(0, 9, 5, 3, 4); // 90, +5, 2d6 = 3 + 4

        var result = DevelopmentPhaseRules.RollSkillImprovement(sheet, Catalog, Find(sheet, "Слух"), dice);

        Assert.Equal(90, result.NewValue);
        Assert.True(result.ReachedMastery);
        Assert.Equal(7, result.SanityGain);
        Assert.Equal(57, sheet.Current.Sanity);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    [Trait("page", "92")]
    public void RollSkillImprovement_MasterySanity_CappedBy99MinusMythos()
    {
        var sheet = WithMythos(30, 66, Skill("Слух", 89));

        var result = DevelopmentPhaseRules.RollSkillImprovement(sheet, Catalog, Find(sheet, "Слух"), ScriptedDice.Of(0, 9, 2, 6, 6));

        Assert.Equal(3, result.SanityGain);
        Assert.Equal(69, sheet.Current.Sanity);
    }

    /// <summary>Уклонение — обычный навык: боевого зеркала, которое надо синхронизировать, больше нет.</summary>
    [Fact]
    [Trait("page", "92")]
    public void RollSkillImprovement_Dodge_ReadByDerived()
    {
        var sheet = NewSheet(50, Skill(Dodge, 30));

        DevelopmentPhaseRules.RollSkillImprovement(sheet, Catalog, Find(sheet, Dodge), ScriptedDice.Of(0, 5, 4)); // 50, +4

        Assert.Equal(34, DerivedAttributeRules.Compute(sheet, Catalog).Dodge);
    }

    // ── Удача ───────────────────────────────────────────────────────────────

    [Theory]
    [Trait("page", "93")]
    [InlineData(50, new[] { 0, 5 }, false, 50)] // 50 — не больше Удачи
    [InlineData(50, new[] { 1, 5, 7 }, true, 57)]
    [InlineData(95, new[] { 7, 9, 9 }, true, 99)] // не выше 99
    public void RollLuckRecovery_AboveLuck_GainsD10UpTo99(int luck, int[] values, bool improved, int expected)
    {
        var sheet = NewSheet();
        sheet.Current.Luck = luck;

        var result = DevelopmentPhaseRules.RollLuckRecovery(sheet, ScriptedDice.Of(values));

        Assert.Equal(improved, result.Improved);
        Assert.Equal(expected, result.NewValue);
        Assert.Equal(expected - luck, result.Gain);
        Assert.Equal(expected, sheet.Current.Luck);
    }

    // ── Самолечение ─────────────────────────────────────────────────────────

    [Fact]
    [Trait("page", "165")]
    public void RollSelfHealing_Success_GainsD6()
    {
        var sheet = NewSheet(40);

        var result = DevelopmentPhaseRules.RollSelfHealing(sheet, Catalog, false, ScriptedDice.Of(0, 4, 4)); // 40 ≤ 40, 1d6 = 4

        Assert.True(result.Success);
        Assert.False(result.CriticalSuccess);
        Assert.Equal(4, result.SanityDelta);
        Assert.Equal(44, sheet.Current.Sanity);
    }

    [Fact]
    [Trait("page", "165")]
    public void RollSelfHealing_Failure_LosesOne_KeyConnectionUntouched()
    {
        var sheet = NewSheet(40);
        sheet.Biography.KeyConnection = "Сестра";

        var result = DevelopmentPhaseRules.RollSelfHealing(sheet, Catalog, false, ScriptedDice.Of(1, 4)); // 41

        Assert.False(result.Success);
        Assert.Equal(-1, result.SanityDelta);
        Assert.Equal(39, sheet.Current.Sanity);
        Assert.Equal("Сестра", sheet.Biography.KeyConnection);
        Assert.False(result.LostKeyConnection);
    }

    [Fact]
    [Trait("page", "165")]
    public void RollSelfHealing_KeyConnection_BonusDieAndCuresIndefinite()
    {
        var sheet = NewSheet(40);
        sheet.Condition.IndefiniteInsanity = true;
        sheet.Condition.IndefiniteInsanityStartedAt = DateTimeOffset.UtcNow;

        var result = DevelopmentPhaseRules.RollSelfHealing(sheet, Catalog, true, ScriptedDice.Of(5, 7, 2, 2)); // 75 и 25 → 25, 1d6 = 2

        Assert.Equal(25, result.Roll);
        Assert.True(result.Success);
        Assert.True(result.CuredIndefiniteInsanity);
        Assert.False(sheet.Condition.IndefiniteInsanity);
        Assert.Null(sheet.Condition.IndefiniteInsanityStartedAt);
        Assert.Equal(42, sheet.Current.Sanity);
    }

    [Fact]
    [Trait("page", "165")]
    public void RollSelfHealing_KeyConnectionFailure_LosesConnection()
    {
        var sheet = NewSheet(40);
        sheet.Biography.KeyConnection = "Сестра";

        var result = DevelopmentPhaseRules.RollSelfHealing(sheet, Catalog, true, ScriptedDice.Of(5, 7, 6)); // 75 и 65 → 65

        Assert.False(result.Success);
        Assert.True(result.LostKeyConnection);
        Assert.Equal("", sheet.Biography.KeyConnection);
        Assert.Equal(39, sheet.Current.Sanity);
    }

    [Fact]
    [Trait("page", "165")]
    public void RollSelfHealing_RollOne_Critical()
    {
        var result = DevelopmentPhaseRules.RollSelfHealing(NewSheet(40), Catalog, false, ScriptedDice.Of(1, 0, 3)); // 01, 1d6 = 3

        Assert.True(result.CriticalSuccess);
        Assert.False(result.CuredIndefiniteInsanity);
    }

    [Fact]
    [Trait("page", "165")]
    public void RollSelfHealing_FailureAtZero_DoesNotGoNegative()
    {
        var sheet = NewSheet(0);

        var result = DevelopmentPhaseRules.RollSelfHealing(sheet, Catalog, false, ScriptedDice.Of(5, 5));

        Assert.Equal(0, result.SanityDelta);
        Assert.Equal(0, sheet.Current.Sanity);
    }

    // ── Средства ────────────────────────────────────────────────────────────

    [Fact]
    [Trait("page", "94")]
    public void CreditRatingOptions_SevenFromBestToWorst()
    {
        Assert.Equal(
        [
            CreditRatingChange.Rich, CreditRatingChange.Promotion, CreditRatingChange.BusinessAsUsual,
            CreditRatingChange.TightenBelt, CreditRatingChange.SoldSilver, CreditRatingChange.RoughPatch,
            CreditRatingChange.Bankrupt,
        ], DevelopmentPhaseRules.CreditRatingOptions.Select(o => o.Change));
    }

    [Theory]
    [Trait("page", "94")]
    [InlineData(CreditRatingChange.Rich, new[] { 7 }, 7, 47)]
    [InlineData(CreditRatingChange.Promotion, new[] { 3 }, 3, 43)]
    [InlineData(CreditRatingChange.BusinessAsUsual, new int[0], 0, 40)]
    [InlineData(CreditRatingChange.TightenBelt, new[] { 5 }, 5, 35)]
    [InlineData(CreditRatingChange.SoldSilver, new[] { 10 }, 10, 30)]
    [InlineData(CreditRatingChange.RoughPatch, new[] { 4, 6 }, 10, 30)]
    [InlineData(CreditRatingChange.Bankrupt, new[] { 80 }, 80, 0)] // 1d100 — грань 1..100, а не d100
    public void ApplyCreditRatingChange_RollsAndClamps(CreditRatingChange change, int[] values, int roll, int expected)
    {
        var sheet = NewSheet(50, Skill(CreditRating, 40));
        var dice = ScriptedDice.Of(values);

        var result = DevelopmentPhaseRules.ApplyCreditRatingChange(sheet, Catalog, change, dice);

        Assert.Equal(roll, result.Roll);
        Assert.Equal(40, result.OldValue);
        Assert.Equal(expected, result.NewValue);
        Assert.Equal(expected, Find(sheet, CreditRating).Value);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    [Trait("page", "94")]
    public void ApplyCreditRatingChange_CappedAt99()
    {
        var sheet = NewSheet(50, Skill(CreditRating, 95));

        Assert.Equal(99, DevelopmentPhaseRules.ApplyCreditRatingChange(sheet, Catalog, CreditRatingChange.Rich, ScriptedDice.Of(10)).NewValue);
    }

    /// <summary>Строки Средств на листе нет — она заводится по справочнику; нет и в справочнике — не пишется.</summary>
    [Fact]
    [Trait("page", "94")]
    public void ApplyCreditRatingChange_NoRow_CreatesFromCatalog_OrWritesNothing()
    {
        var sheet = NewSheet();
        var result = DevelopmentPhaseRules.ApplyCreditRatingChange(sheet, Catalog, CreditRatingChange.Rich, ScriptedDice.Of(5));
        Assert.Equal((0, 5), (result.OldValue, result.NewValue));
        Assert.Equal(5, Find(sheet, CreditRating).Value);

        var bare = NewSheet();
        DevelopmentPhaseRules.ApplyCreditRatingChange(bare, CatalogWithoutMythosAndCredit, CreditRatingChange.Rich, ScriptedDice.Of(5));
        Assert.Empty(bare.Skills);
    }

    [Fact]
    [Trait("page", "94")]
    public void RecalculateFinances_AddsTierCashToRemaining()
    {
        var sheet = NewSheet(50, Skill(CreditRating, 20));
        sheet.Finances.Cash = 148;

        var result = DevelopmentPhaseRules.RecalculateFinances(sheet, Catalog, Era.Classic, creditRatingBefore: 20);

        Assert.Equal("Среднего класса", result.TierName);
        Assert.Equal(148m, result.PreviousCash);
        Assert.Equal(40m, result.TierCash);
        Assert.Equal(188m, sheet.Finances.Cash);
        Assert.Equal(10m, sheet.Finances.PocketMoney);
        Assert.Equal("1000", sheet.Finances.Assets);
    }

    /// <summary>В v1 наличные «всё проиграл» — текст, и пересчёт брал только столбец таблицы; в 2.0 это пустые наличные.</summary>
    [Fact]
    [Trait("page", "94")]
    public void RecalculateFinances_NoCash_TakesTierCashOnly()
    {
        var sheet = NewSheet(50, Skill(CreditRating, 0));

        var result = DevelopmentPhaseRules.RecalculateFinances(sheet, Catalog, Era.Classic, creditRatingBefore: 0);

        Assert.Null(result.PreviousCash);
        Assert.Equal(0.5m, sheet.Finances.Cash);
        Assert.Equal(0.5m, sheet.Finances.PocketMoney);
        Assert.Equal("нет", sheet.Finances.Assets);
    }

    [Fact]
    [Trait("page", "94")]
    public void RecalculateFinances_ModernSuperRich_AssetsWithPlus()
    {
        var sheet = NewSheet(50, Skill(CreditRating, 99));

        DevelopmentPhaseRules.RecalculateFinances(sheet, Catalog, Era.Modern, creditRatingBefore: 99);

        Assert.Equal(1000000m, sheet.Finances.Cash);
        Assert.Equal("100000000+", sheet.Finances.Assets);
    }

    /// <summary>
    /// F-S06 исправлена: в v1 «$1,500» читалось как 1,5 и фаза развития давала 41,50. Наличные 2.0 — число,
    /// 1 500 + 40 = 1 540.
    /// </summary>
    [Fact]
    [Trait("page", "94")]
    [Trait("finding", "F-S06")]
    public void RecalculateFinances_ThousandsAreThousands()
    {
        var sheet = NewSheet(50, Skill(CreditRating, 20));
        sheet.Finances.Cash = 1500;

        DevelopmentPhaseRules.RecalculateFinances(sheet, Catalog, Era.Classic, creditRatingBefore: 20);

        Assert.Equal(1540m, sheet.Finances.Cash);
    }

    /// <summary>
    /// Пример с Харви (стр. 94): Средства 41 → 34, достаток тот же — к 80 долларам прибавляется 68, активы остаются 50.
    /// До 2.0.x пересчёт всегда ставил активы и карманные из таблицы и затирал «дом в Аркхеме».
    /// </summary>
    [Fact]
    [Trait("page", "94")]
    public void RecalculateFinances_SameTier_KeepsAssetsAndPocketMoney()
    {
        var sheet = NewSheet(50, Skill(CreditRating, 34));
        sheet.Finances.Cash = 80;
        sheet.Finances.Assets = "50";
        sheet.Finances.PocketMoney = 12;

        var result = DevelopmentPhaseRules.RecalculateFinances(sheet, Catalog, Era.Classic, creditRatingBefore: 41);

        Assert.False(result.TierChanged);
        Assert.Equal(148m, sheet.Finances.Cash);
        Assert.Equal("50", sheet.Finances.Assets);
        Assert.Equal(12m, sheet.Finances.PocketMoney);
    }

    /// <summary>Достаток сменился (41 → 57, «Я богат!») — активы и карманные по новой строке таблицы.</summary>
    [Fact]
    [Trait("page", "94")]
    public void RecalculateFinances_TierChanged_RecalculatesAssets()
    {
        var sheet = NewSheet(50, Skill(CreditRating, 57));
        sheet.Finances.Assets = "дом в Аркхеме";
        sheet.Finances.PocketMoney = 10;
        var tier = FinanceRules.GetTier(57, Era.Classic);

        var result = DevelopmentPhaseRules.RecalculateFinances(sheet, Catalog, Era.Classic, creditRatingBefore: 41);

        Assert.True(result.TierChanged);
        Assert.Equal(tier.AssetsText, sheet.Finances.Assets);
        Assert.Equal(tier.PocketMoney, sheet.Finances.PocketMoney);
    }

    // ── Привыкание ──────────────────────────────────────────────────────────

    [Fact]
    [Trait("page", "167")]
    public void RelaxHabituations_DecrementsPositiveOnly()
    {
        var sheet = NewSheet();
        sheet.Condition.Habituations =
        [
            new() { CreatureName = "Глубоководный", MaxLoss = 6, LostSanity = 3 },
            new() { CreatureName = "Гуль", MaxLoss = 6, LostSanity = 0 },
            new() { CreatureName = "Шоггот", MaxLoss = 6, LostSanity = 1 },
        ];

        Assert.Equal(2, DevelopmentPhaseRules.RelaxHabituations(sheet));
        Assert.Equal([2, 0, 0], sheet.Condition.Habituations.Select(h => h.LostSanity));
    }
}
