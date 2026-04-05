using CampaignManager.Web.Components.Features.Chase.Model;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Web.Components.Features.Chase.Services;

/// <summary>
/// Сервис управления погонями по правилам Call of Cthulhu 7e (Глава 7).
/// Хранитель вводит все результаты бросков кубиков — сервис не кидает за игроков.
/// </summary>
public sealed class ChaseService
{
    public ChasePhase Phase { get; private set; } = ChasePhase.Setup;
    public List<ChaseParticipant> Participants { get; private set; } = [];
    public List<ChaseLocation> Locations { get; private set; } = [];
    public int CurrentRound { get; private set; } = 1;
    public int CurrentTurnIndex { get; private set; }
    public int MinAdjustedMov { get; private set; }
    public List<ChaseActionResult> ChaseLog { get; private set; } = [];
    public ChaseActionResult? PendingResult { get; private set; }
    public Guid? SelectedCampaignId { get; private set; }

    public event Action? OnChange;

    // ───────────────────── Настройка трассы ─────────────────────

    public void SetupTrack(int locationCount)
    {
        var newLocations = new List<ChaseLocation>();
        for (var i = 1; i <= locationCount; i++)
        {
            newLocations.Add(new ChaseLocation { Number = i });
        }

        Locations = newLocations;
        NotifyStateChanged();
    }

    public void UpdateLocation(int number, ChaseLocation location)
    {
        var index = Locations.FindIndex(l => l.Number == number);
        if (index >= 0)
        {
            Locations[index] = location;
            NotifyStateChanged();
        }
    }

    public ChaseLocation? GetLocation(int number) =>
        Locations.FirstOrDefault(l => l.Number == number);

    // ───────────────────── Управление участниками ─────────────────────

    public void AddParticipant(ChaseParticipant participant)
    {
        Participants.Add(participant);
        NotifyStateChanged();
    }

    public void RemoveParticipant(ChaseParticipant participant)
    {
        Participants.Remove(participant);
        if (CurrentTurnIndex >= Participants.Count && Participants.Count > 0)
            CurrentTurnIndex = 0;
        NotifyStateChanged();
    }

    public void SetParticipantLocation(Guid id, int location)
    {
        var p = Participants.FirstOrDefault(x => x.Id == id);
        if (p is not null)
        {
            p.CurrentLocation = Math.Clamp(location, 1, Locations.Count);
            NotifyStateChanged();
        }
    }

    public void SetParticipantRole(Guid id, ChaseRole role)
    {
        var p = Participants.FirstOrDefault(x => x.Id == id);
        if (p is not null)
        {
            p.Role = role;
            NotifyStateChanged();
        }
    }

    public void SetVehicle(Guid id, string vehicleName, int vehicleSpeed)
    {
        var p = Participants.FirstOrDefault(x => x.Id == id);
        if (p is not null)
        {
            p.IsInVehicle = true;
            p.VehicleName = vehicleName;
            p.VehicleSpeed = vehicleSpeed;
            NotifyStateChanged();
        }
    }

    public void RemoveVehicle(Guid id)
    {
        var p = Participants.FirstOrDefault(x => x.Id == id);
        if (p is not null)
        {
            p.IsInVehicle = false;
            p.VehicleName = null;
            p.VehicleSpeed = 0;
            NotifyStateChanged();
        }
    }

    public void SetCampaign(Guid campaignId)
    {
        SelectedCampaignId = campaignId;
        NotifyStateChanged();
    }

    // ───────────────────── Проверка скорости (Часть 1) ─────────────────────

    public void BeginSpeedChecks()
    {
        if (Participants.Count < 2 || Locations.Count < 3)
            return;

        Phase = ChasePhase.SpeedCheck;
        foreach (var p in Participants)
        {
            p.SpeedCheckCompleted = false;
            p.MovModifier = 0;
        }

        NotifyStateChanged();
    }

    public ChaseActionResult ResolveSpeedCheck(Guid participantId, int? roll)
    {
        var participant = Participants.First(p => p.Id == participantId);

        var skillName = participant.IsInVehicle ? "Вождение" : "ВЫН";
        var skillValue = participant.IsInVehicle
            ? (participant.DrivingSkill > 0 ? participant.DrivingSkill : 20)
            : participant.ConstitutionValue;

        var actualRoll = roll ?? CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(actualRoll, skillValue);

        // Чрезвычайный успех (+1), обычный успех (0), провал (-1)
        participant.MovModifier = level switch
        {
            >= SuccessLevel.ExtremeSuccess => 1,
            >= SuccessLevel.RegularSuccess => 0,
            _ => -1
        };
        participant.SpeedCheckCompleted = true;

        var modText = participant.MovModifier switch
        {
            1 => "+1",
            -1 => "−1",
            _ => "0"
        };

        var result = new ChaseActionResult
        {
            Round = 0,
            ActionType = ChaseActionType.SpeedCheck,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            SkillName = skillName,
            SkillValue = skillValue,
            Roll = actualRoll,
            SuccessLevel = level,
            IsSuccess = level >= SuccessLevel.RegularSuccess,
            Summary = $"{participant.Name}: проверка {skillName} — бросок {actualRoll} против {skillValue} " +
                      $"({CombatService.GetSuccessLevelText(level)}). СКО {participant.AdjustedMov} (модификатор {modText})."
        };

        ChaseLog.Insert(0, result);
        NotifyStateChanged();
        return result;
    }

    public bool AllSpeedChecksCompleted() =>
        Participants.All(p => p.SpeedCheckCompleted);

    public (bool ChaseHappens, string Reason) EvaluateChaseStart()
    {
        var maxPreyMov = Participants
            .Where(p => p.Role == ChaseRole.Prey && p.IsActive)
            .Select(p => p.AdjustedMov)
            .DefaultIfEmpty(0)
            .Max();

        var maxPursuerMov = Participants
            .Where(p => p.Role == ChaseRole.Pursuer && p.IsActive)
            .Select(p => p.AdjustedMov)
            .DefaultIfEmpty(0)
            .Max();

        if (maxPreyMov > maxPursuerMov)
        {
            return (false,
                $"Жертва (СКО {maxPreyMov}) быстрее преследователя (СКО {maxPursuerMov}). " +
                "Жертва убегает — погоня не состоялась!");
        }

        return (true,
            $"Преследователь (СКО {maxPursuerMov}) не уступает жертве (СКО {maxPreyMov}). " +
            "Погоня начинается!");
    }

    public void StartChase()
    {
        // Сортировка по ЛВК (убывание)
        Participants = Participants
            .OrderByDescending(p => p.Dexterity)
            .ToList();

        // Рассчитать минимальный СКО
        MinAdjustedMov = Participants
            .Where(p => p.IsActive)
            .Select(p => p.AdjustedMov)
            .DefaultIfEmpty(1)
            .Min();

        Phase = ChasePhase.Active;
        CurrentRound = 1;
        CurrentTurnIndex = 0;
        CalculateMovementActions();
        NotifyStateChanged();
    }

    // ───────────────────── Управление раундами (Часть 2) ─────────────────────

    public void CalculateMovementActions()
    {
        foreach (var p in Participants.Where(p => p.IsActive))
        {
            var actions = 1 + (p.AdjustedMov - MinAdjustedMov);
            actions = Math.Max(0, actions - p.MovementActionDebt);
            p.MovementActionDebt = Math.Max(0, p.MovementActionDebt - (1 + (p.AdjustedMov - MinAdjustedMov)));
            p.TotalMovementActions = Math.Max(0, actions);
            p.MovementActionsRemaining = p.TotalMovementActions;
            p.HasActedThisRound = false;
        }
    }

    public ChaseParticipant? GetActiveParticipant()
    {
        var active = Participants.Where(p => p.IsActive).ToList();
        if (active.Count == 0) return null;
        if (CurrentTurnIndex >= active.Count) return null;
        return active[CurrentTurnIndex];
    }

    public void NextTurn()
    {
        var current = GetActiveParticipant();
        if (current is not null)
            current.HasActedThisRound = true;

        var active = Participants.Where(p => p.IsActive).ToList();
        if (active.Count == 0) return;

        CurrentTurnIndex++;
        if (CurrentTurnIndex >= active.Count)
        {
            NextRound();
            return;
        }

        NotifyStateChanged();
    }

    public void NextRound()
    {
        CurrentRound++;
        CurrentTurnIndex = 0;
        CalculateMovementActions();
        NotifyStateChanged();
    }

    public void EndTurn(Guid participantId)
    {
        var participant = Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is not null)
        {
            participant.MovementActionsRemaining = 0;
            participant.HasActedThisRound = true;
        }

        NextTurn();
    }

    public void ResetChase()
    {
        Participants.Clear();
        Locations.Clear();
        CurrentRound = 1;
        CurrentTurnIndex = 0;
        MinAdjustedMov = 0;
        ChaseLog.Clear();
        PendingResult = null;
        SelectedCampaignId = null;
        Phase = ChasePhase.Setup;
        NotifyStateChanged();
    }

    // ───────────────────── Перемещение (Часть 3) ─────────────────────

    public ChaseActionResult MoveForward(Guid participantId)
    {
        var participant = Participants.First(p => p.Id == participantId);
        var startLocation = participant.CurrentLocation;
        var newLocation = Math.Min(startLocation + 1, Locations.Count);

        participant.CurrentLocation = newLocation;
        participant.MovementActionsRemaining = Math.Max(0, participant.MovementActionsRemaining - 1);

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.MovementAction,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            IsSuccess = true,
            LocationBefore = startLocation,
            LocationAfter = newLocation,
            Summary = $"{participant.Name} перемещается с локации {startLocation} на {newLocation}."
        };

        ChaseLog.Insert(0, result);
        CheckCaughtAndEscaped();
        NotifyStateChanged();
        return result;
    }

    // ───────────────────── Помехи (Часть 3) ─────────────────────

    /// <summary>
    /// Разрешить помеху. bonusDice (0-2) — каждая стоит 1 действие перемещения.
    /// Провал: урон + потеря действий. Участник ВСЕГДА проходит дальше.
    /// </summary>
    public ChaseActionResult ResolveHazard(Guid participantId, string skillName, int skillValue,
        int? roll, int bonusDice, int? damageRoll, int? lostActionsRoll)
    {
        var participant = Participants.First(p => p.Id == participantId);
        var location = GetLocation(participant.CurrentLocation);

        // Бонусные кости стоят действия перемещения (макс 2)
        bonusDice = Math.Clamp(bonusDice, 0, 2);
        participant.MovementActionsRemaining = Math.Max(0, participant.MovementActionsRemaining - bonusDice);

        var difficulty = location?.HazardDifficulty ?? 1;
        var threshold = GetDifficultyThreshold(skillValue, difficulty);
        var actualRoll = roll ?? CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(actualRoll, threshold);
        var success = level >= SuccessLevel.RegularSuccess;

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.HazardCheck,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            SkillName = $"{location?.HazardName ?? "Помеха"} ({skillName})",
            SkillValue = threshold,
            Roll = actualRoll,
            SuccessLevel = level,
            IsSuccess = success,
            BonusDiceUsed = bonusDice,
            LocationBefore = participant.CurrentLocation,
            LocationAfter = participant.CurrentLocation // Всегда проходит — обновится ниже
        };

        if (success)
        {
            result.Summary = $"{participant.Name}: помеха \"{location?.HazardName}\" ({skillName}) — " +
                             $"бросок {actualRoll} против {threshold} ({CombatService.GetSuccessLevelText(level)}). " +
                             (bonusDice > 0 ? $"Бонусных костей: {bonusDice}. " : "") +
                             "Успех!";
        }
        else
        {
            // Провал — урон
            var damageFormula = location?.HazardDamageFormula;
            if (!string.IsNullOrWhiteSpace(damageFormula))
            {
                var damage = damageRoll ?? CombatService.RollDiceFormula(damageFormula);
                result.HpBefore = participant.CurrentHitPoints;
                participant.CurrentHitPoints = Math.Max(0, participant.CurrentHitPoints - damage);
                result.HpAfter = participant.CurrentHitPoints;
                result.DamageDealt = damage;

                if (participant.CurrentHitPoints <= 0)
                    participant.IsEliminated = true;
            }

            // Потеря действий перемещения (1d3)
            var lostActions = lostActionsRoll ?? CombatService.RollDiceFormula("1D3");
            result.MovementActionsLost = lostActions;

            // Применяем потерю: сначала из оставшихся, остаток → долг
            if (lostActions <= participant.MovementActionsRemaining)
            {
                participant.MovementActionsRemaining -= lostActions;
            }
            else
            {
                var overflow = lostActions - participant.MovementActionsRemaining;
                participant.MovementActionsRemaining = 0;
                participant.MovementActionDebt += overflow;
            }

            result.Summary = $"{participant.Name}: помеха \"{location?.HazardName}\" ({skillName}) — " +
                             $"бросок {actualRoll} против {threshold} ({CombatService.GetSuccessLevelText(level)}). " +
                             (bonusDice > 0 ? $"Бонусных костей: {bonusDice}. " : "") +
                             $"Провал! " +
                             (result.DamageDealt > 0 ? $"Урон: {result.DamageDealt}. " : "") +
                             $"Потеряно действий: {lostActions}.";
        }

        // Участник ВСЕГДА проходит дальше (ключевое правило)
        ChaseLog.Insert(0, result);
        NotifyStateChanged();
        return result;
    }

    // ───────────────────── Преграды (Часть 3) ─────────────────────

    public ChaseActionResult ResolveBarrier(Guid participantId, string skillName, int skillValue, int? roll)
    {
        var participant = Participants.First(p => p.Id == participantId);
        var location = GetLocation(participant.CurrentLocation);

        var difficulty = location?.BarrierDifficulty ?? 1;
        var threshold = GetDifficultyThreshold(skillValue, difficulty);
        var actualRoll = roll ?? CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(actualRoll, threshold);
        var success = level >= SuccessLevel.RegularSuccess;

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.BarrierCheck,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            SkillName = $"{location?.BarrierName ?? "Преграда"} ({skillName})",
            SkillValue = threshold,
            Roll = actualRoll,
            SuccessLevel = level,
            IsSuccess = success,
            LocationBefore = participant.CurrentLocation
        };

        participant.MovementActionsRemaining = Math.Max(0, participant.MovementActionsRemaining - 1);

        if (success)
        {
            var newLocation = Math.Min(participant.CurrentLocation + 1, Locations.Count);
            participant.CurrentLocation = newLocation;
            result.LocationAfter = newLocation;
            result.Summary = $"{participant.Name}: преграда \"{location?.BarrierName}\" ({skillName}) — " +
                             $"бросок {actualRoll} против {threshold} ({CombatService.GetSuccessLevelText(level)}). Преодолено!";
        }
        else
        {
            result.LocationAfter = participant.CurrentLocation;
            result.Summary = $"{participant.Name}: преграда \"{location?.BarrierName}\" ({skillName}) — " +
                             $"бросок {actualRoll} против {threshold} ({CombatService.GetSuccessLevelText(level)}). Не удалось!";
        }

        ChaseLog.Insert(0, result);
        CheckCaughtAndEscaped();
        NotifyStateChanged();
        return result;
    }

    /// <summary>
    /// Разрушение преграды: Комплекция × 1d10 урона. Стоит 1 действие перемещения.
    /// </summary>
    public ChaseActionResult AttemptDestroyBarrier(Guid participantId, int? damageRoll)
    {
        var participant = Participants.First(p => p.Id == participantId);
        var location = GetLocation(participant.CurrentLocation);

        participant.MovementActionsRemaining = Math.Max(0, participant.MovementActionsRemaining - 1);

        // Комплекция × 1d10
        var buildValue = Math.Max(1, participant.BuildValue);
        var damage = damageRoll ?? (buildValue * CombatService.RollDice(10));

        var barrierHpBefore = location?.BarrierCurrentHitPoints ?? 0;
        if (location is not null)
        {
            location.BarrierCurrentHitPoints = Math.Max(0, location.BarrierCurrentHitPoints - damage);

            if (location.BarrierCurrentHitPoints <= 0)
            {
                // Преграда разрушена → становится помехой
                location.IsBarrierDestroyed = true;
                location.HasBarrier = false;
                location.HasHazard = true;
                location.HazardName = $"Обломки: {location.BarrierName}";
                location.HazardDifficulty = 1;
                location.HazardDamageFormula = "1D3";
            }
        }

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.BarrierDestroy,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            IsSuccess = location?.BarrierCurrentHitPoints <= 0,
            LocationBefore = participant.CurrentLocation,
            LocationAfter = participant.CurrentLocation,
            BarrierDamageDealt = damage,
            BarrierHpAfter = location?.BarrierCurrentHitPoints ?? 0,
            Summary = location?.BarrierCurrentHitPoints <= 0
                ? $"{participant.Name} разрушает преграду \"{location?.BarrierName}\"! Урон: {damage}, ПЗ: {barrierHpBefore} → 0. Обломки стали помехой."
                : $"{participant.Name} бьёт преграду \"{location?.BarrierName}\". Урон: {damage}, ПЗ: {barrierHpBefore} → {location?.BarrierCurrentHitPoints}."
        };

        ChaseLog.Insert(0, result);
        NotifyStateChanged();
        return result;
    }

    // ───────────────────── Бой в погоне (Часть 4) ─────────────────────

    public ChaseActionResult ResolveMeleeAttack(Guid attackerId, Guid targetId,
        string skillName, int skillValue, int? roll, int? damageRoll)
    {
        var attacker = Participants.First(p => p.Id == attackerId);
        var target = Participants.First(p => p.Id == targetId);

        attacker.MovementActionsRemaining = Math.Max(0, attacker.MovementActionsRemaining - 1);

        var actualRoll = roll ?? CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(actualRoll, skillValue);
        var success = level >= SuccessLevel.RegularSuccess;

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.MeleeAttack,
            ParticipantId = attacker.Id,
            ParticipantName = attacker.Name,
            TargetId = target.Id,
            TargetName = target.Name,
            SkillName = skillName,
            SkillValue = skillValue,
            Roll = actualRoll,
            SuccessLevel = level,
            IsSuccess = success,
            LocationBefore = attacker.CurrentLocation,
            LocationAfter = attacker.CurrentLocation
        };

        if (success && damageRoll.HasValue)
        {
            var damage = damageRoll.Value;
            result.HpBefore = target.CurrentHitPoints;
            target.CurrentHitPoints = Math.Max(0, target.CurrentHitPoints - damage);
            result.HpAfter = target.CurrentHitPoints;
            result.DamageDealt = damage;

            if (target.CurrentHitPoints <= 0)
                target.IsEliminated = true;

            result.Summary = $"{attacker.Name} атакует {target.Name} ({skillName}) — " +
                             $"бросок {actualRoll} против {skillValue} ({CombatService.GetSuccessLevelText(level)}). " +
                             $"Урон: {damage}.";
        }
        else if (success)
        {
            result.Summary = $"{attacker.Name} атакует {target.Name} ({skillName}) — " +
                             $"бросок {actualRoll} против {skillValue} ({CombatService.GetSuccessLevelText(level)}). Попадание!";
        }
        else
        {
            result.Summary = $"{attacker.Name} атакует {target.Name} ({skillName}) — " +
                             $"бросок {actualRoll} против {skillValue} ({CombatService.GetSuccessLevelText(level)}). Промах!";
        }

        ChaseLog.Insert(0, result);
        NotifyStateChanged();
        return result;
    }

    public ChaseActionResult ResolveRangedAttack(Guid attackerId, Guid targetId,
        string skillName, int skillValue, int? roll, int? damageRoll, bool stoppedToShoot)
    {
        var attacker = Participants.First(p => p.Id == attackerId);
        var target = Participants.First(p => p.Id == targetId);

        // Стоя на месте = 1 действие. На ходу = 0 действий (штрафная кость — Хранитель учитывает)
        if (stoppedToShoot)
            attacker.MovementActionsRemaining = Math.Max(0, attacker.MovementActionsRemaining - 1);

        var actualRoll = roll ?? CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(actualRoll, skillValue);
        var success = level >= SuccessLevel.RegularSuccess;

        var shootStyle = stoppedToShoot ? "стоя" : "на ходу";

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.RangedAttack,
            ParticipantId = attacker.Id,
            ParticipantName = attacker.Name,
            TargetId = target.Id,
            TargetName = target.Name,
            SkillName = skillName,
            SkillValue = skillValue,
            Roll = actualRoll,
            SuccessLevel = level,
            IsSuccess = success,
            LocationBefore = attacker.CurrentLocation,
            LocationAfter = attacker.CurrentLocation
        };

        if (success && damageRoll.HasValue)
        {
            var damage = damageRoll.Value;
            result.HpBefore = target.CurrentHitPoints;
            target.CurrentHitPoints = Math.Max(0, target.CurrentHitPoints - damage);
            result.HpAfter = target.CurrentHitPoints;
            result.DamageDealt = damage;

            if (target.CurrentHitPoints <= 0)
                target.IsEliminated = true;

            result.Summary = $"{attacker.Name} стреляет в {target.Name} ({shootStyle}, {skillName}) — " +
                             $"бросок {actualRoll} против {skillValue} ({CombatService.GetSuccessLevelText(level)}). " +
                             $"Урон: {damage}.";
        }
        else if (success)
        {
            result.Summary = $"{attacker.Name} стреляет в {target.Name} ({shootStyle}, {skillName}) — " +
                             $"бросок {actualRoll} против {skillValue} ({CombatService.GetSuccessLevelText(level)}). Попадание!";
        }
        else
        {
            result.Summary = $"{attacker.Name} стреляет в {target.Name} ({shootStyle}, {skillName}) — " +
                             $"бросок {actualRoll} против {skillValue} ({CombatService.GetSuccessLevelText(level)}). Промах!";
        }

        ChaseLog.Insert(0, result);
        NotifyStateChanged();
        return result;
    }

    /// <summary>
    /// Боевой манёвр: успех → цель теряет 1d3 действий + возможный урон. Стоит 1 действие.
    /// </summary>
    public ChaseActionResult ResolveCombatManeuver(Guid attackerId, Guid targetId,
        string skillName, int skillValue, int? roll, int? lostActionsRoll, int? damageRoll)
    {
        var attacker = Participants.First(p => p.Id == attackerId);
        var target = Participants.First(p => p.Id == targetId);

        attacker.MovementActionsRemaining = Math.Max(0, attacker.MovementActionsRemaining - 1);

        var actualRoll = roll ?? CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(actualRoll, skillValue);
        var success = level >= SuccessLevel.RegularSuccess;

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.CombatManeuver,
            ParticipantId = attacker.Id,
            ParticipantName = attacker.Name,
            TargetId = target.Id,
            TargetName = target.Name,
            SkillName = skillName,
            SkillValue = skillValue,
            Roll = actualRoll,
            SuccessLevel = level,
            IsSuccess = success,
            LocationBefore = attacker.CurrentLocation,
            LocationAfter = attacker.CurrentLocation
        };

        if (success)
        {
            // Цель теряет 1d3 действий
            var lostActions = lostActionsRoll ?? CombatService.RollDiceFormula("1D3");
            result.MovementActionsLost = lostActions;

            if (lostActions <= target.MovementActionsRemaining)
            {
                target.MovementActionsRemaining -= lostActions;
            }
            else
            {
                var overflow = lostActions - target.MovementActionsRemaining;
                target.MovementActionsRemaining = 0;
                target.MovementActionDebt += overflow;
            }

            // Урон (опционально, вводит Хранитель)
            if (damageRoll.HasValue && damageRoll.Value > 0)
            {
                result.HpBefore = target.CurrentHitPoints;
                target.CurrentHitPoints = Math.Max(0, target.CurrentHitPoints - damageRoll.Value);
                result.HpAfter = target.CurrentHitPoints;
                result.DamageDealt = damageRoll.Value;

                if (target.CurrentHitPoints <= 0)
                    target.IsEliminated = true;
            }

            result.Summary = $"{attacker.Name} проводит манёвр против {target.Name} ({skillName}) — " +
                             $"бросок {actualRoll} против {skillValue} ({CombatService.GetSuccessLevelText(level)}). " +
                             $"Успех! {target.Name} теряет {lostActions} действий." +
                             (result.DamageDealt > 0 ? $" Урон: {result.DamageDealt}." : "");
        }
        else
        {
            result.Summary = $"{attacker.Name} проводит манёвр против {target.Name} ({skillName}) — " +
                             $"бросок {actualRoll} против {skillValue} ({CombatService.GetSuccessLevelText(level)}). Не удалось!";
        }

        ChaseLog.Insert(0, result);
        NotifyStateChanged();
        return result;
    }

    // ───────────────────── Прочие действия ─────────────────────

    public void SkipAction(Guid participantId)
    {
        var participant = Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is not null)
        {
            participant.HasActedThisRound = true;
            participant.MovementActionsRemaining = 0;

            var result = new ChaseActionResult
            {
                Round = CurrentRound,
                ActionType = ChaseActionType.SkipAction,
                ParticipantId = participant.Id,
                ParticipantName = participant.Name,
                IsSuccess = true,
                Summary = $"{participant.Name} пропускает ход."
            };
            ChaseLog.Insert(0, result);
            NotifyStateChanged();
        }
    }

    public ChaseActionResult RecordOtherAction(Guid participantId, string description, bool costsMovementAction)
    {
        var participant = Participants.First(p => p.Id == participantId);

        if (costsMovementAction)
            participant.MovementActionsRemaining = Math.Max(0, participant.MovementActionsRemaining - 1);

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.Other,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            IsSuccess = true,
            LocationBefore = participant.CurrentLocation,
            LocationAfter = participant.CurrentLocation,
            Summary = $"{participant.Name}: {description}"
        };

        ChaseLog.Insert(0, result);
        NotifyStateChanged();
        return result;
    }

    // ───────────────────── Проверки состояния ─────────────────────

    public List<ChaseActionResult> CheckCaughtAndEscaped()
    {
        var results = new List<ChaseActionResult>();
        var maxLocation = Locations.Count;

        foreach (var prey in Participants.Where(p => p.Role == ChaseRole.Prey && p.IsActive))
        {
            // Сбежал — вышел за пределы трассы
            if (prey.CurrentLocation >= maxLocation)
            {
                var farthestPursuer = Participants
                    .Where(p => p.Role == ChaseRole.Pursuer && p.IsActive)
                    .MaxBy(p => p.CurrentLocation);

                if (farthestPursuer is null || prey.CurrentLocation > farthestPursuer.CurrentLocation)
                {
                    prey.HasEscaped = true;
                    var escapeResult = new ChaseActionResult
                    {
                        Round = CurrentRound,
                        ActionType = ChaseActionType.EscapedEvent,
                        ParticipantId = prey.Id,
                        ParticipantName = prey.Name,
                        IsSuccess = true,
                        Summary = $"{prey.Name} сбежал!"
                    };
                    results.Add(escapeResult);
                    ChaseLog.Insert(0, escapeResult);
                }
            }

            // Пойман — преследователь на той же или большей позиции
            if (!prey.HasEscaped)
            {
                var catcher = Participants
                    .Where(p => p.Role == ChaseRole.Pursuer && p.IsActive)
                    .FirstOrDefault(p => p.CurrentLocation >= prey.CurrentLocation);

                if (catcher is not null)
                {
                    prey.IsCaught = true;
                    var caughtResult = new ChaseActionResult
                    {
                        Round = CurrentRound,
                        ActionType = ChaseActionType.CaughtEvent,
                        ParticipantId = prey.Id,
                        ParticipantName = prey.Name,
                        IsSuccess = false,
                        Summary = $"{prey.Name} пойман ({catcher.Name} догнал на локации {catcher.CurrentLocation})!"
                    };
                    results.Add(caughtResult);
                    ChaseLog.Insert(0, caughtResult);
                }
            }
        }

        if (results.Count > 0)
        {
            if (IsChaseOver())
                Phase = ChasePhase.Ended;

            NotifyStateChanged();
        }

        return results;
    }

    public int GetDistanceBetween(Guid id1, Guid id2)
    {
        var p1 = Participants.FirstOrDefault(p => p.Id == id1);
        var p2 = Participants.FirstOrDefault(p => p.Id == id2);
        if (p1 is null || p2 is null) return 0;
        return Math.Abs(p1.CurrentLocation - p2.CurrentLocation);
    }

    public bool IsChaseOver() =>
        Participants.Where(p => p.Role == ChaseRole.Prey).All(p => p.HasEscaped || p.IsCaught || p.IsEliminated);

    // ───────────────────── Применение результатов ─────────────────────

    public void SetPendingResult(ChaseActionResult result)
    {
        PendingResult = result;
        NotifyStateChanged();
    }

    public void ApplyResult(ChaseActionResult result)
    {
        var participant = Participants.FirstOrDefault(p => p.Id == result.ParticipantId);
        if (participant is not null)
        {
            if (result.LocationAfter.HasValue)
                participant.CurrentLocation = result.LocationAfter.Value;

            if (result.HpAfter.HasValue)
                participant.CurrentHitPoints = result.HpAfter.Value;

            if (result.DamageDealt > 0 && participant.CurrentHitPoints <= 0)
                participant.IsEliminated = true;
        }

        ChaseLog.Insert(0, result);
        PendingResult = null;
        CheckCaughtAndEscaped();
        NotifyStateChanged();
    }

    public void CancelPendingResult()
    {
        PendingResult = null;
        NotifyStateChanged();
    }

    // ───────────────────── Вспомогательные ─────────────────────

    public static int GetDifficultyThreshold(int skillValue, int difficulty) => difficulty switch
    {
        2 => skillValue / 2,
        3 => skillValue / 5,
        _ => skillValue
    };

    public static string GetDifficultyText(int difficulty) => difficulty switch
    {
        2 => "сложная",
        3 => "экстремальная",
        _ => "обычная"
    };

    public static string GetRoleText(ChaseRole role) => role switch
    {
        ChaseRole.Prey => "Жертва",
        ChaseRole.Pursuer => "Преследователь",
        _ => "Неизвестно"
    };

    /// <summary>
    /// Получить список участников в той же локации (для выбора цели атаки).
    /// </summary>
    public List<ChaseParticipant> GetParticipantsAtLocation(int location, Guid? excludeId = null) =>
        Participants
            .Where(p => p.IsActive && p.CurrentLocation == location && p.Id != excludeId)
            .ToList();

    private void NotifyStateChanged() => OnChange?.Invoke();
}
