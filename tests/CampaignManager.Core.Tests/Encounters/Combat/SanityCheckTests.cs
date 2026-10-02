using CampaignManager.Core.Characters;
using CampaignManager.Core.Dice;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Tests.Infrastructure;
using static CampaignManager.Core.Tests.Encounters.Combat.Fighters;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Encounters.Combat;

/// <summary>
/// Проверка Рассудка в бою (перенесено из T0.2 <c>SanityCheckTests</c>). Пороги ⅕ за день и ноль в 2.0 считает лист
/// (<c>SanityRules</c>, свои тесты), привыкание — тоже лист (<c>HabituationRules</c>): здесь — бросок, потеря, ИНТ и запись в лист.
/// </summary>
public sealed class SanityCheckTests
{
    private readonly EncounterParticipant _investigator = Investigator("Сыщик", sanity: 50, @int: 60);
    private readonly EncounterState _state;

    public SanityCheckTests() => _state = Battle(_investigator);

    private SanityCheckSetup Check(int roll, string success = "1", string failure = "1D6") => new()
    {
        TargetId = _investigator.Id, Roll = Rolled(roll), SuccessLoss = success, FailureLoss = failure, IntRoll = Rolled(99),
    };

    private EncounterEffect? Loss(EncounterResolution resolution) => resolution.Effects.SingleOrDefault(e => e.Kind == EncounterEffectKind.SanityLoss);

    [Fact]
    [Trait("page", "152")]
    public void Success_SuccessLoss()
    {
        var resolution = CombatRules.SanityCheck(_state, Check(30), ScriptedDice.Of());

        Assert.Equal(1, Loss(resolution)!.Amount);
        Assert.Contains("обычный успех", resolution.Title);
    }

    [Fact]
    [Trait("page", "152")]
    public void Failure_RollsFailureLoss()
    {
        var resolution = CombatRules.SanityCheck(_state, Check(70), ScriptedDice.Of(4));
        Apply(_state, resolution);

        Assert.Equal(4, Loss(resolution)!.Amount);
        Assert.Equal(46, _investigator.Sanity);
    }

    [Fact]
    [Trait("page", "152")]
    public void NoEnteredRoll_PlainD100_NoExtraDice()
    {
        // Бонусные и штрафные кости к Рассудку не применяются: одна кость единиц и одна десятков
        var dice = ScriptedDice.Of(0, 3);

        var resolution = CombatRules.SanityCheck(_state, Check(0) with { Roll = null }, dice);

        Assert.Contains("30 против 50", resolution.Lines[0]);
        Assert.Equal(0, dice.Remaining);
    }

    [Theory]
    [Trait("page", "153")]
    [InlineData(100, 50)]
    [InlineData(96, 40)] // рассудок ниже 50 — крах уже на 96
    public void Fumble_MaximumLoss(int roll, int sanity)
    {
        _investigator.Sanity = sanity;

        var resolution = CombatRules.SanityCheck(_state, Check(roll, failure: "1D6+1") with { IntRoll = Rolled(99) }, ScriptedDice.Of());

        Assert.Equal(7, Loss(resolution)!.Amount);
        Assert.Contains("Крах — максимальная потеря", resolution.Lines[1]);
    }

    /// <summary>F-C01 исправлено (T1.7): «1д6» бросается как 1d6, а не теряет 0.</summary>
    [Fact]
    [Trait("page", "153")]
    [Trait("finding", "F-C01")]
    public void CyrillicFormula_Rolls()
    {
        var resolution = CombatRules.SanityCheck(_state, Check(70, failure: "1д6"), ScriptedDice.Of(3));

        Assert.Equal(3, Loss(resolution)!.Amount);
    }

    [Theory]
    [Trait("page", "153")]
    [InlineData(60, true)] // ИНТ пройдена — сыщик осознал ужас, безумие
    [InlineData(61, false)]
    public void FiveOrMoreLost_IntCheck_SuccessMeansInsanity(int intRoll, bool insane)
    {
        var resolution = CombatRules.SanityCheck(_state, Check(70, failure: "5") with { IntRoll = Rolled(intRoll) }, ScriptedDice.Of());

        Assert.Equal(insane, Loss(resolution)!.Check);
    }

    [Fact]
    [Trait("page", "153")]
    public void FourLost_NoIntCheck() =>
        Assert.Null(Loss(CombatRules.SanityCheck(_state, Check(70, failure: "4"), ScriptedDice.Of()))!.Check);

    /// <summary>F-C06 исправлено: ИНТ — через <c>Check</c>: 100 — крах даже при ИНТ 100, 01 — успех даже при нуле.</summary>
    [Theory]
    [Trait("page", "87")]
    [Trait("finding", "F-C06")]
    [InlineData(100, 100, false)]
    [InlineData(0, 1, true)]
    public void IntRoll_ThroughCheck(int intelligence, int intRoll, bool realized)
    {
        _investigator.Stats.Int = intelligence;

        var resolution = CombatRules.SanityCheck(_state, Check(70, failure: "5") with { IntRoll = Rolled(intRoll) }, ScriptedDice.Of());

        Assert.Equal(realized, Loss(resolution)!.Check);
    }

    [Fact]
    [Trait("page", "154")]
    public void ZeroSanity_PermanentInsanityLine()
    {
        _investigator.Sanity = 3;

        var resolution = CombatRules.SanityCheck(_state, Check(99, failure: "4"), ScriptedDice.Of());

        Assert.Contains(resolution.Lines, l => l.Contains("неизлечимое безумие", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("page", "152")]
    public void Creature_NoSanity_NothingToCheck()
    {
        var creature = Make("Глубоководный", side: EncounterSide.Enemies);
        EncounterEngine.Add(_state, creature, Now);

        var resolution = CombatRules.SanityCheck(_state, Check(70) with { TargetId = creature.Id }, ScriptedDice.Of());

        Assert.Empty(resolution.Effects);
    }

    /// <summary>
    /// Запись в лист: потеря одной причиной, привыкание к виду твари (стр. 167) и исход ИНТ — правилами листа. Против v1:
    /// итог боя в лист не попадал вовсе.
    /// </summary>
    [Fact]
    [Trait("page", "153")]
    [Trait("page", "167")]
    public void SheetGetsLoss_Habituation_AndIntOutcome()
    {
        var creature = Make("Глубоководный", side: EncounterSide.Enemies);
        creature.Kind = ParticipantKind.Creature;
        creature.SanityLoss = "0/1D6";
        EncounterEngine.Add(_state, creature, Now);

        var resolution = CombatRules.SanityCheck(_state,
            Check(70, failure: "5") with { CreatureParticipantId = creature.Id, IntRoll = Rolled(10) }, ScriptedDice.Of());
        var sheet = NewSheet(50);

        EncounterSheetEffects.Apply(sheet, Catalog, resolution.Effects);

        Assert.Equal(45, sheet.Current.Sanity);
        Assert.True(sheet.Condition.TemporaryInsanity);
        Assert.True(sheet.Condition.BoutDue);
        var habituation = Assert.Single(sheet.Condition.Habituations);
        Assert.Equal("Глубоководный", habituation.CreatureName);
        Assert.Equal(6, habituation.MaxLoss);
        Assert.Equal(5, habituation.LostSanity);
    }

    [Fact]
    [Trait("page", "167")]
    public void SheetHabituationCapsLossBelowFive_NoInsanity()
    {
        var sheet = NewSheet(50);
        sheet.Condition.Habituations.Add(new MythosHabituation { CreatureName = "Глубоководный", MaxLoss = 6, LostSanity = 4 });
        var effect = new EncounterEffect
        {
            Kind = EncounterEffectKind.SanityLoss, Amount = 5, Check = true, CreatureName = "глубоководный", SanityLossFormula = "0/1D6",
        };

        var lines = EncounterSheetEffects.Apply(sheet, Catalog, [effect]);

        Assert.Equal(48, sheet.Current.Sanity); // осталось 2 до предела
        Assert.False(sheet.Condition.TemporaryInsanity);
        Assert.Contains("проверка ИНТ не нужна", lines[0]);
    }
}
