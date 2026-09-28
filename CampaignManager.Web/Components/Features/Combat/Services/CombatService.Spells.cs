using CampaignManager.Web.Components.Features.Characters.Services;
using CampaignManager.Web.Components.Features.Combat.Model;

namespace CampaignManager.Web.Components.Features.Combat.Services;

/// <summary>
/// Сотворение заклинаний в бою: гл. 9 «Магия» (цена, первое сотворение, повторная проверка,
/// сорванное сотворение — стр. 174–177) и «Гримуар» (встречная проверка МОЩ и время сотворения
/// в бою — стр. 241).
/// <para>
/// Долгое сотворение лежит на самом участнике (<see cref="Combatant.CastingSpell" />), поэтому
/// переживает паузу circuit вместе со списком участников и отдельного поля в
/// <see cref="CombatSnapshot" /> не требует.
/// </para>
/// </summary>
public sealed partial class CombatService
{
    /// <summary>Разница МОЩ, при которой встречную проверку не бросают (стр. 241).</summary>
    public const int AutomaticPowerGap = 100;

    /// <summary>
    /// МОЩ участника. У снятых со старого снапшота поле пустое — тогда берём из листа или статблока.
    /// </summary>
    public static int GetPower(Combatant combatant) =>
        combatant.Power > 0
            ? combatant.Power
            : combatant.CharacterSource?.Characteristics.Power.Regular
              ?? combatant.CreatureSource?.CreatureCharacteristics.Power.Value
              ?? 0;

    /// <summary>
    /// Теряет ли участник рассудок. У чудовищ рассудка нет (у участника-существа в поле
    /// рассудка лежит МОЩ), поэтому цену заклинания в рассудке с них не берём.
    /// </summary>
    public static bool HasSanity(Combatant combatant) => combatant.CharacterSource is not null;

    /// <summary>
    /// Раунд, в котором заклинание сработает: мгновенное и однораундовое — в этом же раунде,
    /// двухраундовое — в следующем на ЛВК заклинателя и так далее (стр. 241).
    /// </summary>
    public int SpellCompletionRound(int castingRounds) => CurrentRound + Math.Max(0, castingRounds - 1);

    /// <summary>
    /// Встречная проверка МОЩ (стр. 241): лучший уровень успеха побеждает, при равенстве —
    /// у кого выше МОЩ, при равной МОЩ Хранитель решает, как задело обоих. Разница МОЩ в 100
    /// и больше решает дело без броска.
    /// </summary>
    public static SpellResistance ResolveOpposedPower(
        SuccessLevel casterLevel, int casterPower, SuccessLevel targetLevel, int targetPower)
    {
        if (casterPower - targetPower >= AutomaticPowerGap) return SpellResistance.CasterWins;
        if (targetPower - casterPower >= AutomaticPowerGap) return SpellResistance.TargetWins;

        if (casterLevel != targetLevel)
            return casterLevel > targetLevel ? SpellResistance.CasterWins : SpellResistance.TargetWins;

        if (casterPower != targetPower)
            return casterPower > targetPower ? SpellResistance.CasterWins : SpellResistance.TargetWins;

        return SpellResistance.Both;
    }

    /// <summary>
    /// Начинает долгое сотворение (2 раунда и больше). Бросать пока нечего и платить тоже:
    /// цена уйдёт, когда заклинание сработает или сорвётся, поэтому запись сразу ложится в журнал
    /// без предпросмотра.
    /// </summary>
    public void StartSpellcasting(SpellCastSetup setup)
    {
        var caster = Combatants.First(c => c.Id == setup.CasterId);
        var completes = SpellCompletionRound(setup.CastingRounds);
        var target = setup.TargetId is { } targetId ? Combatants.FirstOrDefault(c => c.Id == targetId) : null;

        caster.CastingSpell = new SpellcastInProgress
        {
            Setup = setup.WithoutRolls(),
            StartedRound = CurrentRound,
            CompletesInRound = completes
        };
        caster.HasActedThisRound = true;

        CombatLog.Insert(0, new CombatActionResult
        {
            Round = CurrentRound,
            AttackerId = caster.Id,
            AttackerName = caster.Name,
            ActionType = CombatActionType.CastSpell,
            WeaponName = setup.SpellName,
            DefenderId = target?.Id,
            DefenderName = target?.Name ?? string.Empty,
            Spell = new SpellCastOutcome
            {
                SpellName = setup.SpellName,
                Phase = SpellCastPhase.Started,
                CostText = setup.CostText,
                CastingTimeText = setup.CastingTimeText,
                CastingRounds = setup.CastingRounds,
                CompletesInRound = completes
            },
            Summary = $"{caster.Name} начинает творить «{setup.SpellName}»" +
                      (target is null ? "" : $" на {target.Name}") +
                      $" — {setup.CastingRounds} {RoundsWord(setup.CastingRounds)}, сработает в раунде {completes} на ЛВК заклинателя. " +
                      "Пока он занят, удар или выстрел сорвут сотворение (стр. 177, 241)."
        });

        NotifyStateChanged();
    }

    /// <summary>
    /// Сотворение, которое срабатывает сейчас: мгновенное, однораундовое или завершение долгого.
    /// Платит цену, проводит проверку первого сотворения и встречную проверку МОЩ. Возвращает
    /// результат для предпросмотра — к участникам его применяет <see cref="ApplyResult" />.
    /// </summary>
    public CombatActionResult ResolveSpellCast(SpellCastSetup setup)
    {
        var caster = Combatants.First(c => c.Id == setup.CasterId);
        var target = setup.TargetId is { } targetId ? Combatants.FirstOrDefault(c => c.Id == targetId) : null;
        var (result, outcome) = NewSpellResult(caster, target, setup, SpellCastPhase.Cast);
        var notes = new List<string>();

        // 1. Первое сотворение сыщиком — трудная проверка МОЩ (стр. 176)
        var takesEffect = true;
        var multiplier = 1;
        if (setup.IsFirstCast)
        {
            var power = GetPower(caster);
            var roll = setup.ManualCastingRoll ?? RollD100();
            var level = CalculateSuccessLevel(roll, power);

            outcome.IsFirstCast = true;
            outcome.IsPushed = setup.IsPushed;
            outcome.CastingRoll = roll;
            outcome.CastingPower = power;
            outcome.CastingLevel = level;
            outcome.CastingPassed = level >= SuccessLevel.HardSuccess;

            if (outcome.CastingPassed)
            {
                notes.Add($"Первое сотворение: {roll} против МОЩ {power} — {GetSuccessLevelText(level)}, " +
                          "трудная проверка пройдена, дальше это заклинание творится без проверки.");
            }
            else if (setup.IsPushed)
            {
                // Провал повторной: заклинание всё равно срабатывает, но цена ещё ×1d6 (стр. 176)
                var extra = setup.ManualPushMultiplier ?? RollDice(6);
                outcome.PushMultiplier = extra;
                multiplier += extra;
                notes.Add($"Повторная проверка провалена ({roll} против МОЩ {power}, нужен трудный успех): " +
                          $"заклинание срабатывает, но цена уплачена ещё ×{extra} (1d6). " +
                          "Хранитель выбирает побочный эффект по списку книги (стр. 176).");
            }
            else
            {
                takesEffect = false;
                notes.Add($"Первое сотворение: {roll} против МОЩ {power} — {GetSuccessLevelText(level)}, " +
                          "трудная проверка провалена: ничего не происходит, цена уплачена. " +
                          "Новая попытка — повторная проверка (стр. 176).");
            }
        }

        outcome.TakesEffect = takesEffect;

        // 2. Цена — платится при любом исходе проверки
        PaySpellCost(caster, setup, multiplier, includePowerAndHitPoints: true, result, outcome, notes);

        // 3. Сопротивление цели — встречная проверка МОЩ (стр. 241)
        if (takesEffect && target is not null && setup.TargetResists)
        {
            ResolveSpellResistance(caster, target, setup, result, outcome, notes);
        }
        else
        {
            result.AttackerWins = takesEffect;
            outcome.Resistance = SpellResistance.NotResisted;
        }

        if (setup.CastingRounds == 0 && takesEffect)
            notes.Add("Мгновенное заклинание срабатывает на ЛВК+50, как огнестрельное на изготовку (стр. 241).");

        result.Summary = BuildSpellSummary(caster, target, result, outcome, notes);
        return result;
    }

    /// <summary>
    /// Срывает долгое сотворение: эффекта нет, но ПМ и рассудок уплачены (стр. 177). МОЩ и явная
    /// цена в ПЗ не берутся — книга называет только эти две.
    /// </summary>
    public CombatActionResult ResolveSpellInterruption(Combatant caster, int? manualIntRoll, int? manualDurationRoll, int? manualConRoll)
    {
        var casting = caster.CastingSpell
                      ?? throw new InvalidOperationException($"{caster.Name} ничего не творит.");

        var setup = casting.Setup.WithoutRolls();
        setup.ManualIntRoll = manualIntRoll;
        setup.ManualInsanityDurationRoll = manualDurationRoll;
        setup.ManualMajorWoundConRoll = manualConRoll;

        var target = setup.TargetId is { } targetId ? Combatants.FirstOrDefault(c => c.Id == targetId) : null;
        var (result, outcome) = NewSpellResult(caster, target, setup, SpellCastPhase.Interrupted);
        var notes = new List<string>();

        outcome.TakesEffect = false;
        result.AttackerWins = false;
        PaySpellCost(caster, setup, 1, includePowerAndHitPoints: false, result, outcome, notes);
        notes.Add("Последствия срыва Хранитель может взять из списка побочных эффектов повторной проверки (стр. 176–177).");

        result.Summary = BuildSpellSummary(caster, target, result, outcome, notes);
        return result;
    }

    /// <summary>Переносит цену заклинания на заклинателя; вызывается из <see cref="ApplyResult" />.</summary>
    private void ApplySpellCast(CombatActionResult result)
    {
        if (result.Spell is not { } outcome || outcome.Phase == SpellCastPhase.Started)
            return;

        var caster = Combatants.FirstOrDefault(c => c.Id == result.AttackerId);
        if (caster is null)
            return;

        caster.CurrentMagicPoints = outcome.MagicPointsAfter;

        if (outcome.PowerPaid > 0)
            caster.Power = outcome.PowerAfter;

        if (outcome.HitPointsPaid > 0)
        {
            caster.CurrentHitPoints = result.AttackerHpAfter;
            if (result.AttackerFallsProne) caster.IsProne = true;
            if (result.AttackerKnockedUnconscious) caster.IsUnconscious = true;
            if (result.AttackerTriggeredMajorWound) caster.HasMajorWound = true;
            if (result.AttackerDying) caster.IsDying = true;
            if (result.AttackerDead) caster.IsDead = true;

            caster.FirstAidAttempted = false;
            caster.IsStabilized = false;
            caster.TemporaryHitPoints = 0;
            caster.IsAiming = false;
        }

        if (outcome.SanityPaid > 0)
            ApplySanityOutcome(caster, result);

        caster.CastingSpell = null;
        caster.HasActedThisRound = true;
    }

    /// <summary>
    /// Помечает сотворение сорванным, если заклинателя ранили (стр. 177). Цену сразу не берём:
    /// в ней может быть проверка ИНТ, и провести её Хранитель должен сам во вкладке заклинания.
    /// </summary>
    private static void DisruptSpellcasting(Combatant victim, CombatActionResult result)
    {
        if (victim.CastingSpell is not { Disrupted: false } casting)
            return;

        casting.Disrupted = true;
        result.Summary += $" {victim.Name} ранен посреди сотворения «{casting.Setup.SpellName}» — оно сорвано, " +
                          "ПМ и рассудок всё равно платятся (стр. 177): вкладка «Заклинание».";
    }

    private (CombatActionResult Result, SpellCastOutcome Outcome) NewSpellResult(
        Combatant caster, Combatant? target, SpellCastSetup setup, SpellCastPhase phase)
    {
        var outcome = new SpellCastOutcome
        {
            SpellName = setup.SpellName,
            Phase = phase,
            CostText = setup.CostText,
            CastingTimeText = setup.CastingTimeText,
            CastingRounds = setup.CastingRounds,
            CompletesInRound = CurrentRound,
            TargetResists = setup.TargetResists
        };

        var result = new CombatActionResult
        {
            Round = CurrentRound,
            AttackerId = caster.Id,
            AttackerName = caster.Name,
            ActionType = CombatActionType.CastSpell,
            WeaponName = setup.SpellName,
            DefenderId = target?.Id,
            DefenderName = target?.Name ?? string.Empty,
            AttackerHpBefore = caster.CurrentHitPoints,
            AttackerHpAfter = caster.CurrentHitPoints,
            Spell = outcome
        };

        return (result, outcome);
    }

    /// <summary>
    /// Списывает цену: ПМ, а их нехватку — с ПЗ (стр. 174); явную цену в ПЗ; МОЩ навсегда;
    /// рассудок — с проверкой ИНТ и порогами безумия, как у проверки Рассудка.
    /// </summary>
    private static void PaySpellCost(
        Combatant caster, SpellCastSetup setup, int multiplier, bool includePowerAndHitPoints,
        CombatActionResult result, SpellCastOutcome outcome, List<string> notes)
    {
        var magicCost = Math.Max(0, setup.MagicPointsCost) * multiplier;
        var sanityCost = Math.Max(0, setup.SanityCost) * multiplier;
        var powerCost = includePowerAndHitPoints ? Math.Max(0, setup.PowerCost) * multiplier : 0;
        var hitPointsCost = includePowerAndHitPoints ? Math.Max(0, setup.HitPointsCost) * multiplier : 0;

        // ПМ, нехватка — из ПЗ
        outcome.MagicPointsBefore = caster.CurrentMagicPoints;
        outcome.MagicPointsPaid = Math.Min(magicCost, Math.Max(0, caster.CurrentMagicPoints));
        outcome.MagicPointsShortfall = magicCost - outcome.MagicPointsPaid;
        outcome.MagicPointsAfter = caster.CurrentMagicPoints - outcome.MagicPointsPaid;

        if (outcome.MagicPointsShortfall > 0)
            notes.Add($"Не хватило {outcome.MagicPointsShortfall} ПМ — они сняты с ПЗ: раны, язвы, кровь из глаз и ушей (стр. 174).");

        var hitPointsLoss = outcome.MagicPointsShortfall + hitPointsCost;
        if (hitPointsLoss > 0)
            PayHitPoints(caster, hitPointsLoss, setup.ManualMajorWoundConRoll, result, outcome, notes);

        // МОЩ — навсегда
        outcome.PowerBefore = GetPower(caster);
        outcome.PowerPaid = powerCost;
        outcome.PowerAfter = Math.Max(0, outcome.PowerBefore - powerCost);
        if (powerCost > 0)
            notes.Add($"МОЩ {outcome.PowerBefore}→{outcome.PowerAfter} навсегда; максимум ПМ (⅕ МОЩ) на листе поправьте сами.");

        // Рассудок
        if (sanityCost > 0)
        {
            if (HasSanity(caster))
            {
                result.SanityBefore = caster.CurrentSanity;
                EvaluateSanityLoss(caster, sanityCost, setup.ManualIntRoll, setup.ManualInsanityDurationRoll, result, notes);
                outcome.SanityPaid = result.SanityLoss ?? 0;
            }
            else
            {
                outcome.SanitySkipped = true;
            }
        }
    }

    /// <summary>
    /// Снимает ПЗ за магию. Порог серьёзной раны — общий <see cref="WoundRules" />: расплата
    /// за нехватку ПМ ранит так же, как удар (пример с Мэтью, стр. 176).
    /// </summary>
    private static void PayHitPoints(
        Combatant caster, int loss, int? manualConRoll,
        CombatActionResult result, SpellCastOutcome outcome, List<string> notes)
    {
        outcome.HitPointsPaid = loss;
        var newHp = caster.CurrentHitPoints - loss;
        result.AttackerHpAfter = Math.Max(0, newHp);

        if (WoundRules.IsMajorWound(loss, caster.MaxHitPoints))
        {
            result.AttackerTriggeredMajorWound = true;
            result.AttackerFallsProne = true;
            result.AttackerMajorWoundConRoll = manualConRoll ?? RollD100();
            result.AttackerMajorWoundConRollSuccess = result.AttackerMajorWoundConRoll <= caster.ConstitutionValue;
            if (!result.AttackerMajorWoundConRollSuccess)
                result.AttackerKnockedUnconscious = true;

            notes.Add($"Серьёзная рана от расплаты ПЗ: проверка ВЫН {result.AttackerMajorWoundConRoll}/{caster.ConstitutionValue} — " +
                      (result.AttackerMajorWoundConRollSuccess ? "успех, в сознании." : "провал, без сознания."));
        }

        if (newHp <= 0)
        {
            result.AttackerHpAfter = 0;
            if (caster.HasMajorWound || result.AttackerTriggeredMajorWound)
                result.AttackerDying = true;
            else
                result.AttackerKnockedUnconscious = true;
        }
    }

    private static void ResolveSpellResistance(
        Combatant caster, Combatant target, SpellCastSetup setup,
        CombatActionResult result, SpellCastOutcome outcome, List<string> notes)
    {
        var casterPower = GetPower(caster);
        var targetPower = GetPower(target);
        outcome.CasterPower = casterPower;
        outcome.TargetPower = targetPower;
        result.AttackerSkillValue = casterPower;
        result.DefenderSkillValue = targetPower;

        if (Math.Abs(casterPower - targetPower) >= AutomaticPowerGap)
        {
            outcome.ResistanceAutomatic = true;
            outcome.Resistance = ResolveOpposedPower(SuccessLevel.Failure, casterPower, SuccessLevel.Failure, targetPower);
        }
        else
        {
            result.AttackerRoll = setup.ManualCasterPowerRoll ?? RollD100();
            result.AttackerRollDetail = DiceRollResult.Plain(result.AttackerRoll);
            result.AttackerSuccessLevel = CalculateSuccessLevel(result.AttackerRoll, casterPower);

            result.DefenderRoll = setup.ManualTargetPowerRoll ?? RollD100();
            result.DefenderRollDetail = DiceRollResult.Plain(result.DefenderRoll);
            result.DefenderSuccessLevel = CalculateSuccessLevel(result.DefenderRoll, targetPower);

            outcome.Resistance = ResolveOpposedPower(
                result.AttackerSuccessLevel, casterPower, result.DefenderSuccessLevel, targetPower);
        }

        result.AttackerWins = outcome.Resistance is SpellResistance.CasterWins or SpellResistance.Both;

        var rolls = outcome.ResistanceAutomatic
            ? $"разница МОЩ {casterPower} и {targetPower} не меньше {AutomaticPowerGap} — без броска"
            : $"{caster.Name} {result.AttackerRoll}/{casterPower} — {GetSuccessLevelText(result.AttackerSuccessLevel)}, " +
              $"{target.Name} {result.DefenderRoll}/{targetPower} — {GetSuccessLevelText(result.DefenderSuccessLevel)}";

        notes.Add(outcome.Resistance switch
        {
            SpellResistance.CasterWins =>
                $"Встречная проверка МОЩ ({rolls}): {target.Name} не устоял. " +
                "Победа даёт заклинателю проверку повышения МОЩ: 1d100 выше МОЩ или 96+ → +1d10 (стр. 177–178).",
            SpellResistance.TargetWins => $"Встречная проверка МОЩ ({rolls}): {target.Name} устоял — заклинание его не взяло.",
            _ => $"Встречная проверка МОЩ ({rolls}): ничья при равной МОЩ — Хранитель решает, как заклинание задело обоих."
        });
    }

    private static string BuildSpellSummary(
        Combatant caster, Combatant? target, CombatActionResult result, SpellCastOutcome outcome, List<string> notes)
    {
        var head = outcome.Phase == SpellCastPhase.Interrupted
            ? $"{caster.Name}: сотворение «{outcome.SpellName}» сорвано — эффекта нет."
            : $"{caster.Name} творит «{outcome.SpellName}»{(target is null ? "" : $" на {target.Name}")}: " +
              (!outcome.TakesEffect ? "не сработало." : result.AttackerWins ? "сработало." : "цель устояла.");

        var cost = new List<string>();
        if (outcome.MagicPointsPaid > 0) cost.Add($"ПМ {outcome.MagicPointsBefore}→{outcome.MagicPointsAfter}");
        if (outcome.HitPointsPaid > 0) cost.Add($"ПЗ {result.AttackerHpBefore}→{result.AttackerHpAfter}");
        if (outcome.SanityPaid > 0) cost.Add($"РАС {result.SanityBefore}→{result.SanityAfter}");
        if (outcome.PowerPaid > 0) cost.Add($"МОЩ {outcome.PowerBefore}→{outcome.PowerAfter}");
        var costLine = cost.Count > 0 ? $" Цена: {string.Join(", ", cost)}." : " Цены нет.";
        if (outcome.SanitySkipped) costLine += " Рассудка у существа нет — эта часть цены пропущена.";

        return head + costLine + (notes.Count > 0 ? " " + string.Join(" ", notes) : "");
    }

    private static string RoundsWord(int rounds) => (rounds % 100) switch
    {
        >= 11 and <= 14 => "раундов",
        _ => (rounds % 10) switch
        {
            1 => "раунд",
            >= 2 and <= 4 => "раунда",
            _ => "раундов"
        }
    };
}
