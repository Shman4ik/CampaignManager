using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Characters.Services;
using static CampaignManager.Rules.Tests.Sheet.Sheets;

namespace CampaignManager.Rules.Tests.Sheet;

/// <summary>
///     Фаза развития с фиксированными костями. d100 в <see cref="ScriptedRandom" /> — сначала
///     единицы, потом десятки: 41 = (1, 4), 100 = (0, 0).
/// </summary>
public sealed class DevelopmentPhaseRulesTests
{
    // ── Отметки ─────────────────────────────────────────────────────────────

    [Theory]
    [Trait("page", "92")]
    [InlineData("Мифы Ктулху", false)]
    [InlineData("Средства", false)]
    [InlineData("Внимание", true)]
    [InlineData("мифы ктулху", true)] // сравнение точное
    public void CanBeChecked_MythosAndCreditRatingNever(string name, bool expected) =>
        Assert.Equal(expected, DevelopmentPhaseRules.CanBeChecked(name));

    [Fact]
    [Trait("page", "92")]
    public void GetCheckedSkills_OnlyMarked_ExcludingMythosAndCredit()
    {
        var character = Character(50,
            Used(Skill("Слух", 20)), Used(Skill("Внимание", 25)), Skill("Психология", 10),
            Used(Skill(Mythos, 5)), Used(Skill(CreditRating, 30)));

        Assert.Equal(new[] { "Внимание", "Слух" }, DevelopmentPhaseRules.GetCheckedSkills(character).Select(s => s.Name));
    }

    [Fact]
    [Trait("page", "92")]
    public void ClearSkillChecks_UnmarksEverything()
    {
        var character = Character(50, Used(Skill("Слух", 20)), Used(Skill(Mythos, 5)));

        DevelopmentPhaseRules.ClearSkillChecks(character);

        Assert.All(character.Skills.SkillGroups.SelectMany(g => g.Skills), s => Assert.False(s.IsUsed));
    }

    // ── Проверка опыта ──────────────────────────────────────────────────────

    [Fact]
    [Trait("page", "92")]
    public void RollSkillImprovement_RollAboveSkill_GainsD10()
    {
        var character = Character(50, Skill("Слух", 40));
        using var dice = ScriptedRandom.Use(1, 4, 6); // 41, затем 1d10 = 6

        var result = DevelopmentPhaseRules.RollSkillImprovement(character, Find(character, "Слух"));

        Assert.True(result.Improved);
        Assert.Equal(41, result.Roll);
        Assert.Equal(6, result.Gain);
        Assert.Equal(46, result.NewValue);
        Assert.Equal(46, Find(character, "Слух").Value.Regular);
        Assert.Equal(23, Find(character, "Слух").Value.Half);
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
        var character = Character(50, Skill("Слух", value));
        using var dice = ScriptedRandom.Use(units, tens);

        var result = DevelopmentPhaseRules.RollSkillImprovement(character, Find(character, "Слух"));

        Assert.False(result.Improved);
        Assert.Equal(0, result.Gain);
        Assert.Equal(value, result.NewValue);
        Assert.Equal(value, Find(character, "Слух").Value.Regular);
    }

    /// <summary>Выше 95 навык растёт всегда — даже если уже больше 95, и может перейти 100%.</summary>
    [Fact]
    [Trait("page", "92")]
    public void RollSkillImprovement_RollAbove95_ImprovesHighSkill_PastHundred()
    {
        var character = Character(50, Skill("Слух", 97));
        using var dice = ScriptedRandom.Use(6, 9, 5); // 96, +5

        var result = DevelopmentPhaseRules.RollSkillImprovement(character, Find(character, "Слух"));

        Assert.True(result.Improved);
        Assert.Equal(102, result.NewValue);
        Assert.False(result.ReachedMastery); // 90 уже было
    }

    [Fact]
    [Trait("page", "92")]
    public void RollSkillImprovement_Roll100_AlwaysImproves()
    {
        var character = Character(50, Skill("Слух", 88));
        using var dice = ScriptedRandom.Use(0, 0, 1); // 100, +1

        var result = DevelopmentPhaseRules.RollSkillImprovement(character, Find(character, "Слух"));

        Assert.Equal(100, result.Roll);
        Assert.Equal(89, result.NewValue);
        Assert.False(result.ReachedMastery);
    }

    [Fact]
    [Trait("page", "92")]
    public void RollSkillImprovement_ReachingNinety_Grants2d6Sanity()
    {
        var character = Character(50, Skill("Слух", 85));
        using var dice = ScriptedRandom.Use(0, 9, 5, 3, 4); // 90, +5, 2d6 = 3 + 4

        var result = DevelopmentPhaseRules.RollSkillImprovement(character, Find(character, "Слух"));

        Assert.Equal(90, result.NewValue);
        Assert.True(result.ReachedMastery);
        Assert.Equal(7, result.SanityGain);
        Assert.Equal(57, character.DerivedAttributes.Sanity.Value);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    [Trait("page", "92")]
    public void RollSkillImprovement_MasterySanity_CappedBy99MinusMythos()
    {
        var character = WithMythos(30, 66, Skill("Слух", 89));
        using var dice = ScriptedRandom.Use(0, 9, 2, 6, 6); // 90, +2, 2d6 = 12

        var result = DevelopmentPhaseRules.RollSkillImprovement(character, Find(character, "Слух"));

        Assert.Equal(3, result.SanityGain);
        Assert.Equal(69, character.DerivedAttributes.Sanity.Value);
        Assert.Equal(69, character.DerivedAttributes.Sanity.MaxValue);
    }

    [Fact]
    [Trait("page", "92")]
    public void RollSkillImprovement_Dodge_SyncsCombatField()
    {
        var character = Character(50, Skill(Dodge, 30));
        character.PersonalInfo.Dodge = 30;
        using var dice = ScriptedRandom.Use(0, 5, 4); // 50, +4

        DevelopmentPhaseRules.RollSkillImprovement(character, Find(character, Dodge));

        Assert.Equal(34, character.PersonalInfo.Dodge);
    }

    // ── Удача ───────────────────────────────────────────────────────────────

    [Theory]
    [Trait("page", "93")]
    [InlineData(50, new[] { 0, 5 }, false, 50)] // 50 — не больше Удачи
    [InlineData(50, new[] { 1, 5, 7 }, true, 57)]
    [InlineData(95, new[] { 7, 9, 9 }, true, 99)] // не выше 99
    public void RollLuckRecovery_AboveLuck_GainsD10UpTo99(int luck, int[] values, bool improved, int expected)
    {
        var character = Character();
        character.DerivedAttributes.Luck = new AttributeWithMaxValue(luck, 60);
        using var dice = ScriptedRandom.Use(values);

        var result = DevelopmentPhaseRules.RollLuckRecovery(character);

        Assert.Equal(improved, result.Improved);
        Assert.Equal(expected, result.NewValue);
        Assert.Equal(expected - luck, result.Gain);
        Assert.Equal(expected, character.DerivedAttributes.Luck.Value);
        Assert.Equal(improved ? 99 : 60, character.DerivedAttributes.Luck.MaxValue);
    }

    // ── Рассудок ────────────────────────────────────────────────────────────

    [Theory]
    [Trait("page", "164-165")]
    [InlineData(0, 40, 5, 45, 5)]
    [InlineData(30, 60, 20, 69, 9)]
    [InlineData(0, 40, -5, 35, -5)] // отрицательную прибавку не отсекает
    public void GrantSanity_CappedBy99MinusMythos(int mythos, int sanity, int amount, int expected, int actual)
    {
        var character = WithMythos(mythos, sanity);

        Assert.Equal(actual, DevelopmentPhaseRules.GrantSanity(character, amount));
        Assert.Equal(expected, character.DerivedAttributes.Sanity.Value);
        Assert.Equal(99 - mythos, character.DerivedAttributes.Sanity.MaxValue);
    }

    [Fact]
    [Trait("page", "165")]
    public void RollSelfHealing_Success_GainsD6()
    {
        var character = Character(40);
        using var dice = ScriptedRandom.Use(0, 4, 4); // 40 ≤ 40, 1d6 = 4

        var result = DevelopmentPhaseRules.RollSelfHealing(character, useKeyConnection: false);

        Assert.True(result.Success);
        Assert.False(result.CriticalSuccess);
        Assert.Equal(4, result.SanityDelta);
        Assert.Equal(44, character.DerivedAttributes.Sanity.Value);
    }

    [Fact]
    [Trait("page", "165")]
    public void RollSelfHealing_Failure_LosesOne_KeyConnectionUntouched()
    {
        var character = Character(40);
        character.Biography.KeyConnection = "Сестра";
        using var dice = ScriptedRandom.Use(1, 4); // 41

        var result = DevelopmentPhaseRules.RollSelfHealing(character, useKeyConnection: false);

        Assert.False(result.Success);
        Assert.Equal(-1, result.SanityDelta);
        Assert.Equal(39, character.DerivedAttributes.Sanity.Value);
        Assert.Equal("Сестра", character.Biography.KeyConnection);
        Assert.False(result.LostKeyConnection);
    }

    [Fact]
    [Trait("page", "165")]
    public void RollSelfHealing_KeyConnection_BonusDieAndCuresIndefinite()
    {
        var character = Character(40);
        character.State.HasIndefiniteInsanity = true;
        character.State.IndefiniteInsanityStartedAt = DateTime.UtcNow;
        using var dice = ScriptedRandom.Use(5, 7, 2, 2); // 75 и 25 → 25, 1d6 = 2

        var result = DevelopmentPhaseRules.RollSelfHealing(character, useKeyConnection: true);

        Assert.Equal(25, result.Roll);
        Assert.True(result.Success);
        Assert.True(result.CuredIndefiniteInsanity);
        Assert.False(character.State.HasIndefiniteInsanity);
        Assert.Null(character.State.IndefiniteInsanityStartedAt);
        Assert.Equal(42, character.DerivedAttributes.Sanity.Value);
    }

    [Fact]
    [Trait("page", "165")]
    public void RollSelfHealing_KeyConnectionFailure_LosesConnection()
    {
        var character = Character(40);
        character.Biography.KeyConnection = "Сестра";
        using var dice = ScriptedRandom.Use(5, 7, 6); // 75 и 65 → 65

        var result = DevelopmentPhaseRules.RollSelfHealing(character, useKeyConnection: true);

        Assert.False(result.Success);
        Assert.True(result.LostKeyConnection);
        Assert.Equal("", character.Biography.KeyConnection);
        Assert.Equal(39, character.DerivedAttributes.Sanity.Value);
    }

    [Fact]
    [Trait("page", "165")]
    public void RollSelfHealing_RollOne_Critical()
    {
        var character = Character(40);
        using var dice = ScriptedRandom.Use(1, 0, 3); // 01, 1d6 = 3

        var result = DevelopmentPhaseRules.RollSelfHealing(character, useKeyConnection: false);

        Assert.True(result.CriticalSuccess);
        Assert.False(result.CuredIndefiniteInsanity);
    }

    [Fact]
    [Trait("page", "165")]
    public void RollSelfHealing_FailureAtZero_DoesNotGoNegative()
    {
        var character = Character(0);
        using var dice = ScriptedRandom.Use(5, 5);

        var result = DevelopmentPhaseRules.RollSelfHealing(character, useKeyConnection: false);

        Assert.Equal(0, result.SanityDelta);
        Assert.Equal(0, character.DerivedAttributes.Sanity.Value);
    }

    // ── Средства ────────────────────────────────────────────────────────────

    [Fact]
    [Trait("page", "94")]
    public void CreditRatingOptions_SevenFromBestToWorst()
    {
        Assert.Equal(
            new[]
            {
                CreditRatingChange.Rich, CreditRatingChange.Promotion, CreditRatingChange.BusinessAsUsual,
                CreditRatingChange.TightenBelt, CreditRatingChange.SoldSilver, CreditRatingChange.RoughPatch,
                CreditRatingChange.Bankrupt
            },
            DevelopmentPhaseRules.CreditRatingOptions.Select(o => o.Change));
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
        var character = Character(50, Skill(CreditRating, 40));
        using var dice = ScriptedRandom.Use(values);

        var result = DevelopmentPhaseRules.ApplyCreditRatingChange(character, change);

        Assert.Equal(roll, result.Roll);
        Assert.Equal(40, result.OldValue);
        Assert.Equal(expected, result.NewValue);
        Assert.Equal(expected, Find(character, CreditRating).Value.Regular);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    [Trait("page", "94")]
    public void ApplyCreditRatingChange_CappedAt99()
    {
        var character = Character(50, Skill(CreditRating, 95));
        using var dice = ScriptedRandom.Use(10);

        Assert.Equal(99, DevelopmentPhaseRules.ApplyCreditRatingChange(character, CreditRatingChange.Rich).NewValue);
    }

    [Fact]
    [Trait("page", "94")]
    public void ApplyCreditRatingChange_NoSkill_ReportsNewValueButWritesNothing()
    {
        var character = Character();
        using var dice = ScriptedRandom.Use(5);

        var result = DevelopmentPhaseRules.ApplyCreditRatingChange(character, CreditRatingChange.Rich);

        Assert.Equal(0, result.OldValue);
        Assert.Equal(5, result.NewValue);
        Assert.Empty(character.Skills.SkillGroups.SelectMany(g => g.Skills));
    }

    [Fact]
    [Trait("page", "94")]
    public void RecalculateFinances_AddsTierCashToRemaining()
    {
        var character = Character(50, Skill(CreditRating, 20));
        character.Finances.Cash = "$148";

        var result = DevelopmentPhaseRules.RecalculateFinances(character, isModern: false);

        Assert.Equal("Среднего класса", result.TierName);
        Assert.Equal(148m, result.PreviousCash);
        Assert.Equal(40m, result.TierCash);
        Assert.Equal("$188", character.Finances.Cash);
        Assert.Equal("$10", character.Finances.PocketMoney);
        Assert.Equal(new[] { "$1000" }, character.Finances.Assets);
    }

    [Fact]
    [Trait("page", "94")]
    public void RecalculateFinances_CashIsText_TakesTierCashOnly()
    {
        var character = Character(50, Skill(CreditRating, 0));
        character.Finances.Cash = "всё проиграл";

        var result = DevelopmentPhaseRules.RecalculateFinances(character, isModern: false);

        Assert.Null(result.PreviousCash);
        Assert.Equal("$0.50", character.Finances.Cash);
        Assert.Equal("$0.50", character.Finances.PocketMoney);
        Assert.Equal(new[] { "нет" }, character.Finances.Assets);
    }

    [Fact]
    [Trait("page", "94")]
    public void RecalculateFinances_ModernSuperRich_AssetsWithPlus()
    {
        var character = Character(50, Skill(CreditRating, 99));

        DevelopmentPhaseRules.RecalculateFinances(character, isModern: true);

        Assert.Equal("$1000000", character.Finances.Cash);
        Assert.Equal(new[] { "$100000000+" }, character.Finances.Assets);
    }

    /// <summary>Запятая-разделитель тысяч в наличных читается как десятичная — 1 500 превращаются в 1,5.</summary>
    [Fact]
    [Trait("page", "94")]
    [Trait("finding", "F-S06")]
    public void RecalculateFinances_ThousandsComma_ReadAsDecimal()
    {
        var character = Character(50, Skill(CreditRating, 20));
        character.Finances.Cash = "$1,500";

        DevelopmentPhaseRules.RecalculateFinances(character, isModern: false);

        Assert.Equal("$41.50", character.Finances.Cash);
    }

    // ── Привыкание ──────────────────────────────────────────────────────────

    [Fact]
    [Trait("page", "167")]
    public void RelaxHabituations_DecrementsPositiveOnly()
    {
        var character = Character();
        character.State.MythosHabituations =
        [
            new() { CreatureName = "Глубоководный", MaxLoss = 6, LostSanity = 3 },
            new() { CreatureName = "Гуль", MaxLoss = 6, LostSanity = 0 },
            new() { CreatureName = "Шоггот", MaxLoss = 6, LostSanity = 1 }
        ];

        Assert.Equal(2, DevelopmentPhaseRules.RelaxHabituations(character));
        Assert.Equal(new[] { 2, 0, 0 }, character.State.MythosHabituations.Select(h => h.LostSanity));
    }

    private static Skill Used(Skill skill)
    {
        skill.IsUsed = true;
        return skill;
    }
}
