namespace CampaignManager.Core.Encounters.Chase;

/// <summary>
/// Эффекты погони — ветка <see cref="EncounterEngine"/>: единственное место, где они меняют погоню (предпросмотр зовёт то
/// же самое на копии сцены). Чего здесь нет: урон людям — общий <see cref="EncounterEffectKind.Damage"/> ядра (рана и
/// запись в лист; в v1 погоня считала только «ПЗ после», без раны и без листа).
/// </summary>
public static class ChaseEffects
{
    /// <summary>Эффект погони, который меняет бегущего (локация, действия, транспорт, статус).</summary>
    public static bool Handles(EncounterEffectKind kind) => kind is
        EncounterEffectKind.ChaseMove or EncounterEffectKind.ChaseActionsSpent or EncounterEffectKind.ChaseActionsLost
        or EncounterEffectKind.ChaseSpeed or EncounterEffectKind.VehicleBuild or EncounterEffectKind.ChaseBoost
        or EncounterEffectKind.NavigatorAssist or EncounterEffectKind.ChaseAttack or EncounterEffectKind.Escaped
        or EncounterEffectKind.Caught or EncounterEffectKind.LostTrail or EncounterEffectKind.TooSlow;

    /// <summary>Эффект трассы, а не участника: урон преграде, новая помеха.</summary>
    public static bool IsTrackEffect(EncounterEffectKind kind) =>
        kind is EncounterEffectKind.BarrierDamage or EncounterEffectKind.PlaceObstacle;

    internal static EffectPreview DescribeTrack(EncounterState state, EncounterEffect effect)
    {
        var chase = state.Chase!;
        var number = effect.Location ?? 0;
        var name = $"Локация {ChaseText.N(number)}";
        if (chase.Location(number) is not { } location)
            return new EffectPreview(effect.ParticipantId, name, EncounterText.Of(effect.Kind), "—", "—", "нет такой локации");

        switch (effect.Kind)
        {
            case EncounterEffectKind.BarrierDamage when location.Barrier is { } barrier:
            {
                var before = barrier.HitPointsLeft;
                barrier.HitPointsLeft = Math.Max(0, before - Math.Max(0, effect.Amount));
                string? note = null;
                if (barrier.HitPointsLeft <= 0)
                {
                    // Разрушенная преграда — уже не преграда; обломки только «могут стать» помехой (стр. 136) — решил
                    // Хранитель в предпросмотре (ChaseRules.SetDebris), по умолчанию проход свободен.
                    location.Barrier = null;
                    if (effect.Obstacle?.Hazard is { } debris)
                    {
                        location.Hazard = debris with { };
                        note = $"разрушена — обломки стали помехой ({ChaseText.Of(debris.Difficulty)})";
                    }
                    else
                    {
                        note = "разрушена — проход свободен";
                    }
                }

                return new EffectPreview(effect.ParticipantId, name, $"{barrier.Name}: ПЗ", ChaseText.N(before), ChaseText.N(barrier.HitPointsLeft),
                    Join(effect.Detail, note));
            }
            case EncounterEffectKind.BarrierDamage:
                return new EffectPreview(effect.ParticipantId, name, "Преграда", "—", "—", "преграды уже нет");
            case EncounterEffectKind.PlaceObstacle when effect.Obstacle is { } obstacle:
            {
                if (obstacle.Barrier is { } barrier)
                {
                    location.Barrier = barrier with { HitPointsLeft = barrier.HitPoints };
                    return new EffectPreview(effect.ParticipantId, name, "Преграда", "—",
                        $"{barrier.Name} ({ChaseText.Of(barrier.Difficulty)})", effect.Detail);
                }

                if (obstacle.Hazard is { } hazard)
                {
                    location.Hazard = hazard with { };
                    return new EffectPreview(effect.ParticipantId, name, "Помеха", "—",
                        $"{hazard.Name} ({ChaseText.Of(hazard.Difficulty)})", effect.Detail);
                }

                return new EffectPreview(effect.ParticipantId, name, "Препятствие", "—", "—", effect.Detail);
            }
            default:
                return new EffectPreview(effect.ParticipantId, name, EncounterText.Of(effect.Kind), "", "", effect.Detail);
        }
    }

    internal static EffectPreview Describe(EncounterState state, EncounterParticipant p, EncounterEffect effect)
    {
        var chase = state.Chase!;
        if (chase.Runner(p.Id) is not { } runner)
            return new EffectPreview(p.Id, p.Name, EncounterText.Of(effect.Kind), "—", "—", "не в погоне");

        var detail = string.IsNullOrWhiteSpace(effect.Detail) ? null : effect.Detail;
        switch (effect.Kind)
        {
            case EncounterEffectKind.ChaseMove:
            {
                var before = runner.Location;
                runner.Location = Math.Clamp(effect.Amount, 1, Math.Max(1, chase.LastLocation));
                foreach (var passenger in chase.Runners.Where(r => r.CarrierId == p.Id))
                    passenger.Location = runner.Location;

                string? boost = null;
                if (effect.Flag && runner.Boost is { } declared)
                {
                    declared.LocationsLeft -= Math.Max(0, runner.Location - before);
                    if (declared.LocationsLeft <= 0)
                        runner.Boost = null;
                    else
                        boost = $"разгон: ещё {ChaseText.Locations(declared.LocationsLeft)}";
                }

                return new EffectPreview(p.Id, p.Name, "Локация", ChaseText.N(before), ChaseText.N(runner.Location), Join(detail, boost));
            }
            case EncounterEffectKind.ChaseActionsSpent:
            {
                var before = runner.ActionsLeft;
                runner.ActionsLeft = Math.Max(0, before - Math.Max(0, effect.Amount));
                return new EffectPreview(p.Id, p.Name, $"Тратит {ChaseText.Actions(effect.Amount)}", ChaseText.N(before),
                    ChaseText.N(runner.ActionsLeft), detail);
            }
            case EncounterEffectKind.ChaseActionsLost:
            {
                // Потерянные сверх оставшихся — долг на следующий раунд (стр. 133).
                var before = runner.ActionsLeft;
                var lost = Math.Max(0, effect.Amount);
                var debt = Math.Max(0, lost - before);
                runner.ActionsLeft = Math.Max(0, before - lost);
                runner.Debt += debt;
                return new EffectPreview(p.Id, p.Name, $"Теряет {ChaseText.Actions(lost)}", ChaseText.N(before), ChaseText.N(runner.ActionsLeft),
                    Join(detail, debt > 0 ? $"в следующем раунде на {ChaseText.Actions(debt)} меньше" : null));
            }
            case EncounterEffectKind.ChaseSpeed:
            {
                var before = ChaseRules.Move(p, runner);
                runner.SpeedModifier = Math.Clamp(effect.Amount, -1, 1);
                runner.SpeedChecked = true;
                var after = ChaseRules.Move(p, runner);
                ChaseRules.Recalculate(state);
                return new EffectPreview(p.Id, p.Name, "СКО", ChaseText.N(before), ChaseText.N(after), detail);
            }
            case EncounterEffectKind.VehicleBuild:
            {
                if (ChaseRules.VehicleOf(state, p.Id) is not { } vehicle)
                    return new EffectPreview(p.Id, p.Name, "Комплекция транспорта", "—", "—", "не в транспорте");

                var before = vehicle.BuildLeft;
                var loss = Math.Max(0, effect.Amount);
                vehicle.BuildLeft = Math.Max(0, before - loss);
                return new EffectPreview(p.Id, p.Name, $"{vehicle.Name}: Комплекция", ChaseText.Build(before), ChaseText.Build(vehicle.BuildLeft),
                    Join(detail, VehicleRules.Note(vehicle, before, loss)));
            }
            case EncounterEffectKind.ChaseBoost:
            {
                if (effect.Amount <= 0)
                {
                    runner.Boost = null;
                    return new EffectPreview(p.Id, p.Name, "Разгон", "да", "оборвался", detail);
                }

                var penalty = ChaseActions.BoostPenaltyDice(effect.Amount, runner.NavigatorAssist);
                var navigator = runner.NavigatorAssist ? "штурман снял одну штрафную кость" : null;
                runner.NavigatorAssist = false;
                runner.Boost = new ChaseBoost { LocationsLeft = effect.Amount, PenaltyDice = penalty };
                return new EffectPreview(p.Id, p.Name, "Разгон", "нет", ChaseText.Locations(effect.Amount),
                    Join(penalty > 0 ? $"помехи на пути — {ChaseText.PenaltyDice(penalty)}" : "помехи на пути без штрафа", navigator));
            }
            case EncounterEffectKind.NavigatorAssist:
            {
                var before = runner.NavigatorAssist;
                runner.NavigatorAssist = effect.Flag;
                return new EffectPreview(p.Id, p.Name, "Штурман", before ? "помогает" : "нет", effect.Flag ? "помогает" : "нет",
                    Join(detail, effect.Flag ? "следующий разгон — на одну штрафную кость меньше" : null));
            }
            case EncounterEffectKind.ChaseAttack:
            {
                var before = runner.Attacks;
                runner.Attacks += Math.Max(1, effect.Amount);
                return new EffectPreview(p.Id, p.Name, "Атак в раунде", ChaseText.N(before), ChaseText.N(runner.Attacks), detail);
            }
            case EncounterEffectKind.Escaped or EncounterEffectKind.Caught or EncounterEffectKind.LostTrail or EncounterEffectKind.TooSlow:
            {
                var before = ChaseRules.StatusOf(state, p.Id);
                var status = effect.Kind switch
                {
                    EncounterEffectKind.Escaped => ChaseStatus.Escaped,
                    EncounterEffectKind.Caught => ChaseStatus.Caught,
                    EncounterEffectKind.LostTrail => ChaseStatus.LostTrail,
                    _ => ChaseStatus.TooSlow,
                };
                ChaseRules.MarkOut(p, runner, status);
                return new EffectPreview(p.Id, p.Name, "Погоня", ChaseText.Of(before), ChaseText.Of(status), detail);
            }
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
}
