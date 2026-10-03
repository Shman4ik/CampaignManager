using CampaignManager.Core.Dice;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Tests.Infrastructure;
using static CampaignManager.Core.Tests.Encounters.Combat.Fighters;

namespace CampaignManager.Core.Tests.Encounters.Combat;

/// <summary>Ближний бой (перенесено из T0.2 <c>MeleeAttackTests</c>): встречная проверка, внезапность, контратака.</summary>
public sealed class MeleeAttackTests
{
    private readonly EncounterParticipant _attacker = Make("Сыщик");
    private readonly EncounterParticipant _defender = Make("Культист", side: EncounterSide.Enemies);
    private readonly EncounterState _state;

    public MeleeAttackTests() => _state = Battle(_attacker, _defender);

    private MeleeAttackSetup Setup(int attackRoll, int? defenseRoll = null) => new()
    {
        AttackerId = _attacker.Id,
        DefenderId = _defender.Id,
        AttackSkill = 50,
        DefenseSkill = 50,
        AttackRoll = Rolled(attackRoll),
        DefenseRoll = defenseRoll is { } d ? Rolled(d) : null,
        DamageRoll = 2,
        DamageBonusRoll = 0,
    };

    private AttackOutcome Resolve(MeleeAttackSetup setup, ScriptedDice? dice = null) => CombatRules.Melee(_state, setup, dice ?? ScriptedDice.Of());

    [Fact]
    [Trait("page", "101")]
    public void AttackerFails_Miss()
    {
        var outcome = Resolve(Setup(60, 70));

        Assert.False(outcome.Hit);
        Assert.Equal(SuccessLevel.Failure, outcome.Level);
        Assert.Null(outcome.Damage);
        Assert.StartsWith("Сыщик промахивается. Цель: Культист (60 против 50)", outcome.Summary);
        Assert.DoesNotContain(outcome.Resolution.Effects, e => e.Kind == EncounterEffectKind.Defense);
    }

    [Fact]
    [Trait("page", "101")]
    public void DefenderFails_AttackerHits()
    {
        var outcome = Resolve(Setup(40, 70));
        Apply(_state, outcome);

        Assert.True(outcome.Hit);
        Assert.Equal(2, outcome.Damage!.Total);
        Assert.Equal(10, _defender.HitPoints);
    }

    [Theory]
    [Trait("page", "101")]
    // Оба трудный успех: уклонение — победа защитника, контратака — атакующего
    [InlineData(DefenseReaction.Dodge, 20, 20, false)]
    [InlineData(DefenseReaction.FightBack, 20, 20, true)]
    // Лучший уровень побеждает при любой реакции
    [InlineData(DefenseReaction.Dodge, 10, 20, true)]
    [InlineData(DefenseReaction.FightBack, 30, 10, false)]
    public void OpposedRoll(DefenseReaction reaction, int attackRoll, int defenseRoll, bool attackerWins)
    {
        var setup = Setup(attackRoll, defenseRoll) with
        {
            Reaction = reaction, CounterDamageRoll = 1, CounterBonusRoll = 0, ExtraImpaleRoll = 0,
        };

        Assert.Equal(attackerWins, Resolve(setup).Hit);
    }

    [Fact]
    [Trait("page", "101")]
    public void DodgeWins_NoDamageEitherWay()
    {
        var outcome = Resolve(Setup(40, 20));

        Assert.False(outcome.Hit);
        Assert.Null(outcome.Counter);
        Assert.Equal("Культист уклоняется от атаки Сыщик.", outcome.Summary);
    }

    [Fact]
    [Trait("page", "101")]
    public void FightBackWins_CounterDamageToAttacker()
    {
        _attacker.Stats.Armor = 1;
        _defender.Stats.DamageBonus = "+1D4";
        _defender.Profile.Attacks.Add(new CombatAttack { Key = "club", Name = "Дубинка", Damage = "1D6", Skill = 50, DamageBonus = Catalogs.CreatureDamageBonusMode.Full });
        var setup = Setup(40, 20) with { Reaction = DefenseReaction.FightBack, CounterAttackKey = "club", CounterDamageRoll = 4, CounterBonusRoll = 2 };

        var outcome = Resolve(setup);
        Apply(_state, outcome);

        Assert.False(outcome.Hit);
        Assert.Equal("1D6", outcome.Counter!.Formula);
        Assert.Equal(6, outcome.Counter.Raw);
        Assert.Equal(1, outcome.Counter.Armor);
        Assert.Equal(5, outcome.Counter.Total);
        Assert.Equal(7, _attacker.HitPoints);
        Assert.False(outcome.CounterWound!.MajorWound); // 5 из 12 — не серьёзная
        Assert.Equal(12, _defender.HitPoints);
    }

    [Fact]
    [Trait("page", "101")]
    [Trait("page", "117")]
    [Trait("finding", "F-C05")]
    public void FightBackMajorWound_ConRollEntered()
    {
        var setup = Setup(40, 20) with
        {
            Reaction = DefenseReaction.FightBack, CounterDamageRoll = 6, CounterBonusRoll = 0, CounterConRoll = Rolled(70),
        };

        var outcome = Resolve(setup);
        Apply(_state, outcome);

        Assert.True(outcome.CounterWound!.MajorWound);
        Assert.False(outcome.CounterWound.ConPassed);
        Assert.True(_attacker.Unconscious);
        Assert.True(_attacker.Combat.Prone);
    }

    [Fact]
    [Trait("page", "118")]
    [Trait("finding", "F-S02")]
    public void FightBackInstantDeath()
    {
        var outcome = Resolve(Setup(40, 20) with { Reaction = DefenseReaction.FightBack, CounterDamageRoll = 12, CounterBonusRoll = 0 });
        Apply(_state, outcome);

        Assert.True(_attacker.Dead);
        Assert.Equal(0, _attacker.HitPoints);
    }

    [Fact]
    [Trait("page", "101")]
    public void FightBackWithoutWeapon_UsesBrawlOneD3()
    {
        var outcome = Resolve(Setup(40, 20) with { Reaction = DefenseReaction.FightBack, CounterBonusRoll = 0 }, ScriptedDice.Of(3));

        Assert.Equal("1D3", outcome.Counter!.Formula);
        Assert.Equal(3, outcome.Counter.Rolled);
    }

    [Fact]
    [Trait("page", "105")]
    public void AutoHit_OnlyFumbleMisses()
    {
        var outcome = Resolve(Setup(70) with { Surprise = SurpriseMode.AutoHit });
        Apply(_state, outcome);

        // Провал броска — всё равно попадание; цель не бросает и защиту не тратит
        Assert.True(outcome.Hit);
        Assert.Equal(SuccessLevel.Failure, outcome.Level);
        Assert.Null(outcome.DefenseRoll);
        Assert.Equal(2, outcome.Damage!.Total);
        Assert.Equal(0, _defender.Combat.DefensesIn(_state.Round));
    }

    [Fact]
    [Trait("page", "105")]
    public void AutoHit_Fumble_NoDamage()
    {
        var outcome = Resolve(Setup(100) with { Surprise = SurpriseMode.AutoHit });

        Assert.False(outcome.Hit);
        Assert.Contains("крах при внезапной атаке", outcome.Summary);
    }

    [Fact]
    [Trait("page", "105")]
    public void BonusDieSurprise_DefenderDoesNotRoll()
    {
        var outcome = Resolve(Setup(40) with { Surprise = SurpriseMode.BonusDie });

        Assert.True(outcome.Hit);
        Assert.Equal(1, outcome.Modifiers.BonusDice);
        Assert.Null(outcome.DefenseRoll);
        Assert.DoesNotContain(outcome.Resolution.Effects, e => e.Kind == EncounterEffectKind.Defense);
    }

    [Fact]
    [Trait("page", "89")]
    public void AutoRoll_UsesModifierDice()
    {
        // Цель повалена: +1 бонусная кость атакующему — бросаются две кости десятков
        _defender.Combat.Prone = true;
        var dice = ScriptedDice.Of(5, 7, 1, 0, 8);

        var outcome = Resolve(Setup(0, null) with { AttackRoll = null, DefenseRoll = null }, dice);

        Assert.Equal(15, outcome.Roll!.Result);
        Assert.Equal([75, 15], outcome.Roll.Candidates);
        Assert.Equal(80, outcome.DefenseRoll!.Result);
        Assert.Equal(0, dice.Remaining);
    }

    /// <summary>
    /// F-C02 исправлено устройством: разрешение ничего не меняет — ход, атаки, защиты цели и прицел меняет только
    /// «Применить»; «Отменить» выбрасывает результат.
    /// </summary>
    [Fact]
    [Trait("page", "106")]
    [Trait("finding", "F-C02")]
    public void Resolve_ChangesNothing_UntilApply()
    {
        _attacker.Combat.Aiming = true;

        var outcome = Resolve(Setup(40, 20));
        EncounterEngine.Propose(_state, outcome.Resolution);
        EncounterEngine.Cancel(_state);

        Assert.Equal(0, _attacker.Combat.AttacksIn(_state.Round));
        Assert.True(_attacker.Combat.Aiming);
        Assert.Equal(0, _defender.Combat.DefensesIn(_state.Round));

        Apply(_state, outcome);
        Assert.Equal(1, _attacker.Combat.AttacksIn(_state.Round));
        Assert.False(_attacker.Combat.Aiming);
        Assert.Equal(1, _defender.Combat.DefensesIn(_state.Round));
    }

    [Fact]
    [Trait("page", "106")]
    public void NumericalSuperiority_AfterTargetSpentItsDefenses()
    {
        Apply(_state, Resolve(Setup(40, 20)));

        var second = Resolve(Setup(40, 20));

        Assert.Contains("+1 численное превосходство", second.Modifiers.Reasons);
    }

    [Fact]
    [Trait("page", "177")]
    public void DamageToCaster_DisruptsCasting()
    {
        _defender.Combat.Casting = new SpellCasting { SpellName = "Призыв", CompletesInRound = 3 };

        Apply(_state, Resolve(Setup(40, 70)));

        Assert.True(_defender.Combat.Casting!.Disrupted);
        Assert.Contains(_state.Log, e => e.Lines.Any(l => l.Contains("сорвано", StringComparison.Ordinal)));
    }

    [Fact]
    [Trait("page", "118")]
    public void Damage_ResetsFirstAidAndAim()
    {
        _defender.Combat.FirstAidReceived = true;
        _defender.Combat.Aiming = true;

        Apply(_state, Resolve(Setup(40, 70)));

        Assert.False(_defender.Combat.FirstAidReceived);
        Assert.False(_defender.Combat.Aiming);
    }
}
