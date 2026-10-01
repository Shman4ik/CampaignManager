using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Rules.Tests.Combat;

/// <summary>Раунд и очередь: инициатива, ход, отложенный ход, укрытие, лимит атак, Удача против обморока.</summary>
public sealed class RoundAndTurnTests
{
    [Fact]
    [Trait("page", "110")]
    public void SortByInitiative_DescendingDex_FirearmReadyPlusFifty_TieByCombatSkill()
    {
        var slow = new Combatant { Name = "Медленный", Initiative = 40, HasFirearmReady = true };
        var fast = new Combatant { Name = "Быстрый", Initiative = 80 };
        var brawler = new Combatant { Name = "Драчун", Initiative = 60, FightingSkill = 70 };
        var dodger = new Combatant { Name = "Ловкач", Initiative = 60, DodgeSkill = 50 };
        var combat = Fighters.Battle(dodger, slow, brawler, fast);

        combat.SortByInitiative();

        // 40 + 50 за огнестрельное на изготовку = 90
        Assert.Equal(["Медленный", "Быстрый", "Драчун", "Ловкач"], combat.Combatants.Select(c => c.Name));
    }

    [Fact]
    [Trait("page", "122")]
    public void RollInitiative_OrderByLevel_FirearmGetsBonusDie_CriticalAndFumbleMarked()
    {
        var a = new Combatant { Name = "А", Initiative = 50 };
        var b = new Combatant { Name = "Б", Initiative = 70, HasFirearmReady = true };
        var c = new Combatant { Name = "В", Initiative = 40 };
        var d = new Combatant { Name = "Г", Initiative = 60 };
        var combat = Fighters.Battle(a, b, c, d);
        combat.UseInitiativeRolls = true;

        // А: 01. Б: бонусная кость, единицы 0, десятки 0 и 0 — 100 и 100, крах.
        // В: 30 — обычный. Г: 30 — трудный (≤ 30), выше В.
        using var dice = ScriptedRandom.Use(1, 0, 0, 0, 0, 0, 3, 0, 3);

        combat.RollInitiative();

        Assert.Equal(0, dice.Remaining);
        Assert.True(combat.InitiativeRolled);
        Assert.Equal(["А", "Г", "В", "Б"], combat.Combatants.Select(x => x.Name));
        Assert.True(a.HasTacticalAdvantage);
        Assert.True(b.SkipsTurnFromFumble);
        Assert.Equal([100, 100], b.InitiativeRollDetail!.Candidates);
        Assert.Equal((int)SuccessLevel.HardSuccess, d.InitiativeRollLevel);
    }

    [Fact]
    [Trait("page", "122")]
    public void RollInitiative_OrderHeldAfterDexChange_ClearRestoresDexOrder()
    {
        var a = new Combatant { Name = "А", Initiative = 50 };
        var b = new Combatant { Name = "Б", Initiative = 70 };
        var combat = Fighters.Battle(a, b);
        combat.UseInitiativeRolls = true;
        using (ScriptedRandom.Use(5, 0, 0, 9)) // А: 05 — чрезвычайный; Б: 90 — провал
            combat.RollInitiative();

        b.Initiative = 99;
        combat.SortByInitiative();
        Assert.Equal(["А", "Б"], combat.Combatants.Select(x => x.Name));

        combat.ClearInitiativeRolls();
        Assert.False(combat.InitiativeRolled);
        Assert.Null(a.InitiativeRoll);
        Assert.Equal(["Б", "А"], combat.Combatants.Select(x => x.Name));
    }

    [Fact]
    [Trait("page", "122")]
    public void RollInitiative_OptionalRuleOff_RollsButSortsByDex()
    {
        var a = new Combatant { Name = "А", Initiative = 50 };
        var b = new Combatant { Name = "Б", Initiative = 70 };
        var combat = Fighters.Battle(a, b);
        using var _ = ScriptedRandom.Use(1, 0, 9, 9);

        combat.RollInitiative();

        Assert.True(combat.InitiativeRolled);
        Assert.Equal(["Б", "А"], combat.Combatants.Select(x => x.Name));
    }

    [Fact]
    [Trait("page", "?")]
    public void NextTurn_WrapsToNewRound_ResetsRoundTracking()
    {
        var a = Fighters.Make("А");
        var b = Fighters.Make("Б");
        var combat = Fighters.Battle(a, b);
        a.AttacksThisRound = 1;
        a.HasActedThisRound = true;
        a.DefenseCountThisRound = 2;
        a.HasTakenCover = true;
        a.AutofireChecksThisRound = 3;
        a.IsAiming = true;

        combat.NextTurn();
        Assert.Equal(1, combat.CurrentTurnIndex);
        Assert.Equal(1, combat.CurrentRound);

        combat.NextTurn();
        Assert.Equal(0, combat.CurrentTurnIndex);
        Assert.Equal(2, combat.CurrentRound);
        Assert.Equal(0, a.AttacksThisRound);
        Assert.False(a.HasActedThisRound);
        Assert.Equal(0, a.DefenseCountThisRound);
        Assert.False(a.HasTakenCover);
        Assert.Equal(0, a.AutofireChecksThisRound);
        // Прицел живёт до выстрела, урона или движения (стр. 111)
        Assert.True(a.IsAiming);
    }

    [Fact]
    [Trait("page", "?")]
    public void DelayTurn_EarlierCombatant_MovesToEnd_ActiveStaysSame()
    {
        var (combat, a, b, c) = ThreeInRow();
        combat.NextTurn(); // ходит Б

        combat.DelayTurn(a.Id);

        Assert.Equal([b, c, a], combat.Combatants);
        Assert.Same(b, combat.GetActiveCombatant());
        Assert.True(a.IsDelayed);
    }

    /// <summary>
    ///     Отложивший ход сам и есть текущий: ход должен перейти к следующему (В), а откатывается
    ///     к предыдущему (А), уже ходившему.
    /// </summary>
    [Fact]
    [Trait("page", "?")]
    [Trait("finding", "F-C03")]
    public void DelayTurn_CurrentCombatant_TurnRollsBackToPrevious()
    {
        var (combat, a, b, c) = ThreeInRow();
        combat.NextTurn(); // ходит Б

        combat.DelayTurn(b.Id);

        Assert.Equal([a, c, b], combat.Combatants);
        Assert.Equal(0, combat.CurrentTurnIndex);
        Assert.Same(a, combat.GetActiveCombatant());
    }

    [Fact]
    [Trait("page", "?")]
    public void DelayTurn_FirstCombatantOnFirstTurn_NextBecomesActive()
    {
        var (combat, a, b, _) = ThreeInRow();

        combat.DelayTurn(a.Id);

        Assert.Same(b, combat.GetActiveCombatant());
    }

    [Fact]
    [Trait("page", "?")]
    public void RemoveCombatant_AfterCurrent_ActiveStaysSame()
    {
        var (combat, _, b, c) = ThreeInRow();
        combat.NextTurn();

        combat.RemoveCombatant(c);

        Assert.Same(b, combat.GetActiveCombatant());
    }

    /// <summary>
    ///     Удалён участник выше текущего: индекс не сдвигается, и ход перескакивает через того,
    ///     кто ходил.
    /// </summary>
    [Fact]
    [Trait("page", "?")]
    [Trait("finding", "F-C04")]
    public void RemoveCombatant_BeforeCurrent_SkipsCurrentTurn()
    {
        var (combat, a, _, c) = ThreeInRow();
        var d = Fighters.Make("Г");
        combat.AddCombatant(d);
        combat.NextTurn();
        combat.NextTurn(); // ходит В

        combat.RemoveCombatant(a);

        Assert.Equal(2, combat.CurrentTurnIndex);
        Assert.Same(d, combat.GetActiveCombatant());
        Assert.NotSame(c, combat.GetActiveCombatant());
    }

    [Fact]
    [Trait("page", "?")]
    [Trait("finding", "F-C04")]
    public void RemoveCombatant_BeforeLastCurrent_JumpsToFirst()
    {
        var (combat, a, b, _) = ThreeInRow();
        combat.NextTurn();
        combat.NextTurn(); // ходит В — последний

        combat.RemoveCombatant(a);

        Assert.Equal(0, combat.CurrentTurnIndex);
        Assert.Same(b, combat.GetActiveCombatant());
    }

    [Theory]
    [Trait("page", "111")]
    [InlineData(0, 1)] // ещё не атаковал — теряет атаку этого раунда
    [InlineData(1, 2)] // уже атаковал — следующего
    public void TakeCover_BlocksNextAttack(int attacksThisRound, int blockedRound)
    {
        var a = Fighters.Make("А");
        var combat = Fighters.Battle(a);
        a.AttacksThisRound = attacksThisRound;

        combat.TakeCover(a);

        Assert.True(a.HasTakenCover);
        Assert.Equal(blockedRound, a.AttackBlockedInRound);
    }

    [Fact]
    [Trait("page", "111")]
    public void TakeCover_AfterAttack_BlockSurvivesIntoNextRoundOnly()
    {
        var a = Fighters.Make("А");
        var combat = Fighters.Battle(a);
        a.AttacksThisRound = 1;
        combat.TakeCover(a);

        a.AttacksThisRound = 0;
        Assert.Null(combat.GetAttackBlockReason(a)); // этот раунд свободен

        combat.NextRound();
        Assert.Contains("укрывался от огня", combat.GetAttackBlockReason(a));

        combat.NextRound();
        Assert.Null(combat.GetAttackBlockReason(a));
        Assert.Null(a.AttackBlockedInRound);
    }

    [Theory]
    [Trait("page", "100")]
    [InlineData(1, 0, false)]
    [InlineData(1, 1, true)]
    [InlineData(3, 2, false)]
    [InlineData(3, 3, true)]
    [InlineData(0, 1, true)] // ноль атак — как одна
    public void GetAttackBlockReason_AttacksPerRoundLimit(int perRound, int made, bool blocked)
    {
        var a = Fighters.Make("А");
        a.AttacksPerRound = perRound;
        a.AttacksThisRound = made;
        var combat = Fighters.Battle(a);

        Assert.Equal(blocked, combat.GetAttackBlockReason(a) is not null);
    }

    [Fact]
    [Trait("page", "241")]
    public void GetAttackBlockReason_CastingSpell_FirstReason()
    {
        var a = Fighters.Make("А");
        a.CastingSpell = new SpellcastInProgress { Setup = new SpellCastSetup { SpellName = "Призыв" }, CompletesInRound = 3 };
        var combat = Fighters.Battle(a);

        Assert.StartsWith("А творит «Призыв» (сработает в раунде 3)", combat.GetAttackBlockReason(a));
    }

    [Theory]
    [Trait("page", "123")]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(3, 4)]
    [InlineData(7, 8)]
    [InlineData(15, 16)]
    [InlineData(2, 4)] // вне ряда 1+2+4… — округляется вверх до следующей ступени
    public void GetLuckCostToStayConscious_Doubles(int spent, int cost)
    {
        Assert.Equal(cost, CombatService.GetLuckCostToStayConscious(new Combatant { LuckSpentToStayConscious = spent }));
    }

    [Fact]
    [Trait("page", "123")]
    public void SpendLuckToStayConscious_OnlyWithOptionalRule_AndEnoughLuck()
    {
        var a = Fighters.Make("А");
        a.Luck = 2;
        a.IsUnconscious = true;
        var combat = Fighters.Battle(a);

        Assert.False(combat.SpendLuckToStayConscious(a));

        combat.UseLuckToStayConscious = true;
        Assert.True(combat.SpendLuckToStayConscious(a)); // 1
        Assert.False(a.IsUnconscious);
        Assert.False(combat.SpendLuckToStayConscious(a)); // нужно 2, осталась 1
        Assert.Equal(1, a.Luck);
        Assert.Equal(1, a.LuckSpentToStayConscious);
    }

    [Fact]
    [Trait("page", "118")]
    public void GetDyingCombatants_ExcludesDeadAndStabilized()
    {
        var dying = Fighters.Make("Умирающий");
        dying.IsDying = true;
        var dead = Fighters.Make("Мёртвый");
        dead.IsDying = true;
        dead.IsDead = true;
        var stabilized = Fighters.Make("Перевязанный");
        stabilized.IsDying = true;
        stabilized.IsStabilized = true;
        var combat = Fighters.Battle(dying, dead, stabilized);

        Assert.Equal([dying], combat.GetDyingCombatants());
    }

    private static (CombatService Combat, Combatant A, Combatant B, Combatant C) ThreeInRow()
    {
        var a = Fighters.Make("А");
        var b = Fighters.Make("Б");
        var c = Fighters.Make("В");
        return (Fighters.Battle(a, b, c), a, b, c);
    }
}
