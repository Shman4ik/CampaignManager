using CampaignManager.Core.Dice;
using CampaignManager.Core.KeeperScreen;

namespace CampaignManager.Core.Encounters.Chase;

/// <summary>
/// Проверка действия погони: навык (подпись), значение, вписанный бросок (null — бросят кости правила), сложность и кости.
/// Уровень — от полного значения (<see cref="Check.Evaluate"/>), сложность решает только, пройдена ли проверка: в v1 погоня
/// считала уровень от урезанного порога и писала в журнал «трудный» вместо «чрезвычайного» (F-P01).
/// </summary>
public sealed record ChaseCheck(string Skill, int Value, int? Roll = null, Difficulty Difficulty = Difficulty.Regular,
    int BonusDice = 0, int PenaltyDice = 0);

/// <summary>Ответ цели на атаку: уклонение или контратака (стр. 136 — предлагать всегда, сколько бы действий ни осталось).</summary>
public sealed record ChaseDefence(string Skill, int Value, int? Roll = null, DefenceKind Kind = DefenceKind.Dodge);

/// <summary>Урон людям: вписанное число или формула (таблица III, оружие), которую бросят кости правила.</summary>
public sealed record ChaseHarm(string? Formula = null, int? Roll = null);

/// <summary>
/// Последствия провала помехи (и, по решению Хранителя, преграды или удачного манёвра против цели): урон пешему по
/// таблице III, авария транспорта по таблице VI (Комплекция и урон каждому внутри), потеря 1d3 действий (стр. 133, 136,
/// 142–144). Всё, что не вписано, бросают кости.
/// </summary>
public sealed record ChaseMishap(ChaseHarm? Harm = null, CrashTier? Crash = null, int? CrashRoll = null, bool LoseActions = true,
    int? LostActionsRoll = null);

/// <summary>Что получилось: результат для предпросмотра и, для тестов и подписи, бросок и уровень.</summary>
public sealed record ChaseOutcome(EncounterResolution Resolution, D100Roll? Roll, SuccessLevel Level, bool Success);

/// <summary>
/// Действия в ход погони (части 3–5). Каждое <b>только разрешает</b> и возвращает <see cref="EncounterResolution"/> с эффектами —
/// применяет их <c>EncounterEngine.Apply</c> после предпросмотра (контракт v1 погони, теперь общий с боем). Кости — параметром
/// (<see cref="IDiceRoller"/>), любой бросок можно вписать. Объявленные кости (бонусные за осторожность, штрафные разгона,
/// поломки, стрельбы на ходу) попадают и в автобросок.
/// </summary>
public static class ChaseActions
{
    private const int TyreArmour = 3;

    // ───────────────────── Проверка скорости (часть 1) ─────────────────────

    /// <summary>
    /// Проверка скорости (стр. 130): ВЫН пешком, навык управления за рулём (поломка — штрафная кость); чрезвычайный успех —
    /// +1 СКО на всю погоню, успех — 0, провал — −1. Посреди погони (присоединился, сел за руль) — с пересчётом действий.
    /// </summary>
    public static ChaseOutcome SpeedCheck(EncounterState state, Guid participantId, int? roll, IDiceRoller dice)
    {
        var (actor, runner, _) = Context(state, participantId);
        var skill = ChaseRules.SpeedSkill(actor, runner);
        var test = Test(new ChaseCheck(skill.Label, skill.Value, roll, PenaltyDice: skill.PenaltyDice), dice);
        var modifier = ChaseRules.SpeedModifierFor(test.Level);
        var before = ChaseRules.BaseMove(actor, runner);
        var move = Math.Max(0, before + modifier);
        List<string> lines = [test.Line];
        List<EncounterEffect> effects = [];

        // Присоединившийся преследователь медленнее самого медленного убегающего не учитывается (стр. 140).
        if (state.Chase!.Phase == ChasePhase.Active && runner.Role == ChaseRole.Pursuer && move < ChaseRules.SlowestPreyMove(state))
        {
            lines.Add($"Медленнее самого медленного убегающего (СКО {ChaseText.N(ChaseRules.SlowestPreyMove(state))}) — в погоне не учитывается.");
            effects.AddRange(ChaseRules.WithPassengers(state.Chase, participantId, EncounterEffectKind.TooSlow));
        }

        effects.Add(Effect(EncounterEffectKind.ChaseSpeed, participantId, modifier));
        return Done(new EncounterResolution
        {
            Kind = EncounterLogKind.SpeedCheck,
            ActorId = participantId,
            Title = $"{actor.Name}: проверка скорости ({skill.Label}) — СКО {ChaseText.N(move)}" +
                    (modifier switch { > 0 => " (+1)", < 0 => " (−1)", _ => "" }),
            Lines = lines,
            Effects = effects,
        }, test);
    }

    // ───────────────────── Перемещение (часть 3) ─────────────────────

    /// <summary>Вперёд на одну локацию — одно действие (стр. 132); в разгоне — без нового действия.</summary>
    public static ChaseOutcome Move(EncounterState state, Guid actorId)
    {
        var (actor, runner, chase) = Context(state, actorId);
        var to = Math.Min(runner.Location + 1, chase.LastLocation);
        var boosting = runner.Boost is not null;
        List<EncounterEffect> effects = [Effect(EncounterEffectKind.ChaseMove, actorId, to, flag: boosting)];
        if (!boosting)
            effects.Add(Effect(EncounterEffectKind.ChaseActionsSpent, actorId, 1));

        return Done(new EncounterResolution
        {
            Kind = EncounterLogKind.Move,
            ActorId = actorId,
            Title = $"{actor.Name}: локация {ChaseText.N(runner.Location)} → {ChaseText.N(to)}" + (boosting ? " (разгон)" : ""),
            Effects = effects,
        }, true);
    }

    /// <summary>
    /// Помеха в локации <paramref name="location"/> — следующей (стр. 133). Можно обменять 1–2 действия на бонусные кости;
    /// успех и провал одинаково пропускают дальше, провал — урон и 1d3 потерянных действий (<see cref="ChaseMishap"/>). В
    /// разгоне — его штрафные кости и без нового действия; провал обрывает разгон (стр. 139). Водитель с поломкой — штрафная кость.
    /// </summary>
    public static ChaseOutcome Hazard(EncounterState state, Guid actorId, int location, ChaseCheck check, ChaseMishap? failure,
        IDiceRoller dice)
    {
        var (actor, runner, chase) = Context(state, actorId);
        var hazard = chase.Location(location)?.Hazard;
        var name = hazard?.Name ?? "Помеха";
        var difficulty = hazard?.Difficulty ?? Difficulty.Regular;
        var (bonus, penalty) = ActionDice(state, actorId, ChaseActionKind.Hazard, check.BonusDice, check.PenaltyDice);
        var boost = runner.Boost;
        var destination = Math.Clamp(location, 1, Math.Max(1, chase.LastLocation));

        var test = Test(check with { Difficulty = difficulty, BonusDice = bonus, PenaltyDice = penalty }, dice);
        List<string> lines = [test.Line];
        if (bonus > 0)
            lines.Add($"Осторожно: {ChaseText.Actions(bonus)} на бонусные кости.");
        if (boost is { PenaltyDice: > 0 })
            lines.Add($"Разгон: {ChaseText.PenaltyDice(boost.PenaltyDice)}.");

        List<EncounterEffect> effects = [Effect(EncounterEffectKind.ChaseMove, actorId, destination, flag: boost is not null)];
        var spent = (boost is null ? 1 : 0) + bonus;
        if (spent > 0)
            effects.Add(Effect(EncounterEffectKind.ChaseActionsSpent, actorId, spent));

        if (!test.Success)
        {
            effects.AddRange(Mishap(state, actorId, failure ?? new ChaseMishap(), difficulty, dice, lines));
            if (boost is not null)
                effects.Add(Effect(EncounterEffectKind.ChaseBoost, actorId, 0, detail: "провал помехи обрывает разгон"));
        }

        var verdict = test.Success ? "проходит" : "провал, но всё равно проходит";
        return Done(new EncounterResolution
        {
            Kind = EncounterLogKind.Hazard,
            ActorId = actorId,
            Title = $"{actor.Name}: помеха «{name}» ({ChaseText.Of(difficulty)}) — {verdict} на локацию {ChaseText.N(destination)}",
            Lines = lines,
            Effects = effects,
        }, test);
    }

    /// <summary>
    /// Преграда в локации <paramref name="location"/> (стр. 134): успех — вход в неё, провал — остаётся на месте. Одно
    /// действие. Последствия провала (урон, потерянное время) — только если Хранитель их назначил (<paramref name="failure"/>).
    /// </summary>
    public static ChaseOutcome Barrier(EncounterState state, Guid actorId, int location, ChaseCheck check, ChaseMishap? failure,
        IDiceRoller dice)
    {
        var (actor, runner, chase) = Context(state, actorId);
        var barrier = chase.Location(location)?.Barrier;
        var name = barrier?.Name ?? "Преграда";
        var difficulty = barrier?.Difficulty ?? Difficulty.Regular;
        var (bonus, penalty) = ActionDice(state, actorId, ChaseActionKind.Barrier, check.BonusDice, check.PenaltyDice);
        var test = Test(check with { Difficulty = difficulty, BonusDice = bonus, PenaltyDice = penalty }, dice);
        List<string> lines = [test.Line];

        List<EncounterEffect> effects = [Effect(EncounterEffectKind.ChaseActionsSpent, actorId, 1)];
        if (runner.Boost is not null)
            effects.Add(Effect(EncounterEffectKind.ChaseBoost, actorId, 0, detail: "перед преградой разгон кончается"));
        if (test.Success)
            effects.Insert(0, Effect(EncounterEffectKind.ChaseMove, actorId, Math.Clamp(location, 1, Math.Max(1, chase.LastLocation))));
        else if (failure is not null)
            effects.AddRange(Mishap(state, actorId, failure, difficulty, dice, lines));

        return Done(new EncounterResolution
        {
            Kind = EncounterLogKind.Barrier,
            ActorId = actorId,
            Title = $"{actor.Name}: преграда «{name}» ({ChaseText.Of(difficulty)}) — " +
                    (test.Success ? $"преодолена, локация {ChaseText.N(location)}" : "не удалось, остаётся на месте"),
            Lines = lines,
            Effects = effects,
        }, test);
    }

    /// <summary>
    /// Разрушить преграду в локации <paramref name="location"/> (стр. 135–136), без проверки атаки, одно действие, с места.
    /// Пешком — урон вписывает Хранитель (по умолчанию 1d3, как у Харви с забором). Транспорт — 1d10 за каждый пункт
    /// <b>своей</b> Комплекции (в v1 — Комплекции водителя, F-P06) и отдача: пробил — половина ПЗ преграды до удара, не
    /// пробил — машина «сама получает повреждения» (сколько — книга не говорит; как в v1, половина своего урона).
    /// </summary>
    public static ChaseOutcome BreakBarrier(EncounterState state, Guid actorId, int location, int? damageRoll, IDiceRoller dice)
    {
        var (actor, runner, chase) = Context(state, actorId);
        var barrier = chase.Location(location)?.Barrier;
        var name = barrier?.Name ?? "Преграда";
        var vehicle = runner.IsDriver ? runner.Vehicle : null;
        string formula;
        int damage;
        if (vehicle is not null)
        {
            var count = VehicleRules.DamageDice(vehicle.BuildLeft);
            formula = $"{ChaseText.N(count)}d10";
            damage = damageRoll ?? dice.Roll(count, 10);
        }
        else
        {
            formula = "1d3";
            damage = damageRoll ?? dice.Roll(1, 3);
        }

        var before = barrier?.HitPointsLeft ?? 0;
        var after = Math.Max(0, before - damage);
        var destroyed = barrier is not null && after <= 0;
        List<string> lines = [$"Урон преграде: {ChaseText.N(damage)} ({(damageRoll is null ? formula : "вписано")})."];
        List<EncounterEffect> effects =
        [
            new() { Kind = EncounterEffectKind.BarrierDamage, ParticipantId = actorId, Location = location, Amount = damage },
            Effect(EncounterEffectKind.ChaseActionsSpent, actorId, 1),
        ];

        if (vehicle is not null)
        {
            var recoil = destroyed ? before / 2 : damage / 2;
            var loss = VehicleRules.BuildLoss(recoil);
            lines.Add($"Отдача транспорту: {ChaseText.N(recoil)} урона — Комплекция −{ChaseText.N(loss)}" +
                      (destroyed ? " (половина ПЗ преграды)." : " (не пробил — половина своего урона)."));
            if (loss > 0)
                effects.Add(Effect(EncounterEffectKind.VehicleBuild, actorId, loss, detail: "отдача"));
        }

        return Done(new EncounterResolution
        {
            Kind = EncounterLogKind.BarrierBreak,
            ActorId = actorId,
            Title = destroyed
                ? $"{actor.Name} разрушает «{name}»: ПЗ {ChaseText.N(before)} → 0, обломки стали помехой"
                : $"{actor.Name} бьёт «{name}»: ПЗ {ChaseText.N(before)} → {ChaseText.N(after)}",
            Lines = lines,
            Effects = effects,
        }, destroyed);
    }

    // ───────────────────── Столкновение (часть 4) ─────────────────────

    /// <summary>
    /// Ближний бой в одной локации (стр. 136): одно действие перемещения, атака как в бою, цель может уклониться или
    /// контратаковать. Урон — вписанный или по формуле оружия, минус броня цели; рана и запись в лист — ядро.
    /// </summary>
    public static ChaseOutcome Melee(EncounterState state, Guid actorId, Guid targetId, ChaseCheck check, ChaseDefence? defence,
        ChaseHarm? damage, IDiceRoller dice)
    {
        var (actor, _, _) = Context(state, actorId);
        var target = Target(state, targetId);
        var attack = Test(check, dice);
        List<string> lines = [attack.Line];
        var hit = Defend(attack, target, defence, dice, lines);

        List<EncounterEffect> effects =
        [
            Effect(EncounterEffectKind.ChaseActionsSpent, actorId, 1),
            Effect(EncounterEffectKind.ChaseAttack, actorId, 1),
        ];
        if (hit)
            effects.AddRange(Wound(state, target, damage, target.Stats.Armor, dice, lines));

        return Done(new EncounterResolution
        {
            Kind = EncounterLogKind.ChaseAttack,
            ActorId = actorId,
            Title = $"{actor.Name} атакует {target.Name} ({check.Skill}) — {(hit ? "попадание" : "мимо")}",
            Lines = lines,
            Effects = effects,
        }, attack with { Success = hit });
    }

    /// <summary>
    /// Стрельба на любую дистанцию (стр. 136, 139): остановился — одно действие; на ходу — без действия, но со штрафной
    /// костью. Броня транспорта защищает водителя и пассажиров.
    /// </summary>
    public static ChaseOutcome Ranged(EncounterState state, Guid actorId, Guid targetId, ChaseCheck check, bool stopped,
        ChaseHarm? damage, IDiceRoller dice)
    {
        var (actor, _, _) = Context(state, actorId);
        var target = Target(state, targetId);
        var (bonus, penalty) = ActionDice(state, actorId, ChaseActionKind.Ranged, check.BonusDice, check.PenaltyDice, stopped: stopped);
        var attack = Test(check with { BonusDice = bonus, PenaltyDice = penalty }, dice);
        List<string> lines = [attack.Line, stopped ? "Остановился, чтобы выстрелить: одно действие." : "Стреляет на ходу: штрафная кость, без действия."];

        List<EncounterEffect> effects = [Effect(EncounterEffectKind.ChaseAttack, actorId, 1)];
        if (stopped)
            effects.Insert(0, Effect(EncounterEffectKind.ChaseActionsSpent, actorId, 1));
        if (attack.Success)
        {
            var armour = target.Stats.Armor + (ChaseRules.VehicleOf(state, targetId)?.Armor ?? 0);
            effects.AddRange(Wound(state, target, damage, armour, dice, lines));
        }

        return Done(new EncounterResolution
        {
            Kind = EncounterLogKind.ChaseAttack,
            ActorId = actorId,
            Title = $"{actor.Name} стреляет в {target.Name} ({(stopped ? "стоя" : "на ходу")}) — {(attack.Success ? "попадание" : "промах")}",
            Lines = lines,
            Effects = effects,
        }, attack);
    }

    /// <summary>Штрафные кости манёвра за Комплекцию (стр. 136): цель крупнее на 1 — одна, на 2 — две, на 3 и больше — нельзя.</summary>
    public static (int PenaltyDice, bool Impossible) ManeuverBuildPenalty(double attackerBuild, double targetBuild)
    {
        var difference = (int)Math.Floor(targetBuild - attackerBuild);
        return difference >= 3 ? (0, true) : (Math.Max(0, difference), false);
    }

    /// <summary>
    /// Боевой манёвр в погоне (стр. 136): удачный — цель как при провале помехи: теряет 1d3 действия и, если подходит по
    /// ситуации, получает урон (таблица III) или аварию (таблица VI). Комплекция — транспорта у тех, кто в нём.
    /// </summary>
    public static ChaseOutcome Maneuver(EncounterState state, Guid actorId, Guid targetId, ChaseCheck check, ChaseDefence? defence,
        ChaseMishap? effect, IDiceRoller dice)
    {
        var (actor, _, _) = Context(state, actorId);
        var target = Target(state, targetId);
        var (buildPenalty, impossible) = ManeuverBuildPenalty(ChaseRules.Build(state, actorId), ChaseRules.Build(state, targetId));
        if (impossible)
        {
            return new ChaseOutcome(new EncounterResolution
            {
                Kind = EncounterLogKind.ChaseManeuver,
                ActorId = actorId,
                Title = $"Манёвр невозможен: {target.Name} крупнее {actor.Name} на 3 Комплекции и больше (стр. 136)",
            }, null, SuccessLevel.Failure, false);
        }

        var (bonus, penalty) = ActionDice(state, actorId, ChaseActionKind.Maneuver, check.BonusDice, check.PenaltyDice, targetId);
        var attack = Test(check with { BonusDice = bonus, PenaltyDice = penalty }, dice);
        List<string> lines = [attack.Line];
        if (buildPenalty > 0)
            lines.Add($"Цель крупнее: {ChaseText.PenaltyDice(buildPenalty)}.");
        var hit = Defend(attack, target, defence, dice, lines);

        List<EncounterEffect> effects =
        [
            Effect(EncounterEffectKind.ChaseActionsSpent, actorId, 1),
            Effect(EncounterEffectKind.ChaseAttack, actorId, 1),
        ];
        if (hit)
            effects.AddRange(Mishap(state, targetId, effect ?? new ChaseMishap(), Difficulty.Regular, dice, lines));

        return Done(new EncounterResolution
        {
            Kind = EncounterLogKind.ChaseManeuver,
            ActorId = actorId,
            Title = $"{actor.Name}: манёвр против {target.Name} — {(hit ? "удался" : "не удался")}",
            Lines = lines,
            Effects = effects,
        }, attack with { Success = hit });
    }

    /// <summary>
    /// Таран (стр. 136): Вождение против Вождения (уклонения) цели; транспорт — оружие на 1d10 за пункт своей Комплекции.
    /// Цели в транспорте — каждые полные 10 урона минус 1 Комплекции, остаток не учитывается; пешему — урон людям.
    /// Отдача — половина урона, но не больше Комплекции, что была у цели.
    /// </summary>
    public static ChaseOutcome Ram(EncounterState state, Guid actorId, Guid targetId, ChaseCheck check, ChaseDefence? defence,
        int? damageRoll, IDiceRoller dice)
    {
        var (actor, runner, _) = Context(state, actorId);
        var target = Target(state, targetId);
        var vehicle = runner.Vehicle ?? throw new InvalidOperationException("Таранить можно только за рулём.");
        var (bonus, penalty) = ActionDice(state, actorId, ChaseActionKind.Ram, check.BonusDice, check.PenaltyDice);
        var attack = Test(check with { BonusDice = bonus, PenaltyDice = penalty }, dice);
        List<string> lines = [attack.Line];
        var hit = Defend(attack, target, defence, dice, lines);

        List<EncounterEffect> effects =
        [
            Effect(EncounterEffectKind.ChaseActionsSpent, actorId, 1),
            Effect(EncounterEffectKind.ChaseAttack, actorId, 1),
        ];
        if (hit)
        {
            var count = VehicleRules.DamageDice(vehicle.BuildLeft);
            var damage = damageRoll ?? dice.Roll(count, 10);
            var targetVehicle = ChaseRules.VehicleOf(state, targetId);
            var targetBuild = targetVehicle?.BuildLeft ?? Math.Max(0, target.Stats.Build);
            lines.Add($"Урон {ChaseText.N(damage)}" + (damageRoll is null ? $" ({ChaseText.N(count)}d10)." : " (вписано)."));
            if (targetVehicle is not null)
            {
                var loss = VehicleRules.BuildLoss(damage);
                var driver = state.Chase!.Runner(targetId)?.CarrierId ?? targetId;
                effects.Add(Effect(EncounterEffectKind.VehicleBuild, driver, loss, detail: $"{ChaseText.N(damage)} урона: полные десятки"));
            }
            else
            {
                effects.AddRange(Wound(state, target, new ChaseHarm(Roll: damage), target.Stats.Armor, dice, lines));
            }

            var recoil = VehicleRules.RecoilBuildLoss(damage, targetBuild);
            lines.Add($"Отдача {ChaseText.N(damage / 2)}: своя Комплекция −{ChaseText.N(recoil)} (не больше Комплекции цели, {ChaseText.Build(targetBuild)}).");
            if (recoil > 0)
                effects.Add(Effect(EncounterEffectKind.VehicleBuild, actorId, recoil, detail: "отдача"));
        }

        return Done(new EncounterResolution
        {
            Kind = EncounterLogKind.Collision,
            ActorId = actorId,
            Title = $"{actor.Name} таранит {target.Name} — {(hit ? "удар" : "мимо")}",
            Lines = lines,
            Effects = effects,
        }, attack with { Success = hit });
    }

    /// <summary>
    /// Стрельба по шинам (стр. 139): маленькая цель — штрафная кость (и ещё одна на ходу, как любая стрельба на ходу);
    /// броня шины 3, берёт только проникающее оружие; 2 урона после брони — шина лопнула, Комплекция −1.
    /// </summary>
    public static ChaseOutcome Tyres(EncounterState state, Guid actorId, Guid targetId, ChaseCheck check, bool stopped, int? damageRoll,
        IDiceRoller dice)
    {
        var (actor, _, _) = Context(state, actorId);
        var target = Target(state, targetId);
        var (bonus, penalty) = ActionDice(state, actorId, ChaseActionKind.Tyres, check.BonusDice, check.PenaltyDice, stopped: stopped);
        var attack = Test(check with { BonusDice = bonus, PenaltyDice = penalty }, dice);
        List<string> lines = [attack.Line, "Шина — маленькая цель: штрафная кость." + (stopped ? " Остановился: одно действие." : " На ходу: ещё одна штрафная.")];

        List<EncounterEffect> effects = [Effect(EncounterEffectKind.ChaseAttack, actorId, 1)];
        if (stopped)
            effects.Insert(0, Effect(EncounterEffectKind.ChaseActionsSpent, actorId, 1));
        if (attack.Success)
        {
            var raw = damageRoll ?? dice.Roll(1, 10);
            var afterArmour = Math.Max(0, raw - TyreArmour);
            var burst = afterArmour >= 2;
            lines.Add($"Урон {ChaseText.N(raw)} − броня шины {ChaseText.N(TyreArmour)} = {ChaseText.N(afterArmour)}: " +
                      (burst ? "шина лопнула." : "шина выдержала (нужно 2 после брони; берёт только проникающее оружие)."));
            if (burst)
                effects.Add(Effect(EncounterEffectKind.VehicleBuild, targetId, 1, detail: "лопнула шина"));
        }

        return Done(new EncounterResolution
        {
            Kind = EncounterLogKind.TyreShot,
            ActorId = actorId,
            Title = $"{actor.Name} стреляет по шинам {target.Name} — {(attack.Success ? "попадание" : "промах")}",
            Lines = lines,
            Effects = effects,
        }, attack);
    }

    /// <summary>
    /// Водитель с серьёзной раной (стр. 139): проверка, как при трудной помехе; без сознания — управление потеряно сразу.
    /// Потерял — авария (по умолчанию средняя, как у трудной помехи): Комплекция и урон каждому внутри по той же кости,
    /// 1d3 потерянных действия (стр. 144).
    /// </summary>
    public static ChaseOutcome DriverControl(EncounterState state, Guid driverId, ChaseCheck check, bool unconscious, ChaseMishap? crash,
        IDiceRoller dice)
    {
        var (actor, _, _) = Context(state, driverId);
        List<string> lines = [];
        CheckTest test;
        if (unconscious)
        {
            test = new CheckTest(null, SuccessLevel.Failure, false, "Без сознания — управление потеряно автоматически.");
        }
        else
        {
            var (bonus, penalty) = ActionDice(state, driverId, ChaseActionKind.DriverControl, check.BonusDice, check.PenaltyDice);
            test = Test(check with { Skill = $"Управление ({check.Skill})", Difficulty = Difficulty.Hard, BonusDice = bonus, PenaltyDice = penalty }, dice);
        }

        lines.Add(test.Line);
        List<EncounterEffect> effects = [];
        if (!test.Success)
        {
            var mishap = crash ?? new ChaseMishap();
            effects.AddRange(Mishap(state, driverId, mishap with { Crash = mishap.Crash ?? VehicleReference.DefaultCrash(Difficulty.Hard) },
                Difficulty.Hard, dice, lines));
        }

        return Done(new EncounterResolution
        {
            Kind = EncounterLogKind.DriverControl,
            ActorId = driverId,
            Title = test.Success ? $"{actor.Name} удерживает управление" : $"{actor.Name} теряет управление",
            Lines = lines,
            Effects = effects,
        }, test);
    }

    // ───────────────────── Часть 5: необязательные правила ─────────────────────

    /// <summary>
    /// Штрафные кости разгона (стр. 138): 2–3 локации за действие — одна, 4–5 — две; помощь штурмана снимает одну (стр. 139).
    /// </summary>
    public static int BoostPenaltyDice(int locations, bool navigatorAssist)
    {
        var penalty = locations switch
        {
            >= 4 => 2,
            >= 2 => 1,
            _ => 0,
        };
        return Math.Max(0, penalty - (navigatorAssist ? 1 : 0));
    }

    /// <summary>
    /// «Педаль в пол» (стр. 137–139): за одно действие транспорт проезжает 2–5 локаций, объявив это заранее. Свободные локации
    /// проезжает сразу; перед помехой встаёт — её проходят с штрафными костями разгона и без нового действия, успех везёт
    /// дальше; перед преградой разгон кончается. Помощь штурмана расходуется.
    /// </summary>
    public static ChaseOutcome FloorIt(EncounterState state, Guid actorId, int locations)
    {
        var (actor, runner, chase) = Context(state, actorId);
        locations = Math.Clamp(locations, 2, 5);
        var penalty = BoostPenaltyDice(locations, runner.NavigatorAssist);

        var from = runner.Location;
        var to = from;
        string? stop = null;
        var stoppedByBarrier = false;
        while (to - from < locations && to < chase.LastLocation)
        {
            var next = chase.Location(to + 1);
            if (next?.Barrier is not null)
            {
                stop = $"впереди преграда «{next.Barrier.Name}» — разгон кончается перед ней";
                stoppedByBarrier = true;
                break;
            }

            if (next?.Hazard is not null)
            {
                stop = $"впереди помеха «{next.Hazard.Name}» — пройти её с {ChaseText.PenaltyDice(penalty)}, без нового действия";
                break;
            }

            to++;
        }

        List<EncounterEffect> effects =
        [
            Effect(EncounterEffectKind.ChaseActionsSpent, actorId, 1),
            Effect(EncounterEffectKind.ChaseBoost, actorId, locations),
        ];
        if (to > from)
            effects.Add(Effect(EncounterEffectKind.ChaseMove, actorId, to, flag: true));
        if (stoppedByBarrier || (to == chase.LastLocation && to - from < locations))
            effects.Add(Effect(EncounterEffectKind.ChaseBoost, actorId, 0, detail: stoppedByBarrier ? "преграда" : "конец трассы"));

        List<string> lines = [$"Объявлено: {ChaseText.Locations(locations)} за одно действие ({(penalty > 0 ? ChaseText.PenaltyDice(penalty) : "без штрафа")} у помех на пути)."];
        if (runner.NavigatorAssist)
            lines.Add("Штурман снял одну штрафную кость.");
        if (stop is not null)
            lines.Add(stop + ".");

        return Done(new EncounterResolution
        {
            Kind = EncounterLogKind.FloorIt,
            ActorId = actorId,
            Title = $"{actor.Name} жмёт на газ: {ChaseText.N(from)} → {ChaseText.N(to)}",
            Lines = lines,
            Effects = effects,
        }, true);
    }

    /// <summary>Штурман (стр. 139): пассажир своим действием проходит Внимание или Ориентирование; успех — разгон водителя на одну штрафную меньше.</summary>
    public static ChaseOutcome Navigate(EncounterState state, Guid passengerId, ChaseCheck check, IDiceRoller dice)
    {
        var (actor, runner, chase) = Context(state, passengerId);
        var driverId = runner.CarrierId ?? throw new InvalidOperationException("Штурман — пассажир водителя.");
        var driver = Target(state, driverId);
        var test = Test(check, dice);
        return Done(new EncounterResolution
        {
            Kind = EncounterLogKind.Navigate,
            ActorId = passengerId,
            Title = $"{actor.Name} помогает {driver.Name} с дорогой — " + (test.Success ? "следующий разгон легче" : "совет не пригодился"),
            Lines = [test.Line],
            Effects = test.Success ? [Effect(EncounterEffectKind.NavigatorAssist, driverId, 0, flag: true)] : [],
        }, test);
    }

    /// <summary>
    /// Спрятаться (стр. 139): Скрытность; транспорт — совместная проверка Скрытности и Вождения одним броском, который должен
    /// пройти оба навыка (стр. 90; в v1 Вождение сравнивалось мимо общей функции, F-P08). Удалось — сбежал (преследователь
    /// мог бы найти Вниманием — решает Хранитель). Одно действие.
    /// </summary>
    public static ChaseOutcome Hide(EncounterState state, Guid actorId, ChaseCheck stealth, int? driving, IDiceRoller dice)
    {
        var (actor, _, chase) = Context(state, actorId);
        var (bonus, penalty) = ActionDice(state, actorId, ChaseActionKind.Hide, stealth.BonusDice, stealth.PenaltyDice, inVehicle: driving is not null);
        var test = Test(stealth with { BonusDice = bonus, PenaltyDice = penalty }, dice);
        List<string> lines = [test.Line];
        var hidden = test.Success;
        if (driving is { } drivingValue && test.Roll is { } roll)
        {
            var level = Check.Evaluate(roll.Result, drivingValue, stealth.Difficulty);
            var drivingOk = Check.Passes(level, stealth.Difficulty);
            lines.Add($"Вождение {ChaseText.N(drivingValue)}: {RulesText.Of(level)}" + (drivingOk ? "." : " — не хватило."));
            hidden &= drivingOk;
        }

        List<EncounterEffect> effects = [Effect(EncounterEffectKind.ChaseActionsSpent, actorId, 1)];
        if (hidden)
            effects.AddRange(ChaseRules.WithPassengers(chase, actorId, EncounterEffectKind.Escaped));

        var verdict = hidden
            ? "укрылся! Найти его можно только Вниманием"
            : driving is not null && test.Success
                ? "Вождения не хватило: заметили, как свернул в укрытие"
                : "не вышло, укрытие не найдено";
        return Done(new EncounterResolution
        {
            Kind = EncounterLogKind.Hide,
            ActorId = actorId,
            Title = $"{actor.Name} прячется ({(driving is null ? "Скрытность" : "Скрытность + Вождение")}) — {verdict}",
            Lines = lines,
            Effects = effects,
        }, test with { Success = hidden });
    }

    /// <summary>Искать след (стр. 139): провал — погоня для преследователя окончена. Одно действие.</summary>
    public static ChaseOutcome Track(EncounterState state, Guid pursuerId, ChaseCheck check, IDiceRoller dice)
    {
        var (actor, _, chase) = Context(state, pursuerId);
        var test = Test(check, dice);
        List<EncounterEffect> effects = [Effect(EncounterEffectKind.ChaseActionsSpent, pursuerId, 1)];
        if (!test.Success)
            effects.AddRange(ChaseRules.WithPassengers(chase, pursuerId, EncounterEffectKind.LostTrail));

        return Done(new EncounterResolution
        {
            Kind = EncounterLogKind.Tracking,
            ActorId = pursuerId,
            Title = $"{actor.Name} ищет след ({check.Skill}) — " + (test.Success ? "нашёл, погоня продолжается" : "потерял, погоня для него окончена"),
            Lines = [test.Line],
            Effects = effects,
        }, test);
    }

    /// <summary>
    /// Персонаж создаёт помеху или преграду (стр. 141): запереть дверь, придвинуть шкаф, опрокинуть прилавок. Цена в
    /// действиях и нужна ли проверка — решает Хранитель; без навыка — удаётся сама.
    /// </summary>
    public static ChaseOutcome CreateObstacle(EncounterState state, Guid actorId, int location, ChaseLocation obstacle, string description,
        int actionCost, ChaseCheck? check, IDiceRoller dice)
    {
        var (actor, _, _) = Context(state, actorId);
        var test = check is { Value: > 0 } ? Test(check, dice) : new CheckTest(null, SuccessLevel.Regular, true, "Проверка не нужна.");
        List<EncounterEffect> effects = [];
        if (actionCost > 0)
            effects.Add(Effect(EncounterEffectKind.ChaseActionsSpent, actorId, actionCost));
        if (test.Success)
        {
            effects.Add(new EncounterEffect
            {
                Kind = EncounterEffectKind.PlaceObstacle,
                ParticipantId = actorId,
                Location = location,
                Obstacle = obstacle with { Number = location },
            });
        }

        var what = string.IsNullOrWhiteSpace(description) ? "перекрывает путь" : description.Trim();
        return Done(new EncounterResolution
        {
            Kind = EncounterLogKind.CreateObstacle,
            ActorId = actorId,
            Title = $"{actor.Name}: {what} — " + (test.Success ? $"локация {ChaseText.N(location)}" : "не вышло"),
            Lines = [test.Line],
            Effects = effects,
        }, test);
    }

    /// <summary>
    /// Случайные помехи (стр. 137): 1d100 — 01–59 чисто, от 60 обычная, от 85 трудная, от 96 чрезвычайная. Тяжёлая дорога —
    /// штрафная кость, пустая автострада — бонусная (вписанный бросок их не требует). Найденная помеха предлагается в
    /// локацию (Хранитель может переименовать или сделать её преградой).
    /// </summary>
    public static ChaseOutcome RandomHazard(EncounterState state, int location, int? roll, int bonusDice, int penaltyDice, IDiceRoller dice)
    {
        var d100 = D100.RollOrEntered(dice, roll, bonusDice, penaltyDice);
        Difficulty? difficulty = d100.Result switch
        {
            >= 96 => Difficulty.Extreme,
            >= 85 => Difficulty.Hard,
            >= 60 => Difficulty.Regular,
            _ => null,
        };
        var text = difficulty is { } d ? $"{ChaseText.Of(d)} помеха" : "чисто, помех нет";
        var actor = state.Participants.FirstOrDefault();
        List<EncounterEffect> effects = [];
        if (difficulty is { } found && actor is not null)
        {
            effects.Add(new EncounterEffect
            {
                Kind = EncounterEffectKind.PlaceObstacle,
                ParticipantId = actor.Id,
                Location = location,
                Obstacle = new ChaseLocation { Number = location, Hazard = new ChaseHazard { Name = "Случайная помеха", Difficulty = found } },
            });
        }

        return new ChaseOutcome(new EncounterResolution
        {
            Kind = EncounterLogKind.RandomHazard,
            Title = $"Локация {ChaseText.N(location)}: бросок на случайные помехи {ChaseText.N(d100.Result)} — {text}",
            Lines = d100.HasExtraDice ? [$"Кости{RulesText.RollDetail(d100)}"] : [],
            Effects = effects,
        }, d100, SuccessLevel.Regular, difficulty is null);
    }

    /// <summary>
    /// Внезапная помеха (стр. 137): общая проверка Удачи — успех: где и когда, решают игроки; провал — Хранитель, и помеха
    /// угрожает сыщикам. Сложность внезапной помехи — обычная. Очередь сторон — <see cref="ChaseRules.SuddenHazardRejection"/>.
    /// </summary>
    public static ChaseOutcome SuddenHazard(int luck, int? roll, bool declaredByPlayers, IDiceRoller dice)
    {
        var d100 = D100.RollOrEntered(dice, roll);
        var level = Check.Evaluate(d100.Result, luck);
        var playersWin = level.IsSuccess();
        return new ChaseOutcome(new EncounterResolution
        {
            Kind = EncounterLogKind.SuddenHazard,
            Title = $"Внезапная помеха (назначили {(declaredByPlayers ? "игроки" : "Хранитель")}): " +
                    (playersWin ? "где и когда — решают игроки" : "помеху придумывает Хранитель, она угрожает сыщикам"),
            Lines = [$"Общая проверка Удачи {ChaseText.N(luck)}: выпало {ChaseText.N(d100.Result)} ({RulesText.Of(level)}). Сложность внезапной помехи — обычная."],
        }, d100, level, playersWin);
    }

    // ───────────────────── Общее ─────────────────────

    /// <summary>
    /// Кости проверки действия — одна копия для резолва и для поля броска на экране (чтобы «Бросить» на экране бросал с теми
    /// же костями, что и правило): бонусные за осторожность у помехи (не больше двух, стр. 133), штрафные разгона (стр. 138),
    /// поломки транспорта (стр. 143), стрельбы на ходу и маленькой цели (стр. 139), разницы Комплекции в манёвре (стр. 136).
    /// </summary>
    public static (int Bonus, int Penalty) ActionDice(EncounterState state, Guid actorId, ChaseActionKind action, int bonusDice = 0,
        int penaltyDice = 0, Guid? targetId = null, bool stopped = true, bool inVehicle = false)
    {
        var runner = state.Chase?.Runner(actorId);
        var driving = runner is null ? 0 : DrivingPenalty(runner);
        bonusDice = Math.Max(0, bonusDice);
        penaltyDice = Math.Max(0, penaltyDice);
        return action switch
        {
            ChaseActionKind.Hazard => (Math.Min(2, bonusDice), penaltyDice + (runner?.Boost?.PenaltyDice ?? 0) + driving),
            ChaseActionKind.Barrier or ChaseActionKind.Ram or ChaseActionKind.DriverControl => (bonusDice, penaltyDice + driving),
            ChaseActionKind.Maneuver => (bonusDice, penaltyDice + driving + (targetId is { } target
                ? ManeuverBuildPenalty(ChaseRules.Build(state, actorId), ChaseRules.Build(state, target)).PenaltyDice
                : 0)),
            ChaseActionKind.Ranged => (bonusDice, penaltyDice + (stopped ? 0 : 1)),
            ChaseActionKind.Tyres => (bonusDice, penaltyDice + 1 + (stopped ? 0 : 1)),
            ChaseActionKind.Hide => (bonusDice, penaltyDice + (inVehicle ? driving : 0)),
            _ => (bonusDice, penaltyDice),
        };
    }

    /// <summary>Штрафная кость водителю сломанного транспорта (стр. 143) — к проверкам управления.</summary>
    public static int DrivingPenalty(ChaseRunner runner) => runner.IsDriver ? VehicleRules.PenaltyDice(runner.Vehicle) : 0;

    /// <summary>Бросок и уровень проверки. Объявленные кости идут и в автобросок.</summary>
    internal sealed record CheckTest(D100Roll? Roll, SuccessLevel Level, bool Success, string Line);

    private static CheckTest Test(ChaseCheck check, IDiceRoller dice)
    {
        var roll = D100.RollOrEntered(dice, check.Roll, check.BonusDice, check.PenaltyDice);
        var level = Check.Evaluate(roll.Result, check.Value, check.Difficulty);
        var passes = Check.Passes(level, check.Difficulty);
        var dicesText = DiceText(check.BonusDice, check.PenaltyDice);
        var line = $"{check.Skill} {ChaseText.N(check.Value)}" +
                   (check.Difficulty != Difficulty.Regular ? $", {ChaseText.Of(check.Difficulty)} (нужно ≤ {ChaseText.N(Check.Target(check.Value, check.Difficulty))})" : "") +
                   (dicesText is null ? "" : $", {dicesText}") +
                   $": выпало {ChaseText.N(roll.Result)}{RulesText.RollDetail(roll)} — {RulesText.Of(level)}" +
                   (level.IsSuccess() && !passes ? $", нужен {RulesText.Of(Check.Required(check.Difficulty))}" : "") + ".";
        return new CheckTest(roll, level, passes, line);
    }

    private static string? DiceText(int bonus, int penalty)
    {
        var net = Math.Max(0, bonus) - Math.Max(0, penalty);
        return net switch
        {
            > 0 => RulesText.DescribeDice(net, isBonus: true),
            < 0 => RulesText.DescribeDice(-net, isBonus: false),
            _ => null,
        };
    }

    /// <summary>Ответ цели. Возвращает, достигла ли атака цели.</summary>
    private static bool Defend(CheckTest attack, EncounterParticipant target, ChaseDefence? defence, IDiceRoller dice, List<string> lines)
    {
        if (defence is not { Value: > 0 })
            return attack.Success;

        var answer = Test(new ChaseCheck(defence.Skill, defence.Value, defence.Roll), dice);
        var wins = Opposed.AttackerWins(attack.Level, answer.Level, defence.Kind);
        lines.Add($"{target.Name} — {(defence.Kind == DefenceKind.Dodge ? "уклонение" : "контратака")}: {answer.Line}");
        if (Opposed.DefenderStrikes(attack.Level, answer.Level, defence.Kind))
            lines.Add($"{target.Name} перехватывает удар и бьёт сам — урон атакующему: эффект Хранителя.");
        else if (!wins && attack.Success)
            lines.Add("Атака отбита.");
        return wins;
    }

    /// <summary>Урон человеку: число (вписанное или по формуле) минус броня; без сознания — выбывает из погони.</summary>
    private static IEnumerable<EncounterEffect> Wound(EncounterState state, EncounterParticipant target, ChaseHarm? harm, int armour,
        IDiceRoller dice, List<string> lines)
    {
        if (harm is null || (harm.Roll is null && string.IsNullOrWhiteSpace(harm.Formula)))
        {
            lines.Add("Урон не задан — впишите его эффектом Хранителя.");
            return [];
        }

        var (raw, detail) = Amount(harm, dice);
        var damage = Math.Max(0, raw - Math.Max(0, armour));
        lines.Add($"Урон {target.Name}: {detail}" + (armour > 0 ? $" − броня {ChaseText.N(armour)} = {ChaseText.N(damage)}" : "") + ".");
        return Hurt(target, damage, detail);
    }

    private static IEnumerable<EncounterEffect> Hurt(EncounterParticipant target, int damage, string detail)
    {
        if (damage <= 0)
            yield break;

        yield return new EncounterEffect { Kind = EncounterEffectKind.Damage, ParticipantId = target.Id, Amount = damage, Detail = detail };
        if (target.HitPoints - damage <= 0)
            yield return new EncounterEffect { Kind = EncounterEffectKind.Out, ParticipantId = target.Id, Flag = true, Detail = "0 ПЗ — выбывает из погони" };
    }

    /// <summary>
    /// Провал помехи (или удачный манёвр против цели): транспорт — авария по таблице VI (Комплекция и урон каждому внутри
    /// той же костью, стр. 144), пеший — урон людям; потеря 1d3 действий. Невписанное бросают кости.
    /// </summary>
    private static List<EncounterEffect> Mishap(EncounterState state, Guid victimId, ChaseMishap mishap, Difficulty difficulty,
        IDiceRoller dice, List<string> lines)
    {
        var chase = state.Chase!;
        var victim = Target(state, victimId);
        var runner = chase.Runner(victimId);
        var vehicle = ChaseRules.VehicleOf(state, victimId);
        List<EncounterEffect> effects = [];

        if (vehicle is not null && mishap.Crash is { } crash)
        {
            var driverId = runner?.CarrierId ?? victimId;
            var loss = Math.Max(0, mishap.CrashRoll ?? DiceFormula.Roll(crash.BuildLoss, dice));
            lines.Add($"{crash.Name}: Комплекция −{ChaseText.N(loss)} ({(mishap.CrashRoll is null ? crash.BuildLoss : "вписано")}); урон каждому внутри — та же кость.");
            effects.Add(Effect(EncounterEffectKind.VehicleBuild, driverId, loss, detail: crash.Name));
            foreach (var occupantId in Occupants(chase, driverId))
            {
                if (state.Find(occupantId) is not { } occupant)
                    continue;

                var (damage, detail) = mishap.Harm is { Roll: { } entered } ? (entered, "вписано") : Amount(new ChaseHarm(crash.BuildLoss), dice);
                lines.Add($"{occupant.Name}: урон {detail}.");
                effects.AddRange(Hurt(occupant, Math.Max(0, damage), $"{crash.Name}: {detail}"));
            }
        }
        else if (vehicle is null && mishap.Harm is { } harm && (harm.Roll is not null || !string.IsNullOrWhiteSpace(harm.Formula)))
        {
            var (damage, detail) = Amount(harm, dice);
            lines.Add($"Урон {victim.Name}: {detail}.");
            effects.AddRange(Hurt(victim, Math.Max(0, damage), detail));
        }

        if (mishap.LoseActions && runner is { IsPassenger: false })
        {
            var lost = Math.Max(0, mishap.LostActionsRoll ?? dice.Roll(1, 3));
            lines.Add($"{victim.Name} теряет {ChaseText.Actions(lost)}" + (mishap.LostActionsRoll is null ? " (1d3)." : "."));
            effects.Add(Effect(EncounterEffectKind.ChaseActionsLost, victimId, lost));
        }

        return effects;
    }

    /// <summary>Водитель и его пассажиры.</summary>
    private static IEnumerable<Guid> Occupants(ChaseState chase, Guid driverId)
    {
        yield return driverId;
        foreach (var passenger in chase.Runners.Where(r => r.CarrierId == driverId))
            yield return passenger.ParticipantId;
    }

    private static (int Value, string Detail) Amount(ChaseHarm harm, IDiceRoller dice)
    {
        if (harm.Roll is { } entered)
            return (Math.Max(0, entered), $"{ChaseText.N(entered)} (вписано)");

        var formula = DiceFormula.Parse(harm.Formula);
        var value = Math.Max(0, formula.Roll(dice));
        return (value, $"{formula.Text} → {ChaseText.N(value)}");
    }

    private static (EncounterParticipant Actor, ChaseRunner Runner, ChaseState Chase) Context(EncounterState state, Guid actorId)
    {
        var chase = state.Chase ?? throw new InvalidOperationException("В сцене нет погони.");
        var actor = state.Find(actorId) ?? throw new ArgumentException("Нет такого участника.", nameof(actorId));
        var runner = chase.Runner(actorId) ?? throw new ArgumentException("Участник не в погоне.", nameof(actorId));
        return (actor, runner, chase);
    }

    private static EncounterParticipant Target(EncounterState state, Guid targetId) =>
        state.Find(targetId) ?? throw new ArgumentException("Нет такой цели.", nameof(targetId));

    private static EncounterEffect Effect(EncounterEffectKind kind, Guid participantId, int amount, bool flag = false, string? detail = null) =>
        new() { Kind = kind, ParticipantId = participantId, Amount = amount, Flag = flag, Detail = detail };

    private static ChaseOutcome Done(EncounterResolution resolution, bool success) =>
        new(resolution, null, success ? SuccessLevel.Regular : SuccessLevel.Failure, success);

    private static ChaseOutcome Done(EncounterResolution resolution, CheckTest test) =>
        new(resolution, test.Roll, test.Level, test.Success);
}
