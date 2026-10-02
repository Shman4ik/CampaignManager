using CampaignManager.Core.Encounters;
using CampaignManager.Core.Tests.Infrastructure;
using static CampaignManager.Core.Tests.Encounters.Combat.Fighters;

namespace CampaignManager.Core.Tests.Encounters.Combat;

/// <summary>Боевые манёвры (перенесено из T0.2 <c>ManeuverTests</c>): Комплекция, встречная проверка, эффект при «Применить».</summary>
[Trait("page", "103")]
public sealed class ManeuverTests
{
    private readonly EncounterParticipant _attacker = Make("Сыщик");
    private readonly EncounterParticipant _defender = Make("Культист", side: EncounterSide.Enemies);
    private readonly EncounterState _state;

    public ManeuverTests() => _state = Battle(_attacker, _defender);

    private ManeuverSetup Setup(ManeuverType type, int attackRoll = 20, int defenseRoll = 80) => new()
    {
        AttackerId = _attacker.Id,
        DefenderId = _defender.Id,
        Type = type,
        AttackSkill = 50,
        DefenseSkill = 40,
        AttackerBuild = 0,
        DefenderBuild = 0,
        AttackRoll = Rolled(attackRoll),
        DefenseRoll = Rolled(defenseRoll),
    };

    private AttackOutcome Resolve(ManeuverSetup setup) => CombatRules.Maneuver(_state, setup, ScriptedDice.Of());

    [Theory]
    [InlineData(0, 3)]
    [InlineData(-1, 2)]
    [InlineData(0, 5)]
    public void BuildGapThreeOrMore_Impossible_NoTurnSpent(int attackerBuild, int defenderBuild)
    {
        var outcome = Resolve(Setup(ManeuverType.KnockDown) with { AttackerBuild = attackerBuild, DefenderBuild = defenderBuild });

        Assert.False(outcome.Hit);
        Assert.Contains("невозможен", outcome.Summary);
        Assert.Empty(outcome.Resolution.Effects);
        Assert.Null(outcome.Roll);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 0)] // атакующий крупнее — штрафа нет, бонуса тоже
    [InlineData(0, 1, 1)]
    [InlineData(0, 2, 2)]
    public void BuildGap_PenaltyDicePerPoint(int attackerBuild, int defenderBuild, int penalty)
    {
        var outcome = Resolve(Setup(ManeuverType.KnockDown) with
        {
            AttackerBuild = attackerBuild, DefenderBuild = defenderBuild, KeeperPenaltyDice = 1, KeeperBonusDice = 1,
        });

        Assert.Equal(1, outcome.Modifiers.BonusDice);
        Assert.Equal(1 + penalty, outcome.Modifiers.PenaltyDice);
        Assert.Equal(penalty > 0, outcome.Modifiers.Reasons.Contains($"−{penalty} разница Комплекции ({attackerBuild} против {defenderBuild})"));
    }

    [Theory]
    [InlineData(20, 80, DefenseReaction.Dodge, true)] // цель провалила защиту
    [InlineData(60, 10, DefenseReaction.Dodge, false)] // атакующий провалил
    [InlineData(20, 20, DefenseReaction.Dodge, false)] // оба трудный: уклонение побеждает в ничьей
    [InlineData(20, 20, DefenseReaction.FightBack, true)] // ничья при контратаке — атакующему
    [InlineData(40, 8, DefenseReaction.Dodge, false)] // у цели чрезвычайный
    public void OpposedRoll(int attackRoll, int defenseRoll, DefenseReaction reaction, bool succeeded)
    {
        var outcome = Resolve(Setup(ManeuverType.Grapple, attackRoll, defenseRoll) with { Reaction = reaction });

        Assert.Equal(succeeded, outcome.Hit);
        Assert.Contains(outcome.Resolution.Effects, e => e.Kind == EncounterEffectKind.Attack && e.ParticipantId == _attacker.Id);
    }

    [Fact]
    public void DefenderCountedAsDefended_UnlessAttackerFailed()
    {
        Apply(_state, Resolve(Setup(ManeuverType.Push, 60)));
        Assert.Equal(0, _defender.Combat.DefensesIn(_state.Round));

        Apply(_state, Resolve(Setup(ManeuverType.Push, 20)));
        Assert.Equal(1, _defender.Combat.DefensesIn(_state.Round));
    }

    [Theory]
    [InlineData(ManeuverType.KnockDown)]
    [InlineData(ManeuverType.Push)]
    public void KnockDownOrPush_TargetProne(ManeuverType type)
    {
        Apply(_state, Resolve(Setup(type)));

        Assert.True(_defender.Combat.Prone);
    }

    [Fact]
    public void Grapple_TargetHeldByAttacker()
    {
        Apply(_state, Resolve(Setup(ManeuverType.Grapple)));

        Assert.Equal(_attacker.Id, _defender.Combat.GrappledBy);
    }

    [Fact]
    public void DisarmAndDisadvantage_MarkTarget()
    {
        Apply(_state, Resolve(Setup(ManeuverType.Disarm)));
        Apply(_state, Resolve(Setup(ManeuverType.Disadvantage)));

        Assert.True(_defender.Combat.Disarmed);
        Assert.True(_defender.Combat.Disadvantage);
    }

    [Fact]
    [Trait("page", "123")]
    public void Knockout_OnlyWithOptionalRule_OneDamageAndUnconscious()
    {
        Assert.NotNull(Resolve(Setup(ManeuverType.Knockout)).Blocked);

        _state.Combat.CinematicKnockout = true;
        Apply(_state, Resolve(Setup(ManeuverType.Knockout)));

        Assert.Equal(11, _defender.HitPoints);
        Assert.True(_defender.Unconscious);
    }

    [Fact]
    public void FailedManeuver_NoEffect()
    {
        Apply(_state, Resolve(Setup(ManeuverType.KnockDown, 60)));

        Assert.False(_defender.Combat.Prone);
    }

    /// <summary>F-C08 исправлено: «Вырваться» освобождает того, кто вырывается, а не того, кто держит.</summary>
    [Fact]
    [Trait("finding", "F-C08")]
    public void BreakFree_ClearsAttackerGrapple()
    {
        _attacker.Combat.GrappledBy = _defender.Id;

        var outcome = Resolve(Setup(ManeuverType.BreakFree));
        Apply(_state, outcome);

        Assert.True(outcome.Hit);
        Assert.Null(_attacker.Combat.GrappledBy);
    }
}
