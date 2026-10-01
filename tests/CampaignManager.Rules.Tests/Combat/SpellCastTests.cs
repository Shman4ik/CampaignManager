using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Rules.Tests.Combat;

/// <summary>Заклинания в бою: время сотворения, цена, первое сотворение, встречная МОЩ, срыв.</summary>
public sealed class SpellCastTests
{
    private readonly Combatant _caster = Fighters.Investigator("Заклинатель");
    private readonly Combatant _target = Fighters.Make("Культист", side: CombatSide.Enemy);
    private readonly CombatService _combat;

    public SpellCastTests()
    {
        _caster.Power = 60;
        _caster.CurrentMagicPoints = 10;
        _target.Power = 50;
        _combat = Fighters.Battle(_caster, _target);
    }

    private SpellCastSetup Spell(int magicPoints = 5) => new()
    {
        CasterId = _caster.Id,
        SpellName = "Иссушение",
        MagicPointsCost = magicPoints,
        CastingRounds = 1
    };

    [Theory]
    [Trait("page", "241")]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(5, 5)]
    public void SpellCompletionRound_CurrentPlusRoundsMinusOne(int rounds, int expected)
    {
        Assert.Equal(expected, _combat.SpellCompletionRound(rounds));

        _combat.NextRound();
        Assert.Equal(expected + 1, _combat.SpellCompletionRound(rounds));
    }

    [Theory]
    [Trait("page", "241")]
    [InlineData(SuccessLevel.Failure, 150, SuccessLevel.CriticalSuccess, 50, SpellResistance.CasterWins)] // разница 100 — без броска
    [InlineData(SuccessLevel.CriticalSuccess, 50, SuccessLevel.Failure, 150, SpellResistance.TargetWins)]
    [InlineData(SuccessLevel.HardSuccess, 50, SuccessLevel.RegularSuccess, 140, SpellResistance.CasterWins)] // 90 — ещё бросают
    [InlineData(SuccessLevel.RegularSuccess, 60, SuccessLevel.HardSuccess, 50, SpellResistance.TargetWins)]
    [InlineData(SuccessLevel.RegularSuccess, 60, SuccessLevel.RegularSuccess, 50, SpellResistance.CasterWins)] // ничья — у кого МОЩ выше
    [InlineData(SuccessLevel.RegularSuccess, 50, SuccessLevel.RegularSuccess, 60, SpellResistance.TargetWins)]
    [InlineData(SuccessLevel.Failure, 50, SuccessLevel.Failure, 50, SpellResistance.Both)]
    public void ResolveOpposedPower(SuccessLevel casterLevel, int casterPower, SuccessLevel targetLevel, int targetPower,
        SpellResistance expected)
    {
        Assert.Equal(expected, CombatService.ResolveOpposedPower(casterLevel, casterPower, targetLevel, targetPower));
    }

    [Fact]
    [Trait("page", "174")]
    public void ResolveSpellCast_PaysMagicPoints_TakesEffect()
    {
        var result = _combat.ResolveSpellCast(Spell(5));

        Assert.True(result.AttackerWins);
        Assert.Equal(SpellCastPhase.Cast, result.Spell!.Phase);
        Assert.True(result.Spell.TakesEffect);
        Assert.Equal(5, result.Spell.MagicPointsPaid);
        Assert.Equal(5, result.Spell.MagicPointsAfter);
        Assert.Equal(SpellResistance.NotResisted, result.Spell.Resistance);

        _combat.ApplyResult(result);
        Assert.Equal(5, _caster.CurrentMagicPoints);
        Assert.True(_caster.HasActedThisRound);
    }

    [Fact]
    [Trait("page", "174")]
    public void ResolveSpellCast_MagicPointShortfall_TakenFromHitPoints()
    {
        _caster.CurrentMagicPoints = 3;

        var result = _combat.ResolveSpellCast(Spell(5));
        _combat.ApplyResult(result);

        Assert.Equal(2, result.Spell!.MagicPointsShortfall);
        Assert.Equal(2, result.Spell.HitPointsPaid);
        Assert.Equal(0, _caster.CurrentMagicPoints);
        Assert.Equal(10, _caster.CurrentHitPoints);
        Assert.False(result.AttackerTriggeredMajorWound);
    }

    [Fact]
    [Trait("page", "176")]
    public void ResolveSpellCast_ShortfallMajorWound_ConRollFromSetup()
    {
        _caster.CurrentMagicPoints = 0;
        var setup = Spell(6);
        setup.ManualMajorWoundConRoll = 70;

        var result = _combat.ResolveSpellCast(setup);

        Assert.True(result.AttackerTriggeredMajorWound);
        Assert.Equal(70, result.AttackerMajorWoundConRoll);
        Assert.True(result.AttackerKnockedUnconscious);
        Assert.Equal(6, result.AttackerHpAfter);
    }

    [Fact]
    [Trait("page", "174")]
    public void ResolveSpellCast_PowerAndHitPointCost()
    {
        var setup = Spell(0);
        setup.PowerCost = 5;
        setup.HitPointsCost = 1;

        var result = _combat.ResolveSpellCast(setup);
        _combat.ApplyResult(result);

        Assert.Equal(55, _caster.Power);
        Assert.Equal(11, _caster.CurrentHitPoints);
        Assert.Contains("МОЩ 60→55 навсегда", result.Summary);
    }

    [Fact]
    [Trait("page", "174")]
    public void ResolveSpellCast_SanityCost_InvestigatorPays_CreatureSkips()
    {
        var setup = Spell(0);
        setup.SanityCost = 3;
        var investigatorResult = _combat.ResolveSpellCast(setup);

        var creature = Fighters.Make("Жрец-существо", side: CombatSide.Enemy);
        _combat.AddCombatant(creature);
        setup.CasterId = creature.Id;
        var creatureResult = _combat.ResolveSpellCast(setup);

        Assert.Equal(3, investigatorResult.Spell!.SanityPaid);
        Assert.Equal(47, investigatorResult.SanityAfter);
        Assert.True(creatureResult.Spell!.SanitySkipped);
        Assert.Null(creatureResult.SanityAfter);
    }

    [Theory]
    [Trait("page", "176")]
    [Trait("page", "88")]
    [InlineData(30, SuccessLevel.HardSuccess, true)]
    [InlineData(31, SuccessLevel.RegularSuccess, false)]
    [InlineData(96, SuccessLevel.Fumble, false)] // проверка трудная: нужно 30 — крах на 96
    public void ResolveSpellCast_FirstCast_HardPowerCheck(int roll, SuccessLevel level, bool passed)
    {
        var setup = Spell(5);
        setup.IsFirstCast = true;
        setup.ManualCastingRoll = roll;

        var result = _combat.ResolveSpellCast(setup);

        Assert.Equal(level, result.Spell!.CastingLevel);
        Assert.Equal(passed, result.Spell.CastingPassed);
        Assert.Equal(passed, result.Spell.TakesEffect);
        Assert.Equal(passed, result.AttackerWins);
        Assert.Equal(5, result.Spell.MagicPointsPaid); // цена платится при любом исходе
    }

    [Fact]
    [Trait("page", "176")]
    public void ResolveSpellCast_PushedFailure_TakesEffect_CostTimesOnePlusD6()
    {
        var setup = Spell(2);
        setup.SanityCost = 1;
        setup.IsFirstCast = true;
        setup.IsPushed = true;
        setup.ManualCastingRoll = 80;
        setup.ManualPushMultiplier = 3;

        var result = _combat.ResolveSpellCast(setup);

        Assert.True(result.Spell!.TakesEffect);
        Assert.Equal(3, result.Spell.PushMultiplier);
        Assert.Equal(8, result.Spell.MagicPointsPaid); // 2 × (1 + 3)
        Assert.Equal(4, result.Spell.SanityPaid);
    }

    [Fact]
    [Trait("page", "176")]
    public void ResolveSpellCast_PushedFailure_MultiplierRolledOnD6()
    {
        var setup = Spell(1);
        setup.IsFirstCast = true;
        setup.IsPushed = true;
        setup.ManualCastingRoll = 80;
        using var _ = ScriptedRandom.Use(6);

        var result = _combat.ResolveSpellCast(setup);

        Assert.Equal(7, result.Spell!.MagicPointsShortfall + result.Spell.MagicPointsPaid);
    }

    [Theory]
    [Trait("page", "241")]
    [InlineData(20, 80, SpellResistance.CasterWins, true)]
    [InlineData(80, 20, SpellResistance.TargetWins, false)]
    [InlineData(80, 80, SpellResistance.CasterWins, true)] // оба провал — у заклинателя МОЩ выше
    public void ResolveSpellCast_TargetResists_OpposedPowerRolls(int casterRoll, int targetRoll,
        SpellResistance expected, bool works)
    {
        var setup = Spell(1);
        setup.TargetId = _target.Id;
        setup.TargetResists = true;
        setup.ManualCasterPowerRoll = casterRoll;
        setup.ManualTargetPowerRoll = targetRoll;

        var result = _combat.ResolveSpellCast(setup);

        Assert.Equal(expected, result.Spell!.Resistance);
        Assert.Equal(works, result.AttackerWins);
        Assert.False(result.Spell.ResistanceAutomatic);
    }

    [Fact]
    [Trait("page", "241")]
    public void ResolveSpellCast_PowerGapHundred_NoRolls()
    {
        _target.Power = 160;
        var setup = Spell(1);
        setup.TargetId = _target.Id;
        setup.TargetResists = true;

        // Ни одного броска: ScriptedRandom без чисел упал бы
        using var _ = ScriptedRandom.Use();
        var result = _combat.ResolveSpellCast(setup);

        Assert.True(result.Spell!.ResistanceAutomatic);
        Assert.Equal(SpellResistance.TargetWins, result.Spell.Resistance);
    }

    [Fact]
    [Trait("page", "241")]
    public void ResolveSpellCast_CastSpell_DoesNotDamageTargetOnApply()
    {
        var setup = Spell(1);
        setup.TargetId = _target.Id;

        _combat.ApplyResult(_combat.ResolveSpellCast(setup));

        Assert.Equal(12, _target.CurrentHitPoints);
    }

    [Fact]
    [Trait("page", "241")]
    public void StartSpellcasting_PutsCastingOnCaster_NoCostYet()
    {
        _combat.NextRound();
        var setup = Spell(5);
        setup.CastingRounds = 3;
        setup.ManualCastingRoll = 12;

        _combat.StartSpellcasting(setup);

        var casting = _caster.CastingSpell!;
        Assert.Equal(2, casting.StartedRound);
        Assert.Equal(4, casting.CompletesInRound);
        Assert.Null(casting.Setup.ManualCastingRoll);
        Assert.Equal(10, _caster.CurrentMagicPoints);
        Assert.Equal(SpellCastPhase.Started, _combat.CombatLog[0].Spell!.Phase);
        Assert.NotNull(_combat.GetAttackBlockReason(_caster));
    }

    [Fact]
    [Trait("page", "177")]
    public void ResolveSpellInterruption_PaysMagicPointsAndSanity_NotPowerOrHitPoints()
    {
        var setup = Spell(4);
        setup.CastingRounds = 2;
        setup.SanityCost = 2;
        setup.PowerCost = 3;
        setup.HitPointsCost = 1;
        _combat.StartSpellcasting(setup);

        var result = _combat.ResolveSpellInterruption(_caster, manualIntRoll: null, manualDurationRoll: null, manualConRoll: null);
        _combat.ApplyResult(result);

        Assert.Equal(SpellCastPhase.Interrupted, result.Spell!.Phase);
        Assert.False(result.AttackerWins);
        Assert.Equal(4, result.Spell.MagicPointsPaid);
        Assert.Equal(2, result.Spell.SanityPaid);
        Assert.Equal(0, result.Spell.PowerPaid);
        Assert.Equal(0, result.Spell.HitPointsPaid);
        Assert.Equal(6, _caster.CurrentMagicPoints);
        Assert.Equal(48, _caster.CurrentSanity);
        Assert.Equal(60, _caster.Power);
        Assert.Null(_caster.CastingSpell);
    }

    [Fact]
    [Trait("page", "177")]
    public void ResolveSpellInterruption_NotCasting_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _combat.ResolveSpellInterruption(_caster, null, null, null));
    }

    [Fact]
    [Trait("page", "176")]
    public void GetPower_FallsBackToSheet()
    {
        _caster.Power = 0;
        _caster.CharacterSource!.Characteristics.Power.Regular = 45;

        Assert.Equal(45, CombatService.GetPower(_caster));
        Assert.Equal(0, CombatService.GetPower(new Combatant()));
    }
}
