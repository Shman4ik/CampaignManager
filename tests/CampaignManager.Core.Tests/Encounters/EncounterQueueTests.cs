using CampaignManager.Core.Encounters;

namespace CampaignManager.Core.Tests.Encounters;

/// <summary>
/// Очередь сцены по id активного участника (T2.6a). Ожидания F-C03, F-C04, F-P03, F-P04, F-P05 — исправленные:
/// в v1 они держались тестами T0.2 на поведении v1 (<c>RoundAndTurnTests</c>, <c>TurnOrderTests</c>).
/// </summary>
public sealed class EncounterQueueTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    /// <summary>А(80), Б(60), В(40), Г(20) по инициативе.</summary>
    private static (EncounterState State, Guid A, Guid B, Guid C, Guid D) Four()
    {
        var state = new EncounterState();
        var a = Add(state, "А", 80);
        var b = Add(state, "Б", 60);
        var c = Add(state, "В", 40);
        var d = Add(state, "Г", 20);
        return (state, a, b, c, d);
    }

    private static Guid Add(EncounterState state, string name, int initiative)
    {
        var participant = new EncounterParticipant { Name = name, SourceName = name, Initiative = initiative };
        Assert.Null(EncounterEngine.Add(state, participant, Now).Rejection);
        return participant.Id;
    }

    private static string ActiveName(EncounterState state) => state.Active?.Name ?? "—";

    [Fact]
    [Trait("page", "110")]
    public void Order_is_by_initiative_then_dex_then_join_order()
    {
        var state = new EncounterState();
        Add(state, "Медленный", 30);
        var fast = new EncounterParticipant { Name = "Ловкий", Initiative = 50, Stats = new ParticipantStats { Dex = 70 } };
        var same = new EncounterParticipant { Name = "Неловкий", Initiative = 50, Stats = new ParticipantStats { Dex = 40 } };
        EncounterEngine.Add(state, same, Now);
        EncounterEngine.Add(state, fast, Now);

        EncounterQueue.Start(state, Now);

        Assert.Equal(["Ловкий", "Неловкий", "Медленный"], state.TurnOrder.Select(id => state.Find(id)!.Name));
        Assert.Equal(1, state.Round);
        Assert.Equal("Ловкий", ActiveName(state));
    }

    [Fact]
    public void Next_walks_the_round_and_starts_a_new_one()
    {
        var (state, _, _, _, _) = Four();
        EncounterQueue.Next(state, Now); // не начатая сцена начинается

        var seen = new List<string> { ActiveName(state) };
        for (var i = 0; i < 4; i++)
        {
            EncounterQueue.Next(state, Now);
            seen.Add(ActiveName(state));
        }

        Assert.Equal(["А", "Б", "В", "Г", "А"], seen);
        Assert.Equal(2, state.Round);
        Assert.Contains(state.Log, e => e.Kind == EncounterLogKind.Round && e.Round == 2);
    }

    [Fact]
    [Trait("finding", "F-C04")]
    public void Removing_someone_who_already_acted_does_not_skip_the_current_turn()
    {
        var (state, a, _, _, _) = Four();
        EncounterQueue.Start(state, Now);
        EncounterQueue.Next(state, Now);
        EncounterQueue.Next(state, Now); // ходит В

        EncounterEngine.Remove(state, a, Now);
        Assert.Equal("В", ActiveName(state));

        EncounterQueue.Next(state, Now);
        Assert.Equal("Г", ActiveName(state));
    }

    [Fact]
    [Trait("finding", "F-C04")]
    public void Removing_before_the_last_keeps_the_last_on_turn()
    {
        var (state, a, _, _, _) = Four();
        EncounterQueue.Start(state, Now);
        for (var i = 0; i < 3; i++)
            EncounterQueue.Next(state, Now); // ходит Г

        EncounterEngine.Remove(state, a, Now);

        Assert.Equal("Г", ActiveName(state));
        Assert.Equal(1, state.Round);
    }

    [Fact]
    public void Removing_the_active_passes_the_turn_to_the_next()
    {
        var (state, _, b, _, _) = Four();
        EncounterQueue.Start(state, Now);
        EncounterQueue.Next(state, Now); // ходит Б

        EncounterEngine.Remove(state, b, Now);

        Assert.Equal("В", ActiveName(state));
    }

    [Fact]
    public void Removing_the_last_active_starts_the_next_round()
    {
        var (state, _, _, _, d) = Four();
        EncounterQueue.Start(state, Now);
        for (var i = 0; i < 3; i++)
            EncounterQueue.Next(state, Now);

        EncounterEngine.Remove(state, d, Now);

        Assert.Equal(2, state.Round);
        Assert.Equal("А", ActiveName(state));
    }

    [Fact]
    [Trait("finding", "F-P04")]
    public void Someone_above_dropping_out_does_not_move_the_turn()
    {
        var (state, a, _, _, _) = Four();
        EncounterQueue.Start(state, Now);
        EncounterQueue.Next(state, Now); // ходит Б

        EncounterEngine.Apply(state, new EncounterResolution
        {
            Effects = [new EncounterEffect { Kind = EncounterEffectKind.Out, ParticipantId = a, Flag = true }],
        }, Now);

        Assert.Equal("Б", ActiveName(state));
    }

    [Fact]
    public void Next_skips_participants_who_are_out()
    {
        var (state, _, b, _, _) = Four();
        state.Find(b)!.IsOut = true;
        EncounterQueue.Start(state, Now);

        EncounterQueue.Next(state, Now);

        Assert.Equal("В", ActiveName(state));
    }

    [Fact]
    [Trait("finding", "F-C03")]
    public void Delaying_the_current_turn_passes_it_forward_not_back()
    {
        var (state, _, _, _, _) = Four();
        EncounterQueue.Start(state, Now);
        EncounterQueue.Next(state, Now); // ходит Б

        EncounterQueue.Delay(state, afterParticipantId: null, Now);

        Assert.Equal("В", ActiveName(state));
        Assert.Equal(["А", "В", "Г", "Б"], state.TurnOrder.Select(id => state.Find(id)!.Name));
        EncounterQueue.Next(state, Now);
        EncounterQueue.Next(state, Now);
        Assert.Equal("Б", ActiveName(state)); // отложивший ходит в конце раунда
    }

    [Fact]
    public void Delay_after_a_chosen_participant()
    {
        var (state, _, _, c, _) = Four();
        EncounterQueue.Start(state, Now); // ходит А

        EncounterQueue.Delay(state, c, Now);

        Assert.Equal(["Б", "В", "А", "Г"], state.TurnOrder.Select(id => state.Find(id)!.Name));
        Assert.Equal("Б", ActiveName(state));
    }

    [Fact]
    [Trait("finding", "F-P05")]
    public void Delayed_order_lasts_only_this_round()
    {
        var (state, _, _, _, _) = Four();
        EncounterQueue.Start(state, Now);
        EncounterQueue.Delay(state, null, Now); // А в конец

        for (var i = 0; i < 4; i++)
            EncounterQueue.Next(state, Now);

        Assert.Equal(2, state.Round);
        Assert.Equal(["А", "Б", "В", "Г"], state.TurnOrder.Select(id => state.Find(id)!.Name));
        Assert.Equal("А", ActiveName(state));
    }

    [Fact]
    public void Joining_mid_round_waits_if_its_place_has_passed()
    {
        var (state, _, _, _, _) = Four();
        EncounterQueue.Start(state, Now);
        EncounterQueue.Next(state, Now);
        EncounterQueue.Next(state, Now); // ходит В

        Add(state, "Быстрый", 90); // его место уже прошло
        Add(state, "Ползун", 10);  // его место впереди

        Assert.Equal("В", ActiveName(state));
        EncounterQueue.Next(state, Now);
        EncounterQueue.Next(state, Now);
        Assert.Equal("Ползун", ActiveName(state));
        EncounterQueue.Next(state, Now);
        Assert.Equal("Быстрый", ActiveName(state));
        Assert.Equal(2, state.Round);
    }
}
