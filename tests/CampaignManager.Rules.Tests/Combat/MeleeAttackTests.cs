using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Rules.Tests.Combat;

/// <summary>
///     Ближний бой: встречная проверка, внезапность, контратака. Все броски вписаны в
///     <see cref="AttackSetup" />, как их вписывает Хранитель.
/// </summary>
public sealed class MeleeAttackTests
{
    private readonly Combatant _attacker = Fighters.Make("Сыщик");
    private readonly Combatant _defender = Fighters.Make("Культист", side: CombatSide.Enemy);
    private readonly CombatService _combat;

    public MeleeAttackTests() => _combat = Fighters.Battle(_attacker, _defender);

    private AttackSetup Setup(int attackRoll, int? defenseRoll = null) => new()
    {
        AttackerId = _attacker.Id,
        DefenderId = _defender.Id,
        IsMelee = true,
        AttackSkillValue = 50,
        DefenderSkillValue = 50,
        ManualAttackerRoll = attackRoll,
        ManualDefenderRoll = defenseRoll,
        ManualWeaponDamageRoll = 2,
        ManualDamageBonusRoll = 0
    };

    [Fact]
    [Trait("page", "101")]
    public void ResolveMeleeAttack_AttackerFails_Miss()
    {
        var result = _combat.ResolveMeleeAttack(Setup(60, 70));

        Assert.False(result.AttackerWins);
        Assert.Equal(SuccessLevel.Failure, result.AttackerSuccessLevel);
        Assert.Equal(12, result.DefenderHpAfter);
        Assert.Equal(0, result.TotalDamage);
        Assert.StartsWith("Сыщик промахивается (60 против 50)", result.Summary);
    }

    [Fact]
    [Trait("page", "101")]
    public void ResolveMeleeAttack_DefenderFails_AttackerHits()
    {
        var result = _combat.ResolveMeleeAttack(Setup(40, 70));

        Assert.True(result.AttackerWins);
        Assert.Equal(2, result.TotalDamage);
        Assert.Equal(10, result.DefenderHpAfter);
    }

    [Theory]
    [Trait("page", "101")]
    // Оба трудный успех: уклонение — победа защитника, контратака — атакующего
    [InlineData(CombatActionType.Dodge, 20, 20, false)]
    [InlineData(CombatActionType.FightBack, 20, 20, true)]
    // Лучший уровень побеждает при любой реакции
    [InlineData(CombatActionType.Dodge, 10, 20, true)]
    [InlineData(CombatActionType.FightBack, 30, 10, false)]
    public void ResolveMeleeAttack_OpposedRoll(CombatActionType reaction, int attackRoll, int defenseRoll, bool attackerWins)
    {
        var setup = Setup(attackRoll, defenseRoll);
        setup.DefenderReaction = reaction;
        setup.ManualCounterWeaponDamageRoll = 1;
        setup.ManualCounterDamageBonusRoll = 0;
        setup.ManualExtraImpalingRoll = 0;

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.Equal(attackerWins, result.AttackerWins);
    }

    [Fact]
    [Trait("page", "101")]
    public void ResolveMeleeAttack_DodgeWins_NoDamageEitherWay()
    {
        var result = _combat.ResolveMeleeAttack(Setup(40, 20));

        Assert.False(result.AttackerWins);
        Assert.Equal(0, result.CounterTotalDamage);
        Assert.Equal("Культист уклоняется от атаки Сыщик.", result.Summary);
    }

    [Fact]
    [Trait("page", "101")]
    public void ResolveMeleeAttack_FightBackWins_CounterDamageToAttacker()
    {
        _attacker.Armor = 1;
        _defender.DamageBonus = "+1D4";
        var setup = Setup(40, 20);
        setup.DefenderReaction = CombatActionType.FightBack;
        setup.CounterAttackDamageFormula = "1D6";
        setup.ManualCounterWeaponDamageRoll = 4;
        setup.ManualCounterDamageBonusRoll = 2;

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.False(result.AttackerWins);
        Assert.Equal("1D6", result.CounterAttackDamageFormula);
        Assert.Equal(6, result.CounterRawDamage);
        Assert.Equal(1, result.CounterArmorReduction);
        Assert.Equal(5, result.CounterTotalDamage);
        Assert.Equal(7, result.AttackerHpAfter);
        // 5 из 12 — не серьёзная рана
        Assert.False(result.AttackerTriggeredMajorWound);
    }

    [Fact]
    [Trait("page", "101")]
    [Trait("page", "117")]
    public void ResolveMeleeAttack_FightBackMajorWound_ConRollFromSetup()
    {
        var setup = Setup(40, 20);
        setup.DefenderReaction = CombatActionType.FightBack;
        setup.ManualCounterWeaponDamageRoll = 6;
        setup.ManualCounterDamageBonusRoll = 0;
        setup.ManualCounterMajorWoundConRoll = 70;

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.True(result.AttackerTriggeredMajorWound);
        Assert.True(result.AttackerFallsProne);
        Assert.Equal(70, result.AttackerMajorWoundConRoll);
        Assert.False(result.AttackerMajorWoundConRollSuccess);
        Assert.True(result.AttackerKnockedUnconscious);
    }

    [Fact]
    [Trait("page", "118")]
    public void ResolveMeleeAttack_FightBackInstantDeath()
    {
        var setup = Setup(40, 20);
        setup.DefenderReaction = CombatActionType.FightBack;
        setup.ManualCounterWeaponDamageRoll = 12;
        setup.ManualCounterDamageBonusRoll = 0;

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.True(result.AttackerDead);
        Assert.Equal(0, result.AttackerHpAfter);
    }

    [Fact]
    [Trait("page", "101")]
    public void ResolveMeleeAttack_FightBackWithoutFormula_UsesOneD3()
    {
        var setup = Setup(40, 20);
        setup.DefenderReaction = CombatActionType.FightBack;
        setup.CounterAttackDamageFormula = " ";
        setup.ManualCounterDamageBonusRoll = 0;
        using var _ = ScriptedRandom.Use(3);

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.Equal("1D3", result.CounterAttackDamageFormula);
        Assert.Equal(3, result.CounterDamageRolled);
    }

    [Fact]
    [Trait("page", "105")]
    public void ResolveMeleeAttack_AutoHit_OnlyFumbleMisses()
    {
        var setup = Setup(70);
        setup.SurpriseMode = SurpriseMode.AutoHit;

        var result = _combat.ResolveMeleeAttack(setup);

        // Провал броска — всё равно попадание; цель не бросает
        Assert.True(result.AttackerWins);
        Assert.Equal(SuccessLevel.Failure, result.AttackerSuccessLevel);
        Assert.Equal(0, result.DefenderRoll);
        Assert.Equal(2, result.TotalDamage);
        Assert.Equal(0, _defender.DefenseCountThisRound);
    }

    [Fact]
    [Trait("page", "105")]
    public void ResolveMeleeAttack_AutoHit_Fumble_NoDamage()
    {
        var setup = Setup(100);
        setup.SurpriseMode = SurpriseMode.AutoHit;

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.False(result.AttackerWins);
        Assert.Equal(12, result.DefenderHpAfter);
        Assert.Contains("крах при внезапной атаке", result.Summary);
    }

    [Fact]
    [Trait("page", "105")]
    public void ResolveMeleeAttack_BonusDieSurprise_DefenderDoesNotRoll()
    {
        var setup = Setup(40);
        setup.SurpriseMode = SurpriseMode.BonusDie;

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.True(result.AttackerWins);
        Assert.Equal(1, result.Modifiers.BonusDice);
        Assert.Equal(0, result.DefenderRoll);
        Assert.Equal(SuccessLevel.Failure, result.DefenderSuccessLevel);
        Assert.Equal(0, _defender.DefenseCountThisRound);
    }

    [Fact]
    [Trait("page", "89")]
    public void ResolveMeleeAttack_AutoRoll_UsesModifierDice()
    {
        // Цель повалена: +1 бонусная кость атакующему — бросаются две кости десятков
        _defender.IsProne = true;
        var setup = Setup(0, 80);
        setup.ManualAttackerRoll = null;
        using var dice = ScriptedRandom.Use(5, 7, 1);

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.Equal(15, result.AttackerRoll);
        Assert.Equal([75, 15], result.AttackerRollDetail!.Candidates);
        Assert.Equal(0, dice.Remaining);
    }

    [Fact]
    [Trait("page", "89")]
    public void ResolveMeleeAttack_PreRolledDetail_WinsOverManualRoll()
    {
        var setup = Setup(90, 80);
        setup.AttackerRollDetail = DiceRollResult.Plain(30);

        var result = _combat.ResolveMeleeAttack(setup);

        Assert.Equal(30, result.AttackerRoll);
        Assert.True(result.AttackerWins);
    }

    /// <summary>
    ///     Атака меняет участников ещё до «Применить»: ход, число атак, защиты цели, прицел.
    ///     «Отменить» результат этого не возвращает.
    /// </summary>
    [Fact]
    [Trait("page", "106")]
    [Trait("finding", "F-C02")]
    public void ResolveMeleeAttack_ChangesCombatantsBeforeApply()
    {
        _attacker.IsAiming = true;

        var result = _combat.ResolveMeleeAttack(Setup(40, 20));
        _combat.CancelPendingResult();

        Assert.False(result.AttackerWins);
        Assert.True(_attacker.HasActedThisRound);
        Assert.Equal(1, _attacker.AttacksThisRound);
        Assert.False(_attacker.IsAiming);
        Assert.True(_defender.HasDefendedThisRound);
        Assert.Equal(1, _defender.DefenseCountThisRound);
        Assert.Empty(_combat.CombatLog);
    }
}
