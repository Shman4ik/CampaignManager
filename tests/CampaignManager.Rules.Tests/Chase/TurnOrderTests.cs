using CampaignManager.Rules.Tests.Infrastructure;
using CampaignManager.Web.Components.Features.Chase.Model;
using CampaignManager.Web.Components.Features.Chase.Services;
using static CampaignManager.Rules.Tests.Chase.ChaseScene;

namespace CampaignManager.Rules.Tests.Chase;

/// <summary>Порядок ходов по ЛВК, ничья по ЛВК, отложенное действие, выбывание.</summary>
[Trait("page", "131-132")]
public sealed class TurnOrderTests
{
    /// <summary>Трое по убыванию ЛВК: преследователь A (80), жертва B (60), преследователь C (40).</summary>
    private static (ChaseService Chase, ChaseParticipant A, ChaseParticipant B, ChaseParticipant C) ThreeRunners()
    {
        var c = Runner("C", ChaseRole.Pursuer, dex: 40);
        var a = Runner("A", ChaseRole.Pursuer, dex: 80);
        var b = Runner("B", ChaseRole.Prey, dex: 60);
        var chase = Track(6, c, a, b);
        chase.StartChase();
        return (chase, a, b, c);
    }

    [Fact]
    public void StartChase_SortsByDexDescending_AndStartsRoundOne()
    {
        var (chase, a, b, c) = ThreeRunners();

        Assert.Equal([a, b, c], chase.Participants);
        Assert.Equal(ChasePhase.Active, chase.Phase);
        Assert.Equal((1, 0), (chase.CurrentRound, chase.CurrentTurnIndex));
        Assert.Same(a, chase.GetActiveParticipant());
    }

    [Fact]
    [Trait("page", "130")]
    public void StartChase_PlacesPursuersOnOne_PreyStartGapAhead()
    {
        var (_, a, b, c) = ThreeRunners();

        Assert.Equal((1, 3, 1), (a.CurrentLocation, b.CurrentLocation, c.CurrentLocation));
    }

    [Fact]
    public void NextTurn_AfterLast_StartsNextRoundAndRecountsActions()
    {
        var (chase, a, b, c) = ThreeRunners();
        a.MovementActionsRemaining = 0;

        chase.NextTurn();
        chase.NextTurn();
        Assert.Same(c, chase.GetActiveParticipant());

        chase.NextTurn();

        Assert.Equal((2, 0), (chase.CurrentRound, chase.CurrentTurnIndex));
        Assert.Same(a, chase.GetActiveParticipant());
        Assert.Equal(1, a.MovementActionsRemaining);
        Assert.False(b.HasActedThisRound);
    }

    // ── Ничья по ЛВК (стр. 131) ──────────────────────────────────────

    [Theory]
    [Trait("page", "131")]
    // Уровень выше — ходит первым
    [InlineData(10, 30, true)]
    [InlineData(40, 20, false)]
    // Уровни равны — меньший бросок
    [InlineData(30, 28, false)]
    [InlineData(28, 30, true)]
    [InlineData(30, 30, true)]
    public void ResolveDexterityTie_WrittenRolls_WinnerGoesFirst(int rollA, int rollB, bool firstWins)
    {
        var a = Runner("A", ChaseRole.Prey, dex: 50);
        var b = Runner("B", ChaseRole.Pursuer, dex: 50);
        var chase = Track(6, a, b);

        var result = chase.ResolveDexterityTie(a.Id, b.Id, rollA, rollB);

        Assert.Equal(firstWins, result);
        ChaseParticipant[] expected = firstWins ? [a, b] : [b, a];
        Assert.Equal(expected, chase.Participants);
        Assert.Single(chase.ChaseLog);
        Assert.True(chase.ChaseLog[0].IsApplied);
    }

    [Fact]
    [Trait("page", "131")]
    public void ResolveDexterityTie_NoRolls_RollsBothInOrder()
    {
        var a = Runner("A", ChaseRole.Prey, dex: 50);
        var b = Runner("B", ChaseRole.Pursuer, dex: 50);
        var chase = Track(6, a, b);

        using var dice = ScriptedRandom.Use(90, 5);
        var firstWins = chase.ResolveDexterityTie(a.Id, b.Id, null, null);

        Assert.False(firstWins);
        Assert.Equal([b, a], chase.Participants);
        Assert.Equal(5, chase.ChaseLog[0].Roll);
    }

    // ── Ошибки очереди ───────────────────────────────────────────────

    /// <summary>
    ///     F-P03: удалили участника, который стоит в списке выше текущего, — индекс хода остался
    ///     прежним, и ход перескочил через того, кто ходил.
    /// </summary>
    [Fact]
    [Trait("finding", "F-P03")]
    public void RemoveParticipant_AboveCurrent_SkipsCurrentTurn()
    {
        var (chase, a, b, c) = ThreeRunners();
        chase.NextTurn();
        Assert.Same(b, chase.GetActiveParticipant());

        chase.RemoveParticipant(a);

        // По книге ходит всё ещё B; в v1 — уже C
        Assert.Same(c, chase.GetActiveParticipant());
    }

    [Fact]
    public void RemoveParticipant_IndexPastEnd_WrapsToFirst()
    {
        var (chase, a, b, c) = ThreeRunners();
        chase.NextTurn();
        chase.NextTurn();

        chase.RemoveParticipant(c);

        Assert.Equal(0, chase.CurrentTurnIndex);
        Assert.Same(a, chase.GetActiveParticipant());
    }

    /// <summary>
    ///     F-P04: ход — индекс в списке активных. Выбыл тот, кто уже ходил, — список сдвинулся,
    ///     и ход прямо посреди действия B переходит к C.
    /// </summary>
    [Fact]
    [Trait("finding", "F-P04")]
    public void GetActiveParticipant_ActorAboveDropsOut_TurnJumpsToNext()
    {
        var (chase, a, b, c) = ThreeRunners();
        chase.NextTurn();
        Assert.Same(b, chase.GetActiveParticipant());

        // B валит A в ближнем бою
        var hit = chase.ResolveMeleeAttack(b.Id, a.Id, "Драка", 50, 10, 99);
        chase.ApplyResult(hit);

        Assert.True(a.IsEliminated);
        Assert.Same(c, chase.GetActiveParticipant());
    }

    [Fact]
    [Trait("page", "132")]
    public void DelayAction_SwapsWithNextActive()
    {
        var (chase, a, b, c) = ThreeRunners();

        chase.DelayAction(a.Id);

        Assert.Equal([b, a, c], chase.Participants);
        Assert.Same(b, chase.GetActiveParticipant());
        Assert.Single(chase.ChaseLog);
    }

    [Fact]
    [Trait("page", "132")]
    public void DelayAction_Last_JustEndsTurn()
    {
        var (chase, a, _, c) = ThreeRunners();
        chase.NextTurn();
        chase.NextTurn();

        chase.DelayAction(c.Id);

        Assert.Equal(2, chase.CurrentRound);
        Assert.Same(a, chase.GetActiveParticipant());
    }

    /// <summary>
    ///     F-P05: отложенное действие переставляет участников в самом списке, а новый раунд
    ///     порядок по ЛВК не восстанавливает — уступивший так и ходит вторым до конца погони.
    /// </summary>
    [Fact]
    [Trait("page", "132")]
    [Trait("finding", "F-P05")]
    public void DelayAction_OrderStaysSwappedInNextRound()
    {
        var (chase, a, b, c) = ThreeRunners();
        chase.DelayAction(a.Id);

        chase.NextRound();

        Assert.Equal([b, a, c], chase.Participants);
        Assert.Same(b, chase.GetActiveParticipant());
    }
}
