using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Rules.Tests.Combat;

/// <summary>Боевые манёвры: Комплекция, встречная проверка и эффект при «Применить».</summary>
[Trait("page", "103")]
public sealed class ManeuverTests
{
    private readonly Combatant _attacker = Fighters.Make("Сыщик");
    private readonly Combatant _defender = Fighters.Make("Культист", side: CombatSide.Enemy);
    private readonly CombatService _combat;

    public ManeuverTests() => _combat = Fighters.Battle(_attacker, _defender);

    private ManeuverSetup Setup(ManeuverType type, int attackRoll = 20, int defenseRoll = 80) => new()
    {
        AttackerId = _attacker.Id,
        DefenderId = _defender.Id,
        ManeuverType = type,
        AttackSkillValue = 50,
        DefenderSkillValue = 40,
        ManualAttackerRoll = attackRoll,
        ManualDefenderRoll = defenseRoll
    };

    [Theory]
    [InlineData(0, 3)]
    [InlineData(-1, 2)]
    [InlineData(0, 5)]
    public void ResolveManeuver_BuildGapThreeOrMore_Impossible_NoTurnSpent(int attackerBuild, int defenderBuild)
    {
        var setup = Setup(ManeuverType.KnockDown);
        setup.AttackerBuild = attackerBuild;
        setup.DefenderBuild = defenderBuild;

        var result = _combat.ResolveManeuver(setup);

        Assert.False(result.AttackerWins);
        Assert.False(result.ManeuverSucceeded);
        Assert.Contains("невозможен", result.Summary);
        Assert.Equal(0, _attacker.AttacksThisRound);
        Assert.Equal(0, result.AttackerRoll);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 0)] // атакующий крупнее — штрафа нет, бонуса тоже
    [InlineData(0, 1, 1)]
    [InlineData(0, 2, 2)]
    public void ResolveManeuver_BuildGap_PenaltyDicePerPoint(int attackerBuild, int defenderBuild, int penalty)
    {
        var setup = Setup(ManeuverType.KnockDown);
        setup.AttackerBuild = attackerBuild;
        setup.DefenderBuild = defenderBuild;
        setup.PenaltyDice = 1;
        setup.BonusDice = 1;

        var result = _combat.ResolveManeuver(setup);

        Assert.Equal(1, result.Modifiers.BonusDice);
        Assert.Equal(1 + penalty, result.Modifiers.PenaltyDice);
        Assert.Equal(penalty > 0,
            result.Modifiers.Reasons.Contains($"−{penalty} разница Комплекции ({attackerBuild} против {defenderBuild})"));
    }

    [Theory]
    [InlineData(20, 80, CombatActionType.Dodge, true)] // цель провалила защиту
    [InlineData(60, 10, CombatActionType.Dodge, false)] // атакующий провалил
    [InlineData(20, 20, CombatActionType.Dodge, false)] // оба трудный: уклонение побеждает в ничьей
    [InlineData(20, 20, CombatActionType.FightBack, true)] // ничья при контратаке — атакующему
    [InlineData(40, 8, CombatActionType.Dodge, false)] // у цели чрезвычайный
    public void ResolveManeuver_OpposedRoll(int attackRoll, int defenseRoll, CombatActionType reaction, bool succeeded)
    {
        var setup = Setup(ManeuverType.Grapple, attackRoll, defenseRoll);
        setup.DefenderReaction = reaction;

        var result = _combat.ResolveManeuver(setup);

        Assert.Equal(succeeded, result.ManeuverSucceeded);
        Assert.Equal(succeeded, result.AttackerWins);
        Assert.Equal(12, result.DefenderHpAfter);
        Assert.Equal(1, _attacker.AttacksThisRound);
    }

    [Fact]
    public void ResolveManeuver_DefenderCountedAsDefended_UnlessAttackerFailed()
    {
        _combat.ResolveManeuver(Setup(ManeuverType.Push, 60));
        Assert.Equal(0, _defender.DefenseCountThisRound);

        _combat.ResolveManeuver(Setup(ManeuverType.Push, 20));
        Assert.Equal(1, _defender.DefenseCountThisRound);
    }

    [Theory]
    [InlineData(ManeuverType.KnockDown)]
    [InlineData(ManeuverType.Push)]
    public void ApplyResult_KnockDownOrPush_TargetProne(ManeuverType type)
    {
        _combat.ApplyResult(_combat.ResolveManeuver(Setup(type)));

        Assert.True(_defender.IsProne);
    }

    [Fact]
    public void ApplyResult_Grapple_TargetHeldByAttacker()
    {
        _combat.ApplyResult(_combat.ResolveManeuver(Setup(ManeuverType.Grapple)));

        Assert.True(_defender.IsGrappled);
        Assert.Equal(_attacker.Id, _defender.GrappledBy);
    }

    [Fact]
    public void ApplyResult_DisarmAndDisadvantage_MarkTarget()
    {
        _combat.ApplyResult(_combat.ResolveManeuver(Setup(ManeuverType.Disarm)));
        _combat.ApplyResult(_combat.ResolveManeuver(Setup(ManeuverType.Disadvantage)));

        Assert.True(_defender.IsDisarmed);
        Assert.True(_defender.HasDisadvantage);
    }

    [Fact]
    [Trait("page", "123")]
    public void ApplyResult_Knockout_OneDamageAndUnconscious()
    {
        _combat.ApplyResult(_combat.ResolveManeuver(Setup(ManeuverType.Knockout)));

        Assert.Equal(11, _defender.CurrentHitPoints);
        Assert.True(_defender.IsUnconscious);
    }

    [Fact]
    public void ApplyResult_FailedManeuver_NoEffect()
    {
        _combat.ApplyResult(_combat.ResolveManeuver(Setup(ManeuverType.KnockDown, 60)));

        Assert.False(_defender.IsProne);
        Assert.Single(_combat.CombatLog);
    }

    /// <summary>
    ///     «Вырваться» снимает захват с цели манёвра — того, кто держит, — а сам вырвавшийся
    ///     остаётся «в захвате».
    /// </summary>
    [Fact]
    [Trait("finding", "F-C08")]
    public void ApplyResult_BreakFree_ClearsDefenderNotAttacker()
    {
        _attacker.IsGrappled = true;
        _attacker.GrappledBy = _defender.Id;

        var result = _combat.ResolveManeuver(Setup(ManeuverType.BreakFree));
        _combat.ApplyResult(result);

        Assert.True(result.ManeuverSucceeded);
        Assert.True(_attacker.IsGrappled);
        Assert.Equal(_defender.Id, _attacker.GrappledBy);
        Assert.False(_defender.IsGrappled);
    }
}
