using CampaignManager.Web.Components.Features.Chase.Model;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Web.Components.Features.Chase.Services;

/// <summary>
/// Части 4–5 главы 7: столкновения транспорта и необязательные правила погони.
/// Как и в основном файле, все Resolve* — чистые функции; состояние меняет только ApplyResult.
/// </summary>
public sealed partial class ChaseService
{
    // ───────────────────── Комплекция и манёвры (стр. 136) ─────────────────────

    /// <summary>
    /// Штрафные кости за разницу Комплекции при боевом манёвре: цель крупнее на 1 — одна кость,
    /// на 2 — две, на 3 и больше — манёвр невозможен.
    /// </summary>
    public static (int PenaltyDice, bool IsImpossible) GetManeuverBuildPenalty(
        ChaseParticipant attacker, ChaseParticipant target)
    {
        var difference = (int)Math.Floor(target.EffectiveBuild - attacker.EffectiveBuild);
        if (difference >= 3) return (0, true);
        return (Math.Max(0, difference), false);
    }

    // ───────────────────── Столкновение транспорта (стр. 136) ─────────────────────

    /// <summary>
    /// Таран: транспорт считается оружием, наносящим 1d10 урона за каждый пункт своей Комплекции.
    /// Атакующий получает половину нанесённого урона, но не больше исходной Комплекции цели.
    /// Каждые полные 10 пунктов урона снимают 1 Комплекции, остаток копится.
    /// </summary>
    public ChaseActionResult ResolveVehicleCollision(Guid attackerId, Guid targetId,
        string skillName, int skillValue, int? roll, int? damageRoll)
    {
        var attacker = Participants.First(p => p.Id == attackerId);
        var target = Participants.First(p => p.Id == targetId);

        var actualRoll = roll ?? CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(actualRoll, skillValue);
        var success = level >= SuccessLevel.RegularSuccess;

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.VehicleCollision,
            ParticipantId = attacker.Id,
            ParticipantName = attacker.Name,
            TargetId = target.Id,
            TargetName = target.Name,
            SkillName = skillName,
            SkillValue = skillValue,
            Roll = actualRoll,
            SuccessLevel = level,
            IsSuccess = success,
            ActorMovementActionsSpent = 1,
            LocationBefore = attacker.CurrentLocation,
            LocationAfter = attacker.CurrentLocation
        };

        if (!success)
        {
            result.Summary = $"{attacker.Name} пытается таранить {target.Name} ({skillName}) — " +
                             $"бросок {actualRoll} против {skillValue} " +
                             $"({CombatService.GetSuccessLevelText(level)}). Мимо!";
            return result;
        }

        var buildDice = Math.Max(1, (int)Math.Round(attacker.EffectiveBuild));
        var damage = damageRoll ?? RollBuildDice(buildDice);

        // Цель: каждые полные 10 урона — минус 1 Комплекции, остаток переносится
        var targetTotal = target.VehicleDamageCarry + damage;
        var targetBuildLoss = targetTotal / 10;
        result.TargetVehicleDamage = targetTotal % 10;
        result.TargetBuildLoss = targetBuildLoss;
        result.DamageDealt = damage;

        // Отдача: половина урона, но не больше исходной Комплекции цели
        var recoil = damage / 2;
        var recoilBuildLoss = Math.Min(recoil / 10, (int)Math.Ceiling(target.EffectiveBuild));
        result.ActorBuildLoss = recoilBuildLoss;

        result.Summary = $"{attacker.Name} таранит {target.Name} ({skillName}) — " +
                         $"бросок {actualRoll} против {skillValue} " +
                         $"({CombatService.GetSuccessLevelText(level)}). " +
                         $"Урон {damage} ({buildDice}d10): Комплекция цели −{targetBuildLoss}. " +
                         $"Отдача {recoil}: своя Комплекция −{recoilBuildLoss}.";

        return result;
    }

    private static int RollBuildDice(int diceCount)
    {
        var total = 0;
        for (var i = 0; i < diceCount; i++)
            total += CombatService.RollDice(10);
        return total;
    }

    // ───────────────────── Стрельба по шинам (стр. 139) ─────────────────────

    /// <summary>
    /// Шина — маленькая цель: одна штрафная кость. Броня шины 3, повреждает только проникающее оружие,
    /// 2 пункта урона — шина лопнула, Комплекция транспорта падает на 1.
    /// </summary>
    public ChaseActionResult ResolveTyreShot(Guid attackerId, Guid targetId,
        string skillName, int skillValue, int? roll, int? damageRoll)
    {
        var attacker = Participants.First(p => p.Id == attackerId);
        var target = Participants.First(p => p.Id == targetId);

        var actualRoll = roll ?? CombatService.RollD100(0, 1).Result;
        var level = CombatService.CalculateSuccessLevel(actualRoll, skillValue);
        var success = level >= SuccessLevel.RegularSuccess;

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.TyreShot,
            ParticipantId = attacker.Id,
            ParticipantName = attacker.Name,
            TargetId = target.Id,
            TargetName = target.Name,
            SkillName = skillName,
            SkillValue = skillValue,
            Roll = actualRoll,
            SuccessLevel = level,
            IsSuccess = success,
            PenaltyDice = 1,
            ActorMovementActionsSpent = 0,
            LocationBefore = attacker.CurrentLocation,
            LocationAfter = attacker.CurrentLocation
        };

        if (!success)
        {
            result.Summary = $"{attacker.Name} стреляет по шинам {target.Name} ({skillName}, штрафная кость " +
                             $"за малый размер) — бросок {actualRoll} против {skillValue} " +
                             $"({CombatService.GetSuccessLevelText(level)}). Промах!";
            return result;
        }

        var rawDamage = damageRoll ?? CombatService.RollDice(10);
        var afterArmour = Math.Max(0, rawDamage - TyreArmour);
        var burst = afterArmour >= 2;

        result.DamageDealt = afterArmour;
        result.TargetBuildLoss = burst ? 1 : 0;

        result.Summary = $"{attacker.Name} стреляет по шинам {target.Name} ({skillName}) — " +
                         $"бросок {actualRoll} против {skillValue} " +
                         $"({CombatService.GetSuccessLevelText(level)}). " +
                         $"Урон {rawDamage} − броня {TyreArmour} = {afterArmour}. " +
                         (burst
                             ? "Шина лопнула — Комплекция транспорта −1."
                             : "Шина выдержала (нужно 2 пункта после брони).");

        return result;
    }

    private const int TyreArmour = 3;

    // ───────────────────── Урон водителю (стр. 139) ─────────────────────

    /// <summary>
    /// Водитель, получивший серьёзную рану, немедленно проходит проверку как при трудной помехе.
    /// Потерявший сознание теряет управление автоматически.
    /// </summary>
    public ChaseActionResult ResolveDriverControlCheck(Guid participantId, string skillName, int skillValue,
        int? roll, bool isUnconscious, string crashTierName, int? buildLossRoll, int? damageRoll,
        int? lostActionsRoll)
    {
        var participant = Participants.First(p => p.Id == participantId);
        var threshold = GetDifficultyThreshold(skillValue, 2); // как при трудной помехе
        var actualRoll = roll ?? CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(actualRoll, threshold);
        var keptControl = !isUnconscious && level >= SuccessLevel.RegularSuccess;

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.DriverControlCheck,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            SkillName = $"Управление ({skillName})",
            SkillValue = threshold,
            Roll = isUnconscious ? 0 : actualRoll,
            SuccessLevel = level,
            IsSuccess = keptControl,
            LocationBefore = participant.CurrentLocation,
            LocationAfter = participant.CurrentLocation
        };

        if (keptControl)
        {
            result.Summary = $"{participant.Name} удерживает управление после серьёзной раны — " +
                             $"бросок {actualRoll} против {threshold} (трудная сложность, " +
                             $"{CombatService.GetSuccessLevelText(level)}).";
            return result;
        }

        var tier = ChaseReference.CrashTiers.FirstOrDefault(t => t.Name == crashTierName)
                   ?? ChaseReference.CrashTiers[1];

        result.TargetBuildLoss = buildLossRoll ?? CombatService.RollDiceFormula(tier.BuildLossFormula);
        result.MovementActionsLost = lostActionsRoll ?? CombatService.RollDiceFormula("1D3");

        if (damageRoll is > 0)
        {
            result.DamageDealt = damageRoll.Value;
            result.HpBefore = participant.CurrentHitPoints;
            result.HpAfter = Math.Max(0, participant.CurrentHitPoints - damageRoll.Value);
        }

        result.Summary = isUnconscious
            ? $"{participant.Name} без сознания — управление потеряно автоматически. {tier.Name}: " +
              $"Комплекция −{result.TargetBuildLoss}, потеряно {Actions(result.MovementActionsLost)}."
            : $"{participant.Name} теряет управление — бросок {actualRoll} против {threshold} " +
              $"({CombatService.GetSuccessLevelText(level)}). {tier.Name}: " +
              $"Комплекция −{result.TargetBuildLoss}, потеряно {Actions(result.MovementActionsLost)}." +
              (result.DamageDealt > 0 ? $" Урон водителю: {result.DamageDealt}." : "");

        return result;
    }

    // ───────────────────── «Педаль в пол» (стр. 137) ─────────────────────

    /// <summary>
    /// Штрафные кости за разгон: 2–3 локации за действие — одна кость, 4–5 — две.
    /// Помощь штурмана снимает одну (стр. 139).
    /// </summary>
    public static int GetBoostPenaltyDice(int locations, bool navigatorAssist)
    {
        var penalty = locations switch
        {
            >= 4 => 2,
            >= 2 => 1,
            _ => 0
        };

        return Math.Max(0, penalty - (navigatorAssist ? 1 : 0));
    }

    /// <summary>
    /// Разгон: за одно действие перемещения транспорт проезжает 2–5 локаций. Заявляется до перемещения;
    /// если по дороге провалена проверка помехи, разгон обрывается и дальше тратится новое действие.
    /// </summary>
    public ChaseActionResult ResolveFloorIt(Guid participantId, int locations)
    {
        var participant = Participants.First(p => p.Id == participantId);
        locations = Math.Clamp(locations, 2, 5);

        var startLocation = participant.CurrentLocation;
        var destination = Math.Min(startLocation + locations, Locations.Count);
        var penalty = GetBoostPenaltyDice(locations, participant.HasNavigatorAssist);

        return new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.FloorIt,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            IsSuccess = true,
            ActorMovementActionsSpent = 1,
            PenaltyDice = penalty,
            LocationBefore = startLocation,
            LocationAfter = destination,
            Summary = $"{participant.Name} жмёт на газ: {LocationsText(locations)} за одно действие " +
                      $"({startLocation} → {destination}). " +
                      (penalty > 0
                          ? $"Все помехи на пути дают {PenaltyDiceText(penalty)}."
                          : "Штурман снял штрафную кость — помехи без штрафа.") +
                      (participant.HasNavigatorAssist ? " Помощь штурмана израсходована." : "")
        };
    }

    // ───────────────────── Случайные помехи (стр. 137) ─────────────────────

    /// <summary>
    /// Бросок 1d100: 01–59 — чисто, ≥60 обычная помеха, ≥85 трудная, ≥96 чрезвычайная.
    /// Тяжёлая дорога даёт штрафную кость, свободная автострада — бонусную.
    /// </summary>
    public ChaseActionResult RollRandomHazard(int? roll, int bonusDice, int penaltyDice, int locationNumber)
    {
        var detail = roll.HasValue
            ? DiceRollResult.Plain(roll.Value)
            : CombatService.RollD100(bonusDice, penaltyDice);

        var difficulty = detail.Result switch
        {
            >= 96 => 3,
            >= 85 => 2,
            >= 60 => 1,
            _ => 0
        };

        var text = difficulty switch
        {
            3 => "чрезвычайная помеха",
            2 => "трудная помеха",
            1 => "обычная помеха",
            _ => "чисто, помех нет"
        };

        return new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.RandomHazardRoll,
            ParticipantId = Guid.Empty,
            ParticipantName = "Хранитель",
            SkillName = "Случайная помеха",
            Roll = detail.Result,
            IsSuccess = difficulty == 0,
            IsApplied = true,
            BonusDiceUsed = bonusDice,
            PenaltyDice = penaltyDice,
            ObstacleLocation = locationNumber,
            Summary = $"Локация {locationNumber}: бросок на случайные помехи {detail.Result} — {text}."
        };
    }

    // ───────────────────── Внезапные помехи (стр. 137) ─────────────────────

    /// <summary>
    /// Общая проверка Удачи. Успех — помеху размещают игроки, провал — Хранитель.
    /// Право объявления чередуется: одна сторона не может объявить дважды подряд.
    /// </summary>
    public ChaseActionResult ResolveSuddenHazard(int luckValue, int? roll)
    {
        var actualRoll = roll ?? CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(actualRoll, luckValue);
        var playersWin = level >= SuccessLevel.RegularSuccess;
        var declaredBy = SuddenHazardIsPlayersTurn ? "Игроки" : "Хранитель";

        return new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.SuddenHazard,
            ParticipantId = Guid.Empty,
            ParticipantName = declaredBy,
            SkillName = "Удача",
            SkillValue = luckValue,
            Roll = actualRoll,
            SuccessLevel = level,
            IsSuccess = playersWin,
            IsApplied = true,
            Summary = $"Внезапная помеха. Очередь объявлять: {declaredBy.ToLowerInvariant()}. " +
                      $"Общая проверка Удачи: {actualRoll} против {luckValue} " +
                      $"({CombatService.GetSuccessLevelText(level)}). " +
                      (playersWin
                          ? "Успех — где и когда появится помеха, решают игроки."
                          : "Провал — помеху придумывает Хранитель, и она угрожает сыщикам.") +
                      " Сложность внезапной помехи — обычная."
        };
    }

    /// <summary>Передать право объявления внезапной помехи другой стороне.</summary>
    public void PassSuddenHazardTurn()
    {
        SuddenHazardIsPlayersTurn = !SuddenHazardIsPlayersTurn;
        NotifyStateChanged();
    }

    // ───────────────────── Уйти от преследования (стр. 139) ─────────────────────

    /// <summary>
    /// Преследователь ищет след. Провал Чтения следов — погоня для него окончена.
    /// </summary>
    public ChaseActionResult ResolveTrackingCheck(Guid pursuerId, string skillName, int skillValue,
        int difficulty, int? roll)
    {
        var pursuer = Participants.First(p => p.Id == pursuerId);
        var threshold = GetDifficultyThreshold(skillValue, difficulty);
        var actualRoll = roll ?? CombatService.RollD100();
        var level = CombatService.CalculateSuccessLevel(actualRoll, threshold);
        var success = level >= SuccessLevel.RegularSuccess;

        return new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.TrackingCheck,
            ParticipantId = pursuer.Id,
            ParticipantName = pursuer.Name,
            SkillName = skillName,
            SkillValue = threshold,
            Roll = actualRoll,
            SuccessLevel = level,
            IsSuccess = success,
            ActorMovementActionsSpent = 1,
            RemovesActorFromChase = !success,
            LocationBefore = pursuer.CurrentLocation,
            LocationAfter = pursuer.CurrentLocation,
            Summary = $"{pursuer.Name} ищет след ({skillName}) — бросок {actualRoll} против {threshold} " +
                      $"({CombatService.GetSuccessLevelText(level)}). " +
                      (success ? "След найден, погоня продолжается." : "След потерян — погоня для него окончена.")
        };
    }

    // ───────────────────── Спрятаться (стр. 139) ─────────────────────

    /// <summary>
    /// Убегающий прячется. Пешком — Скрытность; на транспорте нужна совместная проверка
    /// Скрытности и Вождения: результат должен быть ниже обоих навыков.
    /// </summary>
    public ChaseActionResult ResolveHide(Guid participantId, int stealthValue, int? drivingValue,
        int difficulty, int bonusDice, int? roll)
    {
        var participant = Participants.First(p => p.Id == participantId);

        var stealthThreshold = GetDifficultyThreshold(stealthValue, difficulty);
        var actualRoll = roll ?? CombatService.RollD100(bonusDice, 0).Result;
        var stealthLevel = CombatService.CalculateSuccessLevel(actualRoll, stealthThreshold);
        var stealthOk = stealthLevel >= SuccessLevel.RegularSuccess;

        var combined = drivingValue.HasValue;
        var drivingThreshold = combined ? GetDifficultyThreshold(drivingValue!.Value, difficulty) : 0;
        var drivingOk = !combined || actualRoll <= drivingThreshold;
        var hidden = stealthOk && drivingOk;

        var skillLabel = combined ? "Скрытность + Вождение" : "Скрытность";
        var thresholdLabel = combined ? $"{stealthThreshold} и {drivingThreshold}" : $"{stealthThreshold}";

        return new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.HideAttempt,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            SkillName = skillLabel,
            SkillValue = stealthThreshold,
            Roll = actualRoll,
            SuccessLevel = stealthLevel,
            IsSuccess = hidden,
            BonusDiceUsed = bonusDice,
            ActorMovementActionsSpent = 1,
            RemovesActorFromChase = hidden,
            LocationBefore = participant.CurrentLocation,
            LocationAfter = participant.CurrentLocation,
            Summary = $"{participant.Name} пытается спрятаться ({skillLabel}) — бросок {actualRoll} " +
                      $"против {thresholdLabel} " +
                      (bonusDice > 0 ? $"(бонусных костей: {bonusDice}) " : "") +
                      (hidden
                          ? "— укрылся! Преследователю нужна проверка Внимания, чтобы найти."
                          : combined && stealthOk
                              ? "— Вождения не хватило: заметили, как он свернул в укрытие."
                              : "— не вышло, укрытие не найдено.")
        };
    }

    // ───────────────────── Персонажи создают помехи (стр. 141) ─────────────────────

    /// <summary>
    /// Заблокировать локацию помехой или преградой: запереть дверь, придвинуть шкаф,
    /// опрокинуть прилавок. Обычно стоит действие перемещения и часто требует проверки навыка.
    /// </summary>
    public ChaseActionResult CreateObstacle(Guid participantId, int locationNumber, ChaseLocation obstacle,
        string description, int movementActionCost, string? skillName, int skillValue, int? roll)
    {
        var participant = Participants.First(p => p.Id == participantId);
        var needsCheck = !string.IsNullOrWhiteSpace(skillName) && skillValue > 0;

        var actualRoll = needsCheck ? roll ?? CombatService.RollD100() : 0;
        var level = needsCheck
            ? CombatService.CalculateSuccessLevel(actualRoll, skillValue)
            : SuccessLevel.RegularSuccess;
        var success = level >= SuccessLevel.RegularSuccess;

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.CreateObstacle,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            SkillName = needsCheck ? skillName : null,
            SkillValue = needsCheck ? skillValue : 0,
            Roll = actualRoll,
            SuccessLevel = level,
            IsSuccess = success,
            ActorMovementActionsSpent = Math.Max(0, movementActionCost),
            LocationBefore = participant.CurrentLocation,
            LocationAfter = participant.CurrentLocation
        };

        if (success)
        {
            result.ObstacleToPlace = obstacle;
            result.ObstacleLocation = locationNumber;
            result.Summary = $"{participant.Name}: {description} — локация {locationNumber} перекрыта." +
                             (needsCheck
                                 ? $" Проверка {skillName}: {actualRoll} против {skillValue} " +
                                   $"({CombatService.GetSuccessLevelText(level)})."
                                 : " Проверка не требуется.");
        }
        else
        {
            result.Summary = $"{participant.Name}: {description} — не вышло. " +
                             $"Проверка {skillName}: {actualRoll} против {skillValue} " +
                             $"({CombatService.GetSuccessLevelText(level)}).";
        }

        return result;
    }

    // ───────────────────── Смена способа передвижения (стр. 141) ─────────────────────

    /// <summary>
    /// Пеший переход между мускульными способами не требует новой проверки скорости —
    /// меняется только СКО. Переход к вождению и обратно требует новой проверки.
    /// </summary>
    public ChaseActionResult ChangeMovementMode(Guid participantId, MovementMode mode, int nativeModeSpeed)
    {
        var participant = Participants.First(p => p.Id == participantId);

        var modeName = mode switch
        {
            MovementMode.Swimming => "вплавь",
            MovementMode.Flying => "по воздуху",
            _ => "пешком"
        };

        var previousMov = participant.AdjustedMov;
        participant.Mode = mode;
        // Хранитель не вписал свою СКО — берём ту, что книга указала у существа
        // («6 / полёт 20»), иначе останется половина обычной (стр. 141).
        participant.NativeModeSpeed = nativeModeSpeed > 0
            ? nativeModeSpeed
            : participant.NativeSpeedFor(mode) ?? 0;
        var newMov = participant.AdjustedMov;

        RecalculateChaseSpeeds();

        var result = new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.ModeChange,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            IsSuccess = true,
            IsApplied = true,
            LocationBefore = participant.CurrentLocation,
            LocationAfter = participant.CurrentLocation,
            Summary = $"{participant.Name} двигается {modeName}: СКО {previousMov} → {newMov}. " +
                      (nativeModeSpeed > 0
                          ? "Использована собственная СКО для этого способа."
                          : "Своей СКО для способа нет — половина обычной.") +
                      " Проверка скорости не нужна: мускульная сила остаётся мускульной."
        };

        ChaseLog.Insert(0, result);
        NotifyStateChanged();
        return result;
    }

    // ───────────────────── Присоединение к погоне (стр. 140) ─────────────────────

    /// <summary>
    /// Новый участник проходит проверку скорости и встаёт в порядок ходов. Преследователь со СКО
    /// ниже самого медленного убегающего в погоне не учитывается. Если присоединился более медленный
    /// убегающий, действия перемещения пересчитываются для всех.
    /// </summary>
    public void JoinChase(ChaseParticipant participant, int location)
    {
        participant.CurrentLocation = Math.Clamp(location, 1, Math.Max(1, Locations.Count));
        Participants.Add(participant);

        Participants = Participants.OrderByDescending(p => p.Dexterity).ToList();
        RecalculateChaseSpeeds();

        ChaseLog.Insert(0, new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.JoinChase,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            IsSuccess = !participant.IsOutOfChase,
            IsApplied = true,
            LocationAfter = participant.CurrentLocation,
            Summary = participant.IsOutOfChase
                ? $"{participant.Name} (СКО {participant.AdjustedMov}) слишком медленный для этой погони — " +
                  "в ней не учитывается."
                : $"{participant.Name} присоединяется к погоне на локации {participant.CurrentLocation} " +
                  $"со СКО {participant.AdjustedMov}. Действия перемещения пересчитаны."
        });

        NotifyStateChanged();
    }

    // ───────────────────── Скорость погони (стр. 140) ─────────────────────

    /// <summary>
    /// Пересчитать самую медленную СКО и действия перемещения. Преследователи медленнее самого
    /// медленного убегающего выбывают из расчёта: «Если СКО нового участника меньше, его не учитывают».
    /// Пассажиры в расчёте скорости не участвуют вовсе.
    /// </summary>
    public void RecalculateChaseSpeeds()
    {
        var contenders = Participants
            .Where(p => !p.IsPassenger && !p.IsEliminated && !p.HasEscaped && !p.IsCaught)
            .ToList();

        var slowestPrey = contenders
            .Where(p => p.Role == ChaseRole.Prey)
            .Select(p => p.AdjustedMov)
            .DefaultIfEmpty(0)
            .Min();

        foreach (var pursuer in contenders.Where(p => p.Role == ChaseRole.Pursuer))
            pursuer.IsOutOfChase = slowestPrey > 0 && pursuer.AdjustedMov < slowestPrey;

        MinAdjustedMov = contenders
            .Where(p => p.IsActive)
            .Select(p => p.AdjustedMov)
            .DefaultIfEmpty(1)
            .Min();

        CalculateMovementActions();
        NotifyStateChanged();
    }

    // ───────────────────── Чудовища без навыков (стр. 142) ─────────────────────

    /// <summary>
    /// У чудовищ и персонажей Хранителя часто нет Прыжков, Плавания или Вождения. Тогда навык
    /// заменяют на ЛВК (способность есть), половину ЛВК (неясно) или пятую часть (способности нет).
    /// </summary>
    public static int SubstituteSkillFromDexterity(int dexterity, SkillAptitude aptitude) => aptitude switch
    {
        SkillAptitude.Capable => dexterity,
        SkillAptitude.Unlikely => dexterity / 5,
        _ => dexterity / 2
    };

    // ───────────────────── Бегство с места событий (стр. 142) ─────────────────────

    /// <summary>
    /// Сколько локаций участник покроет за указанное число раундов при текущей скорости —
    /// правилами погони можно отсчитывать и обычное бегство со сцены.
    /// </summary>
    public int GetLocationsCoveredIn(Guid participantId, int rounds)
    {
        var participant = Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null || rounds <= 0) return 0;

        var perRound = Math.Max(0, 1 + (participant.AdjustedMov - MinAdjustedMov));
        return perRound * rounds;
    }

    // ───────────────────── Порядок ходов (стр. 131–132) ─────────────────────

    /// <summary>
    /// Встречная проверка ЛВК при равной инициативе: победитель ходит первым.
    /// Возвращает true, если первым идёт <paramref name="firstId"/>.
    /// </summary>
    public bool ResolveDexterityTie(Guid firstId, Guid secondId, int? firstRoll, int? secondRoll)
    {
        var first = Participants.First(p => p.Id == firstId);
        var second = Participants.First(p => p.Id == secondId);

        var rollA = firstRoll ?? CombatService.RollD100();
        var rollB = secondRoll ?? CombatService.RollD100();
        var levelA = CombatService.CalculateSuccessLevel(rollA, first.Dexterity);
        var levelB = CombatService.CalculateSuccessLevel(rollB, second.Dexterity);

        var firstWins = levelA > levelB || (levelA == levelB && rollA <= rollB);
        var winner = firstWins ? first : second;

        if (!firstWins)
        {
            var indexA = Participants.IndexOf(first);
            var indexB = Participants.IndexOf(second);
            Participants[indexA] = second;
            Participants[indexB] = first;
        }

        ChaseLog.Insert(0, new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.Other,
            ParticipantId = winner.Id,
            ParticipantName = winner.Name,
            SkillName = "ЛВК (встречная проверка)",
            Roll = firstWins ? rollA : rollB,
            IsSuccess = true,
            IsApplied = true,
            Summary = $"Равная ЛВК: встречная проверка — {first.Name} {rollA} против {first.Dexterity}, " +
                      $"{second.Name} {rollB} против {second.Dexterity}. Первым ходит {winner.Name}."
        });

        NotifyStateChanged();
        return firstWins;
    }

    /// <summary>
    /// Отложить действие (стр. 132): участник уступает очередь и ходит после другого.
    /// Если уступают все, раунд просто заканчивается.
    /// </summary>
    public void DelayAction(Guid participantId)
    {
        var active = Participants.Where(p => p.IsActive).ToList();
        var participant = Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null || active.Count < 2) return;

        var index = active.IndexOf(participant);
        if (index < 0 || index >= active.Count - 1)
        {
            // Уступать больше некому — ход просто заканчивается
            NextTurn();
            return;
        }

        // Переставить участника на одну позицию ниже в порядке ходов этого раунда
        var globalIndex = Participants.IndexOf(participant);
        var nextActive = active[index + 1];
        var nextGlobalIndex = Participants.IndexOf(nextActive);

        Participants[globalIndex] = nextActive;
        Participants[nextGlobalIndex] = participant;

        ChaseLog.Insert(0, new ChaseActionResult
        {
            Round = CurrentRound,
            ActionType = ChaseActionType.Other,
            ParticipantId = participant.Id,
            ParticipantName = participant.Name,
            IsSuccess = true,
            IsApplied = true,
            Summary = $"{participant.Name} откладывает действие и ходит после {nextActive.Name}."
        });

        NotifyStateChanged();
    }

    // ───────────────────── Разделение погони (стр. 142) ─────────────────────

    /// <summary>Пометить участника маршрутом: разные метки — разные, независимо отслеживаемые погони.</summary>
    public void SetRoute(Guid participantId, string? routeLabel)
    {
        var participant = Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null) return;

        participant.RouteLabel = string.IsNullOrWhiteSpace(routeLabel) ? null : routeLabel.Trim();
        NotifyStateChanged();
    }

}

/// <summary>Насколько существу подходит навык, которого у него нет (стр. 142).</summary>
public enum SkillAptitude
{
    /// <summary>Способность подразумевается — берём полную ЛВК.</summary>
    Capable,

    /// <summary>Точно определить нельзя — половина ЛВК.</summary>
    Uncertain,

    /// <summary>Способности заведомо нет — пятая часть ЛВК или автопровал.</summary>
    Unlikely
}
