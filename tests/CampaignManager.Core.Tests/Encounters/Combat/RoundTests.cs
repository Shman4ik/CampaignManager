using CampaignManager.Core.Dice;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Tests.Infrastructure;
using static CampaignManager.Core.Tests.Encounters.Combat.Fighters;

namespace CampaignManager.Core.Tests.Encounters.Combat;

/// <summary>
/// Раунд боя (перенесено из T0.2 <c>RoundAndTurnTests</c>, кроме очереди — она в <c>EncounterQueueTests</c>): инициатива с
/// огнестрелом и броском, счётчики раунда, укрытие, лимит атак, Удача против обморока.
/// </summary>
public sealed class RoundTests
{
    private static EncounterParticipant With(string name, int dex, bool firearm = false)
    {
        var p = Make(name, dex: dex);
        p.Combat.FirearmReady = firearm;
        return p;
    }

    private static List<string> Names(EncounterState state) =>
        [.. EncounterQueue.Order(state).Select(id => state.Find(id)!.Name)];

    [Fact]
    [Trait("page", "110")]
    public void Order_DescendingDex_FirearmReadyPlusFifty()
    {
        var state = new EncounterState();
        foreach (var p in new[] { With("Ловкач", 60), With("Медленный", 40, firearm: true), With("Быстрый", 80) })
            EncounterEngine.Add(state, p, Now);

        // 40 + 50 за огнестрел наготове = 90
        Assert.Equal(["Медленный", "Быстрый", "Ловкач"], Names(state));
    }

    [Fact]
    [Trait("page", "118")]
    public void Dead_GetsNoTurn()
    {
        var a = Make("А", dex: 80);
        var b = Make("Б", dex: 60);
        var c = Make("В", dex: 40);
        var state = Battle(a, b, c);
        b.Dead = true;

        EncounterQueue.Next(state, Now);

        Assert.Equal(c.Id, state.ActiveParticipantId);
    }

    [Fact]
    [Trait("page", "122")]
    public void RollInitiative_OrderByLevel_FirearmGetsBonusDie_CriticalAndFumbleNoted()
    {
        var state = new EncounterState();
        var a = With("А", 50);
        var b = With("Б", 70, firearm: true);
        var c = With("В", 40);
        var d = With("Г", 60);
        foreach (var p in new[] { a, b, c, d })
            EncounterEngine.Add(state, p, Now);

        // Порядок бросков — по очереди до броска: Б (70+50), Г (60), А (50), В (40).
        // Б: бонусная кость, единицы 0, десятки 0 и 0 — 100 и 100, крах. Г: 30 — трудный. А: 01. В: 30 — обычный.
        var dice = ScriptedDice.Of(0, 0, 0, 0, 3, 1, 0, 0, 3);
        CombatRules.RollInitiative(state, new Dictionary<Guid, D100Roll>(), dice, Now);

        Assert.Equal(0, dice.Remaining);
        Assert.True(state.Combat.InitiativeRolled);
        Assert.Equal(["А", "Г", "В", "Б"], Names(state));
        Assert.Equal(SuccessLevel.Fumble, b.Combat.InitiativeLevel);
        Assert.Equal(SuccessLevel.Hard, d.Combat.InitiativeLevel);
        Assert.Contains(state.Log[^1].Lines, l => l.Contains("тактическое преимущество", StringComparison.Ordinal));
        Assert.Contains(state.Log[^1].Lines, l => l.Contains("пропускает первый ход", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("page", "122")]
    public void RollInitiative_EnteredRolls_ClearRestoresDexOrder()
    {
        var state = new EncounterState();
        var a = With("А", 50);
        var b = With("Б", 70);
        EncounterEngine.Add(state, a, Now);
        EncounterEngine.Add(state, b, Now);

        CombatRules.RollInitiative(state, new Dictionary<Guid, D100Roll> { [a.Id] = Rolled(5), [b.Id] = Rolled(90) }, ScriptedDice.Of(), Now);
        b.Initiative = 99;
        Assert.Equal(["А", "Б"], Names(state)); // порядок броска держится до конца боя

        CombatRules.ClearInitiative(state, Now);
        Assert.Null(a.Combat.InitiativeRoll);
        Assert.Equal(["Б", "А"], Names(state));
    }

    [Fact]
    [Trait("page", "106")]
    public void RoundCounters_ResetByThemselvesInNewRound()
    {
        var a = Make("А");
        var b = Make("Б");
        var state = Battle(a, b);
        Apply(state, new EncounterResolution
        {
            Effects =
            [
                new EncounterEffect { Kind = EncounterEffectKind.Attack, ParticipantId = a.Id, Amount = 1 },
                new EncounterEffect { Kind = EncounterEffectKind.Defense, ParticipantId = a.Id },
                new EncounterEffect { Kind = EncounterEffectKind.Autofire, ParticipantId = a.Id },
                new EncounterEffect { Kind = EncounterEffectKind.Aim, ParticipantId = a.Id, Flag = true },
            ],
        });
        Assert.Equal(1, a.Combat.AttacksIn(1));

        EncounterQueue.Next(state, Now);
        EncounterQueue.Next(state, Now); // раунд 2

        Assert.Equal(0, a.Combat.AttacksIn(state.Round));
        Assert.Equal(0, a.Combat.DefensesIn(state.Round));
        Assert.Equal(0, a.Combat.AutofireIn(state.Round));
        // Прицел живёт до выстрела, урона или движения (стр. 111)
        Assert.True(a.Combat.Aiming);
    }

    [Theory]
    [Trait("page", "111")]
    [InlineData(0, 1)] // ещё не атаковал — теряет атаку этого раунда
    [InlineData(1, 2)] // уже атаковал — следующего
    public void TakeCover_BlocksNextAttack(int attacksThisRound, int blockedRound)
    {
        var a = Make("А");
        var state = Battle(a);
        a.Combat.Touch(1);
        a.Combat.AttacksMade = attacksThisRound;

        Apply(state, CombatRules.TakeCover(state, a.Id, Rolled(10), ScriptedDice.Of()));

        Assert.True(a.Combat.TakingCoverIn(1));
        Assert.Equal(blockedRound, a.Combat.AttackBlockedRound);
    }

    [Fact]
    [Trait("page", "111")]
    public void TakeCover_FailedDodge_NoCover()
    {
        var a = Make("А");
        var state = Battle(a);

        Assert.Empty(CombatRules.TakeCover(state, a.Id, Rolled(90), ScriptedDice.Of()).Effects);
    }

    [Fact]
    [Trait("page", "111")]
    public void TakeCover_AfterAttack_BlocksNextRoundOnly()
    {
        var a = Make("А");
        var state = Battle(a);
        a.Combat.Touch(1);
        a.Combat.AttacksMade = 1;
        Apply(state, CombatRules.TakeCover(state, a.Id, Rolled(10), ScriptedDice.Of()));
        a.Combat.AttacksMade = 0;
        Assert.Null(CombatRules.AttackBlockReason(state, a)); // этот раунд свободен

        EncounterQueue.Next(state, Now);
        Assert.Contains("укрывался от огня", CombatRules.AttackBlockReason(state, a));

        EncounterQueue.Next(state, Now);
        Assert.Null(CombatRules.AttackBlockReason(state, a));
    }

    [Theory]
    [Trait("page", "100")]
    [InlineData(1, 0, false)]
    [InlineData(1, 1, true)]
    [InlineData(3, 2, false)]
    [InlineData(3, 3, true)]
    [InlineData(0, 1, true)] // ноль атак — как одна
    public void AttackBlockReason_AttacksPerRoundLimit(int perRound, int made, bool blocked)
    {
        var a = Make("А");
        var state = Battle(a);
        a.Profile.AttacksPerRound = perRound;
        a.Combat.Touch(1);
        a.Combat.AttacksMade = made;

        Assert.Equal(blocked, CombatRules.AttackBlockReason(state, a) is not null);
    }

    [Fact]
    [Trait("page", "241")]
    public void AttackBlockReason_CastingSpell_FirstReason()
    {
        var a = Make("А");
        a.Combat.Casting = new SpellCasting { SpellName = "Призыв", CompletesInRound = 3 };
        var state = Battle(a);

        Assert.StartsWith("А творит «Призыв» (сработает в раунде 3)", CombatRules.AttackBlockReason(state, a));
    }

    [Theory]
    [Trait("page", "123")]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(3, 4)]
    [InlineData(7, 8)]
    [InlineData(15, 16)]
    [InlineData(2, 4)] // вне ряда 1+2+4… — до следующей ступени
    public void LuckCostToStayConscious_Doubles(int spent, int cost) =>
        Assert.Equal(cost, CombatRules.LuckCostToStayConscious(spent));

    [Fact]
    [Trait("page", "123")]
    public void StayConscious_OnlyWithOptionalRule_AndEnoughLuck()
    {
        var a = Make("А");
        a.Luck = 2;
        a.Unconscious = true;
        var state = Battle(a);

        Assert.Null(CombatRules.StayConscious(state, a.Id));

        state.Combat.LuckToStayConscious = true;
        Apply(state, CombatRules.StayConscious(state, a.Id)!); // 1
        Assert.False(a.Unconscious);
        Assert.Equal(1, a.Luck);
        Assert.Null(CombatRules.StayConscious(state, a.Id)); // нужно 2, осталась 1
        Assert.Equal(1, a.Combat.LuckSpentToStayConscious);
    }
}
