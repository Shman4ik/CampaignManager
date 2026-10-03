using CampaignManager.Core.Dice;
using CampaignManager.Core.KeeperScreen;

namespace CampaignManager.Core.Encounters.Chase;

/// <summary>Насколько существу подходит навык, которого у него нет (стр. 142).</summary>
public enum SkillAptitude
{
    /// <summary>Способность подразумевается — полная ЛВК.</summary>
    Capable,

    /// <summary>Неясно — половина ЛВК.</summary>
    Uncertain,

    /// <summary>Способности нет — пятая часть ЛВК (или автоматический провал).</summary>
    Unlikely,
}

/// <summary>Навык проверки скорости: подпись, значение и штрафные кости поломки транспорта.</summary>
public sealed record SpeedCheckSkill(string Label, int Value, int PenaltyDice);

/// <summary>
/// Погоня (гл. 7, стр. 128–149) на ядре сцены: трасса и расстановка, проверка скорости, действия перемещения, что можно
/// сделать в ход (<see cref="AvailableActions"/>), побег и конец. Разрешение действий — <see cref="ChaseActions"/>
/// (возвращают <see cref="EncounterResolution"/>, ничего не меняя), изменения — эффекты погони через
/// <c>EncounterEngine.Apply</c> (<see cref="ChaseEffects"/>). Здесь только настройка, которую делает Хранитель (роль,
/// транспорт, место на трассе), и шаги, которые ядро зовёт само.
/// </summary>
public static class ChaseRules
{
    public const int MinLocations = 3;

    public const int MaxLocations = 40;

    // ───────────────────── Трасса и участники ─────────────────────

    /// <summary>
    /// Завести погоню в сцене: трасса из <paramref name="locations"/> локаций, бегущие — все участники сцены (роль — по стороне:
    /// противники преследуют, остальные убегают). Повторный вызов меняет длину трассы и отрыв, не трогая бегущих.
    /// </summary>
    public static ChaseState Begin(EncounterState state, int locations, int startGap = 2)
    {
        var chase = state.Chase ??= new ChaseState();
        chase.StartGap = Math.Clamp(startGap, 1, 2);
        Resize(chase, locations);
        foreach (var participant in state.Participants.Where(p => chase.Runner(p.Id) is null))
            chase.Runners.Add(NewRunner(chase, participant));

        // До проверки скорости — простая расстановка (стр. 131): преследователи на 1, убегающие — на отрыв впереди. Книжная
        // расстановка по СКО (стр. 145) — в Start, когда СКО известны.
        if (chase.Phase == ChasePhase.Setup)
        {
            foreach (var runner in chase.Runners)
                runner.Location = DefaultLocation(chase, runner.Role);
        }

        return chase;
    }

    /// <summary>Длина трассы: новые локации — пустые в конце, лишние уходят с конца; бегущие остаются на трассе.</summary>
    public static void Resize(ChaseState chase, int locations)
    {
        locations = Math.Clamp(locations, MinLocations, MaxLocations);
        if (chase.Locations.Count > locations)
            chase.Locations.RemoveRange(locations, chase.Locations.Count - locations);
        for (var number = chase.Locations.Count + 1; number <= locations; number++)
            chase.Locations.Add(new ChaseLocation { Number = number });
        foreach (var runner in chase.Runners)
            runner.Location = Math.Clamp(runner.Location, 1, chase.LastLocation);
    }

    /// <summary>Правка локации Хранителем (название, помеха, преграда).</summary>
    public static void SetLocation(ChaseState chase, ChaseLocation location)
    {
        var index = chase.Locations.FindIndex(l => l.Number == location.Number);
        if (index < 0)
            return;

        if (location.Barrier is { } barrier)
            barrier.HitPointsLeft = Math.Clamp(barrier.HitPointsLeft, 0, Math.Max(0, barrier.HitPoints));
        chase.Locations[index] = location;
    }

    /// <summary>Новичок сцены — новый бегущий. Посреди погони он присоединяется (стр. 140): сначала проверка скорости.</summary>
    internal static void OnAdded(EncounterState state, EncounterParticipant participant)
    {
        var chase = state.Chase!;
        if (chase.Runner(participant.Id) is not null)
            return;

        var runner = NewRunner(chase, participant);
        if (chase.Phase is ChasePhase.Active or ChasePhase.Ended)
            runner.Location = chase.Runners.Where(r => r.Role == runner.Role).Select(r => r.Location).DefaultIfEmpty(runner.Location).Min();
        chase.Runners.Add(runner);
    }

    internal static void OnRemoved(EncounterState state, Guid participantId)
    {
        var chase = state.Chase!;
        chase.Runners.RemoveAll(r => r.ParticipantId == participantId);
        foreach (var passenger in chase.Runners.Where(r => r.CarrierId == participantId))
            passenger.CarrierId = null;
    }

    private static ChaseRunner NewRunner(ChaseState chase, EncounterParticipant participant)
    {
        var role = participant.Side == EncounterSide.Enemies ? ChaseRole.Pursuer : ChaseRole.Prey;
        return new ChaseRunner
        {
            ParticipantId = participant.Id,
            Role = role,
            Location = chase.Locations.Count == 0 ? 1 : DefaultLocation(chase, role),
        };
    }

    private static int DefaultLocation(ChaseState chase, ChaseRole role) =>
        role == ChaseRole.Prey ? Math.Clamp(1 + chase.StartGap, 1, Math.Max(1, chase.LastLocation)) : 1;

    public static void SetRole(EncounterState state, Guid participantId, ChaseRole role)
    {
        if (state.Chase?.Runner(participantId) is not { } runner)
            return;

        runner.Role = role;
        if (state.Chase.Phase == ChasePhase.Setup)
            runner.Location = DefaultLocation(state.Chase, role);
    }

    /// <summary>Хранитель переставил бегущего (пассажиры едут с водителем).</summary>
    public static void SetPosition(EncounterState state, Guid participantId, int location)
    {
        if (state.Chase is not { } chase || chase.Runner(participantId) is not { } runner || runner.IsPassenger)
            return;

        runner.Location = Math.Clamp(location, 1, Math.Max(1, chase.LastLocation));
        foreach (var passenger in chase.Runners.Where(r => r.CarrierId == participantId))
            passenger.Location = runner.Location;
    }

    /// <summary>
    /// Посадить за руль (таблица V) или высадить (<paramref name="template"/> = null). Навык водителя — из листа, если есть.
    /// Посреди погони смена пеший ↔ транспорт требует новой проверки скорости (стр. 141).
    /// </summary>
    public static void SetVehicle(EncounterState state, Guid participantId, VehicleTemplate? template, int? driverSkill = null)
    {
        if (state.Chase is not { } chase || chase.Runner(participantId) is not { } runner)
            return;

        var hadVehicle = runner.Vehicle is not null;
        runner.Vehicle = template is null
            ? null
            : new ChaseVehicle
            {
                Name = template.Name,
                Speed = template.Speed,
                Build = template.Build,
                BuildLeft = template.Build,
                Armor = template.Armor,
                SkillCode = template.SkillCode,
                Skill = driverSkill,
            };
        if (template is not null)
        {
            runner.Mode = MovementMode.OnFoot;
            runner.ModeSpeed = null;
            runner.CarrierId = null;
        }
        else
        {
            foreach (var passenger in chase.Runners.Where(r => r.CarrierId == participantId))
                passenger.CarrierId = null;
        }

        if (chase.Phase != ChasePhase.Setup && hadVehicle != (template is not null))
            runner.SpeedChecked = false;
    }

    /// <summary>Навык водителя вписан или прочитан из листа.</summary>
    public static void SetDriverSkill(EncounterState state, Guid participantId, int? skill)
    {
        if (state.Chase?.Runner(participantId)?.Vehicle is { } vehicle)
            vehicle.Skill = skill is > 0 ? skill : null;
    }

    /// <summary>
    /// Пассажир водителя <paramref name="carrierId"/> (null — сам по себе): без проверки скорости и действий перемещения,
    /// ходит в своём порядке ЛВК, едет вместе с водителем (стр. 139).
    /// </summary>
    public static void SetPassenger(EncounterState state, Guid participantId, Guid? carrierId)
    {
        if (state.Chase is not { } chase || chase.Runner(participantId) is not { } runner)
            return;

        if (carrierId is { } id && (id == participantId || chase.Runner(id) is not { IsDriver: true } carrier))
            return;

        runner.CarrierId = carrierId;
        if (carrierId is { } driverId)
        {
            runner.Vehicle = null;
            runner.SpeedChecked = true;
            runner.SpeedModifier = 0;
            runner.Actions = runner.ActionsLeft = runner.Debt = 0;
            runner.Location = chase.Runner(driverId)!.Location;
            runner.Role = chase.Runner(driverId)!.Role;
        }
        else if (chase.Phase != ChasePhase.Setup)
        {
            runner.SpeedChecked = false;
        }
    }

    /// <summary>
    /// Смена способа передвижения мускульной силой (стр. 141): новой проверки скорости не нужно, меняется СКО (с той же
    /// поправкой), и пересчитываются действия перемещения. Возвращает запись для журнала.
    /// </summary>
    public static string? ChangeMode(EncounterState state, Guid participantId, MovementMode mode, int? modeSpeed, DateTimeOffset now)
    {
        if (state.Chase is not { } chase || chase.Runner(participantId) is not { } runner || state.Find(participantId) is not { } participant)
            return null;
        if (runner.IsDriver || runner.IsPassenger)
            return null;

        var before = Move(participant, runner);
        runner.Mode = mode;
        runner.ModeSpeed = mode == MovementMode.OnFoot || modeSpeed is not > 0 ? null : modeSpeed;
        var after = Move(participant, runner);
        if (chase.Phase == ChasePhase.Active)
            Recalculate(state);

        var source = mode == MovementMode.OnFoot
            ? "обычная СКО"
            : runner.ModeSpeed is not null
                ? "своя СКО, вписанная Хранителем"
                : NativeSpeed(participant, mode) is > 0
                    ? "своя СКО из статблока"
                    : "своей СКО для способа нет — половина обычной";
        var text = $"{participant.Name} двигается {ChaseText.Of(mode)}: СКО {ChaseText.N(before)} → {ChaseText.N(after)} ({source}). " +
                   "Проверка скорости не нужна: мускульная сила остаётся мускульной.";
        EncounterEngine.Log(state, new EncounterLogEntry { Kind = EncounterLogKind.ModeChange, ActorId = participantId, Text = text, At = now });
        return text;
    }

    // ───────────────────── СКО ─────────────────────

    /// <summary>Своя СКО способа из статблока («полёт 20»).</summary>
    public static int? NativeSpeed(EncounterParticipant participant, MovementMode mode) => mode switch
    {
        MovementMode.Swimming => participant.Stats.Swim,
        MovementMode.Flying => participant.Stats.Fly,
        _ => null,
    };

    /// <summary>
    /// СКО без поправки проверки (стр. 141): транспорт — своя; плавание и полёт — вписанная Хранителем, потом из статблока, и
    /// только потом половина обычной (без этого бьякхи летел со СКО 2 вместо 16). Пассажир движется с водителем.
    /// </summary>
    public static int BaseMove(EncounterParticipant participant, ChaseRunner runner)
    {
        if (runner.Vehicle is { } vehicle && !runner.IsPassenger)
            return vehicle.Speed;

        return runner.Mode switch
        {
            MovementMode.OnFoot => participant.Stats.Move,
            _ when runner.ModeSpeed is > 0 => runner.ModeSpeed.Value,
            _ when NativeSpeed(participant, runner.Mode) is > 0 => NativeSpeed(participant, runner.Mode)!.Value,
            _ => participant.Stats.Move / 2,
        };
    }

    /// <summary>СКО в погоне: с поправкой проверки скорости, не ниже нуля.</summary>
    public static int Move(EncounterParticipant participant, ChaseRunner runner) =>
        Math.Max(0, BaseMove(participant, runner) + runner.SpeedModifier);

    /// <summary>Комплекция в погоне: у водителя и пассажира — транспорта, иначе своя (стр. 136).</summary>
    public static double Build(EncounterState state, Guid participantId) =>
        VehicleOf(state, participantId) is { } vehicle
            ? vehicle.BuildLeft
            : state.Find(participantId)?.Stats.Build ?? 0;

    /// <summary>Транспорт, в котором сидит участник: свой (водитель) или водителя (пассажир).</summary>
    public static ChaseVehicle? VehicleOf(EncounterState state, Guid participantId)
    {
        if (state.Chase?.Runner(participantId) is not { } runner)
            return null;
        if (runner.CarrierId is { } carrier)
            return state.Chase.Runner(carrier)?.Vehicle;
        return runner.Vehicle;
    }

    /// <summary>Действий перемещения за раунд (стр. 132): одно плюс разница со СКО самого медленного.</summary>
    public static int EarnedActions(EncounterParticipant participant, ChaseRunner runner, int slowestMove) =>
        Math.Max(0, 1 + Move(participant, runner) - slowestMove);

    /// <summary>Сколько локаций участник покроет за <paramref name="rounds"/> раундов — «Бегство с места событий» (стр. 142).</summary>
    public static int LocationsCoveredIn(EncounterState state, Guid participantId, int rounds) =>
        state.Chase?.Runner(participantId) is { } runner && state.Find(participantId) is { } participant && rounds > 0
            ? EarnedActions(participant, runner, state.Chase.SlowestMove) * rounds
            : 0;

    /// <summary>Нет навыка у твари или НПС (стр. 142): ЛВК, половина или пятая часть.</summary>
    public static int SubstituteFromDex(int dexterity, SkillAptitude aptitude) => aptitude switch
    {
        SkillAptitude.Capable => dexterity,
        SkillAptitude.Unlikely => dexterity / 5,
        _ => dexterity / 2,
    };

    // ───────────────────── Проверка скорости (часть 1) ─────────────────────

    /// <summary>Почему нельзя перейти к проверке скорости; null — можно.</summary>
    public static string? SpeedChecksRejection(EncounterState state) =>
        state.Chase is not { } chase ? "Сначала создайте трассу."
        : chase.Phase != ChasePhase.Setup ? "Проверка скорости уже идёт."
        : chase.LastLocation < MinLocations ? $"Нужно хотя бы {MinLocations} локации."
        : chase.Runners.Count(r => !r.IsPassenger) < 2 ? "Нужны хотя бы двое: убегающий и преследователь."
        : !chase.Runners.Any(r => r.Role == ChaseRole.Prey) ? "Некому убегать: отметьте убегающего."
        : null;

    /// <summary>Часть 1: каждый проходит проверку скорости; пассажиры — нет (стр. 139).</summary>
    public static bool BeginSpeedChecks(EncounterState state)
    {
        if (SpeedChecksRejection(state) is not null)
            return false;

        var chase = state.Chase!;
        chase.Phase = ChasePhase.SpeedCheck;
        foreach (var runner in chase.Runners)
        {
            runner.SpeedChecked = runner.IsPassenger;
            runner.SpeedModifier = 0;
        }

        return true;
    }

    /// <summary>
    /// Исправить ошибочно записанную проверку скорости: строка снова открыта (поправка сброшена), бросок вписывают заново.
    /// Только пока погоня не началась.
    /// </summary>
    public static void ReopenSpeedCheck(EncounterState state, Guid participantId)
    {
        if (state.Chase is not { Phase: ChasePhase.SpeedCheck } chase || chase.Runner(participantId) is not { IsPassenger: false } runner)
            return;

        runner.SpeedChecked = false;
        runner.SpeedModifier = 0;
    }

    /// <summary>Вернуться к трассе из проверки скорости (поправки сброшены — их бросят заново).</summary>
    public static void BackToSetup(EncounterState state)
    {
        if (state.Chase is not { Phase: ChasePhase.SpeedCheck } chase)
            return;

        chase.Phase = ChasePhase.Setup;
        foreach (var runner in chase.Runners)
        {
            runner.SpeedChecked = false;
            runner.SpeedModifier = 0;
        }
    }

    /// <summary>Навык проверки скорости (стр. 130): пешком и мускульной тягой — ВЫН, за рулём — навык управления, а его нет — ½ ЛВК.</summary>
    public static SpeedCheckSkill SpeedSkill(EncounterParticipant participant, ChaseRunner runner)
    {
        if (runner.Vehicle is not { } vehicle || runner.IsPassenger)
            return new SpeedCheckSkill("ВЫН", participant.Stats.Con, 0);

        var value = vehicle.Skill is > 0
            ? vehicle.Skill.Value
            : SubstituteFromDex(participant.Stats.Dex, SkillAptitude.Uncertain);
        return new SpeedCheckSkill(DriveLabel(vehicle), Math.Max(1, value), VehicleRules.PenaltyDice(vehicle));
    }

    /// <summary>Подпись навыка управления транспортом.</summary>
    public static string DriveLabel(ChaseVehicle vehicle) => vehicle.SkillCode switch
    {
        VehicleReference.Ride => "Верховая езда",
        VehicleReference.HeavyMachinery => "Управление тяжёлыми машинами",
        VehicleReference.PilotAircraft or VehicleReference.PilotBoat or VehicleReference.Pilot => "Пилотирование",
        _ => "Вождение",
    };

    /// <summary>Поправка СКО по уровню проверки (стр. 130): чрезвычайный и выше — +1, успех — 0, провал — −1.</summary>
    public static int SpeedModifierFor(SuccessLevel level) => level switch
    {
        >= SuccessLevel.Extreme => 1,
        >= SuccessLevel.Regular => 0,
        _ => -1,
    };

    public static bool AllSpeedChecksDone(ChaseState chase) => chase.Runners.All(r => r.SpeedChecked);

    /// <summary>
    /// Кому проверка скорости ещё нужна: бегущие в погоне (не выбыли и не мертвы), не пассажиры. Мёртвого и выбывшего
    /// строки проверки нет, поэтому и ждать его нельзя — иначе «Начать погоню» молча блокировал погибший сыщик.
    /// </summary>
    public static List<EncounterParticipant> AwaitingSpeedCheck(EncounterState state) =>
    [
        .. InChase(state).Where(x => !x.Runner.IsPassenger && !x.Runner.SpeedChecked).Select(x => x.Participant),
    ];

    /// <summary>
    /// Предупреждение до «Начать погоню»: если по проверкам скорости погоня закончится сразу (все убегающие быстрее
    /// самого быстрого преследователя или гнаться некому), об этом говорим заранее, а не после нажатия. null — погоня
    /// начнётся как обычно.
    /// </summary>
    public static string? StartNote(EncounterState state)
    {
        var inChase = InChase(state).Where(x => !x.Runner.IsPassenger).ToList();
        var prey = inChase.Where(x => x.Runner.Role == ChaseRole.Prey).ToList();
        if (prey.Count == 0)
            return null;

        var fastestPursuer = inChase.Where(x => x.Runner.Role == ChaseRole.Pursuer)
            .Select(x => Move(x.Participant, x.Runner)).DefaultIfEmpty(-1).Max();
        return prey.All(x => Move(x.Participant, x.Runner) > fastestPursuer)
            ? "Все убегающие быстрее преследователей: погоня закончится сразу, они уйдут."
            : null;
    }

    // ───────────────────── Начало погони (части 1–2) ─────────────────────

    /// <summary>Почему погоню нельзя начать; null — можно.</summary>
    public static string? StartRejection(EncounterState state) =>
        state.Chase is not { } chase ? "Сначала создайте трассу."
        : chase.Phase != ChasePhase.SpeedCheck ? "Сначала — проверка скорости."
        : AwaitingSpeedCheck(state) is { Count: > 0 } awaiting ? $"Не проверен: {string.Join(", ", awaiting.Select(p => p.Name))}."
        : null;

    /// <summary>
    /// Начать погоню после проверки скорости (стр. 130, 145):
    /// <list type="number">
    /// <item>убегающий быстрее самого быстрого преследователя сразу отрывается; некому гнаться — убегают все;</item>
    /// <item>преследователь медленнее самого медленного оставшегося убегающего безнадёжно отстаёт;</item>
    /// <item>расстановка: самый медленный преследователь — на локацию 1, остальные впереди на разницу СКО; самый медленный
    /// убегающий — на отрыв (2, бывает 1) впереди самого быстрого преследователя, остальные — на разницу СКО;</item>
    /// <item>действия перемещения — от СКО самого медленного; первый раунд по ЛВК.</item>
    /// </list>
    /// Никого не осталось — погоня не состоялась (<see cref="ChasePhase.Ended"/>). Возвращает запись журнала.
    /// </summary>
    public static ApplyOutcome? Start(EncounterState state, DateTimeOffset now)
    {
        if (StartRejection(state) is not null)
            return null;

        var chase = state.Chase!;
        var inChase = InChase(state).Where(x => !x.Runner.IsPassenger).ToList();
        var pursuers = inChase.Where(x => x.Runner.Role == ChaseRole.Pursuer).ToList();
        var prey = inChase.Where(x => x.Runner.Role == ChaseRole.Prey).ToList();
        var fastestPursuer = pursuers.Select(x => Move(x.Participant, x.Runner)).DefaultIfEmpty(-1).Max();

        List<EncounterEffect> effects = [];
        List<string> lines = [];
        var escaping = prey.Where(x => Move(x.Participant, x.Runner) > fastestPursuer).ToList();
        foreach (var (participant, runner) in escaping)
        {
            lines.Add($"{participant.Name} (СКО {ChaseText.N(Move(participant, runner))}) быстрее любого преследователя — сразу отрывается.");
            effects.AddRange(WithPassengers(chase, participant.Id, EncounterEffectKind.Escaped));
        }

        var staying = prey.Except(escaping).ToList();
        var slowestPrey = staying.Select(x => Move(x.Participant, x.Runner)).DefaultIfEmpty(0).Min();
        List<(EncounterParticipant Participant, ChaseRunner Runner)> tooSlow =
            staying.Count == 0 ? [] : [.. pursuers.Where(x => Move(x.Participant, x.Runner) < slowestPrey)];
        foreach (var (participant, runner) in tooSlow)
        {
            lines.Add($"{participant.Name} (СКО {ChaseText.N(Move(participant, runner))}) медленнее самого медленного убегающего — безнадёжно отстаёт.");
            effects.AddRange(WithPassengers(chase, participant.Id, EncounterEffectKind.TooSlow));
        }

        var chasing = pursuers.Except(tooSlow).ToList();
        string title;
        if (staying.Count == 0 || chasing.Count == 0)
        {
            title = "Погоня не состоялась: убегающие оторвались сразу.";
            if (staying.Count > 0)
            {
                foreach (var (participant, _) in staying)
                    effects.AddRange(WithPassengers(chase, participant.Id, EncounterEffectKind.Escaped));
            }
        }
        else
        {
            title = "Погоня начинается!";
            lines.AddRange(Place(state, chasing, staying));
        }

        var outcome = EncounterEngine.Apply(state, new EncounterResolution
        {
            Kind = EncounterLogKind.ChaseStart,
            Title = title,
            Lines = lines,
            Effects = effects,
        }, now);

        if (staying.Count == 0 || chasing.Count == 0)
        {
            chase.Phase = ChasePhase.Ended;
            return outcome;
        }

        chase.SlowestMove = inChase.Except(tooSlow).Except(escaping).Select(x => Move(x.Participant, x.Runner)).DefaultIfEmpty(1).Min();
        chase.Phase = ChasePhase.Active;
        EncounterQueue.Start(state, now);
        return outcome;
    }

    /// <summary>Расстановка по СКО (стр. 145); трасса удлиняется, если кто-то не поместился (впереди нужна хоть одна локация).</summary>
    private static List<string> Place(EncounterState state, List<(EncounterParticipant Participant, ChaseRunner Runner)> pursuers,
        List<(EncounterParticipant Participant, ChaseRunner Runner)> prey)
    {
        var chase = state.Chase!;
        var slowestPursuer = pursuers.Min(x => Move(x.Participant, x.Runner));
        foreach (var (participant, runner) in pursuers)
            runner.Location = 1 + Move(participant, runner) - slowestPursuer;

        var front = pursuers.Max(x => x.Runner.Location);
        var slowestPrey = prey.Min(x => Move(x.Participant, x.Runner));
        foreach (var (participant, runner) in prey)
            runner.Location = front + chase.StartGap + Move(participant, runner) - slowestPrey;

        foreach (var passenger in chase.Runners.Where(r => r.CarrierId is not null))
            passenger.Location = chase.Runner(passenger.CarrierId!.Value)?.Location ?? passenger.Location;

        List<string> lines = [];
        var farthest = chase.Runners.Max(r => r.Location);
        if (farthest >= chase.LastLocation)
        {
            Resize(chase, farthest + 1);
            lines.Add($"Трасса удлинена до {ChaseText.Locations(chase.LastLocation)}: убегающим нужна дорога впереди.");
        }

        lines.Add("Расстановка (стр. 145): " + string.Join(", ",
            pursuers.Concat(prey).OrderBy(x => x.Runner.Location).Select(x => $"{x.Participant.Name} — {ChaseText.N(x.Runner.Location)}")) + ".");
        return lines;
    }

    // ───────────────────── Раунды (часть 2) ─────────────────────

    /// <summary>
    /// Начало раунда (зовёт <see cref="EncounterQueue"/>): у каждого в погоне — действия перемещения за вычетом долга
    /// (стр. 133), счёт атак обнулён, недоеханный разгон забыт. Пассажиры действий не имеют (стр. 139); присоединившийся без
    /// проверки скорости ждёт её.
    /// </summary>
    public static void OnRoundStarted(EncounterState state)
    {
        if (state.Chase is not { Phase: ChasePhase.Active } chase)
            return;

        foreach (var (participant, runner) in InChase(state))
        {
            runner.Attacks = 0;
            runner.Boost = null;
            if (runner.IsPassenger || !runner.SpeedChecked)
            {
                runner.Actions = runner.ActionsLeft = 0;
                if (runner.IsPassenger)
                    runner.Debt = 0;
                continue;
            }

            var earned = EarnedActions(participant, runner, chase.SlowestMove);
            var actions = Math.Max(0, earned - runner.Debt);
            runner.Debt = Math.Max(0, runner.Debt - earned);
            runner.Actions = runner.ActionsLeft = actions;
        }
    }

    /// <summary>
    /// СКО изменилась посреди погони (присоединился, сменил способ, прошёл новую проверку): самая медленная СКО и действия
    /// пересчитываются (стр. 140–141). Число действий этого раунда меняется на разницу — потраченные не возвращаются.
    /// </summary>
    public static void Recalculate(EncounterState state)
    {
        if (state.Chase is not { Phase: ChasePhase.Active } chase)
            return;

        var counted = InChase(state).Where(x => !x.Runner.IsPassenger && x.Runner.SpeedChecked).ToList();
        if (counted.Count == 0)
            return;

        chase.SlowestMove = counted.Min(x => Move(x.Participant, x.Runner));
        foreach (var (participant, runner) in counted)
        {
            var total = EarnedActions(participant, runner, chase.SlowestMove);
            runner.ActionsLeft = Math.Max(0, runner.ActionsLeft + total - runner.Actions);
            runner.Actions = total;
        }
    }

    /// <summary>
    /// Самый медленный убегающий в погоне (для присоединившегося преследователя: медленнее — не учитывается, стр. 140).
    /// </summary>
    public static int SlowestPreyMove(EncounterState state) =>
        InChase(state).Where(x => x.Runner.Role == ChaseRole.Prey && !x.Runner.IsPassenger)
            .Select(x => Move(x.Participant, x.Runner)).DefaultIfEmpty(0).Min();

    /// <summary>
    /// После каждого <c>Apply</c> (зовёт ядро): убегающий на последней локации, впереди всех преследователей, — сбежал сам
    /// (стр. 135); поимку объявляет только Хранитель — одна локация с преследователем открывает лишь бой и манёвр. Все
    /// убегающие вне погони — конец.
    /// </summary>
    public static void AfterApply(EncounterState state, DateTimeOffset now)
    {
        if (state.Chase is not { Phase: ChasePhase.Active } chase)
            return;

        // Преследователей не осталось (отстали, потеряли след, без сознания) — гнаться некому: убегающие уходят.
        var pursuers = InChase(state).Where(x => x.Runner.Role == ChaseRole.Pursuer).ToList();
        var pursuerFront = pursuers.Select(x => x.Runner.Location).DefaultIfEmpty(0).Max();
        foreach (var (participant, runner) in InChase(state).Where(x => x.Runner.Role == ChaseRole.Prey && !x.Runner.IsPassenger).ToList())
        {
            var reachedEnd = runner.Location >= chase.LastLocation && runner.Location > pursuerFront;
            if (!reachedEnd && pursuers.Count > 0)
                continue;

            List<string> names = [participant.Name];
            MarkOut(participant, runner, ChaseStatus.Escaped);
            foreach (var passenger in chase.Runners.Where(r => r.CarrierId == participant.Id))
            {
                if (state.Find(passenger.ParticipantId) is { } p)
                {
                    MarkOut(p, passenger, ChaseStatus.Escaped);
                    names.Add(p.Name);
                }
            }

            EncounterEngine.Log(state, new EncounterLogEntry
            {
                Kind = EncounterLogKind.Escaped,
                ActorId = participant.Id,
                Text = pursuers.Count == 0
                    ? $"{string.Join(", ", names)}: гнаться больше некому — сбежал!"
                    : $"{string.Join(", ", names)}: трасса пройдена, преследователи позади — сбежал!",
                At = now,
            });
        }

        if (IsOver(state))
        {
            chase.Phase = ChasePhase.Ended;
            EncounterEngine.Log(state, new EncounterLogEntry
            {
                Kind = EncounterLogKind.ChaseStart,
                // «Погоня окончена. Пойманы: A. Сбежали: B, C.» — с заглавной и по группам, как плашка итога на экране.
                Text = ("Погоня окончена. " + string.Join(" ", state.Participants
                    .Where(p => chase.Runner(p.Id) is { Role: ChaseRole.Prey })
                    .GroupBy(p => StatusOf(state, p.Id))
                    .OrderBy(g => g.Key)
                    .Select(g => $"{ChaseText.Group(g.Key)}: {string.Join(", ", g.Select(p => p.Name))}."))).TrimEnd(),
                At = now,
            });
        }
    }

    /// <summary>Все убегающие вне погони: сбежали, пойманы или выбыли (без сознания).</summary>
    public static bool IsOver(EncounterState state) =>
        state.Chase is { } chase && state.Participants
            .Where(p => chase.Runner(p.Id) is { Role: ChaseRole.Prey })
            .All(p => p.IsOut);

    /// <summary>
    /// Статус участника в погоне. Выбывший из очереди без особой причины (без сознания, «Выбыл» в строке) — свой статус
    /// <see cref="ChaseStatus.Running"/> с <c>IsOut</c>; «Вернуть» возвращает в погоню любого.
    /// </summary>
    public static ChaseStatus StatusOf(EncounterState state, Guid participantId) =>
        state.Find(participantId) is { IsOut: true } && state.Chase?.Runner(participantId) is { } runner ? runner.Status : ChaseStatus.Running;

    internal static void MarkOut(EncounterParticipant participant, ChaseRunner runner, ChaseStatus status)
    {
        participant.IsOut = true;
        runner.Status = status;
    }

    /// <summary>Участники в погоне: не выбыли из очереди.</summary>
    public static IEnumerable<(EncounterParticipant Participant, ChaseRunner Runner)> InChase(EncounterState state)
    {
        if (state.Chase is not { } chase)
            yield break;

        foreach (var participant in state.Participants)
        {
            if (!participant.IsOut && !participant.Dead && chase.Runner(participant.Id) is { } runner)
                yield return (participant, runner);
        }
    }

    /// <summary>Эффект участнику и всем, кто едет с ним пассажиром.</summary>
    internal static IEnumerable<EncounterEffect> WithPassengers(ChaseState chase, Guid participantId, EncounterEffectKind kind)
    {
        yield return new EncounterEffect { Kind = kind, ParticipantId = participantId };
        foreach (var passenger in chase.Runners.Where(r => r.CarrierId == participantId))
            yield return new EncounterEffect { Kind = kind, ParticipantId = passenger.ParticipantId };
    }

    // ───────────────────── Ход: что можно сделать ─────────────────────

    /// <summary>
    /// Что может сделать участник в свой ход — одна функция вместо цепочки условий разметки v1:
    /// <list type="bullet">
    /// <item>помеха и преграда лежат в <b>следующей</b> локации: перед помехой «вперёд» — это «пройти помеху» (помеху проходят
    /// всегда, но с проверкой), перед преградой — преодолеть или разрушить (если у неё есть ПЗ);</item>
    /// <item>ближний бой, манёвр, таран — с теми, кто в той же локации (стр. 136); огнестрел — на любую дистанцию;</item>
    /// <item>атак за раунд столько же, сколько в бою; действие перемещения нужно на всё, кроме стрельбы на ходу;</item>
    /// <item>разгон идёт без новых действий, пока не проехал объявленное;</item>
    /// <item>пассажир не двигается сам: стреляет, помогает штурманом, создаёт помеху (стр. 139);</item>
    /// <item>спрятаться — убегающему, искать след — преследователю (стр. 139); «Педаль в пол» — если включено правило части 5.</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<ChaseActionKind> AvailableActions(EncounterState state, Guid participantId)
    {
        if (state.Chase is not { Phase: ChasePhase.Active } chase
            || state.Find(participantId) is not { IsOut: false } participant
            || chase.Runner(participantId) is not { } runner)
            return [];

        List<ChaseActionKind> actions = [];
        var attacksLeft = runner.Attacks < Math.Max(1, participant.Stats.AttacksPerRound);
        var others = Others(state, participantId).ToList();

        if (runner.IsPassenger)
        {
            if (attacksLeft && others.Count > 0)
                actions.Add(ChaseActionKind.Ranged);
            if (chase.Runner(runner.CarrierId!.Value) is { Vehicle: { } vehicle } && !VehicleRules.IsWrecked(vehicle))
                actions.Add(ChaseActionKind.Navigate);
            actions.Add(ChaseActionKind.CreateObstacle);
            return actions;
        }

        var hasAction = runner.ActionsLeft > 0;
        var canMove = hasAction || runner.Boost is not null;
        var wrecked = runner.Vehicle is { } own && VehicleRules.IsWrecked(own);
        var next = chase.Location(runner.Location + 1);
        var barrier = next?.Barrier;
        var hazard = barrier is null ? next?.Hazard : null;
        var sameLocation = others.Where(x => x.Runner.Location == runner.Location).ToList();

        // Летящему помехи и преграды на земле обычно не мешают (стр. 142: летающим не нужны Прыжки и Лазание) — Хранитель
        // может просто пропустить его над ними; проверка остаётся на выбор.
        var flying = runner.Mode == MovementMode.Flying && runner.Vehicle is null;
        if (!wrecked && canMove && next is not null && ((barrier is null && hazard is null) || flying))
            actions.Add(ChaseActionKind.Move);
        if (!wrecked && canMove && hazard is not null)
            actions.Add(ChaseActionKind.Hazard);
        if (!wrecked && hasAction && barrier is not null)
            actions.Add(ChaseActionKind.Barrier);
        if (hasAction && barrier is { CanBreak: true })
            actions.Add(ChaseActionKind.BreakBarrier);
        if (hasAction && attacksLeft && sameLocation.Count > 0)
        {
            actions.Add(ChaseActionKind.Melee);
            actions.Add(ChaseActionKind.Maneuver);
        }

        if (attacksLeft && others.Count > 0)
            actions.Add(ChaseActionKind.Ranged);
        if (runner.IsDriver && !wrecked && hasAction && attacksLeft && sameLocation.Count > 0)
            actions.Add(ChaseActionKind.Ram);
        if (attacksLeft && others.Any(x => x.Runner.IsDriver))
            actions.Add(ChaseActionKind.Tyres);
        if (chase.FloorIt && runner.IsDriver && !wrecked && hasAction && runner.Boost is null && next is not null && barrier is null)
            actions.Add(ChaseActionKind.FloorIt);
        if (hasAction && runner.Role == ChaseRole.Prey)
            actions.Add(ChaseActionKind.Hide);
        if (hasAction && runner.Role == ChaseRole.Pursuer)
            actions.Add(ChaseActionKind.Track);
        actions.Add(ChaseActionKind.CreateObstacle);
        if (runner.IsDriver)
            actions.Add(ChaseActionKind.DriverControl);
        return actions;
    }

    /// <summary>Цели ближнего боя, манёвра и тарана — в той же локации (стр. 136).</summary>
    public static IReadOnlyList<EncounterParticipant> CloseTargets(EncounterState state, Guid participantId)
    {
        var location = state.Chase?.Runner(participantId)?.Location;
        return [.. Others(state, participantId).Where(x => x.Runner.Location == location).Select(x => x.Participant)];
    }

    /// <summary>Цели огнестрела — любые в погоне (стр. 136, 139).</summary>
    public static IReadOnlyList<EncounterParticipant> RangedTargets(EncounterState state, Guid participantId) =>
        [.. Others(state, participantId).Select(x => x.Participant)];

    /// <summary>Цели стрельбы по шинам — водители в погоне.</summary>
    public static IReadOnlyList<EncounterParticipant> VehicleTargets(EncounterState state, Guid participantId) =>
        [.. Others(state, participantId).Where(x => x.Runner.IsDriver).Select(x => x.Participant)];

    /// <summary>Остальные в погоне, кроме тех, кто едет в одной машине с участником (свой водитель, свои пассажиры).</summary>
    private static IEnumerable<(EncounterParticipant Participant, ChaseRunner Runner)> Others(EncounterState state, Guid participantId)
    {
        var vehicle = state.Chase?.Runner(participantId) is { } runner ? runner.CarrierId ?? (runner.IsDriver ? participantId : null) : null;
        return InChase(state).Where(x => x.Participant.Id != participantId
                                         && (vehicle is null || (x.Participant.Id != vehicle && x.Runner.CarrierId != vehicle)));
    }

    /// <summary>Убегающие, с которыми в одной локации есть преследователь: поимку можно объявить (стр. 135).</summary>
    public static IReadOnlyList<(EncounterParticipant Prey, IReadOnlyList<EncounterParticipant> Pursuers)> Contacts(EncounterState state)
    {
        var inChase = InChase(state).ToList();
        return
        [
            .. inChase.Where(x => x.Runner.Role == ChaseRole.Prey)
                .Select(x => (x.Participant, (IReadOnlyList<EncounterParticipant>)
                    [.. inChase.Where(p => p.Runner.Role == ChaseRole.Pursuer && p.Runner.Location == x.Runner.Location).Select(p => p.Participant)]))
                .Where(x => x.Item2.Count > 0),
        ];
    }

    /// <summary>Пары с одинаковой ЛВК — их порядок решает встречная проверка ЛВК (стр. 132).</summary>
    public static IReadOnlyList<(EncounterParticipant First, EncounterParticipant Second)> DexTies(EncounterState state)
    {
        var ordered = InChase(state).Select(x => x.Participant).ToList();
        List<(EncounterParticipant, EncounterParticipant)> ties = [];
        for (var i = 0; i < ordered.Count; i++)
        {
            for (var j = i + 1; j < ordered.Count; j++)
            {
                if (ordered[i].Initiative == ordered[j].Initiative && ordered[i].Stats.Dex == ordered[j].Stats.Dex
                    && !Resolved(state.Chase!, ordered[i].Id, ordered[j].Id))
                    ties.Add((ordered[i], ordered[j]));
            }
        }

        return ties;
    }

    private static bool Resolved(ChaseState chase, Guid a, Guid b) =>
        chase.Ties.Any(t => (t.First == a && t.Second == b) || (t.First == b && t.Second == a));

    /// <summary>
    /// Встречная проверка ЛВК при равенстве (стр. 132): выше уровень — ходит первым; уровни равны — меньший бросок. Броски
    /// можно вписать (F-P09: в v1 кнопка всегда бросала сама). Победитель встаёт раньше в порядке добавления — им очередь
    /// разводит равных, и так в каждом следующем раунде. Возвращает true, если первым ходит <paramref name="firstId"/>.
    /// </summary>
    public static bool ResolveDexTie(EncounterState state, Guid firstId, Guid secondId, int? firstRoll, int? secondRoll,
        IDiceRoller dice, DateTimeOffset now)
    {
        var first = state.Find(firstId) ?? throw new ArgumentException("Нет участника.", nameof(firstId));
        var second = state.Find(secondId) ?? throw new ArgumentException("Нет участника.", nameof(secondId));

        var rollA = D100.RollOrEntered(dice, firstRoll).Result;
        var rollB = D100.RollOrEntered(dice, secondRoll).Result;
        var levelA = Check.Evaluate(rollA, first.Stats.Dex);
        var levelB = Check.Evaluate(rollB, second.Stats.Dex);
        var firstWins = levelA > levelB || (levelA == levelB && rollA <= rollB);
        var (winner, loser) = firstWins ? (first, second) : (second, first);

        if (state.Chase is { } chase && !Resolved(chase, winner.Id, loser.Id))
            chase.Ties.Add(new ChaseTie { First = winner.Id, Second = loser.Id });

        // Победитель встаёт сразу перед проигравшим (а не меняется с ним местами): так при трёх равных уже решённые пары
        // не переворачиваются.
        var winnerIndex = state.Participants.IndexOf(winner);
        var loserIndex = state.Participants.IndexOf(loser);
        if (winnerIndex > loserIndex)
        {
            state.Participants.RemoveAt(winnerIndex);
            state.Participants.Insert(loserIndex, winner);
        }

        EncounterEngine.Log(state, new EncounterLogEntry
        {
            Kind = EncounterLogKind.ChaseStart,
            ActorId = winner.Id,
            Text = $"Равная ЛВК: первым ходит {winner.Name}" + (state.Round > 0 ? " (со следующего раунда)." : "."),
            Lines =
            [
                $"{first.Name}: {ChaseText.N(rollA)} против ЛВК {ChaseText.N(first.Stats.Dex)} ({RulesText.Of(levelA)})",
                $"{second.Name}: {ChaseText.N(rollB)} против ЛВК {ChaseText.N(second.Stats.Dex)} ({RulesText.Of(levelB)})",
            ],
            At = now,
        });
        return firstWins;
    }

    /// <summary>
    /// Хранитель объявляет убегающего пойманным (после манёвра, атаки или по сцене) — автоматически поимки нет (стр. 135).
    /// Возвращает результат с эффектом; null — уже не в погоне.
    /// </summary>
    public static EncounterResolution? Catch(EncounterState state, Guid preyId, Guid? catcherId)
    {
        if (state.Chase is not { } chase || state.Find(preyId) is not { IsOut: false } prey)
            return null;

        var catcher = catcherId is { } id ? state.Find(id) : null;
        return new EncounterResolution
        {
            Kind = EncounterLogKind.Caught,
            ActorId = catcher?.Id,
            Title = catcher is null
                ? $"{prey.Name} пойман!"
                : $"{prey.Name} пойман — {catcher.Name} настиг его на локации {ChaseText.N(chase.Runner(preyId)?.Location ?? 0)}.",
            Effects = [.. WithPassengers(chase, preyId, EncounterEffectKind.Caught)],
        };
    }

    /// <summary>Внезапные помехи назначают по очереди (стр. 137): сторона не объявляет дважды подряд. null — можно.</summary>
    public static string? SuddenHazardRejection(ChaseState chase, bool byPlayers) =>
        chase.SuddenHazardByPlayers == byPlayers
            ? byPlayers ? "Игроки уже назначали внезапную помеху — теперь очередь Хранителя." : "Хранитель уже назначал — теперь очередь игроков."
            : null;

    /// <summary>Отметить, кто назначил внезапную помеху.</summary>
    public static void DeclareSuddenHazard(ChaseState chase, bool byPlayers) => chase.SuddenHazardByPlayers = byPlayers;

    // ───────────────────── Обломки разрушенной преграды ─────────────────────

    /// <summary>Урон обломков-помехи при провале — лёгкая травма таблицы III (для людей), как у v1; правится в локации.</summary>
    public const string DebrisDamage = "1d3";

    /// <summary>
    /// Эффект урона преграде в результате, который её разрушит (ПЗ дойдут до 0). Только у такого результата Хранитель
    /// решает, станут ли обломки помехой (стр. 136: «могут стать»). null — преграда устоит или её нет.
    /// </summary>
    public static EncounterEffect? BarrierBreaking(EncounterState state, EncounterResolution resolution) =>
        state.Chase is not { } chase
            ? null
            : resolution.Effects.FirstOrDefault(e => e.Kind == EncounterEffectKind.BarrierDamage
                                                     && chase.Location(e.Location ?? 0)?.Barrier is { } barrier
                                                     && barrier.HitPointsLeft - Math.Max(0, e.Amount) <= 0);

    /// <summary>Какой помехой лягут обломки по решению Хранителя; null — не лягут (по умолчанию).</summary>
    public static Difficulty? DebrisOf(EncounterResolution resolution) =>
        resolution.Effects.FirstOrDefault(e => e.Kind == EncounterEffectKind.BarrierDamage)?.Obstacle?.Hazard?.Difficulty;

    /// <summary>
    /// Решение Хранителя в предпросмотре разрушения преграды (стр. 136): оставить ли на её месте помеху-обломки и какой
    /// сложности (обычная, трудная, чрезвычайная — как у любой помехи, стр. 133). <paramref name="difficulty"/> null — проход
    /// свободен. Меняет только предложенный результат (<see cref="EncounterState.Pending"/>); применит его <c>Apply</c>.
    /// В v1 (и до решения владельца 2026-10-02) обломки становились обычной помехой всегда.
    /// </summary>
    public static void SetDebris(EncounterState state, Difficulty? difficulty)
    {
        if (state.Pending is not { } pending || BarrierBreaking(state, pending) is not { Location: { } number } effect)
            return;

        var barrier = state.Chase!.Location(number)!.Barrier!;
        effect.Obstacle = difficulty is { } chosen
            ? new ChaseLocation { Number = number, Hazard = new ChaseHazard { Name = $"Обломки: {barrier.Name}", Difficulty = chosen, Damage = DebrisDamage } }
            : null;
    }
}
