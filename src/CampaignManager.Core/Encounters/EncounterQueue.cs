namespace CampaignManager.Core.Encounters;

/// <summary>
/// Очередь ходов сцены (стр. 110 — бой, стр. 131–132 — погоня): по убыванию <see cref="EncounterParticipant.Initiative"/>,
/// при равенстве — по ЛВК, затем в порядке добавления. Чей ход — <see cref="EncounterState.ActiveParticipantId"/>, а не
/// индекс. Исправлено против v1:
/// <list type="bullet">
/// <item>удаление участника выше текущего не пропускает ход текущего (F-C04, F-P03);</item>
/// <item>выбывший из очереди не сдвигает ход посреди действия (F-P04) — его просто пропускает «Следующий»;</item>
/// <item>отложивший ход отдаёт его следующему, а не предыдущему (F-C03);</item>
/// <item>отложенный ход меняет порядок только этого раунда (F-P05): <see cref="EncounterState.TurnOrder"/> строится
/// заново в начале каждого раунда.</item>
/// </list>
/// </summary>
public static class EncounterQueue
{
    /// <summary>Полный порядок участников по правилу очереди (выбывшие тоже — они могут вернуться).</summary>
    public static List<Guid> Order(EncounterState state) =>
    [
        .. state.Participants
            .Select((participant, index) => (participant, index))
            .OrderByDescending(x => x.participant.Initiative)
            .ThenByDescending(x => x.participant.Stats.Dex)
            .ThenBy(x => x.index)
            .Select(x => x.participant.Id),
    ];

    /// <summary>Начать сцену: раунд 1, ход первого в очереди, кто не выбыл.</summary>
    public static void Start(EncounterState state, DateTimeOffset now)
    {
        state.Round = 1;
        state.TurnOrder = Order(state);
        state.ActiveParticipantId = FirstInPlay(state, 0);
        EncounterEngine.Log(state, new EncounterLogEntry
        {
            Kind = EncounterLogKind.Started,
            Text = state.Active is { } first ? $"Раунд 1. Первым ходит {first.Name}." : "Раунд 1.",
            At = now,
        });
    }

    /// <summary>
    /// Ход следующего, кто не выбыл; кончился раунд — новый раунд в порядке очереди (отложенные ходы прошлого раунда
    /// забыты). Не начатая сцена начинается.
    /// </summary>
    public static void Next(EncounterState state, DateTimeOffset now)
    {
        if (state.Round == 0)
        {
            Start(state, now);
            return;
        }

        var index = state.ActiveParticipantId is { } active ? state.TurnOrder.IndexOf(active) : -1;
        if (FirstInPlay(state, index + 1) is { } next)
        {
            state.ActiveParticipantId = next;
            return;
        }

        NewRound(state, now);
    }

    private static void NewRound(EncounterState state, DateTimeOffset now)
    {
        state.Round++;
        state.TurnOrder = Order(state);
        state.ActiveParticipantId = FirstInPlay(state, 0);
        EncounterEngine.Log(state, new EncounterLogEntry
        {
            Kind = EncounterLogKind.Round,
            Text = state.Active is { } first ? $"Раунд {state.Round}. Первым ходит {first.Name}." : $"Раунд {state.Round}.",
            At = now,
        });
    }

    /// <summary>
    /// Отложить ход активного: он встаёт после <paramref name="afterParticipantId"/> (null — в конец раунда), ход переходит
    /// к следующему за ним по очереди. Только этот раунд.
    /// </summary>
    public static void Delay(EncounterState state, Guid? afterParticipantId, DateTimeOffset now)
    {
        if (state.Round == 0 || state.Active is not { } delayed)
            return;

        var index = state.TurnOrder.IndexOf(delayed.Id);
        state.TurnOrder.RemoveAt(index);
        var after = afterParticipantId is { } id ? state.TurnOrder.IndexOf(id) : -1;
        state.TurnOrder.Insert(after >= index ? after + 1 : state.TurnOrder.Count, delayed.Id);

        // На месте отложившего теперь стоит следующий по очереди — ход его.
        state.ActiveParticipantId = FirstInPlay(state, index) ?? delayed.Id;
        var afterName = afterParticipantId is { } a && state.Find(a) is { } target ? $" — после {target.Name}" : " — в конец раунда";
        EncounterEngine.Log(state, new EncounterLogEntry
        {
            Kind = EncounterLogKind.Delayed,
            ActorId = delayed.Id,
            Text = $"{delayed.Name} откладывает ход{afterName}.",
            At = now,
        });
    }

    /// <summary>
    /// Новый участник посреди раунда встаёт в порядок этого раунда по своей инициативе: если его место уже прошло — он
    /// ходит со следующего раунда, кто ходит сейчас — не меняется.
    /// </summary>
    internal static void OnAdded(EncounterState state, Guid participantId)
    {
        if (state.Round == 0)
            return;

        var order = Order(state);
        var rank = order.IndexOf(participantId);
        var position = state.TurnOrder.FindIndex(id => order.IndexOf(id) > rank);
        state.TurnOrder.Insert(position < 0 ? state.TurnOrder.Count : position, participantId);
    }

    /// <summary>Удалённый участник уходит из порядка; если ходил он — ход переходит к следующему.</summary>
    internal static void OnRemoved(EncounterState state, Guid participantId, DateTimeOffset now)
    {
        if (state.ActiveParticipantId == participantId)
        {
            var index = state.TurnOrder.IndexOf(participantId);
            state.TurnOrder.Remove(participantId);
            if (FirstInPlay(state, index) is { } next)
            {
                state.ActiveParticipantId = next;
            }
            else if (state.Participants.Count > 0)
            {
                NewRound(state, now);
            }
            else
            {
                state.ActiveParticipantId = null;
            }

            return;
        }

        state.TurnOrder.Remove(participantId);
    }

    /// <summary>Первый не выбывший в порядке раунда, начиная с <paramref name="from"/>.</summary>
    private static Guid? FirstInPlay(EncounterState state, int from)
    {
        for (var i = Math.Max(0, from); i < state.TurnOrder.Count; i++)
        {
            if (state.Find(state.TurnOrder[i]) is { IsOut: false } participant)
                return participant.Id;
        }

        return null;
    }
}
