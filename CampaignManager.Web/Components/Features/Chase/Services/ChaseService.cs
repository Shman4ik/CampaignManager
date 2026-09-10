using CampaignManager.Web.Components.Features.Chase.Model;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;
using Microsoft.AspNetCore.Components;

namespace CampaignManager.Web.Components.Features.Chase.Services;

/// <summary>
/// Сервис управления погонями по правилам Call of Cthulhu 7e (Глава 7).
/// Хранитель вводит все результаты бросков кубиков — сервис не кидает за игроков.
/// </summary>
public sealed partial class ChaseService
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

    // ───────────────────── Сохранение и восстановление ─────────────────────

    /// <summary>
    /// Точка, за которую Blazor держит состояние погони (см. корневой CLAUDE.md, «Circuit State Persistence»).
    /// Геттер вызывается при постановке circuit на паузу, сеттер — при возобновлении, поэтому
    /// сцена возвращается сразу, без обращения к базе. Долговременное хранение остаётся
    /// за <see cref="ChaseSessionService"/>.
    /// </summary>
    [PersistentState]
    public ChaseSnapshot? PersistedState
    {
        get => HasContent() ? CreateSnapshot() : null;
        set
        {
            if (value is not null)
                RestoreSnapshot(value);
        }
    }

    public ChaseSnapshot CreateSnapshot() => new()
    {
        Phase = Phase,
        Participants = Participants,
        Locations = Locations,
        CurrentRound = CurrentRound,
        CurrentTurnIndex = CurrentTurnIndex,
        MinAdjustedMov = MinAdjustedMov,
        StartGap = StartGap,
        ChaseLog = ChaseLog,
        UseRandomHazards = UseRandomHazards,
        UseSuddenHazards = UseSuddenHazards,
        UseFloorIt = UseFloorIt,
        SuddenHazardIsPlayersTurn = SuddenHazardIsPlayersTurn
    };

    public void RestoreSnapshot(ChaseSnapshot snapshot)
    {
        Phase = snapshot.Phase;
        Participants = snapshot.Participants;
        Locations = snapshot.Locations;
        CurrentRound = snapshot.CurrentRound;
        CurrentTurnIndex = snapshot.CurrentTurnIndex;
        MinAdjustedMov = snapshot.MinAdjustedMov;
        StartGap = snapshot.StartGap;
        ChaseLog = snapshot.ChaseLog;
        UseRandomHazards = snapshot.UseRandomHazards;
        UseSuddenHazards = snapshot.UseSuddenHazards;
        UseFloorIt = snapshot.UseFloorIt;
        SuddenHazardIsPlayersTurn = snapshot.SuddenHazardIsPlayersTurn;
        PendingResult = null;
        NotifyStateChanged();
    }

    /// <summary>Есть ли что восстанавливать — пустая заготовка не считается сценой.</summary>
    public bool HasContent() => Participants.Count > 0 || Locations.Count > 0;

    // ───────────────────── Необязательные правила (часть 5, стр. 137) ─────────────────────

    // Переключение правила должно перерисовать всю страницу, а не только сам чекбокс:
    // от «Педали в пол» зависит кнопка действия в соседней колонке.
    private bool _useRandomHazards;
    private bool _useSuddenHazards;
    private bool _useFloorIt;

    public bool UseRandomHazards
    {
        get => _useRandomHazards;
        set { if (_useRandomHazards == value) return; _useRandomHazards = value; NotifyStateChanged(); }
    }

    public bool UseSuddenHazards
    {
        get => _useSuddenHazards;
        set { if (_useSuddenHazards == value) return; _useSuddenHazards = value; NotifyStateChanged(); }
    }

    public bool UseFloorIt
    {
        get => _useFloorIt;
        set { if (_useFloorIt == value) return; _useFloorIt = value; NotifyStateChanged(); }
    }

    public bool SuddenHazardIsPlayersTurn { get; private set; } = true;

    // ───────────────────── Настройка трассы ─────────────────────

    /// <summary>
    /// Стартовый отрыв жертвы от преследователей в локациях (стр. 130:
    /// «Хранитель задаёт стартовую дистанцию в две локации, но может уменьшить её до одной»).
    /// </summary>
    public int StartGap { get; private set; } = 2;

    public void SetupTrack(int locationCount, int startGap = 2)
    {
        StartGap = Math.Clamp(startGap, 1, 2);

        var newLocations = new List<ChaseLocation>();
        for (var i = 1; i <= locationCount; i++)
        {
            newLocations.Add(new ChaseLocation { Number = i });
        }

        Locations = newLocations;
        ApplyDefaultPositions();
        NotifyStateChanged();
    }

    /// <summary>
    /// Расставить участников по правилу «Ближе к делу» (стр. 130): преследователи на первой локации,
    /// жертвы — на StartGap локаций впереди.
    /// </summary>
    public void ApplyDefaultPositions()
    {
        if (Locations.Count == 0) return;

        foreach (var p in Participants)
            p.CurrentLocation = DefaultLocationFor(p.Role);
    }

    private int DefaultLocationFor(ChaseRole role) =>
        role == ChaseRole.Prey
            ? Math.Clamp(1 + StartGap, 1, Locations.Count)
            : 1;

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
        if (Locations.Count > 0 && participant.CurrentLocation < 1)
            participant.CurrentLocation = DefaultLocationFor(participant.Role);

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

    /// <summary>Посадить участника в транспорт из таблицы V (стр. 143).</summary>
    public void SetVehicleFromTemplate(Guid id, VehicleTemplate template)
    {
        var p = Participants.FirstOrDefault(x => x.Id == id);
        if (p is null) return;

        ApplyVehicle(p, template.Name, template.Speed, template.Build, template.Armor, template.SkillName);
        NotifyStateChanged();
    }

    private static void ApplyVehicle(ChaseParticipant p, string vehicleName, int speed,
        double build, int armor, string? skillName)
    {
        p.IsInVehicle = true;
        p.VehicleName = vehicleName;
        p.VehicleSpeed = speed;
        p.VehicleBuild = build;
        p.VehicleCurrentBuild = build;
        p.VehicleArmor = armor;
        p.VehicleSkillName = skillName;
        p.VehicleDamageCarry = 0;
        p.BurstTyres = 0;
        p.Mode = MovementMode.OnFoot;
    }

    public void RemoveVehicle(Guid id)
    {
        var p = Participants.FirstOrDefault(x => x.Id == id);
        if (p is not null)
        {
            p.IsInVehicle = false;
            p.VehicleName = null;
            p.VehicleSpeed = 0;
            p.VehicleBuild = 0;
            p.VehicleCurrentBuild = 0;
            p.VehicleArmor = 0;
            p.VehicleSkillName = null;
            p.VehicleDamageCarry = 0;
            p.BurstTyres = 0;
            NotifyStateChanged();
        }
    }

    /// <summary>Пассажир (стр. 139): без проверки скорости и без действий перемещения.</summary>
    public void SetPassenger(Guid id, bool isPassenger, Guid? carrierId)
    {
        var p = Participants.FirstOrDefault(x => x.Id == id);
        if (p is null) return;

        p.IsPassenger = isPassenger;
        p.CarrierId = isPassenger ? carrierId : null;
        if (isPassenger)
        {
            p.SpeedCheckCompleted = true;
            p.MovModifier = 0;
            p.TotalMovementActions = 0;
            p.MovementActionsRemaining = 0;
        }

        NotifyStateChanged();
    }

    /// <summary>Штурман помогает водителю: следующий разгон получает на одну штрафную кость меньше.</summary>
    public ChaseActionResult ResolveNavigatorAssist(Guid passengerId, Guid driverId,
        string skillName, int skillValue, int? roll)
    {
        var passenger = Participants.First(p => p.Id == passengerId);
        var driver = Participants.First(p => p.Id == driverId);

        var actualRoll = roll ?? CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(actualRoll, skillValue);
        var success = level >= SuccessLevel.RegularSuccess;

        if (success)
            driver.HasNavigatorAssist = true;

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.Other,
            ParticipantId = passenger.Id,
            ParticipantName = passenger.Name,
            TargetId = driver.Id,
            TargetName = driver.Name,
            SkillName = skillName,
            SkillValue = skillValue,
            Roll = actualRoll,
            SuccessLevel = level,
            IsSuccess = success,
            IsApplied = true,
            Summary = $"{passenger.Name} помогает с навигацией ({skillName}) — бросок {actualRoll} " +
                      $"против {skillValue} ({CombatService.GetSuccessLevelText(level)}). " +
                      (success
                          ? $"Следующий разгон {driver.Name} получает на одну штрафную кость меньше."
                          : "Совет не пригодился.")
        };

        ChaseLog.Insert(0, result);
        NotifyStateChanged();
        return result;
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
            // Пассажиры проверку скорости не проходят (стр. 139)
            p.SpeedCheckCompleted = p.IsPassenger;
            p.MovModifier = 0;
        }

        NotifyStateChanged();
    }

    /// <summary>Навык проверки скорости: пешком — ВЫН, за рулём — навык управления транспортом (стр. 130).</summary>
    public static (string SkillName, int SkillValue) GetSpeedCheckSkill(ChaseParticipant participant)
    {
        if (!participant.IsInVehicle)
            return ("ВЫН", participant.ConstitutionValue);

        var skillName = string.IsNullOrWhiteSpace(participant.VehicleSkillName)
            ? "Вождение"
            : participant.VehicleSkillName;

        var skillValue = participant.DrivingSkill > 0
            ? participant.DrivingSkill
            // У чудовищ и НПС навыка вождения обычно нет — берём половину ЛВК (стр. 142)
            : SubstituteSkillFromDexterity(participant.Dexterity, SkillAptitude.Uncertain);

        return (skillName, Math.Max(1, skillValue));
    }

    public ChaseActionResult ResolveSpeedCheck(Guid participantId, int? roll)
    {
        var participant = Participants.First(p => p.Id == participantId);

        var (skillName, skillValue) = GetSpeedCheckSkill(participant);

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
            IsApplied = true,
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
        // Никто не должен остаться вне трассы (локации нумеруются с 1)
        foreach (var p in Participants.Where(p => p.CurrentLocation < 1))
            p.CurrentLocation = DefaultLocationFor(p.Role);

        // Сортировка по ЛВК (убывание)
        Participants = Participants
            .OrderByDescending(p => p.Dexterity)
            .ToList();

        Phase = ChasePhase.Active;
        CurrentRound = 1;
        CurrentTurnIndex = 0;

        // Отсекает слишком медленных преследователей и считает действия перемещения
        RecalculateChaseSpeeds();
    }

    // ───────────────────── Управление раундами (Часть 2) ─────────────────────

    public void CalculateMovementActions()
    {
        foreach (var p in Participants.Where(p => p.IsActive))
        {
            p.HasActedThisRound = false;

            // Пассажиры действуют раз в свой ход по ЛВК, но действий перемещения у них нет (стр. 139)
            if (!p.HasMovementActions)
            {
                p.TotalMovementActions = 0;
                p.MovementActionsRemaining = 0;
                p.MovementActionDebt = 0;
                continue;
            }

            var earned = 1 + (p.AdjustedMov - MinAdjustedMov);
            var actions = Math.Max(0, earned - p.MovementActionDebt);
            p.MovementActionDebt = Math.Max(0, p.MovementActionDebt - earned);
            p.TotalMovementActions = Math.Max(0, actions);
            p.MovementActionsRemaining = p.TotalMovementActions;
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
    //
    // Все методы Resolve*/Move* ниже — ЧИСТЫЕ: они только считают результат броска и не меняют
    // состояние погони. Изменения применяет единственная точка — ApplyResult. Благодаря этому
    // «Отменить» в панели результата действительно отменяет действие, а журнал не двоится.

    public ChaseActionResult MoveForward(Guid participantId)
    {
        var participant = Participants.First(p => p.Id == participantId);
        var startLocation = participant.CurrentLocation;
        var newLocation = Math.Min(startLocation + 1, Locations.Count);

        return new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.MovementAction,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            IsSuccess = true,
            ActorMovementActionsSpent = 1,
            LocationBefore = startLocation,
            LocationAfter = newLocation,
            Summary = $"{participant.Name} перемещается с локации {startLocation} на {newLocation}."
        };
    }

    // ───────────────────── Помехи (Часть 3) ─────────────────────

    /// <summary>
    /// Разрешить помеху на локации <paramref name="locationNumber"/> (стр. 133).
    /// Стоит 1 действие перемещения плюс по одному за каждую бонусную кость (максимум 2).
    /// Провал: урон + потеря 1d3 действий. Участник ВСЕГДА проходит дальше — успешно или нет.
    /// </summary>
    public ChaseActionResult ResolveHazard(Guid participantId, int locationNumber, string skillName, int skillValue,
        int? roll, int bonusDice, int? damageRoll, int? lostActionsRoll)
    {
        var participant = Participants.First(p => p.Id == participantId);
        var location = GetLocation(locationNumber);

        // Бонусные кости стоят действия перемещения (макс 2)
        bonusDice = Math.Clamp(bonusDice, 0, 2);

        var difficulty = location?.HazardDifficulty ?? 1;
        var threshold = GetDifficultyThreshold(skillValue, difficulty);
        var actualRoll = roll ?? CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(actualRoll, threshold);
        var success = level >= SuccessLevel.RegularSuccess;
        var hazardName = location?.HazardName ?? "Помеха";

        // Помеха стоит перед локацией: разобравшись с ней, персонаж входит в эту локацию
        var destination = Math.Clamp(locationNumber, 1, Math.Max(1, Locations.Count));

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.HazardCheck,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            SkillName = $"{hazardName} ({skillName})",
            SkillValue = threshold,
            Roll = actualRoll,
            SuccessLevel = level,
            IsSuccess = success,
            BonusDiceUsed = bonusDice,
            ActorMovementActionsSpent = 1 + bonusDice,
            LocationBefore = participant.CurrentLocation,
            LocationAfter = destination
        };

        if (success)
        {
            result.Summary = $"{participant.Name}: помеха \"{hazardName}\" ({skillName}) — " +
                             $"бросок {actualRoll} против {threshold} ({CombatService.GetSuccessLevelText(level)}). " +
                             (bonusDice > 0 ? $"Бонусных костей: {bonusDice}. " : "") +
                             $"Успех! Проходит на локацию {destination}.";
        }
        else
        {
            // Провал — урон по формуле помехи
            var damageFormula = location?.HazardDamageFormula;
            if (damageRoll is > 0)
            {
                result.DamageDealt = damageRoll.Value;
            }
            else if (!string.IsNullOrWhiteSpace(damageFormula))
            {
                result.DamageDealt = CombatService.RollDiceFormula(damageFormula);
            }

            if (result.DamageDealt is > 0)
            {
                result.HpBefore = participant.CurrentHitPoints;
                result.HpAfter = Math.Max(0, participant.CurrentHitPoints - result.DamageDealt.Value);
            }

            // Потеря действий перемещения (1d3)
            result.MovementActionsLost = lostActionsRoll ?? CombatService.RollDiceFormula("1D3");

            result.Summary = $"{participant.Name}: помеха \"{hazardName}\" ({skillName}) — " +
                             $"бросок {actualRoll} против {threshold} ({CombatService.GetSuccessLevelText(level)}). " +
                             (bonusDice > 0 ? $"Бонусных костей: {bonusDice}. " : "") +
                             "Провал! " +
                             (result.DamageDealt > 0 ? $"Урон: {result.DamageDealt}. " : "") +
                             $"Потеряно {Actions(result.MovementActionsLost)}. " +
                             $"Всё равно проходит на локацию {destination}.";
        }

        return result;
    }

    // ───────────────────── Преграды (Часть 3) ─────────────────────

    /// <summary>
    /// Преодолеть преграду на локации <paramref name="locationNumber"/> (стр. 134).
    /// Успех — участник входит в локацию; провал — остаётся на месте. Стоит 1 действие перемещения.
    /// </summary>
    public ChaseActionResult ResolveBarrier(Guid participantId, int locationNumber,
        string skillName, int skillValue, int? roll)
    {
        var participant = Participants.First(p => p.Id == participantId);
        var location = GetLocation(locationNumber);

        var difficulty = location?.BarrierDifficulty ?? 1;
        var threshold = GetDifficultyThreshold(skillValue, difficulty);
        var actualRoll = roll ?? CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(actualRoll, threshold);
        var success = level >= SuccessLevel.RegularSuccess;
        var barrierName = location?.BarrierName ?? "Преграда";

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.BarrierCheck,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            SkillName = $"{barrierName} ({skillName})",
            SkillValue = threshold,
            Roll = actualRoll,
            SuccessLevel = level,
            IsSuccess = success,
            ActorMovementActionsSpent = 1,
            LocationBefore = participant.CurrentLocation,
            LocationAfter = success
                ? Math.Clamp(locationNumber, 1, Math.Max(1, Locations.Count))
                : participant.CurrentLocation
        };

        result.Summary = $"{participant.Name}: преграда \"{barrierName}\" ({skillName}) — " +
                         $"бросок {actualRoll} против {threshold} ({CombatService.GetSuccessLevelText(level)}). " +
                         (success ? $"Преодолено, проходит на локацию {result.LocationAfter}!" : "Не удалось!");

        return result;
    }

    /// <summary>
    /// Разрушение преграды на локации <paramref name="locationNumber"/> (стр. 135).
    /// Транспорт наносит 1d10 урона за каждый пункт Комплекции; пешеход бьёт «вручную» —
    /// урон в этом случае целиком задаёт Хранитель. Стоит 1 действие перемещения.
    /// </summary>
    public ChaseActionResult AttemptDestroyBarrier(Guid participantId, int locationNumber, int? damageRoll)
    {
        var participant = Participants.First(p => p.Id == participantId);
        var location = GetLocation(locationNumber);

        var damage = damageRoll ?? GetDefaultBarrierDamage(participant);

        var barrierHpBefore = location?.BarrierCurrentHitPoints ?? 0;
        var barrierHpAfter = Math.Max(0, barrierHpBefore - damage);
        var destroyed = location is not null && barrierHpAfter <= 0;
        var barrierName = location?.BarrierName ?? "Преграда";

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.BarrierDestroy,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            IsSuccess = destroyed,
            ActorMovementActionsSpent = 1,
            LocationBefore = participant.CurrentLocation,
            LocationAfter = participant.CurrentLocation,
            BarrierLocation = locationNumber,
            BarrierDamageDealt = damage,
            BarrierHpAfter = barrierHpAfter
        };

        result.Summary = destroyed
            ? $"{participant.Name} разрушает преграду \"{barrierName}\"! Урон: {damage}, ПЗ: {barrierHpBefore} → 0. Обломки стали помехой."
            : $"{participant.Name} бьёт преграду \"{barrierName}\". Урон: {damage}, ПЗ: {barrierHpBefore} → {barrierHpAfter}.";

        // Отдача для транспорта (стр. 135): пробил — половина ПЗ преграды до столкновения,
        // не пробил — «если машина врезалась в преграду, но так её и не пробила, она сама получает повреждения»
        if (participant.IsInVehicle)
        {
            var recoilDamage = destroyed ? barrierHpBefore / 2 : damage / 2;
            var recoilBuildLoss = recoilDamage / 10;

            if (recoilBuildLoss > 0)
            {
                result.ActorBuildLoss = recoilBuildLoss;
                result.Summary += $" Транспорт получает {recoilDamage} — Комплекция −{recoilBuildLoss}.";
            }
            else
            {
                result.Summary += $" Транспорт получает {recoilDamage} (Комплекции не теряет).";
            }
        }

        return result;
    }

    /// <summary>
    /// Урон по преграде по умолчанию: транспорт — 1d10 за пункт Комплекции (стр. 135),
    /// пешеход — 1d3 (как в примере с Харви и дощатым забором).
    /// </summary>
    private static int GetDefaultBarrierDamage(ChaseParticipant participant)
    {
        if (!participant.IsInVehicle)
            return CombatService.RollDiceFormula("1D3");

        var build = Math.Max(1, participant.BuildValue);
        var total = 0;
        for (var i = 0; i < build; i++)
            total += CombatService.RollDice(10);

        return total;
    }

    // ───────────────────── Бой в погоне (Часть 4) ─────────────────────

    /// <summary>
    /// Ближний бой в погоне. Защита необязательна, но по правилам (стр. 136) её нужно предлагать всегда,
    /// сколько бы действий перемещения у цели ни оставалось: <paramref name="defenceValue"/> больше нуля
    /// превращает атаку во встречный бросок, а <paramref name="defenceIsFightBack"/> различает
    /// контратаку (ничья за атакующим) и уклонение (ничья за защитником).
    /// </summary>
    public ChaseActionResult ResolveMeleeAttack(Guid attackerId, Guid targetId,
        string skillName, int skillValue, int? roll, int? damageRoll,
        string? defenceSkillName = null, int defenceValue = 0, int? defenceRoll = null,
        bool defenceIsFightBack = false)
    {
        var attacker = Participants.First(p => p.Id == attackerId);
        var target = Participants.First(p => p.Id == targetId);

        var actualRoll = roll ?? CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(actualRoll, skillValue);
        var success = level >= SuccessLevel.RegularSuccess;

        // Ответ цели: контратака или уклонение (стр. 136)
        var defended = false;
        var defenceText = string.Empty;
        if (defenceValue > 0)
        {
            var actualDefenceRoll = defenceRoll ?? CombatService.RollD100();
            var defenceLevel = CombatService.CalculateSuccessLevel(actualDefenceRoll, defenceValue);
            var attackerWins = CombatService.ResolveOpposedRoll(
                level, skillValue, defenceLevel, defenceValue,
                defenceIsFightBack ? CombatActionType.FightBack : CombatActionType.Dodge);

            defended = !attackerWins;
            success = success && attackerWins;

            defenceText = $" {target.Name} отвечает ({defenceSkillName ?? (defenceIsFightBack ? "Контратака" : "Уклонение")}): " +
                          $"бросок {actualDefenceRoll} против {defenceValue} " +
                          $"({CombatService.GetSuccessLevelText(defenceLevel)}) — " +
                          (attackerWins ? "не помогло." : "атака отбита!");
        }

        var result = SkillCheckResult(ChaseActionType.MeleeAttack, attacker, skillName, skillValue,
            actualRoll, level, success, target);

        if (success && damageRoll is > 0)
        {
            var damage = damageRoll.Value;
            result.HpBefore = target.CurrentHitPoints;
            result.HpAfter = Math.Max(0, target.CurrentHitPoints - damage);
            result.DamageDealt = damage;

            result.Summary = $"{attacker.Name} атакует {target.Name} ({skillName}) — " +
                             $"бросок {actualRoll} против {skillValue} ({CombatService.GetSuccessLevelText(level)}). " +
                             $"Урон: {damage}.{defenceText}";
        }
        else if (success)
        {
            result.Summary = $"{attacker.Name} атакует {target.Name} ({skillName}) — " +
                             $"бросок {actualRoll} против {skillValue} ({CombatService.GetSuccessLevelText(level)}). " +
                             $"Попадание!{defenceText}";
        }
        else
        {
            result.Summary = $"{attacker.Name} атакует {target.Name} ({skillName}) — " +
                             $"бросок {actualRoll} против {skillValue} ({CombatService.GetSuccessLevelText(level)}). " +
                             (defended ? "Атака отбита!" : "Промах!") + defenceText;
        }

        return result;
    }

    public ChaseActionResult ResolveRangedAttack(Guid attackerId, Guid targetId,
        string skillName, int skillValue, int? roll, int? damageRoll, bool stoppedToShoot)
    {
        var attacker = Participants.First(p => p.Id == attackerId);
        var target = Participants.First(p => p.Id == targetId);

        var actualRoll = roll ?? CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(actualRoll, skillValue);
        var success = level >= SuccessLevel.RegularSuccess;

        var shootStyle = stoppedToShoot ? "стоя" : "на ходу";

        // Стоя на месте — 1 действие; на ходу — 0 действий и штрафная кость (стр. 139)
        var result = SkillCheckResult(ChaseActionType.RangedAttack, attacker, skillName, skillValue,
            actualRoll, level, success, target, movementActionsSpent: stoppedToShoot ? 1 : 0);

        if (success && damageRoll is > 0)
        {
            var damage = damageRoll.Value;
            result.HpBefore = target.CurrentHitPoints;
            result.HpAfter = Math.Max(0, target.CurrentHitPoints - damage);
            result.DamageDealt = damage;

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

        // Разница Комплекции: +1 — штрафная кость, +2 — две, +3 и больше манёвр невозможен (стр. 136)
        var (buildPenalty, impossible) = GetManeuverBuildPenalty(attacker, target);
        if (impossible)
        {
            return new ChaseActionResult
            {
                Round = CurrentRound,
                ActionType = ChaseActionType.CombatManeuver,
                ParticipantId = attacker.Id,
                ParticipantName = attacker.Name,
                TargetId = target.Id,
                TargetName = target.Name,
                IsSuccess = false,
                ActorMovementActionsSpent = 0,
                LocationBefore = attacker.CurrentLocation,
                LocationAfter = attacker.CurrentLocation,
                Summary = $"Манёвр невозможен: Комплекция {target.Name} " +
                          $"({target.EffectiveBuild:0.#}) больше, чем у {attacker.Name} " +
                          $"({attacker.EffectiveBuild:0.#}), на 3 и более пунктов — разница в размере слишком велика."
            };
        }

        var actualRoll = roll ?? CombatService.RollD100(0, buildPenalty).Result;
        var level = CombatService.CalculateSuccessLevel(actualRoll, skillValue);
        var success = level >= SuccessLevel.RegularSuccess;

        var result = SkillCheckResult(ChaseActionType.CombatManeuver, attacker, skillName, skillValue,
            actualRoll, level, success, target, penaltyDice: buildPenalty);

        if (success)
        {
            // Цель теряет 1d3 действий
            result.MovementActionsLost = lostActionsRoll ?? CombatService.RollDiceFormula("1D3");

            // Урон (опционально, вводит Хранитель)
            if (damageRoll is > 0)
            {
                result.HpBefore = target.CurrentHitPoints;
                result.HpAfter = Math.Max(0, target.CurrentHitPoints - damageRoll.Value);
                result.DamageDealt = damageRoll.Value;
            }

            result.Summary = $"{attacker.Name} проводит манёвр против {target.Name} ({skillName}) — " +
                             $"бросок {actualRoll} против {skillValue} ({CombatService.GetSuccessLevelText(level)}" +
                             (buildPenalty > 0 ? $", штрафных костей за Комплекцию: {buildPenalty}" : "") + "). " +
                             $"Успех! {target.Name} теряет {Actions(result.MovementActionsLost)}." +
                             (result.DamageDealt > 0 ? $" Урон: {result.DamageDealt}." : "");
        }
        else
        {
            result.Summary = $"{attacker.Name} проводит манёвр против {target.Name} ({skillName}) — " +
                             $"бросок {actualRoll} против {skillValue} ({CombatService.GetSuccessLevelText(level)}" +
                             (buildPenalty > 0 ? $", штрафных костей за Комплекцию: {buildPenalty}" : "") + "). Не удалось!";
        }

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
                IsApplied = true,
                Summary = $"{participant.Name} пропускает ход."
            };
            ChaseLog.Insert(0, result);
            NotifyStateChanged();
        }
    }

    // ───────────────────── Проверки состояния ─────────────────────

    /// <summary>
    /// Обновить состояние погони после перемещения. Побег фиксируется автоматически,
    /// поимка — НЕТ: по правилам (стр. 135) преследователь, оказавшийся в одной локации с жертвой,
    /// лишь получает возможность атаковать её или провести манёвр. Решение «пойман» принимает
    /// Хранитель через <see cref="MarkCaught"/>.
    /// </summary>
    public void RefreshChaseState()
    {
        var maxLocation = Locations.Count;
        var changed = false;

        foreach (var prey in Participants.Where(p => p.Role == ChaseRole.Prey && p.IsActive))
        {
            if (prey.CurrentLocation < maxLocation) continue;

            var farthestPursuer = Participants
                .Where(p => p.Role == ChaseRole.Pursuer && p.IsActive)
                .MaxBy(p => p.CurrentLocation);

            if (farthestPursuer is not null && prey.CurrentLocation <= farthestPursuer.CurrentLocation)
                continue;

            prey.HasEscaped = true;
            changed = true;
            ChaseLog.Insert(0, new ChaseActionResult
            {
                Round = CurrentRound,
                ActionType = ChaseActionType.EscapedEvent,
                ParticipantId = prey.Id,
                ParticipantName = prey.Name,
                IsSuccess = true,
                IsApplied = true,
                Summary = $"{prey.Name} сбежал — трасса пройдена, преследователи позади!"
            });
        }

        if (changed && IsChaseOver())
            Phase = ChasePhase.Ended;
    }

    /// <summary>
    /// Жертва настигнута: преследователь находится в той же локации. Это ещё не поимка —
    /// только возможность ближнего боя и боевых манёвров.
    /// </summary>
    public List<ChaseParticipant> GetPursuersInContactWith(ChaseParticipant prey) =>
        Participants
            .Where(p => p.Role == ChaseRole.Pursuer && p.IsActive && p.CurrentLocation == prey.CurrentLocation)
            .ToList();

    public bool HasContact() =>
        Participants
            .Where(p => p.Role == ChaseRole.Prey && p.IsActive)
            .Any(prey => GetPursuersInContactWith(prey).Count > 0);

    /// <summary>Хранитель объявляет жертву пойманной (после удачной атаки, манёвра или по описанию сцены).</summary>
    public void MarkCaught(Guid preyId, Guid? catcherId = null)
    {
        var prey = Participants.FirstOrDefault(p => p.Id == preyId);
        if (prey is null || !prey.IsActive) return;

        var catcher = catcherId is { } id ? Participants.FirstOrDefault(p => p.Id == id) : null;
        prey.IsCaught = true;

        ChaseLog.Insert(0, new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.CaughtEvent,
            ParticipantId = prey.Id,
            ParticipantName = prey.Name,
            TargetId = catcher?.Id,
            TargetName = catcher?.Name,
            IsSuccess = false,
            IsApplied = true,
            Summary = catcher is null
                ? $"{prey.Name} пойман!"
                : $"{prey.Name} пойман — {catcher.Name} схватил его на локации {prey.CurrentLocation}!"
        });

        if (IsChaseOver())
            Phase = ChasePhase.Ended;

        NotifyStateChanged();
    }

    public bool IsChaseOver() =>
        Participants.Where(p => p.Role == ChaseRole.Prey).All(p => p.HasEscaped || p.IsCaught || p.IsEliminated);

    // ───────────────────── Применение результатов ─────────────────────

    public void SetPendingResult(ChaseActionResult result)
    {
        PendingResult = result;
        NotifyStateChanged();
    }

    /// <summary>
    /// Единственная точка, меняющая состояние погони по результату действия.
    /// Все Resolve*/Move* только считают — применяет только этот метод, он же пишет в журнал.
    /// </summary>
    public void ApplyResult(ChaseActionResult result)
    {
        if (result.IsApplied)
        {
            PendingResult = null;
            NotifyStateChanged();
            return;
        }

        var actor = Participants.FirstOrDefault(p => p.Id == result.ParticipantId);
        var target = result.TargetId is { } targetId
            ? Participants.FirstOrDefault(p => p.Id == targetId)
            : null;

        // Урон и потеря действий достаются цели, а если её нет — самому исполнителю
        var victim = target ?? actor;

        if (actor is not null && result.ActorMovementActionsSpent > 0)
        {
            actor.MovementActionsRemaining =
                Math.Max(0, actor.MovementActionsRemaining - result.ActorMovementActionsSpent);
        }

        if (victim is not null && result.MovementActionsLost > 0)
            LoseMovementActions(victim, result.MovementActionsLost);

        if (victim is not null && result.HpAfter.HasValue)
        {
            victim.CurrentHitPoints = Math.Max(0, result.HpAfter.Value);
            if (victim.CurrentHitPoints <= 0)
                victim.IsEliminated = true;
        }

        if (result.BarrierLocation is { } barrierNumber)
            ApplyBarrierDamage(barrierNumber, result.BarrierHpAfter ?? 0);

        // Комплекция транспорта (стр. 136, 139)
        if (victim is not null && result.TargetBuildLoss is > 0)
            ApplyBuildLoss(victim, result.TargetBuildLoss.Value, result.TargetVehicleDamage);

        if (actor is not null && result.ActorBuildLoss is > 0)
            ApplyBuildLoss(actor, result.ActorBuildLoss.Value, null);

        // Созданная персонажем помеха или преграда (стр. 141)
        if (result.ObstacleToPlace is { } obstacle && result.ObstacleLocation is { } obstacleNumber)
            PlaceObstacle(obstacleNumber, obstacle);

        if (actor is not null && result.LocationAfter.HasValue)
            actor.CurrentLocation = Math.Clamp(result.LocationAfter.Value, 1, Math.Max(1, Locations.Count));

        // Разгон расходует помощь штурмана (стр. 139)
        if (actor is not null && result.ActionType == ChaseActionType.FloorIt)
            actor.HasNavigatorAssist = false;

        // Спрятался или потерял след — выбывает из погони (стр. 139)
        if (actor is not null && result.RemovesActorFromChase)
        {
            if (actor.Role == ChaseRole.Prey)
                actor.HasEscaped = true;
            else
                actor.IsOutOfChase = true;
        }

        result.IsApplied = true;
        ChaseLog.Insert(0, result);
        PendingResult = null;
        RefreshChaseState();
        NotifyStateChanged();
    }

    /// <summary>
    /// Снять пункты Комплекции с транспорта и запомнить остаток урона до следующего полного десятка.
    /// У пешехода Комплекция — характеристика персонажа и в погоне не снижается.
    /// </summary>
    private static void ApplyBuildLoss(ChaseParticipant participant, double buildLoss, int? carryOver)
    {
        if (!participant.IsInVehicle) return;

        participant.VehicleCurrentBuild = Math.Max(0, participant.VehicleCurrentBuild - buildLoss);
        if (carryOver.HasValue)
            participant.VehicleDamageCarry = carryOver.Value;
    }

    private void PlaceObstacle(int locationNumber, ChaseLocation obstacle)
    {
        var location = GetLocation(locationNumber);
        if (location is null) return;

        if (obstacle.HasBarrier)
        {
            location.HasBarrier = true;
            location.IsBarrierDestroyed = false;
            location.BarrierName = obstacle.BarrierName;
            location.BarrierSkillName = obstacle.BarrierSkillName;
            location.BarrierSkillValue = obstacle.BarrierSkillValue;
            location.BarrierDifficulty = obstacle.BarrierDifficulty;
            location.BarrierHitPoints = obstacle.BarrierHitPoints;
            location.BarrierCurrentHitPoints = obstacle.BarrierHitPoints;
        }

        if (obstacle.HasHazard)
        {
            location.HasHazard = true;
            location.HazardName = obstacle.HazardName;
            location.HazardSkillName = obstacle.HazardSkillName;
            location.HazardSkillValue = obstacle.HazardSkillValue;
            location.HazardDifficulty = obstacle.HazardDifficulty;
            location.HazardDamageFormula = obstacle.HazardDamageFormula;
        }
    }

    /// <summary>
    /// Списать потерянные действия перемещения: сначала из оставшихся в этом раунде,
    /// остаток переносится долгом на следующий раунд (стр. 133).
    /// </summary>
    private static void LoseMovementActions(ChaseParticipant participant, int lostActions)
    {
        if (lostActions <= participant.MovementActionsRemaining)
        {
            participant.MovementActionsRemaining -= lostActions;
            return;
        }

        participant.MovementActionDebt += lostActions - participant.MovementActionsRemaining;
        participant.MovementActionsRemaining = 0;
    }

    /// <summary>Разрушенная преграда превращается в помеху-обломки (стр. 136).</summary>
    private void ApplyBarrierDamage(int locationNumber, int barrierHpAfter)
    {
        var location = GetLocation(locationNumber);
        if (location is null) return;

        location.BarrierCurrentHitPoints = Math.Max(0, barrierHpAfter);
        if (location.BarrierCurrentHitPoints > 0) return;

        location.IsBarrierDestroyed = true;
        location.HasBarrier = false;
        location.HasHazard = true;
        location.HazardName = $"Обломки: {location.BarrierName}";
        location.HazardDifficulty = 1;
        location.HazardDamageFormula = "1D3";
    }

    public void CancelPendingResult()
    {
        PendingResult = null;
        NotifyStateChanged();
    }

    /// <summary>
    /// Записать в журнал результат, который уже применён сам по себе (броски Хранителя,
    /// не меняющие состояние участников): случайные и внезапные помехи.
    /// </summary>
    public void LogExternalResult(ChaseActionResult result)
    {
        result.IsApplied = true;
        ChaseLog.Insert(0, result);
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

    /// <summary>Русское склонение по числу: 1 локация, 2 локации, 5 локаций.</summary>
    public static string Plural(int count, string one, string few, string many)
    {
        var mod100 = Math.Abs(count) % 100;
        if (mod100 is >= 11 and <= 14) return many;

        return (Math.Abs(count) % 10) switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many
        };
    }

    public static string LocationsText(int count) => $"{count} {Plural(count, "локацию", "локации", "локаций")}";

    public static string Actions(int count) => $"{count} {Plural(count, "действие", "действия", "действий")}";

    public static string PenaltyDiceText(int count) =>
        $"{count} {Plural(count, "штрафную кость", "штрафные кости", "штрафных костей")}";

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
