using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Rules.Tests.Combat;

/// <summary>
///     Проверка Рассудка в бою (<see cref="CombatService.ResolveSanityCheck" />) и последствия потери —
///     <c>EvaluateSanityLoss</c>, общий с ценой заклинания (приватный, проверяется через проверку).
/// </summary>
public sealed class SanityCheckTests
{
    private readonly Combatant _investigator = Fighters.Investigator("Сыщик", sanity: 50, intelligence: 60);
    private readonly CombatService _combat;

    public SanityCheckTests() => _combat = Fighters.Battle(_investigator);

    private SanityCheckSetup Check(int roll, string success = "1", string failure = "1D6") => new()
    {
        TargetId = _investigator.Id,
        ManualRoll = roll,
        SuccessLoss = success,
        FailureLoss = failure,
        ManualIntRoll = 99,
        ManualInsanityDurationRoll = 5
    };

    [Fact]
    [Trait("page", "152")]
    public void ResolveSanityCheck_Success_SuccessLoss()
    {
        var result = _combat.ResolveSanityCheck(Check(30));

        Assert.Equal(SuccessLevel.RegularSuccess, result.AttackerSuccessLevel);
        Assert.Equal(1, result.SanityLoss);
        Assert.Equal(49, result.SanityAfter);
        Assert.Equal(50, result.SanityBefore);
    }

    [Fact]
    [Trait("page", "152")]
    public void ResolveSanityCheck_Failure_RollsFailureLoss()
    {
        using var _ = ScriptedRandom.Use(4);

        var result = _combat.ResolveSanityCheck(Check(70));

        Assert.Equal(4, result.SanityLoss);
        Assert.Equal(46, result.SanityAfter);
        Assert.False(result.SanityFumble);
    }

    [Fact]
    [Trait("page", "152")]
    public void ResolveSanityCheck_NoManualRoll_PlainD100_NoExtraDice()
    {
        // Бонусные и штрафные кости к Рассудку не применяются: один бросок 1..100
        var setup = Check(0);
        setup.ManualRoll = null;
        using var dice = ScriptedRandom.Use(30);

        var result = _combat.ResolveSanityCheck(setup);

        Assert.Equal(30, result.AttackerRoll);
        Assert.Equal(0, dice.Remaining);
    }

    [Theory]
    [Trait("page", "153")]
    [InlineData(100, 50)]
    [InlineData(96, 40)] // рассудок ниже 50 — крах уже на 96
    public void ResolveSanityCheck_Fumble_MaximumLoss(int roll, int sanity)
    {
        _investigator.CurrentSanity = sanity;

        var result = _combat.ResolveSanityCheck(Check(roll, failure: "1D6+1"));

        Assert.True(result.SanityFumble);
        Assert.Equal(7, result.SanityLoss);
        Assert.Contains("Крах — максимальная потеря: 7 ед.", result.Summary);
    }

    [Fact]
    [Trait("page", "153")]
    [Trait("finding", "F-C01")]
    public void ResolveSanityCheck_CyrillicFormula_LosesNothing()
    {
        var result = _combat.ResolveSanityCheck(Check(70, failure: "1д6"));

        Assert.Equal(0, result.SanityLoss);
        Assert.Equal(50, result.SanityAfter);
    }

    [Theory]
    [Trait("page", "153")]
    [InlineData(60, true)] // ИНТ пройдена — сыщик осознал ужас, безумие
    [InlineData(61, false)]
    public void FiveOrMoreLost_IntCheck_SuccessMeansInsanity(int intRoll, bool insane)
    {
        var setup = Check(70, failure: "5");
        setup.ManualIntRoll = intRoll;

        var result = _combat.ResolveSanityCheck(setup);

        Assert.Equal(intRoll, result.IntelligenceRoll);
        Assert.Equal(insane, result.TriggeredTemporaryInsanity);
        Assert.Equal(insane ? 5 : (int?)null, result.TemporaryInsanityHours);
    }

    [Fact]
    [Trait("page", "153")]
    public void FourLost_NoIntCheck()
    {
        var result = _combat.ResolveSanityCheck(Check(70, failure: "4"));

        Assert.Null(result.IntelligenceRoll);
        Assert.Null(result.TriggeredTemporaryInsanity);
    }

    [Fact]
    [Trait("page", "153")]
    public void FiveOrMoreLost_DurationRolledOnD10()
    {
        var setup = Check(70, failure: "5");
        setup.ManualIntRoll = 10;
        setup.ManualInsanityDurationRoll = null;
        using var _ = ScriptedRandom.Use(8);

        var result = _combat.ResolveSanityCheck(setup);

        Assert.Equal(8, result.TemporaryInsanityHours);
    }

    [Theory]
    [Trait("page", "154")]
    // Пятая часть рассудка на начало дня: 20 → порог 4
    [InlineData(20, 0, "4", true)]
    [InlineData(20, 0, "3", false)]
    [InlineData(17, 3, "1", true)] // за день 3 + 1 = 4 из 20
    [InlineData(21, 0, "4", false)] // 21 → порог (21 + 4) / 5 = 5
    public void IndefiniteInsanity_FifthOfDayStartSanity(int sanity, int lostToday, string loss, bool indefinite)
    {
        _investigator.CurrentSanity = sanity;
        _investigator.SanityLostToday = lostToday;

        var result = _combat.ResolveSanityCheck(Check(99, failure: loss));

        Assert.Equal(indefinite, result.TriggeredIndefiniteInsanity);
        Assert.Equal(lostToday + int.Parse(loss), result.SanityLostToday);
    }

    [Fact]
    [Trait("page", "154")]
    public void IndefiniteInsanity_AlreadyInsane_NotTriggeredAgain()
    {
        _investigator.CurrentSanity = 20;
        _investigator.HasIndefiniteInsanity = true;

        var result = _combat.ResolveSanityCheck(Check(99, failure: "4"));

        Assert.False(result.TriggeredIndefiniteInsanity);
    }

    [Fact]
    [Trait("page", "154")]
    public void ZeroSanity_PermanentInsanity()
    {
        _investigator.CurrentSanity = 3;

        var result = _combat.ResolveSanityCheck(Check(99, failure: "4"));

        Assert.Equal(0, result.SanityAfter);
        Assert.True(result.TriggeredPermanentInsanity);
    }

    [Fact]
    [Trait("page", "167")]
    public void Habituation_CapsLossAtRemaining_ByNameIgnoringCase()
    {
        _investigator.CharacterSource!.State.MythosHabituations.Add(
            new MythosHabituation { CreatureName = "Глубоководный", MaxLoss = 6, LostSanity = 4 });
        var setup = Check(70);
        setup.SourceCreatureName = "глубоководный";
        using var _ = ScriptedRandom.Use(5);

        var result = _combat.ResolveSanityCheck(setup);

        Assert.True(result.SanityCappedByHabituation);
        Assert.Equal(2, result.SanityLoss);
        Assert.Contains("потеря урезана с 5 до 2", result.Summary);
    }

    [Fact]
    [Trait("page", "167")]
    public void Habituation_LimitReached_NoLoss()
    {
        _investigator.CharacterSource!.State.MythosHabituations.Add(
            new MythosHabituation { CreatureName = "Глубоководный", MaxLoss = 6, LostSanity = 6 });
        var setup = Check(70);
        setup.SourceCreatureName = "Глубоководный";
        using var _ = ScriptedRandom.Use(5);

        var result = _combat.ResolveSanityCheck(setup);

        Assert.Equal(0, result.SanityLoss);
        Assert.Contains("сыщик привык", result.Summary);
    }

    [Fact]
    [Trait("page", "167")]
    public void Habituation_NoSheet_NoCap()
    {
        var creature = Fighters.Make("Существо");
        _combat.AddCombatant(creature);
        var setup = Check(70);
        setup.TargetId = creature.Id;
        setup.SourceCreatureName = "Глубоководный";
        using var _ = ScriptedRandom.Use(5);

        var result = _combat.ResolveSanityCheck(setup);

        Assert.Equal(5, result.SanityLoss);
        Assert.False(result.SanityCappedByHabituation);
    }

    [Fact]
    [Trait("page", "153-154")]
    [Trait("page", "167")]
    public void ApplyResult_SanityCheck_MovesToCombatantAndSheet_RecordsHabituation()
    {
        var setup = Check(70, failure: "5");
        setup.ManualIntRoll = 10;
        setup.SourceCreatureName = "Глубоководный";
        setup.SourceSanityLossFormula = "0/1D6";

        _combat.ApplyResult(_combat.ResolveSanityCheck(setup));

        var sheet = _investigator.CharacterSource!;
        Assert.Equal(45, _investigator.CurrentSanity);
        Assert.Equal(5, _investigator.SanityLostToday);
        Assert.True(_investigator.HasTemporaryInsanity);
        Assert.Equal(5, _investigator.TemporaryInsanityHours);
        Assert.Equal(45, sheet.DerivedAttributes.Sanity.Value);
        Assert.Equal(5, sheet.State.SanityLossEpisode);
        Assert.True(sheet.State.HasTemporaryInsanity);
        var habituation = Assert.Single(sheet.State.MythosHabituations);
        Assert.Equal("Глубоководный", habituation.CreatureName);
        Assert.Equal(6, habituation.MaxLoss);
        Assert.Equal(5, habituation.LostSanity);
    }

    /// <summary>Предел привыкания из «0/1д6» — 6, а потеря из той же «1д6» — 0.</summary>
    [Fact]
    [Trait("page", "167")]
    [Trait("finding", "F-C01")]
    public void ApplyResult_CyrillicFormula_LimitSixButLossZero()
    {
        var setup = Check(70, failure: "1д6");
        setup.SourceCreatureName = "Глубоководный";
        setup.SourceSanityLossFormula = "0/1д6";

        _combat.ApplyResult(_combat.ResolveSanityCheck(setup));

        var habituation = Assert.Single(_investigator.CharacterSource!.State.MythosHabituations);
        Assert.Equal(6, habituation.MaxLoss);
        Assert.Equal(0, habituation.LostSanity);
    }
}
