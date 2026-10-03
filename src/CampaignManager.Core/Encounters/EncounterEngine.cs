using System.Globalization;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.Core.Encounters.Chase;

namespace CampaignManager.Core.Encounters;

/// <summary>Одна строка предпросмотра: кто, что, было → станет и чем это чревато.</summary>
public sealed record EffectPreview(Guid ParticipantId, string Name, string Label, string Before, string After, string? Note);

/// <summary>Что сделал <see cref="EncounterEngine.Apply(EncounterState, DateTimeOffset)"/>: запись журнала и записи в листы.</summary>
public sealed record ApplyOutcome(EncounterLogEntry Entry, IReadOnlyList<SheetWrite> SheetWrites);

/// <summary>Почему участника нельзя добавить; null — можно.</summary>
public sealed record AddOutcome(EncounterParticipant? Participant, string? Rejection);

/// <summary>
/// Ядро сцены — общее для боя и погони (T2.6a). Контракт, который в v1 держала только погоня:
/// <list type="number">
/// <item>правило <b>разрешает</b> действие и возвращает <see cref="EncounterResolution"/> — строки и эффекты, ничего не меняя;</item>
/// <item>страница кладёт его в <see cref="EncounterState.Pending"/> (<see cref="Propose"/>) и показывает предпросмотр
/// (<see cref="Preview"/>);</item>
/// <item>«Применить» — <see cref="Apply(EncounterState, DateTimeOffset)"/>: единственное место, где эффекты меняют
/// участников; оно же пишет журнал и ставит записи в листы в <see cref="EncounterState.SheetWrites"/>;</item>
/// <item>«Отменить» — <see cref="Cancel"/>: выбросить эффекты. Отменять нечего, потому что ничего не менялось.</item>
/// </list>
/// Третьего пути мимо <c>Apply</c> нет: в v1 первая помощь, медицина, укрытие и полдюжины действий погони правили
/// участников прямо в разметке (AUDIT, «Сцены»). Правка Хранителя руками — тоже эффект.
/// </summary>
public static class EncounterEngine
{
    /// <summary>Журнал хранит последние записи: состояние пишется целиком при каждом автосохранении.</summary>
    public const int MaxLogEntries = 500;

    /// <summary>Участников в одной сцене — с запасом на толпу культистов.</summary>
    public const int MaxParticipants = 100;

    /// <summary>
    /// Добавить участника. Один лист — один раз (повтор — отказ с текстом); тварей одного вида — сколько угодно, они
    /// получают номера «#1», «#2» (знание v1). Посреди раунда новичок встаёт в очередь по инициативе.
    /// </summary>
    public static AddOutcome Add(EncounterState state, EncounterParticipant participant, DateTimeOffset now) =>
        Add(state, participant, now, log: true);

    /// <summary>
    /// Добавить нескольких за раз: в журнале одна запись «Вступили: …» вместо строки на каждого (восемь строк шума в
    /// начале боя). Отказы — по одному на отклонённого, как у <see cref="Add(EncounterState, EncounterParticipant, DateTimeOffset)"/>.
    /// </summary>
    public static List<AddOutcome> AddRange(EncounterState state, IEnumerable<EncounterParticipant> participants, DateTimeOffset now)
    {
        var outcomes = participants.Select(p => Add(state, p, now, log: false)).ToList();
        var joined = outcomes.Select(o => o.Participant).OfType<EncounterParticipant>().ToList();
        if (joined.Count > 0)
        {
            Log(state, new EncounterLogEntry
            {
                Kind = EncounterLogKind.Joined,
                ActorId = joined.Count == 1 ? joined[0].Id : null,
                Text = joined.Count == 1
                    ? $"{joined[0].Name} вступает в сцену ({EncounterText.Of(joined[0].Side)})."
                    : $"Вступили: {string.Join(", ", joined.Select(p => p.Name))}.",
                At = now,
            });
        }

        return outcomes;
    }

    private static AddOutcome Add(EncounterState state, EncounterParticipant participant, DateTimeOffset now, bool log)
    {
        if (participant.SourceCharacterId is { } characterId
            && state.Participants.FirstOrDefault(p => p.SourceCharacterId == characterId) is { } present)
            return new AddOutcome(null, $"{present.Name} уже в сцене: один лист — один участник.");

        if (state.Participants.Count >= MaxParticipants)
            return new AddOutcome(null, $"В сцене уже {MaxParticipants} участников.");

        if (string.IsNullOrWhiteSpace(participant.SourceName))
            participant.SourceName = participant.Name;
        participant.Name = NumberedName(state, participant);

        state.Participants.Add(participant);
        EncounterQueue.OnAdded(state, participant.Id);
        if (state.Chase is not null)
            ChaseRules.OnAdded(state, participant);
        if (log)
        {
            Log(state, new EncounterLogEntry
            {
                Kind = EncounterLogKind.Joined,
                ActorId = participant.Id,
                Text = $"{participant.Name} вступает в сцену ({EncounterText.Of(participant.Side)}).",
                At = now,
            });
        }

        return new AddOutcome(participant, null);
    }

    /// <summary>Убрать участника: кто ходит — не меняется, если только не ходил он сам (тогда ход — следующему).</summary>
    public static void Remove(EncounterState state, Guid participantId, DateTimeOffset now)
    {
        if (state.Find(participantId) is not { } participant)
            return;

        state.Participants.Remove(participant);
        EncounterQueue.OnRemoved(state, participantId, now);
        if (state.Chase is not null)
            ChaseRules.OnRemoved(state, participantId);
        // Записи в лист остаются: урон уже случился, лист его получит и без участника в сцене.
        if (state.Pending is { } pending && (pending.ActorId == participantId || pending.Effects.Any(e => e.ParticipantId == participantId)))
            state.Pending = null;

        Log(state, new EncounterLogEntry
        {
            Kind = EncounterLogKind.Left,
            ActorId = participantId,
            Text = $"{participant.Name} покидает сцену.",
            At = now,
        });
    }

    public static void SetSide(EncounterState state, Guid participantId, EncounterSide side)
    {
        if (state.Find(participantId) is not { } participant)
            return;

        participant.Side = side;
        // Роль в погоне выводится из стороны (противники преследуют) — пока погоня не началась; потом роль меняют отдельно.
        if (state.Chase is { Phase: ChasePhase.Setup })
            ChaseRules.SetRole(state, participantId, side == EncounterSide.Enemies ? ChaseRole.Pursuer : ChaseRole.Prey);
    }

    /// <summary>Положить результат на предпросмотр. Прежний неприменённый выбрасывается.</summary>
    public static void Propose(EncounterState state, EncounterResolution resolution) => state.Pending = resolution;

    /// <summary>«Отменить»: эффекты выброшены, участники не тронуты.</summary>
    public static void Cancel(EncounterState state) => state.Pending = null;

    /// <summary>
    /// Что изменит результат — по строке на эффект; считает тем же кодом, что <see cref="Apply(EncounterState, DateTimeOffset)"/>,
    /// на копии сцены (участники и погоня; журнал и очередь записей копировать незачем).
    /// </summary>
    public static IReadOnlyList<EffectPreview> Preview(EncounterState state, EncounterResolution resolution)
    {
        var copy = CmJson.Clone(state with { Log = [], SheetWrites = [], Pending = null });
        List<EffectPreview> lines = [];
        foreach (var effect in resolution.Effects)
        {
            if (Describe(copy, effect) is { } line)
                lines.Add(line);
        }

        return lines;
    }

    /// <summary>Применить предложенный результат (<see cref="EncounterState.Pending"/>). Нечего — null.</summary>
    public static ApplyOutcome? Apply(EncounterState state, DateTimeOffset now) =>
        state.Pending is { } pending ? Apply(state, pending, now) : null;

    /// <summary>
    /// Применить результат: эффекты меняют снимки участников, запись — в журнал, эффекты участников с листом — в очередь
    /// записи в листы (одна запись на участника). Предложенный результат снимается.
    /// </summary>
    public static ApplyOutcome Apply(EncounterState state, EncounterResolution resolution, DateTimeOffset now)
    {
        List<string> lines = [.. resolution.Lines];
        foreach (var effect in resolution.Effects)
        {
            if (Describe(state, effect) is { } line)
                lines.Add(line.ToLogLine());
        }

        List<SheetWrite> writes = [];
        foreach (var group in resolution.Effects
                     .Where(e => EncounterSheetEffects.TouchesSheet(e.Kind))
                     .GroupBy(e => e.ParticipantId))
        {
            if (state.Find(group.Key) is not { SourceCharacterId: { } characterId } participant)
                continue;
            if (participant.Kind == ParticipantKind.Creature)
                continue;

            writes.Add(new SheetWrite
            {
                ParticipantId = participant.Id,
                CharacterId = characterId,
                Title = resolution.Title,
                Effects = [.. group],
            });
        }

        state.SheetWrites.AddRange(writes);
        if (state.Pending?.Id == resolution.Id)
            state.Pending = null;

        var entry = new EncounterLogEntry
        {
            Kind = resolution.Kind,
            ActorId = resolution.ActorId,
            Text = resolution.Title,
            Lines = lines,
            At = now,
        };
        Log(state, entry);
        if (state.Chase is not null)
            ChaseRules.AfterApply(state, now);
        return new ApplyOutcome(entry, writes);
    }

    /// <summary>
    /// Запись в лист прошла: снимок участника — из записанного листа (там привыкание и поправки книги), запись снята с
    /// очереди, итог — в журнал.
    /// </summary>
    public static void CompleteSheetWrite(EncounterState state, Guid writeId, CharacterSheet sheet, SkillCatalog catalog,
        IReadOnlyList<string> lines, DateTimeOffset now)
    {
        if (state.SheetWrites.FirstOrDefault(w => w.Id == writeId) is not { } write)
            return;

        state.SheetWrites.Remove(write);
        var participant = state.Find(write.ParticipantId);
        if (participant is not null)
            EncounterParticipants.Refresh(participant, sheet, catalog);

        Log(state, new EncounterLogEntry
        {
            Kind = EncounterLogKind.Sheet,
            ActorId = write.ParticipantId,
            Text = $"Записано в лист: {participant?.Name ?? sheet.Personal.Name}.",
            Lines = [.. lines],
            At = now,
        });
    }

    /// <summary>
    /// Запись в лист не прошла — эффект остаётся в очереди с текстом ошибки. Временная беда (нет связи) — повторит
    /// следующая попытка; <paramref name="blocked"/> (листа нет, нет прав) — ждёт решения Хранителя.
    /// </summary>
    public static void FailSheetWrite(EncounterState state, Guid writeId, string error, bool blocked = false)
    {
        if (state.SheetWrites.FirstOrDefault(w => w.Id == writeId) is { } write)
        {
            write.Error = error;
            write.Blocked = blocked;
        }
    }

    /// <summary>«Повторить»: заблокированная запись снова в работе.</summary>
    public static void RetrySheetWrite(EncounterState state, Guid writeId)
    {
        if (state.SheetWrites.FirstOrDefault(w => w.Id == writeId) is { } write)
        {
            write.Blocked = false;
            write.Error = null;
        }
    }

    /// <summary>Хранитель отказался записывать в лист (лист удалён, нет прав): эффект остаётся только в сцене.</summary>
    public static void DropSheetWrite(EncounterState state, Guid writeId, DateTimeOffset now)
    {
        if (state.SheetWrites.FirstOrDefault(w => w.Id == writeId) is not { } write)
            return;

        state.SheetWrites.Remove(write);
        Log(state, new EncounterLogEntry
        {
            Kind = EncounterLogKind.Sheet,
            ActorId = write.ParticipantId,
            Text = $"Не записано в лист: {state.Find(write.ParticipantId)?.Name ?? "участник"} — «{write.Title}».",
            At = now,
        });
    }

    public static void Note(EncounterState state, string text, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        Log(state, new EncounterLogEntry { Kind = EncounterLogKind.Note, Text = text.Trim(), At = now });
    }

    /// <summary>Запись в журнал с номером раунда; старше <see cref="MaxLogEntries"/> — отбрасываются.</summary>
    public static void Log(EncounterState state, EncounterLogEntry entry)
    {
        entry.Round = state.Round;
        state.Log.Add(entry);
        if (state.Log.Count > MaxLogEntries)
            state.Log.RemoveRange(0, state.Log.Count - MaxLogEntries);
    }

    /// <summary>
    /// Применяет эффект к снимку участника и описывает, что изменилось. Единственное место, где эффекты меняют участника;
    /// <see cref="Preview"/> зовёт его на копии. Правила — листа (<see cref="WoundRules"/>): та же серьёзная рана и то же
    /// сознание, что запишутся в лист.
    /// </summary>
    private static EffectPreview? Describe(EncounterState state, EncounterEffect effect)
    {
        // Трасса погони (преграда, новая помеха) — эффект без участника.
        if (state.Chase is not null && ChaseEffects.IsTrackEffect(effect.Kind))
            return ChaseEffects.DescribeTrack(state, effect);

        return state.Find(effect.ParticipantId) is { } participant ? Describe(state, participant, effect) : null;
    }

    private static EffectPreview Describe(EncounterState state, EncounterParticipant p, EncounterEffect effect)
    {
        string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
        var detail = string.IsNullOrWhiteSpace(effect.Detail) ? null : effect.Detail;

        // Раны, лечение и состояние боя (T2.6b) — CombatEffects: правила ран там одни с листом (WoundRules).
        if (CombatEffects.Describe(state.Round, p, effect) is { } combat)
            return combat;

        switch (effect.Kind)
        {
            case EncounterEffectKind.MagicPoints:
            {
                var before = p.MagicPoints;
                p.MagicPoints = Math.Clamp(p.MagicPoints + effect.Amount, 0, Math.Max(p.MaxMagicPoints, 0));
                return new EffectPreview(p.Id, p.Name, "ПМ", Number(before), Number(p.MagicPoints), detail);
            }
            case EncounterEffectKind.SanityLoss when p.Sanity is { } sanity:
            {
                p.Sanity = Math.Max(0, sanity - Math.Max(0, effect.Amount));
                var habituation = !string.IsNullOrWhiteSpace(effect.CreatureName) && p.HasSheet
                    ? $"привыкание к виду «{effect.CreatureName}» учтёт лист"
                    : null;
                return new EffectPreview(p.Id, p.Name, $"Рассудок −{Number(effect.Amount)}", Number(sanity), Number(p.Sanity.Value),
                    Join(detail, habituation));
            }
            case EncounterEffectKind.SanityLoss:
                return new EffectPreview(p.Id, p.Name, "Рассудок", "—", "—", "у твари рассудка нет");
            case EncounterEffectKind.Power:
            {
                var before = p.Stats.Pow;
                p.Stats.Pow = Math.Max(0, before + effect.Amount);
                return new EffectPreview(p.Id, p.Name, "МОЩ", Number(before), Number(p.Stats.Pow), Join(detail, "навсегда"));
            }
            case EncounterEffectKind.Out:
            {
                var before = p.IsOut;
                p.IsOut = effect.Flag;
                return new EffectPreview(p.Id, p.Name, "Очередь", before ? "выбыл" : "в очереди", p.IsOut ? "выбыл" : "в очереди", detail);
            }
            case var kind when state.Chase is not null && ChaseEffects.Handles(kind):
                return ChaseEffects.Describe(state, p, effect);
            default:
                return new EffectPreview(p.Id, p.Name, EncounterText.Of(effect.Kind), "", "", detail);
        }
    }

    private static string? Join(string? first, string? second) =>
        (first, second) switch
        {
            (null, null) => null,
            (null, _) => second,
            (_, null) => first,
            _ => $"{first}; {second}",
        };

    private static string ToLogLine(this EffectPreview preview) =>
        $"{preview.Name}: {preview.Label}, {preview.Before} → {preview.After}" + (preview.Note is { } note ? $" ({note})" : "");

    /// <summary>«Глубоководный», «Глубоководный #2»: номер — только если из того же источника уже кто-то есть.</summary>
    private static string NumberedName(EncounterState state, EncounterParticipant participant)
    {
        var baseName = participant.SourceName.Trim();
        var peers = state.Participants
            .Where(p => p.SourceCharacterId is null && participant.SourceCharacterId is null
                        && (participant.SourceCreatureId is { } creature ? p.SourceCreatureId == creature : p.SourceName == baseName))
            .ToList();
        if (peers.Count == 0)
            return participant.Name.Trim() is { Length: > 0 } name && name != baseName ? name : baseName;

        var max = 0;
        foreach (var peer in peers)
        {
            var number = NumberOf(peer.Name, peer.SourceName);
            if (number is null && peer.Name == peer.SourceName)
            {
                // Первый из вида становится «#1», имя, данное Хранителем, не трогаем.
                peer.Name = $"{peer.SourceName} #1";
                number = 1;
            }

            max = Math.Max(max, number ?? 0);
        }

        return $"{baseName} #{(max + 1).ToString(CultureInfo.InvariantCulture)}";
    }

    private static int? NumberOf(string name, string baseName) =>
        name.StartsWith(baseName + " #", StringComparison.Ordinal)
        && int.TryParse(name.AsSpan(baseName.Length + 2), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
}
