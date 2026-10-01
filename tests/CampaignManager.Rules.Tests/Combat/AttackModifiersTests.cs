using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Rules.Tests.Combat;

/// <summary>
///     Бонусные и штрафные кости атаки, внезапность, встречный бросок и численное превосходство.
///     Каждое условие — отдельно, остальные выключены.
/// </summary>
public sealed class AttackModifiersTests
{
    private static AttackModifiers Calc(AttackSetup setup, Combatant? attacker = null, Combatant? defender = null) =>
        CombatService.CalculateAttackModifiers(setup, attacker, defender);

    [Theory]
    [Trait("page", "106")]
    [InlineData(true)]
    [InlineData(false)]
    public void CalculateAttackModifiers_NoConditions_NoDice(bool isMelee)
    {
        var modifiers = Calc(new AttackSetup { IsMelee = isMelee }, new Combatant(), new Combatant());

        Assert.Equal(0, modifiers.BonusDice);
        Assert.Equal(0, modifiers.PenaltyDice);
        Assert.Empty(modifiers.Reasons);
    }

    [Fact]
    [Trait("page", "106")]
    public void CalculateAttackModifiers_KeeperDice_AddedAsIs_NotCancelled()
    {
        var modifiers = Calc(new AttackSetup { IsMelee = true, BonusDice = 2, PenaltyDice = 1 });

        Assert.Equal(2, modifiers.BonusDice);
        Assert.Equal(1, modifiers.PenaltyDice);
        Assert.Equal(1, modifiers.Net);
        Assert.Equal(["+2 от Хранителя", "−1 от Хранителя"], modifiers.Reasons);
    }

    [Fact]
    [Trait("page", "106")]
    public void CalculateAttackModifiers_NegativeKeeperDice_TreatedAsZero()
    {
        var modifiers = Calc(new AttackSetup { BonusDice = -1, PenaltyDice = -2 });

        Assert.Equal(0, modifiers.BonusDice);
        Assert.Equal(0, modifiers.PenaltyDice);
        Assert.Empty(modifiers.Reasons);
    }

    [Theory]
    [Trait("page", "105")]
    [InlineData(true, SurpriseMode.BonusDie, 1)]
    [InlineData(false, SurpriseMode.BonusDie, 1)]
    [InlineData(true, SurpriseMode.AutoHit, 0)] // в ближнем бою автопопадание костей не даёт
    [InlineData(false, SurpriseMode.AutoHit, 1)] // в стрельбе понижено до бонусной кости
    [InlineData(true, SurpriseMode.TargetReady, 0)]
    public void CalculateAttackModifiers_Surprise(bool isMelee, SurpriseMode mode, int bonus)
    {
        var modifiers = Calc(new AttackSetup { IsMelee = isMelee, SurpriseMode = mode });

        Assert.Equal(bonus, modifiers.BonusDice);
        Assert.Equal(0, modifiers.PenaltyDice);
        if (bonus > 0)
            Assert.Equal(["+1 цель застигнута врасплох"], modifiers.Reasons);
    }

    /// <summary>Сценарий → (бонусные, штрафные, единственная причина).</summary>
    [Theory]
    [Trait("page", "106")]
    [Trait("page", "110-114")]
    [InlineData("melee-prone-target", 1, 0, "+1 цель повалена")]
    [InlineData("melee-superiority", 1, 0, "+1 численное превосходство")]
    [InlineData("point-blank", 1, 0, "+1 стрельба в упор")]
    [InlineData("aiming-setup", 1, 0, "+1 прицеливание")]
    [InlineData("aiming-state", 1, 0, "+1 прицеливание")]
    [InlineData("aiming-both", 1, 0, "+1 прицеливание")]
    [InlineData("shooter-prone", 1, 0, "+1 стрельба лёжа")]
    [InlineData("big-target", 1, 0, "+1 крупная цель")]
    [InlineData("taking-cover-setup", 0, 1, "−1 цель укрылась от огня")]
    [InlineData("taking-cover-state", 0, 1, "−1 цель укрылась от огня")]
    [InlineData("taking-cover-both", 0, 1, "−1 цель укрылась от огня")]
    [InlineData("behind-cover", 0, 1, "−1 частичное укрытие")]
    [InlineData("fast-moving", 0, 1, "−1 быстро движущаяся цель")]
    [InlineData("into-melee", 0, 1, "−1 стрельба в ближнем бою")]
    [InlineData("pistol-burst", 0, 1, "−1 серия выстрелов")]
    [InlineData("reload-and-fire", 0, 1, "−1 зарядка и выстрел")]
    [InlineData("small-target", 0, 1, "−1 мелкая цель")]
    [InlineData("ranged-prone-target", 0, 1, "−1 цель лежит")]
    [InlineData("point-blank-prone-target", 1, 0, "+1 стрельба в упор")]
    [InlineData("volley-second", 0, 1, "−1 проверка №2 при автоматической стрельбе")]
    [InlineData("volley-third", 0, 2, "−2 проверка №3 при автоматической стрельбе")]
    [InlineData("volley-sixth", 0, 2, "−2 проверка №6 при автоматической стрельбе")]
    public void CalculateAttackModifiers_SingleCondition(string scenario, int bonus, int penalty, string reason)
    {
        var (setup, attacker, defender) = Scenario(scenario);

        var modifiers = Calc(setup, attacker, defender);

        Assert.Equal(bonus, modifiers.BonusDice);
        Assert.Equal(penalty, modifiers.PenaltyDice);
        Assert.Equal([reason], modifiers.Reasons);
    }

    [Theory]
    [Trait("page", "106")]
    [InlineData("melee-ignores-firearm-flags")]
    [InlineData("build-3-is-not-big")]
    [InlineData("build-minus-1-is-not-small")]
    [InlineData("volley-first-check")]
    [InlineData("single-with-autofire-index")]
    [InlineData("superiority-not-yet")]
    public void CalculateAttackModifiers_ConditionNotApplicable_NoDice(string scenario)
    {
        var (setup, attacker, defender) = Scenario(scenario);

        var modifiers = Calc(setup, attacker, defender);

        Assert.Equal(0, modifiers.BonusDice);
        Assert.Equal(0, modifiers.PenaltyDice);
    }

    private static (AttackSetup Setup, Combatant Attacker, Combatant Defender) Scenario(string name)
    {
        var attacker = new Combatant();
        var defender = new Combatant();
        var melee = new AttackSetup { IsMelee = true };
        var ranged = new AttackSetup { IsMelee = false };

        switch (name)
        {
            case "melee-prone-target":
                defender.IsProne = true;
                return (melee, attacker, defender);
            case "melee-superiority":
                defender.DefenseCountThisRound = 1;
                return (melee, attacker, defender);
            case "point-blank":
                ranged.IsPointBlank = true;
                break;
            case "aiming-setup":
                ranged.IsAiming = true;
                break;
            case "aiming-state":
                attacker.IsAiming = true;
                break;
            case "aiming-both":
                ranged.IsAiming = true;
                attacker.IsAiming = true;
                break;
            case "shooter-prone":
                attacker.IsProne = true;
                break;
            case "big-target":
                defender.Build = 4;
                break;
            case "taking-cover-setup":
                ranged.IsTargetTakingCover = true;
                break;
            case "taking-cover-state":
                defender.HasTakenCover = true;
                break;
            case "taking-cover-both":
                ranged.IsTargetTakingCover = true;
                defender.HasTakenCover = true;
                break;
            case "behind-cover":
                ranged.IsTargetBehindCover = true;
                break;
            case "fast-moving":
                ranged.IsTargetFastMoving = true;
                break;
            case "into-melee":
                ranged.IsFiringIntoMelee = true;
                break;
            case "pistol-burst":
                ranged.FiringMode = FiringMode.PistolBurst;
                break;
            case "reload-and-fire":
                ranged.IsReloadAndFire = true;
                break;
            case "small-target":
                defender.Build = -2;
                break;
            case "ranged-prone-target":
                defender.IsProne = true;
                break;
            case "point-blank-prone-target":
                ranged.IsPointBlank = true;
                defender.IsProne = true;
                break;
            case "volley-second":
                ranged.FiringMode = FiringMode.Volley;
                ranged.AutofireCheckIndex = 1;
                break;
            case "volley-third":
                ranged.FiringMode = FiringMode.Volley;
                ranged.AutofireCheckIndex = 2;
                break;
            case "volley-sixth":
                ranged.FiringMode = FiringMode.Volley;
                ranged.AutofireCheckIndex = 5;
                break;
            case "melee-ignores-firearm-flags":
                melee.IsPointBlank = true;
                melee.IsAiming = true;
                melee.IsTargetBehindCover = true;
                melee.IsTargetFastMoving = true;
                melee.IsFiringIntoMelee = true;
                melee.IsReloadAndFire = true;
                melee.FiringMode = FiringMode.Volley;
                melee.AutofireCheckIndex = 3;
                attacker.IsProne = true;
                defender.Build = 6;
                return (melee, attacker, defender);
            case "build-3-is-not-big":
                defender.Build = 3;
                break;
            case "build-minus-1-is-not-small":
                defender.Build = -1;
                break;
            case "volley-first-check":
                ranged.FiringMode = FiringMode.Volley;
                ranged.AutofireCheckIndex = 0;
                break;
            case "single-with-autofire-index":
                ranged.FiringMode = FiringMode.Single;
                ranged.AutofireCheckIndex = 3;
                break;
            case "superiority-not-yet":
                defender.DefenseCountThisRound = 1;
                defender.AttacksPerRound = 2;
                return (melee, attacker, defender);
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, null);
        }

        return (ranged, attacker, defender);
    }

    [Theory]
    [Trait("page", "105")]
    [InlineData(true, SurpriseMode.AutoHit, SurpriseMode.AutoHit)]
    [InlineData(false, SurpriseMode.AutoHit, SurpriseMode.BonusDie)]
    [InlineData(false, SurpriseMode.BonusDie, SurpriseMode.BonusDie)]
    [InlineData(false, SurpriseMode.TargetReady, SurpriseMode.TargetReady)]
    [InlineData(true, SurpriseMode.BonusDie, SurpriseMode.BonusDie)]
    public void NormalizeSurprise_RangedAutoHitBecomesBonusDie(bool isMelee, SurpriseMode mode, SurpriseMode expected)
    {
        Assert.Equal(expected, CombatService.NormalizeSurprise(new AttackSetup { IsMelee = isMelee, SurpriseMode = mode }));
    }

    [Theory]
    [Trait("page", "101")]
    [InlineData(SuccessLevel.HardSuccess, SuccessLevel.RegularSuccess, CombatActionType.Dodge, true)]
    [InlineData(SuccessLevel.RegularSuccess, SuccessLevel.HardSuccess, CombatActionType.FightBack, false)]
    // Ничья: уклонение — победа защитника, контратака — победа атакующего
    [InlineData(SuccessLevel.HardSuccess, SuccessLevel.HardSuccess, CombatActionType.Dodge, false)]
    [InlineData(SuccessLevel.HardSuccess, SuccessLevel.HardSuccess, CombatActionType.FightBack, true)]
    [InlineData(SuccessLevel.CriticalSuccess, SuccessLevel.CriticalSuccess, CombatActionType.Dodge, false)]
    [InlineData(SuccessLevel.RegularSuccess, SuccessLevel.RegularSuccess, CombatActionType.MeleeAttack, false)]
    public void ResolveOpposedRoll_HigherLevelWins_TieByReaction(
        SuccessLevel attacker, SuccessLevel defender, CombatActionType reaction, bool attackerWins)
    {
        // Значения навыков на ничью не влияют
        Assert.Equal(attackerWins, CombatService.ResolveOpposedRoll(attacker, 10, defender, 90, reaction));
        Assert.Equal(attackerWins, CombatService.ResolveOpposedRoll(attacker, 90, defender, 10, reaction));
    }

    [Theory]
    [Trait("page", "106")]
    [InlineData(0, 1, false)]
    [InlineData(1, 1, true)]
    [InlineData(2, 1, true)]
    [InlineData(1, 2, false)]
    [InlineData(2, 2, true)]
    [InlineData(1, 0, true)] // атак за раунд 0 считается как одна
    [InlineData(0, 0, false)]
    public void HasNumericalSuperiority_DefensesUsedUpToAttacksPerRound(int defenses, int attacksPerRound, bool expected)
    {
        var defender = new Combatant { DefenseCountThisRound = defenses, AttacksPerRound = attacksPerRound };

        Assert.Equal(expected, CombatService.HasNumericalSuperiority(defender));
    }

    [Fact]
    [Trait("page", "106")]
    public void HasNumericalSuperiority_NoDefender_False()
    {
        Assert.False(CombatService.HasNumericalSuperiority(null));
    }
}
