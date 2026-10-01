using CampaignManager.Web.Components.Features.Combat.Components.Attack;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Rules.Tests.Combat;

/// <summary>«Применить»: перенос результата на участников и в журнал.</summary>
public sealed class ApplyResultTests
{
    private readonly Combatant _attacker = Fighters.Make("Сыщик");
    private readonly Combatant _defender = Fighters.Make("Культист", side: CombatSide.Enemy);
    private readonly CombatService _combat;

    public ApplyResultTests() => _combat = Fighters.Battle(_attacker, _defender);

    private AttackSetup Hit(int damage) => new()
    {
        AttackerId = _attacker.Id,
        DefenderId = _defender.Id,
        IsMelee = true,
        AttackSkillValue = 50,
        SurpriseMode = SurpriseMode.BonusDie,
        ManualAttackerRoll = 40,
        ManualWeaponDamageRoll = damage,
        ManualDamageBonusRoll = 0,
        ManualMajorWoundConRoll = 90
    };

    [Fact]
    [Trait("page", "117-118")]
    public void ApplyResult_Damage_MovesHpAndWoundFlags_ResetsFirstAidAndAim()
    {
        _defender.FirstAidAttempted = true;
        _defender.IsStabilized = true;
        _defender.TemporaryHitPoints = 1;
        _defender.IsAiming = true;
        var result = _combat.ResolveMeleeAttack(Hit(7));
        _combat.SetPendingResult(result);

        _combat.ApplyResult(result);

        Assert.Equal(5, _defender.CurrentHitPoints);
        Assert.True(_defender.HasMajorWound);
        Assert.True(_defender.IsProne);
        Assert.True(_defender.IsUnconscious); // ВЫН 90 против 50
        Assert.False(_defender.FirstAidAttempted);
        Assert.False(_defender.IsStabilized);
        Assert.Equal(0, _defender.TemporaryHitPoints);
        Assert.False(_defender.IsAiming);
        Assert.Same(result, _combat.CombatLog[0]);
        Assert.Null(_combat.PendingResult);
    }

    [Fact]
    [Trait("page", "118")]
    public void ApplyResult_NoDamage_KeepsFirstAidState()
    {
        _defender.FirstAidAttempted = true;
        _defender.IsAiming = true;

        _combat.ApplyResult(_combat.ResolveMeleeAttack(Hit(0)));

        Assert.True(_defender.FirstAidAttempted);
        Assert.True(_defender.IsAiming);
    }

    [Fact]
    [Trait("page", "118")]
    public void ApplyResult_InstantDeath_MarksDead()
    {
        _combat.ApplyResult(_combat.ResolveMeleeAttack(Hit(12)));

        Assert.True(_defender.IsDead);
        Assert.Equal(0, _defender.CurrentHitPoints);
    }

    [Fact]
    [Trait("page", "101")]
    public void ApplyResult_CounterAttack_DamagesAttacker()
    {
        var setup = Hit(3);
        setup.SurpriseMode = SurpriseMode.TargetReady;
        setup.DefenderSkillValue = 50;
        setup.ManualDefenderRoll = 10;
        setup.DefenderReaction = CombatActionType.FightBack;
        setup.ManualCounterWeaponDamageRoll = 4;
        setup.ManualCounterDamageBonusRoll = 0;

        _combat.ApplyResult(_combat.ResolveMeleeAttack(setup));

        Assert.Equal(8, _attacker.CurrentHitPoints);
        Assert.Equal(12, _defender.CurrentHitPoints);
    }

    [Fact]
    [Trait("page", "177")]
    public void ApplyResult_DamageToCaster_DisruptsSpellcasting()
    {
        _defender.CastingSpell = new SpellcastInProgress { Setup = new SpellCastSetup { SpellName = "Призыв" } };

        var result = _combat.ResolveMeleeAttack(Hit(2));
        _combat.ApplyResult(result);

        Assert.True(_defender.CastingSpell!.Disrupted);
        Assert.Contains("оно сорвано", result.Summary);
    }

    /// <summary>
    ///     Проверку ВЫН при серьёзной ране от атаки вписать нельзя: у панели атаки поле есть
    ///     только приватное и лишь обнуляется — в <see cref="AttackSetup" /> всегда уходит null,
    ///     и движок бросает сам.
    /// </summary>
    [Fact]
    [Trait("page", "117")]
    [Trait("finding", "F-C05")]
    public void AttackSetupState_HasNoWayToEnterMajorWoundConRoll()
    {
        var state = new AttackSetupState();

        var setup = state.BuildSetup();

        Assert.Null(setup.ManualMajorWoundConRoll);
        Assert.Null(setup.ManualCounterMajorWoundConRoll);
        Assert.DoesNotContain(typeof(AttackSetupState).GetMembers(),
            m => m.Name.Contains("ConRoll", StringComparison.Ordinal)
                 || m.Name.Contains("MajorWound", StringComparison.Ordinal));
    }
}
