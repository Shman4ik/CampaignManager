using CampaignManager.Web.Components.Features.Chase.Model;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Web.Components.Features.Chase.Services;

/// <summary>
/// Сервис управления погонями по правилам Call of Cthulhu 7e
/// </summary>
public sealed class ChaseService
{
    public List<ChaseParticipant> Participants { get; private set; } = [];
    public List<ChaseLocation> Locations { get; private set; } = [];
    public int CurrentRound { get; private set; } = 1;
    public int CurrentTurnIndex { get; private set; }
    public List<ChaseActionResult> ChaseLog { get; private set; } = [];
    public ChaseActionResult? PendingResult { get; private set; }
    public Guid? SelectedCampaignId { get; private set; }
    public bool IsChaseActive { get; private set; }

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
        if (p != null)
        {
            p.CurrentLocation = Math.Clamp(location, 1, Locations.Count);
            NotifyStateChanged();
        }
    }

    public void SetParticipantRole(Guid id, ChaseRole role)
    {
        var p = Participants.FirstOrDefault(x => x.Id == id);
        if (p != null)
        {
            p.Role = role;
            NotifyStateChanged();
        }
    }

    public void SetVehicle(Guid id, string vehicleName, int vehicleSpeed)
    {
        var p = Participants.FirstOrDefault(x => x.Id == id);
        if (p != null)
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
        if (p != null)
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

    // ───────────────────── Управление раундами ─────────────────────

    public void StartChase()
    {
        SortByDexterity();
        IsChaseActive = true;
        CurrentRound = 1;
        CurrentTurnIndex = 0;
        NotifyStateChanged();
    }

    public void SortByDexterity()
    {
        Participants = Participants
            .OrderByDescending(p => p.Dexterity)
            .ToList();
        NotifyStateChanged();
    }

    public ChaseParticipant? GetActiveParticipant()
    {
        var active = Participants.Where(p => p.IsActive).ToList();
        if (active.Count == 0) return null;
        if (CurrentTurnIndex >= active.Count) return active[0];
        return active[CurrentTurnIndex];
    }

    public void NextTurn()
    {
        var active = Participants.Where(p => p.IsActive).ToList();
        if (active.Count == 0) return;

        CurrentTurnIndex++;
        if (CurrentTurnIndex >= active.Count)
        {
            CurrentTurnIndex = 0;
            CurrentRound++;
            ResetRoundState();
        }

        NotifyStateChanged();
    }

    public void NextRound()
    {
        CurrentRound++;
        CurrentTurnIndex = 0;
        ResetRoundState();
        NotifyStateChanged();
    }

    private void ResetRoundState()
    {
        foreach (var p in Participants.Where(p => p.IsActive))
        {
            p.HasActedThisRound = false;
            p.ExtraMovesAttempted = 0;
            p.SpeedMovesThisRound = 0;
            p.IsExhausted = false;
        }
    }

    public void ResetChase()
    {
        Participants.Clear();
        Locations.Clear();
        CurrentRound = 1;
        CurrentTurnIndex = 0;
        ChaseLog.Clear();
        PendingResult = null;
        SelectedCampaignId = null;
        IsChaseActive = false;
        NotifyStateChanged();
    }

    // ───────────────────── Фаза скорости ─────────────────────

    public List<ChaseActionResult> ExecuteSpeedPhase()
    {
        var results = new List<ChaseActionResult>();

        foreach (var participant in Participants.Where(p => p.IsActive))
        {
            var speed = participant.EffectiveSpeed;
            var startLocation = participant.CurrentLocation;
            var targetLocation = startLocation + speed;
            var actualLocation = startLocation;

            // Двигаемся по локациям, проверяя барьеры и опасности
            for (var loc = startLocation + 1; loc <= targetLocation && loc <= Locations.Count; loc++)
            {
                var location = GetLocation(loc);
                if (location == null) continue;

                // Проверка опасности (не блокирует, но может нанести урон)
                if (location.HasHazard)
                {
                    var hazardResult = AutoResolveHazard(participant, location);
                    if (hazardResult != null)
                        results.Add(hazardResult);
                }

                // Проверка барьера (блокирует движение)
                if (location.HasBarrier)
                {
                    actualLocation = loc; // Останавливаемся НА локации барьера
                    break;
                }

                actualLocation = loc;
            }

            // Если вышел за пределы трассы — сбежал (только для Prey)
            if (actualLocation > Locations.Count)
                actualLocation = Locations.Count;

            participant.CurrentLocation = actualLocation;
            participant.SpeedMovesThisRound = actualLocation - startLocation;

            var moveResult = new ChaseActionResult
            {
                Round = CurrentRound,
                ActionType = ChaseActionType.SpeedMove,
                ParticipantId = participant.Id,
                ParticipantName = participant.Name,
                LocationBefore = startLocation,
                LocationAfter = actualLocation,
                IsSuccess = true,
                Summary = $"{participant.Name} перемещается с {startLocation} на {actualLocation} (скорость {speed})."
            };
            results.Add(moveResult);
        }

        // Добавляем все результаты в лог
        foreach (var result in results)
        {
            ChaseLog.Insert(0, result);
        }

        // Проверка пойман/сбежал
        var statusResults = CheckCaughtAndEscaped();
        results.AddRange(statusResults);

        NotifyStateChanged();
        return results;
    }

    private ChaseActionResult? AutoResolveHazard(ChaseParticipant participant, ChaseLocation location)
    {
        var skillValue = location.HazardSkillValue;

        // Попробовать найти навык у персонажа
        if (participant.CharacterSource != null && !string.IsNullOrEmpty(location.HazardSkillName))
        {
            var charSkill = CombatService.FindSkillValue(participant.CharacterSource, location.HazardSkillName);
            if (charSkill > 0) skillValue = charSkill;
        }

        if (skillValue <= 0) skillValue = 50; // значение по умолчанию

        var threshold = GetDifficultyThreshold(skillValue, location.HazardDifficulty);
        var roll = CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(roll, threshold);
        var success = level >= SuccessLevel.RegularSuccess;

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.HazardCheck,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            SkillName = location.HazardName ?? location.HazardSkillName,
            SkillValue = threshold,
            Roll = roll,
            SuccessLevel = level,
            IsSuccess = success,
            LocationBefore = participant.CurrentLocation,
            LocationAfter = participant.CurrentLocation
        };

        if (!success && !string.IsNullOrWhiteSpace(location.HazardConsequence))
        {
            var damage = CombatService.RollDiceFormula(location.HazardConsequence);
            result.HpBefore = participant.CurrentHitPoints;
            participant.CurrentHitPoints = Math.Max(0, participant.CurrentHitPoints - damage);
            result.HpAfter = participant.CurrentHitPoints;
            result.DamageDealt = damage;

            if (participant.CurrentHitPoints <= 0)
                participant.IsEliminated = true;

            result.Summary = $"{participant.Name}: опасность \"{location.HazardName}\" — бросок {roll} против {threshold} " +
                             $"({CombatService.GetSuccessLevelText(level)}). Получает {damage} ед. урона!";
        }
        else if (!success)
        {
            result.Summary = $"{participant.Name}: опасность \"{location.HazardName}\" — бросок {roll} против {threshold} " +
                             $"({CombatService.GetSuccessLevelText(level)}). Неудача!";
        }
        else
        {
            result.Summary = $"{participant.Name}: опасность \"{location.HazardName}\" — бросок {roll} против {threshold} " +
                             $"({CombatService.GetSuccessLevelText(level)}). Успех!";
        }

        return result;
    }

    // ───────────────────── Действия участника ─────────────────────

    public ChaseActionResult AttemptExtraMove(Guid participantId)
    {
        var participant = Participants.First(p => p.Id == participantId);
        var attemptNumber = participant.ExtraMovesAttempted + 1;

        string skillName;
        int skillValue;
        int difficulty;

        if (participant.IsInVehicle)
        {
            skillName = "Вождение";
            skillValue = participant.DrivingSkill > 0 ? participant.DrivingSkill : 20;
            difficulty = GetConDifficultyForExtraMove(attemptNumber);
        }
        else
        {
            skillName = "ТЕЛ";
            skillValue = participant.ConstitutionValue;
            difficulty = GetConDifficultyForExtraMove(attemptNumber);
        }

        var threshold = GetDifficultyThreshold(skillValue, difficulty);
        var roll = CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(roll, threshold);
        var success = level >= SuccessLevel.RegularSuccess;

        var startLocation = participant.CurrentLocation;

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.ExtraMove,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            SkillName = $"{skillName} ({GetDifficultyText(difficulty)})",
            SkillValue = threshold,
            Roll = roll,
            SuccessLevel = level,
            IsSuccess = success,
            LocationBefore = startLocation
        };

        participant.ExtraMovesAttempted = attemptNumber;
        participant.HasActedThisRound = true;

        if (success)
        {
            // Перемещение на +1 локацию
            var newLocation = Math.Min(startLocation + 1, Locations.Count);

            // Проверить барьер на новой локации
            var loc = GetLocation(newLocation);
            if (loc?.HasBarrier == true)
            {
                newLocation = startLocation; // Не может пройти через барьер рывком
                result.Summary = $"{participant.Name}: рывок ({skillName}) — бросок {roll} против {threshold} " +
                                 $"({CombatService.GetSuccessLevelText(level)}). Успех, но путь преграждает \"{loc.BarrierName}\"!";
            }
            else
            {
                // Проверить опасность на новой локации
                if (loc?.HasHazard == true)
                {
                    var hazardResult = AutoResolveHazard(participant, loc);
                    if (hazardResult != null)
                        ChaseLog.Insert(0, hazardResult);
                }

                result.Summary = $"{participant.Name}: рывок ({skillName}) — бросок {roll} против {threshold} " +
                                 $"({CombatService.GetSuccessLevelText(level)}). Перемещается на {newLocation}!";
            }

            participant.CurrentLocation = newLocation;
            result.LocationAfter = newLocation;
        }
        else
        {
            // Провал рывка
            participant.IsExhausted = true;
            result.LocationAfter = startLocation;

            int damage;
            if (participant.IsInVehicle)
            {
                damage = CombatService.RollDiceFormula("2D6");
                result.Summary = $"{participant.Name}: рывок ({skillName}) — бросок {roll} против {threshold} " +
                                 $"({CombatService.GetSuccessLevelText(level)}). Авария! Получает {damage} ед. урона!";
            }
            else
            {
                damage = CombatService.RollDiceFormula("1D3");
                result.Summary = $"{participant.Name}: рывок ({skillName}) — бросок {roll} против {threshold} " +
                                 $"({CombatService.GetSuccessLevelText(level)}). Выдохся! Получает {damage} ед. урона!";
            }

            result.HpBefore = participant.CurrentHitPoints;
            participant.CurrentHitPoints = Math.Max(0, participant.CurrentHitPoints - damage);
            result.HpAfter = participant.CurrentHitPoints;
            result.DamageDealt = damage;

            if (participant.CurrentHitPoints <= 0)
                participant.IsEliminated = true;
        }

        return result;
    }

    public ChaseActionResult ResolveBarrier(Guid participantId, string skillName, int skillValue)
    {
        var participant = Participants.First(p => p.Id == participantId);
        var location = GetLocation(participant.CurrentLocation);

        var difficulty = location?.BarrierDifficulty ?? 1;
        var threshold = GetDifficultyThreshold(skillValue, difficulty);
        var roll = CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(roll, threshold);
        var success = level >= SuccessLevel.RegularSuccess;

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.BarrierCheck,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            SkillName = $"{location?.BarrierName ?? "Препятствие"} ({skillName})",
            SkillValue = threshold,
            Roll = roll,
            SuccessLevel = level,
            IsSuccess = success,
            LocationBefore = participant.CurrentLocation
        };

        participant.HasActedThisRound = true;

        if (success)
        {
            // Преодолел — двигается на +1 за барьер
            var newLocation = Math.Min(participant.CurrentLocation + 1, Locations.Count);
            participant.CurrentLocation = newLocation;
            result.LocationAfter = newLocation;
            result.Summary = $"{participant.Name}: препятствие \"{location?.BarrierName}\" ({skillName}) — " +
                             $"бросок {roll} против {threshold} ({CombatService.GetSuccessLevelText(level)}). Преодолено!";
        }
        else
        {
            result.LocationAfter = participant.CurrentLocation;
            result.Summary = $"{participant.Name}: препятствие \"{location?.BarrierName}\" ({skillName}) — " +
                             $"бросок {roll} против {threshold} ({CombatService.GetSuccessLevelText(level)}). Не удалось!";
        }

        return result;
    }

    public ChaseActionResult ResolveHazard(Guid participantId, string skillName, int skillValue)
    {
        var participant = Participants.First(p => p.Id == participantId);
        var location = GetLocation(participant.CurrentLocation);

        var difficulty = location?.HazardDifficulty ?? 1;
        var threshold = GetDifficultyThreshold(skillValue, difficulty);
        var roll = CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(roll, threshold);
        var success = level >= SuccessLevel.RegularSuccess;

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.HazardCheck,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            SkillName = $"{location?.HazardName ?? "Опасность"} ({skillName})",
            SkillValue = threshold,
            Roll = roll,
            SuccessLevel = level,
            IsSuccess = success,
            LocationBefore = participant.CurrentLocation,
            LocationAfter = participant.CurrentLocation
        };

        if (!success && location != null && !string.IsNullOrWhiteSpace(location.HazardConsequence))
        {
            var damage = CombatService.RollDiceFormula(location.HazardConsequence);
            result.HpBefore = participant.CurrentHitPoints;
            participant.CurrentHitPoints = Math.Max(0, participant.CurrentHitPoints - damage);
            result.HpAfter = participant.CurrentHitPoints;
            result.DamageDealt = damage;

            if (participant.CurrentHitPoints <= 0)
                participant.IsEliminated = true;

            result.Summary = $"{participant.Name}: опасность \"{location.HazardName}\" ({skillName}) — " +
                             $"бросок {roll} против {threshold} ({CombatService.GetSuccessLevelText(level)}). " +
                             $"Получает {damage} ед. урона!";
        }
        else if (!success)
        {
            result.Summary = $"{participant.Name}: опасность \"{location?.HazardName}\" ({skillName}) — " +
                             $"бросок {roll} против {threshold} ({CombatService.GetSuccessLevelText(level)}). Неудача!";
        }
        else
        {
            result.Summary = $"{participant.Name}: опасность \"{location?.HazardName}\" ({skillName}) — " +
                             $"бросок {roll} против {threshold} ({CombatService.GetSuccessLevelText(level)}). Успех!";
        }

        return result;
    }

    public void SkipAction(Guid participantId)
    {
        var participant = Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant != null)
        {
            participant.HasActedThisRound = true;
            NotifyStateChanged();
        }
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

                if (farthestPursuer == null || prey.CurrentLocation > farthestPursuer.CurrentLocation)
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

                if (catcher != null)
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
            NotifyStateChanged();

        return results;
    }

    public int GetDistanceBetween(Guid id1, Guid id2)
    {
        var p1 = Participants.FirstOrDefault(p => p.Id == id1);
        var p2 = Participants.FirstOrDefault(p => p.Id == id2);
        if (p1 == null || p2 == null) return 0;
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
        if (participant != null)
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

        // Проверка пойман/сбежал
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

    public static int GetConDifficultyForExtraMove(int attemptNumber) => attemptNumber switch
    {
        1 => 1,
        2 => 2,
        3 => 3,
        _ => 3
    };

    public static string GetRoleText(ChaseRole role) => role switch
    {
        ChaseRole.Prey => "Жертва",
        ChaseRole.Pursuer => "Преследователь",
        _ => "Неизвестно"
    };

    private void NotifyStateChanged() => OnChange?.Invoke();
}
