using CampaignManager.Core.Dice;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Tests.Infrastructure;
using static CampaignManager.Core.Tests.Encounters.Combat.Fighters;

namespace CampaignManager.Core.Tests.Encounters.Combat;

/// <summary>Заклинания в бою (перенесено из T0.2 <c>SpellCastTests</c>): время, цена, первое сотворение, встречная МОЩ, срыв.</summary>
public sealed class SpellCastTests
{
    private readonly EncounterParticipant _caster = Investigator("Заклинатель");
    private readonly EncounterParticipant _target = Make("Культист", side: EncounterSide.Enemies, pow: 50);
    private readonly EncounterState _state;

    public SpellCastTests()
    {
        _caster.Stats.Pow = 60;
        _caster.MagicPoints = 10;
        _caster.MaxMagicPoints = 12;
        _state = Battle(_caster, _target);
    }

    private SpellCastSetup Spell(int magicPoints = 5) => new()
    {
        CasterId = _caster.Id, SpellName = "Иссушение", MagicPoints = magicPoints, CastingRounds = 1,
    };

    private SpellOutcome Cast(SpellCastSetup setup, ScriptedDice? dice = null) => CombatRules.CastSpell(_state, setup, dice ?? ScriptedDice.Of());

    [Theory]
    [Trait("page", "241")]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(5, 5)]
    public void CompletionRound_CurrentPlusRoundsMinusOne(int rounds, int expected)
    {
        Assert.Equal(expected, CombatRules.SpellCompletionRound(1, rounds));
        Assert.Equal(expected + 1, CombatRules.SpellCompletionRound(2, rounds));
    }

    [Theory]
    [Trait("page", "241")]
    [InlineData(SuccessLevel.Failure, 150, SuccessLevel.Critical, 50, SpellResistance.CasterWins)] // разница 100 — без броска
    [InlineData(SuccessLevel.Critical, 50, SuccessLevel.Failure, 150, SpellResistance.TargetWins)]
    [InlineData(SuccessLevel.Hard, 50, SuccessLevel.Regular, 140, SpellResistance.CasterWins)] // 90 — ещё бросают
    [InlineData(SuccessLevel.Regular, 60, SuccessLevel.Hard, 50, SpellResistance.TargetWins)]
    [InlineData(SuccessLevel.Regular, 60, SuccessLevel.Regular, 50, SpellResistance.CasterWins)] // ничья — у кого МОЩ выше
    [InlineData(SuccessLevel.Regular, 50, SuccessLevel.Regular, 60, SpellResistance.TargetWins)]
    [InlineData(SuccessLevel.Failure, 50, SuccessLevel.Failure, 50, SpellResistance.Both)]
    public void OpposedPower(SuccessLevel casterLevel, int casterPower, SuccessLevel targetLevel, int targetPower, SpellResistance expected) =>
        Assert.Equal(expected, CombatRules.OpposedPower(casterLevel, casterPower, targetLevel, targetPower));

    [Fact]
    [Trait("page", "174")]
    public void PaysMagicPoints_TakesEffect()
    {
        var outcome = Cast(Spell(5));
        Apply(_state, outcome.Resolution);

        Assert.True(outcome.Works);
        Assert.True(outcome.TakesEffect);
        Assert.Equal(5, outcome.MagicPointsPaid);
        Assert.Equal(SpellResistance.NotResisted, outcome.Resistance);
        Assert.Equal(5, _caster.MagicPoints);
    }

    [Fact]
    [Trait("page", "174")]
    public void MagicPointShortfall_TakenFromHitPoints()
    {
        _caster.MagicPoints = 3;

        var outcome = Cast(Spell(5));
        Apply(_state, outcome.Resolution);

        Assert.Equal(2, outcome.MagicPointsShortfall);
        Assert.Equal(2, outcome.HitPointsPaid);
        Assert.Equal(0, _caster.MagicPoints);
        Assert.Equal(10, _caster.HitPoints);
        Assert.False(outcome.Wound!.MajorWound);
    }

    [Fact]
    [Trait("page", "176")]
    public void ShortfallMajorWound_ConRollEntered()
    {
        _caster.MagicPoints = 0;

        var outcome = Cast(Spell(6) with { ConRoll = Rolled(70) });
        Apply(_state, outcome.Resolution);

        Assert.True(outcome.Wound!.MajorWound);
        Assert.False(outcome.Wound.ConPassed);
        Assert.True(_caster.Unconscious);
        Assert.Equal(6, _caster.HitPoints);
    }

    /// <summary>F-C06 исправлено: ВЫН при расплате ПЗ — через <c>Check</c>: 100 — крах даже при ВЫН 120, 01 — успех при нуле.</summary>
    [Theory]
    [Trait("page", "87")]
    [Trait("finding", "F-C06")]
    [InlineData(120, 100, false)]
    [InlineData(0, 1, true)]
    public void ShortfallConRoll_ThroughCheck(int constitution, int conRoll, bool conscious)
    {
        _caster.MagicPoints = 0;
        _caster.Stats.Con = constitution;

        var outcome = Cast(Spell(6) with { ConRoll = Rolled(conRoll) });

        Assert.Equal(conscious, outcome.Wound!.ConPassed);
    }

    [Fact]
    [Trait("page", "174")]
    public void PowerAndHitPointCost()
    {
        var outcome = Cast(Spell(0) with { Power = 5, HitPoints = 1 });
        Apply(_state, outcome.Resolution);

        Assert.Equal(55, _caster.Stats.Pow);
        Assert.Equal(11, _caster.HitPoints);
        Assert.Contains("МОЩ −5", outcome.Summary);
    }

    [Fact]
    [Trait("page", "174")]
    public void SanityCost_InvestigatorPays_CreatureSkips()
    {
        var investigator = Cast(Spell(0) with { Sanity = 3 });

        var creature = Make("Жрец-существо", side: EncounterSide.Enemies);
        EncounterEngine.Add(_state, creature, Now);
        var monster = Cast(Spell(0) with { CasterId = creature.Id, Sanity = 3 });

        Assert.Equal(3, investigator.SanityPaid);
        Apply(_state, investigator.Resolution);
        Assert.Equal(47, _caster.Sanity);
        Assert.True(monster.SanitySkipped);
        Assert.DoesNotContain(monster.Resolution.Effects, e => e.Kind == EncounterEffectKind.SanityLoss);
    }

    [Theory]
    [Trait("page", "176")]
    [Trait("page", "88")]
    [InlineData(30, SuccessLevel.Hard, true)]
    [InlineData(31, SuccessLevel.Regular, false)]
    [InlineData(96, SuccessLevel.Fumble, false)] // проверка трудная: нужно 30 — крах на 96
    public void FirstCast_HardPowerCheck(int roll, SuccessLevel level, bool passed)
    {
        var outcome = Cast(Spell(5) with { FirstCast = true, CastingRoll = Rolled(roll) });

        Assert.Equal(level, outcome.CastingLevel);
        Assert.Equal(passed, outcome.TakesEffect);
        Assert.Equal(passed, outcome.Works);
        Assert.Equal(5, outcome.MagicPointsPaid); // цена платится при любом исходе
    }

    [Fact]
    [Trait("page", "176")]
    public void PushedFailure_TakesEffect_CostTimesOnePlusD6()
    {
        var outcome = Cast(Spell(2) with { Sanity = 1, FirstCast = true, Pushed = true, CastingRoll = Rolled(80), PushMultiplier = 3, IntRoll = Rolled(99) });

        Assert.True(outcome.TakesEffect);
        Assert.Equal(4, outcome.Multiplier);
        Assert.Equal(8, outcome.MagicPointsPaid); // 2 × (1 + 3)
        Assert.Equal(4, outcome.SanityPaid);
    }

    [Fact]
    [Trait("page", "176")]
    public void PushedFailure_MultiplierRolledOnD6()
    {
        var outcome = Cast(Spell(1) with { FirstCast = true, Pushed = true, CastingRoll = Rolled(80) }, ScriptedDice.Of(6));

        Assert.Equal(7, outcome.MagicPointsShortfall + outcome.MagicPointsPaid);
    }

    [Theory]
    [Trait("page", "241")]
    [InlineData(20, 80, SpellResistance.CasterWins, true)]
    [InlineData(80, 20, SpellResistance.TargetWins, false)]
    [InlineData(80, 80, SpellResistance.CasterWins, true)] // оба провал — у заклинателя МОЩ выше
    public void TargetResists_OpposedPowerRolls(int casterRoll, int targetRoll, SpellResistance expected, bool works)
    {
        var outcome = Cast(Spell(1) with
        {
            TargetId = _target.Id, TargetResists = true, CasterPowerRoll = Rolled(casterRoll), TargetPowerRoll = Rolled(targetRoll),
        });

        Assert.Equal(expected, outcome.Resistance);
        Assert.Equal(works, outcome.Works);
        Assert.False(outcome.ResistanceAutomatic);
    }

    [Fact]
    [Trait("page", "241")]
    public void PowerGapHundred_NoRolls()
    {
        _target.Stats.Pow = 160;

        // Ни одного броска: ScriptedDice без чисел упал бы
        var outcome = Cast(Spell(1) with { TargetId = _target.Id, TargetResists = true });

        Assert.True(outcome.ResistanceAutomatic);
        Assert.Equal(SpellResistance.TargetWins, outcome.Resistance);
    }

    [Fact]
    [Trait("page", "241")]
    public void CastSpell_DoesNotDamageTarget()
    {
        var outcome = Cast(Spell(1) with { TargetId = _target.Id });

        Assert.DoesNotContain(outcome.Resolution.Effects, e => e.ParticipantId == _target.Id);
    }

    [Fact]
    [Trait("page", "241")]
    public void LongCasting_StartsWithoutCost_BlocksAttacks()
    {
        EncounterQueue.Next(_state, Now);
        EncounterQueue.Next(_state, Now); // раунд 2
        var outcome = Cast(Spell(5) with { CastingRounds = 3, CastingRoll = Rolled(12) });
        Apply(_state, outcome.Resolution);

        var casting = _caster.Combat.Casting!;
        Assert.True(outcome.Started);
        Assert.Equal(2, casting.StartedRound);
        Assert.Equal(4, casting.CompletesInRound);
        Assert.Equal(10, _caster.MagicPoints);
        Assert.NotNull(CombatRules.AttackBlockReason(_state, _caster));
    }

    [Fact]
    [Trait("page", "177")]
    public void Interrupted_PaysMagicPointsAndSanity_NotPowerOrHitPoints()
    {
        Apply(_state, Cast(Spell(4) with { CastingRounds = 2, Sanity = 2, Power = 3, HitPoints = 1 }).Resolution);

        var outcome = CombatRules.InterruptCasting(_state, _caster.Id, new SpellCastSetup(), ScriptedDice.Of());
        Apply(_state, outcome.Resolution);

        Assert.True(outcome.Interrupted);
        Assert.Equal(4, outcome.MagicPointsPaid);
        Assert.Equal(2, outcome.SanityPaid);
        Assert.Equal(6, _caster.MagicPoints);
        Assert.Equal(48, _caster.Sanity);
        Assert.Equal(60, _caster.Stats.Pow);
        Assert.Equal(12, _caster.HitPoints);
        Assert.Null(_caster.Combat.Casting);
    }

    [Fact]
    [Trait("page", "177")]
    public void Complete_AfterWound_IsInterruption()
    {
        Apply(_state, Cast(Spell(4) with { CastingRounds = 2 }).Resolution);
        _caster.Combat.Casting!.Disrupted = true;

        var outcome = CombatRules.CompleteCasting(_state, _caster.Id, new SpellCastSetup(), ScriptedDice.Of());

        Assert.True(outcome.Interrupted);
        Assert.False(outcome.Works);
    }

    [Fact]
    [Trait("page", "241")]
    public void Complete_PaysFullCost_ClearsCasting()
    {
        Apply(_state, Cast(Spell(4) with { CastingRounds = 2, Power = 1 }).Resolution);

        var outcome = CombatRules.CompleteCasting(_state, _caster.Id, new SpellCastSetup(), ScriptedDice.Of());
        Apply(_state, outcome.Resolution);

        Assert.True(outcome.Works);
        Assert.Equal(6, _caster.MagicPoints);
        Assert.Equal(59, _caster.Stats.Pow);
        Assert.Null(_caster.Combat.Casting);
    }

    [Fact]
    [Trait("page", "177")]
    public void NotCasting_InterruptThrows() =>
        Assert.Throws<InvalidOperationException>(() => CombatRules.InterruptCasting(_state, _caster.Id, new SpellCastSetup(), ScriptedDice.Of()));
}
