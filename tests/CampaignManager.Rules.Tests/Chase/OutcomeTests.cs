using CampaignManager.Web.Components.Features.Chase.Model;
using CampaignManager.Web.Components.Features.Chase.Services;
using static CampaignManager.Rules.Tests.Chase.ChaseScene;

namespace CampaignManager.Rules.Tests.Chase;

/// <summary>Поимка, побег и конец погони.</summary>
[Trait("page", "135")]
public sealed class OutcomeTests
{
    private static (ChaseService Chase, ChaseParticipant Prey, ChaseParticipant Pursuer) Pair(int locations = 6)
    {
        var prey = Runner("Артур", ChaseRole.Prey);
        var pursuer = Runner("Вампир", ChaseRole.Pursuer);
        var chase = Track(locations, prey, pursuer);
        chase.StartChase();
        return (chase, prey, pursuer);
    }

    [Fact]
    public void MarkCaught_LastPrey_EndsChase_LogsCatcher()
    {
        var (chase, prey, pursuer) = Pair();

        chase.MarkCaught(prey.Id, pursuer.Id);

        Assert.True(prey.IsCaught);
        Assert.False(prey.IsActive);
        Assert.True(chase.IsChaseOver());
        Assert.Equal(ChasePhase.Ended, chase.Phase);
        var entry = Assert.Single(chase.ChaseLog);
        Assert.Equal((ChaseActionType.CaughtEvent, pursuer.Id), (entry.ActionType, entry.TargetId));
        Assert.True(entry.IsApplied);
    }

    [Fact]
    public void MarkCaught_AlreadyInactive_DoesNothing()
    {
        var (chase, prey, _) = Pair();
        chase.MarkCaught(prey.Id);

        chase.MarkCaught(prey.Id);

        Assert.Single(chase.ChaseLog);
    }

    [Fact]
    public void MarkCaught_OneOfTwoPrey_ChaseGoesOn()
    {
        var first = Runner("Артур", ChaseRole.Prey);
        var second = Runner("Билл", ChaseRole.Prey);
        var chase = Track(6, first, second, Runner("Вампир", ChaseRole.Pursuer));
        chase.StartChase();

        chase.MarkCaught(first.Id);

        Assert.False(chase.IsChaseOver());
        Assert.Equal(ChasePhase.Active, chase.Phase);
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    public void IsChaseOver_EveryPreyEscapedCaughtOrEliminated(bool escaped, bool caught, bool eliminated, bool over)
    {
        var (chase, prey, _) = Pair();
        prey.HasEscaped = escaped;
        prey.IsCaught = caught;
        prey.IsEliminated = eliminated;

        Assert.Equal(over, chase.IsChaseOver());
    }

    [Fact]
    public void IsChaseOver_NoPreyAtAll_True() =>
        Assert.True(Track(6, Runner("Вампир", ChaseRole.Pursuer)).IsChaseOver());

    [Fact]
    public void ApplyResult_PursuerReachesPrey_ContactButNotCaught()
    {
        var (chase, prey, pursuer) = Pair();
        chase.SetParticipantLocation(pursuer.Id, 2);

        chase.ApplyResult(chase.MoveForward(pursuer.Id));

        Assert.Equal(prey.CurrentLocation, pursuer.CurrentLocation);
        Assert.False(prey.IsCaught);
        Assert.True(chase.HasContact());
        Assert.Equal([pursuer], chase.GetPursuersInContactWith(prey));
    }

    [Fact]
    public void ApplyResult_PreyReachesLastLocationAhead_EscapesAutomatically()
    {
        var (chase, prey, _) = Pair(locations: 4);

        chase.ApplyResult(chase.MoveForward(prey.Id));

        Assert.Equal(4, prey.CurrentLocation);
        Assert.True(prey.HasEscaped);
        Assert.Equal(ChasePhase.Ended, chase.Phase);
        Assert.Equal(ChaseActionType.EscapedEvent, chase.ChaseLog[0].ActionType);
    }

    [Fact]
    public void ApplyResult_PreyOnLastLocationWithPursuer_NoEscape()
    {
        var (chase, prey, pursuer) = Pair(locations: 4);
        chase.SetParticipantLocation(pursuer.Id, 4);

        chase.ApplyResult(chase.MoveForward(prey.Id));

        Assert.False(prey.HasEscaped);
        Assert.Equal(ChasePhase.Active, chase.Phase);
    }

    [Fact]
    public void MoveForward_AtTrackEnd_StaysOnLast()
    {
        var (chase, _, pursuer) = Pair(locations: 4);
        chase.SetParticipantLocation(pursuer.Id, 4);

        var result = chase.MoveForward(pursuer.Id);

        Assert.Equal((4, 4), (result.LocationBefore, result.LocationAfter));
        Assert.Equal(1, result.ActorMovementActionsSpent);
    }

    [Fact]
    public void ApplyResult_AlreadyApplied_IsNotAppliedTwice()
    {
        var (chase, _, pursuer) = Pair();
        var move = chase.MoveForward(pursuer.Id);
        chase.ApplyResult(move);

        chase.ApplyResult(move);

        Assert.Equal(2, pursuer.CurrentLocation);
        Assert.Single(chase.ChaseLog);
    }
}
