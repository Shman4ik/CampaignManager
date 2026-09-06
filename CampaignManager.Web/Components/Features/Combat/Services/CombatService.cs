using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Weapons.Model;
using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Combat.Services;

/// <summary>
/// Сервис управления боевыми столкновениями по правилам Call of Cthulhu 7e (Глава 6)
/// </summary>
public sealed partial class CombatService
{
    public List<Combatant> Combatants { get; private set; } = [];
    public int CurrentRound { get; private set; } = 1;
    public int CurrentTurnIndex { get; private set; }

    public List<CombatActionResult> CombatLog { get; private set; } = [];
    public CombatActionResult? PendingResult { get; private set; }
    public Guid? SelectedCampaignId { get; private set; }

    public event Action? OnChange;

    // ───────────────────── Управление участниками ─────────────────────

    public void AddCombatant(Combatant combatant)
    {
        Combatants.Add(combatant);
        NotifyStateChanged();
    }

    public void RemoveCombatant(Combatant combatant)
    {
        Combatants.Remove(combatant);
        if (CurrentTurnIndex >= Combatants.Count && Combatants.Count > 0)
            CurrentTurnIndex = 0;
        NotifyStateChanged();
    }

    /// <summary>
    /// Сортировка по инициативе. Учитывает +50 к ЛВК при огнестрельном оружии на изготовку.
    /// </summary>
    public void SortByInitiative()
    {
        Combatants = Combatants
            .OrderByDescending(c => c.HasFirearmReady ? c.Initiative + 50 : c.Initiative)
            .ThenByDescending(c => Math.Max(c.FightingSkill, c.DodgeSkill))
            .ToList();
        NotifyStateChanged();
    }

    public void NextTurn()
    {
        if (Combatants.Count == 0) return;

        CurrentTurnIndex++;
        if (CurrentTurnIndex >= Combatants.Count)
        {
            CurrentTurnIndex = 0;
            CurrentRound++;
            ResetRoundTracking();
        }

        NotifyStateChanged();
    }

    public void NextRound()
    {
        CurrentRound++;
        CurrentTurnIndex = 0;
        ResetRoundTracking();
        NotifyStateChanged();
    }

    /// <summary>Сброс флагов защиты и раунда для всех участников.</summary>
    private void ResetRoundTracking()
    {
        foreach (var c in Combatants)
        {
            c.HasDefendedThisRound = false;
            c.DefenseCountThisRound = 0;
            c.HasActedThisRound = false;
            c.IsDelayed = false;
            c.HasTakenCover = false;

            // Если укрывался — теряет атаку в этом раунде
            if (c.LostNextAttackFromCover)
                c.LostNextAttackFromCover = false;

            // Если прицеливался — флаг сохраняется до использования
        }
    }

    public Combatant? GetActiveCombatant()
    {
        if (Combatants.Count == 0) return null;
        if (CurrentTurnIndex >= Combatants.Count) return Combatants[0];
        return Combatants[CurrentTurnIndex];
    }

    /// <summary>
    /// Отложить ход: переместить бойца в конец текущего порядка инициативы.
    /// </summary>
    public void DelayTurn(Guid combatantId)
    {
        var combatant = Combatants.FirstOrDefault(c => c.Id == combatantId);
        if (combatant == null) return;

        combatant.IsDelayed = true;
        var idx = Combatants.IndexOf(combatant);
        Combatants.RemoveAt(idx);
        Combatants.Add(combatant);

        // Скорректировать CurrentTurnIndex если нужно
        if (idx <= CurrentTurnIndex && CurrentTurnIndex > 0)
            CurrentTurnIndex--;

        NotifyStateChanged();
    }

    /// <summary>
    /// Умирающие, которым нужна проверка ВЫН в конце раунда. Стабилизированные
    /// первой помощью сюда не входят: они проверяются раз в час (стр. 118).
    /// </summary>
    public List<Combatant> GetDyingCombatants() =>
        Combatants.Where(c => c.IsDying && !c.IsDead && !c.IsStabilized).ToList();

    public void SetCampaign(Guid campaignId)
    {
        SelectedCampaignId = campaignId;
        NotifyStateChanged();
    }

    public void ResetCombat()
    {
        Combatants.Clear();
        CurrentRound = 1;
        CurrentTurnIndex = 0;
        CombatLog.Clear();
        PendingResult = null;
        SelectedCampaignId = null;
        NotifyStateChanged();
    }

    // ───────────────────── Броски кубиков ─────────────────────

    public static int RollD100() => RandomNumberGenerator.GetInt32(1, 101);

    public static int RollDice(int sides) => RandomNumberGenerator.GetInt32(1, sides + 1);

    /// <summary>
    /// Бросок d100 с бонусными и штрафными костями (CoC 7e, стр. 89).
    /// <para>
    /// Бросается одна кость единиц и (1 + |нетто|) костей десятков. Из полученных
    /// вариантов берётся наименьший при бонусных костях и наибольший при штрафных.
    /// Одна бонусная кость отменяет одну штрафную. Комбинация «00» + «0» равна 100,
    /// поэтому выбор делается по итоговому результату, а не по значению кости десятков.
    /// </para>
    /// </summary>
    public static DiceRollResult RollD100(int bonusDice, int penaltyDice)
    {
        var net = Math.Max(0, bonusDice) - Math.Max(0, penaltyDice);
        var extraDice = Math.Abs(net);

        var units = RandomNumberGenerator.GetInt32(0, 10);

        var candidates = new int[extraDice + 1];
        for (var i = 0; i < candidates.Length; i++)
        {
            var tens = RandomNumberGenerator.GetInt32(0, 10) * 10;
            var value = tens + units;
            candidates[i] = value == 0 ? 100 : value;
        }

        var result = net switch
        {
            > 0 => candidates.Min(),
            < 0 => candidates.Max(),
            _ => candidates[0]
        };

        return new DiceRollResult
        {
            Result = result,
            Units = units,
            Candidates = candidates,
            BonusDice = net > 0 ? net : 0,
            PenaltyDice = net < 0 ? -net : 0
        };
    }

    /// <summary>
    /// Парсит и бросает формулу урона: "1D6", "2D6+2", "1D8+1D6", "0", "+1D4", "-1"
    /// </summary>
    public static int RollDiceFormula(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula)) return 0;

        var cleaned = formula.Trim().ToUpperInvariant().Replace(" ", "");
        if (cleaned == "0") return 0;

        var total = 0;
        var tokens = TokenizeDiceFormula(cleaned);

        foreach (var token in tokens)
        {
            var match = DicePattern().Match(token);
            if (match.Success)
            {
                var sign = match.Groups[1].Value == "-" ? -1 : 1;
                var count = string.IsNullOrEmpty(match.Groups[2].Value) ? 1 : int.Parse(match.Groups[2].Value);
                var sides = int.Parse(match.Groups[3].Value);

                for (var i = 0; i < count; i++)
                    total += sign * RollDice(sides);
            }
            else if (int.TryParse(token, out var flat))
            {
                total += flat;
            }
        }

        return total;
    }

    /// <summary>
    /// Максимизирует бросок по формуле (все кости на максимум).
    /// Используется при чрезвычайном/критическом успехе.
    /// </summary>
    public static int MaximizeDiceFormula(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula)) return 0;

        var cleaned = formula.Trim().ToUpperInvariant().Replace(" ", "");
        if (cleaned == "0") return 0;

        var total = 0;
        var tokens = TokenizeDiceFormula(cleaned);

        foreach (var token in tokens)
        {
            var match = DicePattern().Match(token);
            if (match.Success)
            {
                var sign = match.Groups[1].Value == "-" ? -1 : 1;
                var count = string.IsNullOrEmpty(match.Groups[2].Value) ? 1 : int.Parse(match.Groups[2].Value);
                var sides = int.Parse(match.Groups[3].Value);
                total += sign * count * sides;
            }
            else if (int.TryParse(token, out var flat))
            {
                total += flat;
            }
        }

        return total;
    }

    private static List<string> TokenizeDiceFormula(string formula)
    {
        var tokens = new List<string>();
        var current = "";

        for (var i = 0; i < formula.Length; i++)
        {
            var ch = formula[i];
            if ((ch == '+' || ch == '-') && i > 0)
            {
                if (!string.IsNullOrEmpty(current))
                    tokens.Add(current);
                current = ch.ToString();
            }
            else
            {
                current += ch;
            }
        }

        if (!string.IsNullOrEmpty(current))
            tokens.Add(current);

        return tokens;
    }

    [GeneratedRegex(@"^([+-]?)(\d*)D(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex DicePattern();

    /// <summary>
    /// Бросает структурированную формулу урона <see cref="DamageExpression"/>.
    /// Не включает Б.К.У. — его нужно добавлять отдельно через <see cref="RollDamageBonus"/>.
    /// </summary>
    public static int RollDamageExpression(DamageExpression expr)
    {
        var total = 0;

        foreach (var die in expr.Dice)
        {
            for (var i = 0; i < die.Count; i++)
                total += die.IsNegative ? -RollDice(die.Sides) : RollDice(die.Sides);
        }

        total += expr.FlatModifier;
        return total;
    }

    /// <summary>
    /// Максимизирует структурированную формулу урона (все кости на максимум).
    /// Используется при чрезвычайном/критическом успехе.
    /// </summary>
    public static int MaximizeDamageExpression(DamageExpression expr)
    {
        var total = 0;

        foreach (var die in expr.Dice)
            total += die.IsNegative ? -(die.Count * die.Sides) : die.Count * die.Sides;

        total += expr.FlatModifier;
        return total;
    }

    // ───────────────────── Правила CoC 7e ─────────────────────

    /// <summary>
    /// Рассчитывает уровень успеха по правилам CoC 7e (стр. 86–87).
    /// </summary>
    public static SuccessLevel CalculateSuccessLevel(int roll, int skillValue)
    {
        if (roll == 1) return SuccessLevel.CriticalSuccess;
        if (roll == 100) return SuccessLevel.Fumble;
        if (roll >= 96 && skillValue < 50) return SuccessLevel.Fumble;
        if (roll > skillValue) return SuccessLevel.Failure;
        if (skillValue >= 5 && roll <= skillValue / 5) return SuccessLevel.ExtremeSuccess;
        if (skillValue >= 2 && roll <= skillValue / 2) return SuccessLevel.HardSuccess;
        return SuccessLevel.RegularSuccess;
    }

    /// <summary>
    /// Уровень успеха, необходимый для попадания на данной дальности (CoC 7e, стр. 110).
    /// Дальность задаёт уровень сложности, а не урезает значение навыка.
    /// </summary>
    public static SuccessLevel GetRequiredLevelForRange(RangeLevel range) => range switch
    {
        RangeLevel.Long => SuccessLevel.HardSuccess,
        RangeLevel.Extreme => SuccessLevel.ExtremeSuccess,
        _ => SuccessLevel.RegularSuccess
    };

    /// <summary>
    /// Порог броска для данной дальности — то же число, что и уровень сложности,
    /// но в виде значения навыка. Нужно только для подписей в интерфейсе.
    /// </summary>
    public static int GetRollThresholdForRange(int baseSkill, RangeLevel range) => range switch
    {
        RangeLevel.Long => baseSkill / 2,
        RangeLevel.Extreme => baseSkill / 5,
        _ => baseSkill
    };

    /// <summary>Название уровня сложности для подписей: «трудный», «чрезвычайный».</summary>
    public static string GetDifficultyName(RangeLevel range) => range switch
    {
        RangeLevel.Long => "трудный",
        RangeLevel.Extreme => "чрезвычайный",
        _ => "обычный"
    };

    /// <summary>
    /// Определяет бросок: уже брошенный в панели используется как есть, ручной ввод
    /// Хранителя — как есть (кости он бросил сам), иначе бросается автоматически
    /// с учётом бонусных и штрафных костей.
    /// </summary>
    private static DiceRollResult RollFor(DiceRollResult? preRolled, int? manualRoll, int bonusDice, int penaltyDice)
    {
        if (preRolled is not null) return preRolled;
        return manualRoll.HasValue
            ? DiceRollResult.Plain(manualRoll.Value)
            : RollD100(bonusDice, penaltyDice);
    }

    /// <summary>
    /// Разрешение встречного броска. Возвращает true если атакующий побеждает.
    /// Правила ничьей (CoC 7e стр. 101):
    ///   — при уклонении: ничья = победа защитника
    ///   — при контратаке: ничья = победа атакующего
    /// </summary>
    public static bool ResolveOpposedRoll(
        SuccessLevel attackerLevel, int attackerSkill,
        SuccessLevel defenderLevel, int defenderSkill,
        CombatActionType defenderReaction)
    {
        if (attackerLevel > defenderLevel) return true;
        if (defenderLevel > attackerLevel) return false;

        // Ничья — победитель определяется типом реакции
        return defenderReaction == CombatActionType.FightBack;
    }

    // ───────────────────── Разрешение ближнего боя ─────────────────────

    /// <summary>
    /// Полное разрешение атаки ближнего боя по правилам CoC 7e.
    /// Поддерживает ручной ввод бросков через <see cref="AttackSetup"/>.
    /// </summary>
    public CombatActionResult ResolveMeleeAttack(AttackSetup setup)
    {
        var attacker = Combatants.First(c => c.Id == setup.AttackerId);
        var defender = Combatants.First(c => c.Id == setup.DefenderId);

        var result = new CombatActionResult
        {
            Round = CurrentRound,
            AttackerId = attacker.Id,
            AttackerName = attacker.Name,
            ActionType = CombatActionType.MeleeAttack,
            DefenderId = defender.Id,
            DefenderName = defender.Name,
            DefenderReaction = setup.DefenderReaction,
            DefenderHpBefore = defender.CurrentHitPoints,
            AttackerHpBefore = attacker.CurrentHitPoints,
            WeaponName = setup.SelectedWeapon?.Name ?? setup.CreatureAttackName ?? "Безоружная атака"
        };

        // Бонусные и штрафные кости атакующего (ручные + автоматические)
        var modifiers = CalculateAttackModifiers(setup, attacker, defender);
        result.Modifiers = modifiers;

        // 1. Бросок атакующего (ручной или авто)
        result.AttackerSkillValue = setup.AttackSkillValue;
        result.AttackerRollDetail = RollFor(setup.AttackerRollDetail, setup.ManualAttackerRoll, modifiers.BonusDice, modifiers.PenaltyDice);
        result.AttackerRoll = result.AttackerRollDetail.Result;
        result.AttackerSuccessLevel = CalculateSuccessLevel(result.AttackerRoll, result.AttackerSkillValue);

        // 2. Бросок защитника — только если цель начеку (стр. 104)
        result.DefenderSkillValue = setup.DefenderSkillValue;
        var surprise = NormalizeSurprise(setup);
        result.SurpriseMode = surprise;

        if (surprise == SurpriseMode.TargetReady)
        {
            result.DefenderRollDetail = RollFor(setup.DefenderRollDetail, setup.ManualDefenderRoll, setup.DefenderBonusDice, setup.DefenderPenaltyDice);
            result.DefenderRoll = result.DefenderRollDetail.Result;
            result.DefenderSuccessLevel = CalculateSuccessLevel(result.DefenderRoll, result.DefenderSkillValue);
        }
        else
        {
            // Цель не готова: она не уклоняется и не контратакует
            result.DefenderRoll = 0;
            result.DefenderSuccessLevel = SuccessLevel.Failure;
        }

        // 3. Автоматическое попадание: провалом считается только крах (стр. 105)
        if (surprise == SurpriseMode.AutoHit)
        {
            if (result.AttackerSuccessLevel == SuccessLevel.Fumble)
            {
                result.AttackerWins = false;
                result.DefenderHpAfter = defender.CurrentHitPoints;
                result.Summary = $"{attacker.Name}: крах при внезапной атаке ({result.AttackerRoll})" +
                                 $"{FormatRollDetail(result.AttackerRollDetail)} — даже застигнутая врасплох цель не пострадала.";
                return result;
            }

            result.AttackerWins = true;
            CalculateDamage(result, setup, attacker, defender);
            return result;
        }

        // 4. Атакующий провалил — промах
        if (result.AttackerSuccessLevel <= SuccessLevel.Failure)
        {
            result.AttackerWins = false;
            result.DefenderHpAfter = defender.CurrentHitPoints;
            result.Summary = $"{attacker.Name} промахивается ({result.AttackerRoll} против {result.AttackerSkillValue}){FormatRollDetail(result.AttackerRollDetail)}.";
            return result;
        }

        // 5. Определяем победителя встречного броска
        if (result.DefenderSuccessLevel <= SuccessLevel.Failure)
        {
            result.AttackerWins = true;
        }
        else
        {
            result.AttackerWins = ResolveOpposedRoll(
                result.AttackerSuccessLevel, result.AttackerSkillValue,
                result.DefenderSuccessLevel, result.DefenderSkillValue,
                setup.DefenderReaction);
        }

        // Численное превосходство считает только настоящие защитные действия (стр. 106)
        if (surprise == SurpriseMode.TargetReady)
        {
            defender.HasDefendedThisRound = true;
            defender.DefenseCountThisRound++;
        }

        // Если атакующий победил в рукопашной и цель в лежачем положении — сбрасываем прицеливание
        if (attacker.IsAiming) attacker.IsAiming = false;

        if (!result.AttackerWins)
        {
            result.DefenderHpAfter = defender.CurrentHitPoints;

            if (setup.DefenderReaction == CombatActionType.FightBack)
            {
                // Защитник победил при контратаке — он наносит урон атакующему
                result.Summary = $"{defender.Name} отбивает атаку и контратакует {attacker.Name}!";
                CalculateCounterAttackDamage(result, setup, defender, attacker);
            }
            else
            {
                result.Summary = $"{defender.Name} уклоняется от атаки {attacker.Name}.";
            }

            return result;
        }

        // 5. Атакующий победил — рассчитываем урон
        CalculateDamage(result, setup, attacker, defender);

        return result;
    }

    // ───────────────────── Модификаторы атаки (CoC 7e стр. 106, 110–113) ───

    /// <summary>
    /// Единый расчёт бонусных и штрафных костей для атаки. Используется и при
    /// предпросмотре в панели, и при разрешении атаки, чтобы они не расходились.
    /// К ручным костям Хранителя добавляются автоматические по правилам CoC 7e.
    /// Взаимное погашение выполняется в <see cref="RollD100(int, int)"/>.
    /// </summary>
    public static AttackModifiers CalculateAttackModifiers(AttackSetup setup, Combatant? attacker, Combatant? defender)
    {
        var reasons = new List<string>();
        var bonus = Math.Max(0, setup.BonusDice);
        var penalty = Math.Max(0, setup.PenaltyDice);

        if (bonus > 0) reasons.Add($"+{bonus} от Хранителя");
        if (penalty > 0) reasons.Add($"−{penalty} от Хранителя");

        // Цель не готова, но исход не предрешён — бонусная кость (стр. 105)
        if (NormalizeSurprise(setup) == SurpriseMode.BonusDie)
        {
            bonus++;
            reasons.Add("+1 цель застигнута врасплох");
        }

        if (setup.IsMelee)
        {
            // Поваленную цель проще пинать (стр. 113)
            if (defender?.IsProne == true)
            {
                bonus++;
                reasons.Add("+1 цель повалена");
            }

            // Численное превосходство (стр. 106): цель уже израсходовала защитные действия
            if (HasNumericalSuperiority(defender))
            {
                bonus++;
                reasons.Add("+1 численное превосходство");
            }
        }
        else
        {
            // ── Бонусные кости ──
            if (setup.IsPointBlank)
            {
                bonus++;
                reasons.Add("+1 стрельба в упор");
            }

            // Прицеливание учитывается один раз: из настройки или из состояния бойца
            if (setup.IsAiming || attacker?.IsAiming == true)
            {
                bonus++;
                reasons.Add("+1 прицеливание");
            }

            if (attacker?.IsProne == true)
            {
                bonus++;
                reasons.Add("+1 стрельба лёжа");
            }

            if (defender is { Build: >= 4 })
            {
                bonus++;
                reasons.Add("+1 крупная цель");
            }

            // ── Штрафные кости ──
            // Укрытие от огня учитывается один раз: из настройки или из состояния цели
            if (setup.IsTargetTakingCover || defender?.HasTakenCover == true)
            {
                penalty++;
                reasons.Add("−1 цель укрылась от огня");
            }

            if (setup.IsTargetBehindCover)
            {
                penalty++;
                reasons.Add("−1 частичное укрытие");
            }

            if (setup.IsTargetFastMoving)
            {
                penalty++;
                reasons.Add("−1 быстро движущаяся цель");
            }

            if (setup.IsFiringIntoMelee)
            {
                penalty++;
                reasons.Add("−1 стрельба в ближнем бою");
            }

            if (setup.IsMultipleShot)
            {
                penalty++;
                reasons.Add("−1 серия выстрелов");
            }

            if (setup.IsReloadAndFire)
            {
                penalty++;
                reasons.Add("−1 зарядка и выстрел");
            }

            if (defender is { Build: <= -2 })
            {
                penalty++;
                reasons.Add("−1 мелкая цель");
            }

            if (defender?.IsProne == true && !setup.IsPointBlank)
            {
                penalty++;
                reasons.Add("−1 цель лежит");
            }
        }

        return new AttackModifiers(bonus, penalty, reasons);
    }

    /// <summary>
    /// Приводит режим внезапной атаки к допустимому: для дистанционной атаки
    /// автоматическое попадание правилами не разрешено — «всегда нужно делать
    /// бросок на попадание» (стр. 105), поэтому он понижается до бонусной кости.
    /// </summary>
    public static SurpriseMode NormalizeSurprise(AttackSetup setup) =>
        setup is { IsMelee: false, SurpriseMode: SurpriseMode.AutoHit }
            ? SurpriseMode.BonusDie
            : setup.SurpriseMode;

    /// <summary>
    /// Численное превосходство (стр. 106): цель уже уклонялась или контратаковала
    /// столько раз, сколько у неё атак за раунд.
    /// </summary>
    public static bool HasNumericalSuperiority(Combatant? defender) =>
        defender is not null
        && defender.DefenseCountThisRound > 0
        && defender.DefenseCountThisRound >= Math.Max(1, defender.AttacksPerRound);

    // ───────────────────── Разрешение дальнего боя ─────────────────────

    /// <summary>
    /// Разрешение атаки из огнестрельного или метательного оружия.
    /// Стрельба без встречного броска. Сложность зависит от дальности.
    /// </summary>
    public CombatActionResult ResolveRangedAttack(AttackSetup setup)
    {
        var attacker = Combatants.First(c => c.Id == setup.AttackerId);
        var defender = Combatants.First(c => c.Id == setup.DefenderId);

        // Дальность задаёт уровень сложности, а не урезает навык (стр. 110)
        var requiredLevel = GetRequiredLevelForRange(setup.RangeLevel);

        var result = new CombatActionResult
        {
            Round = CurrentRound,
            AttackerId = attacker.Id,
            AttackerName = attacker.Name,
            ActionType = CombatActionType.RangedAttack,
            DefenderId = defender.Id,
            DefenderName = defender.Name,
            DefenderHpBefore = defender.CurrentHitPoints,
            AttackerHpBefore = attacker.CurrentHitPoints,
            WeaponName = setup.SelectedWeapon?.Name ?? setup.CreatureAttackName ?? "Дальняя атака"
        };

        // Бонусные и штрафные кости стрельбы (ручные + автоматические)
        var modifiers = CalculateAttackModifiers(setup, attacker, defender);
        result.Modifiers = modifiers;

        result.AttackerSkillValue = setup.AttackSkillValue;
        result.RequiredSuccessLevel = requiredLevel;
        result.RangeLevel = setup.RangeLevel;
        result.SurpriseMode = NormalizeSurprise(setup);
        result.AttackerRollDetail = RollFor(setup.AttackerRollDetail, setup.ManualAttackerRoll, modifiers.BonusDice, modifiers.PenaltyDice);
        result.AttackerRoll = result.AttackerRollDetail.Result;
        result.AttackerSuccessLevel = CalculateSuccessLevel(result.AttackerRoll, setup.AttackSkillValue);

        // Проверка осечки: бросок ≥ значения осечки → оружие заклинило (CoC 7e стр. 113)
        if (setup.SelectedWeapon != null
            && !string.IsNullOrWhiteSpace(setup.SelectedWeapon.Malfunction)
            && int.TryParse(setup.SelectedWeapon.Malfunction, out var malfunctionValue)
            && result.AttackerRoll >= malfunctionValue)
        {
            result.IsMalfunction = true;
            result.AttackerWins = false;
            result.DefenderHpAfter = defender.CurrentHitPoints;
            attacker.JammedWeaponName = setup.SelectedWeapon.Name;
            result.MalfunctionMessage = $"Осечка! {setup.SelectedWeapon.Name} заклинило (бросок {result.AttackerRoll} ≥ {malfunctionValue}).";
            result.Summary = $"{attacker.Name}: {result.MalfunctionMessage} Требуется ремонт (проверка Механики или Стрельбы, 1d6 раундов).";
            return result;
        }

        // Дальний бой — без встречного броска. Попадание требует уровня сложности по дальности.
        if (result.AttackerSuccessLevel < requiredLevel)
        {
            result.AttackerWins = false;
            result.DefenderHpAfter = defender.CurrentHitPoints;

            var threshold = GetRollThresholdForRange(setup.AttackSkillValue, setup.RangeLevel);
            var needed = setup.RangeLevel == RangeLevel.Base
                ? $"против {setup.AttackSkillValue}"
                : $"нужен {GetDifficultyName(setup.RangeLevel)} успех — не выше {threshold}";

            result.Summary = $"{attacker.Name} промахивается ({result.AttackerRoll}, {needed}){FormatRollDetail(result.AttackerRollDetail)}.";
            return result;
        }

        result.AttackerWins = true;
        result.IsImpalingWeapon = true; // Всё огнестрельное = проникающее

        // Прицеливание использовано — сбросить флаг
        if (attacker.IsAiming) attacker.IsAiming = false;

        // При сверхбольшой дальности проникающая рана только при критическом успехе (стр. 110)
        if (setup.RangeLevel == RangeLevel.Extreme
            && result.AttackerSuccessLevel != SuccessLevel.CriticalSuccess)
        {
            result.IsImpalingWeapon = false;
        }

        CalculateDamage(result, setup, attacker, defender);

        return result;
    }

    // ───────────────────── Боевые манёвры ─────────────────────

    /// <summary>
    /// Разрешение боевого манёвра (CoC 7e стр. 103):
    /// разоружить, сбить с ног, захват, толкнуть, вырваться, невыгодное положение.
    /// </summary>
    public CombatActionResult ResolveManeuver(ManeuverSetup setup)
    {
        var attacker = Combatants.First(c => c.Id == setup.AttackerId);
        var defender = Combatants.First(c => c.Id == setup.DefenderId);

        var result = new CombatActionResult
        {
            Round = CurrentRound,
            AttackerId = attacker.Id,
            AttackerName = attacker.Name,
            ActionType = CombatActionType.Maneuver,
            DefenderId = defender.Id,
            DefenderName = defender.Name,
            DefenderReaction = setup.DefenderReaction,
            DefenderHpBefore = defender.CurrentHitPoints,
            ManeuverType = setup.ManeuverType
        };

        // Разница в Комплекции
        int buildDiff = setup.DefenderBuild - setup.AttackerBuild;
        if (buildDiff >= 3)
        {
            result.AttackerWins = false;
            result.Summary = $"Манёвр «{GetManeuverName(setup.ManeuverType)}» невозможен: " +
                             $"Комплекция {attacker.Name} слишком низкая (разница ≥3).";
            return result;
        }

        // Разница Комплекции даёт штрафные кости: по одной за каждый пункт, максимум две (стр. 103)
        var buildPenalty = Math.Clamp(buildDiff, 0, 2);
        var reasons = new List<string>();
        if (setup.BonusDice > 0) reasons.Add($"+{setup.BonusDice} от Хранителя");
        if (setup.PenaltyDice > 0) reasons.Add($"−{setup.PenaltyDice} от Хранителя");
        if (buildPenalty > 0) reasons.Add($"−{buildPenalty} разница Комплекции ({setup.AttackerBuild} против {setup.DefenderBuild})");

        var modifiers = new AttackModifiers(
            Math.Max(0, setup.BonusDice),
            Math.Max(0, setup.PenaltyDice) + buildPenalty,
            reasons);
        result.Modifiers = modifiers;

        result.AttackerSkillValue = setup.AttackSkillValue;
        result.AttackerRollDetail = RollFor(setup.AttackerRollDetail, setup.ManualAttackerRoll, modifiers.BonusDice, modifiers.PenaltyDice);
        result.AttackerRoll = result.AttackerRollDetail.Result;
        result.AttackerSuccessLevel = CalculateSuccessLevel(result.AttackerRoll, result.AttackerSkillValue);

        result.DefenderSkillValue = setup.DefenderSkillValue;
        result.DefenderRollDetail = RollFor(setup.DefenderRollDetail, setup.ManualDefenderRoll, setup.DefenderBonusDice, setup.DefenderPenaltyDice);
        result.DefenderRoll = result.DefenderRollDetail.Result;
        result.DefenderSuccessLevel = CalculateSuccessLevel(result.DefenderRoll, result.DefenderSkillValue);

        if (result.AttackerSuccessLevel <= SuccessLevel.Failure)
        {
            result.AttackerWins = false;
            result.ManeuverSucceeded = false;
            result.DefenderHpAfter = defender.CurrentHitPoints;
            result.Summary = $"{attacker.Name}: манёвр «{GetManeuverName(setup.ManeuverType)}» не удался ({result.AttackerRoll}/{result.AttackerSkillValue}){FormatRollDetail(result.AttackerRollDetail)}.";
            return result;
        }

        if (result.DefenderSuccessLevel <= SuccessLevel.Failure)
        {
            result.AttackerWins = true;
        }
        else
        {
            result.AttackerWins = ResolveOpposedRoll(
                result.AttackerSuccessLevel, result.AttackerSkillValue,
                result.DefenderSuccessLevel, result.DefenderSkillValue,
                setup.DefenderReaction);
        }

        result.ManeuverSucceeded = result.AttackerWins;
        defender.HasDefendedThisRound = true;
        defender.DefenseCountThisRound++;

        result.DefenderHpAfter = defender.CurrentHitPoints;

        var rollLine =
            $"{attacker.Name}: {result.AttackerRoll}/{result.AttackerSkillValue} — " +
            $"{GetSuccessLevelText(result.AttackerSuccessLevel)}{FormatRollDetail(result.AttackerRollDetail)}. " +
            $"{defender.Name}: {result.DefenderRoll}/{result.DefenderSkillValue} — " +
            $"{GetSuccessLevelText(result.DefenderSuccessLevel)}{FormatRollDetail(result.DefenderRollDetail)}.";

        if (result.ManeuverSucceeded)
        {
            result.Summary = $"{attacker.Name} успешно выполняет манёвр «{GetManeuverName(setup.ManeuverType)}» " +
                             $"против {defender.Name}. {rollLine}";
        }
        else
        {
            result.Summary = $"{attacker.Name}: манёвр «{GetManeuverName(setup.ManeuverType)}» не удался — " +
                             $"{defender.Name} успешно защитился. {rollLine}";
        }

        return result;
    }

    // ───────────────────── Расчёт урона ─────────────────────

    /// <summary>
    /// Определяет, как применяется бонус к урону (CoC 7e, стр. 106).
    /// <para>
    /// По умолчанию решает правило: ближний бой — полный Б.к.У., дистанционная
    /// атака — без Б.к.У. Данные оружия могут переопределить это, явно указав
    /// <see cref="DamageBonusType.Half"/> (лук, праща, метательное оружие) или
    /// <see cref="DamageBonusType.Full"/>. Значение <see cref="DamageBonusType.None"/>
    /// в данных совпадает со значением по умолчанию и потому не считается
    /// переопределением — иначе у любого оружия с незаполненным DamageInfo
    /// бонус к урону молча пропадал бы.
    /// </para>
    /// <para>
    /// В <c>WeaponType</c> нет категории метательного оружия, поэтому половинный
    /// Б.к.У. задаётся только через данные конкретного оружия.
    /// </para>
    /// </summary>
    public static DamageBonusType ResolveDamageBonusType(bool isMelee, DamageExpression? damageExpr)
    {
        if (damageExpr is not null && damageExpr.DamageBonus != DamageBonusType.None)
            return damageExpr.DamageBonus;

        return isMelee ? DamageBonusType.Full : DamageBonusType.None;
    }

    private static void CalculateDamage(CombatActionResult result, AttackSetup setup, Combatant attacker, Combatant defender)
    {
        var damageFormula = setup.SelectedWeapon?.Damage ?? setup.CreatureAttackDamage ?? "1D3";
        result.DamageFormula = damageFormula;

        // Структурированная формула урона (если доступна)
        var damageExpr = setup.SelectedWeapon?.DamageInfo?.GetDefaultDamage();

        // Определяем тип оружия (проникающее или ударное)
        if (setup.SelectedWeapon != null && setup.IsMelee)
            result.IsImpalingWeapon = IsImpalingWeapon(setup.SelectedWeapon);
        // Для дальнего боя IsImpalingWeapon устанавливается в ResolveRangedAttack

        var dbType = ResolveDamageBonusType(setup.IsMelee, damageExpr);
        var applyDamageBonus = dbType != DamageBonusType.None;

        // Чрезвычайный урон: критический (01) или экстремальный (≤навык/5)
        // Правило: только при атаке в свой ход, НЕ при контратаке
        bool isExtraordinary = result.AttackerSuccessLevel >= SuccessLevel.ExtremeSuccess;
        result.IsExtreme = isExtraordinary;
        result.IsCritical = result.AttackerSuccessLevel == SuccessLevel.CriticalSuccess;

        if (isExtraordinary)
        {
            // Чрезвычайный успех: максимальный урон оружия + максимальный БкУ (CoC 7e стр. 101)
            result.DamageRolled = damageExpr is not null
                ? MaximizeDamageExpression(damageExpr)
                : MaximizeDiceFormula(damageFormula);

            if (applyDamageBonus)
            {
                result.BonusDamage = dbType == DamageBonusType.Half
                    ? MaximizeDiceFormula(attacker.DamageBonus) / 2
                    : MaximizeDiceFormula(attacker.DamageBonus);
            }

            // Проникающее оружие: дополнительный бросок урона
            if (result.IsImpalingWeapon)
            {
                result.ExtraDamage = setup.ManualExtraImpalingRoll ?? (damageExpr is not null
                    ? RollDamageExpression(damageExpr)
                    : RollDiceFormula(damageFormula));
            }
        }
        else
        {
            // Обычный бросок урона
            result.DamageRolled = setup.ManualWeaponDamageRoll ?? (damageExpr is not null
                ? RollDamageExpression(damageExpr)
                : RollDiceFormula(damageFormula));

            if (applyDamageBonus)
            {
                var rawBonus = setup.ManualDamageBonusRoll ?? RollDamageBonus(attacker.DamageBonus);
                result.BonusDamage = dbType == DamageBonusType.Half ? rawBonus / 2 : rawBonus;
            }
        }

        // Итого до вычета брони
        result.RawDamage = Math.Max(0, result.DamageRolled + result.BonusDamage + result.ExtraDamage);

        // Броня снижает урон
        result.ArmorReduction = Math.Min(defender.Armor, result.RawDamage);
        result.TotalDamage = result.RawDamage - result.ArmorReduction;

        // ── Расчёт последствий урона ──

        // Мгновенная смерть: один удар наносит урон, равный максимуму ПЗ или больше (стр. 118)
        if (result.TotalDamage >= defender.MaxHitPoints)
        {
            result.IsInstantDeath = true;
            result.DefenderDead = true;
            result.DefenderHpAfter = 0;
            result.Summary = BuildAttackSummary(result);
            return;
        }

        int newHp = defender.CurrentHitPoints - result.TotalDamage;
        result.DefenderHpAfter = Math.Max(0, newHp);

        // Серьёзная рана: урон ≥ половины макс. ПЗ за одну атаку (CoC 7e стр. 117)
        bool majorWound = result.TotalDamage * 2 >= defender.MaxHitPoints;
        if (majorWound)
        {
            result.TriggeredMajorWound = true;
            result.DefenderFallsProne = true; // Автоматическое падение

            // Проверка ВЫН: при провале — потеря сознания
            result.MajorWoundConRoll = setup.ManualMajorWoundConRoll ?? RollD100();
            result.MajorWoundConRollSuccess = result.MajorWoundConRoll <= defender.ConstitutionValue;
            if (!result.MajorWoundConRollSuccess)
                result.DefenderKnockedUnconscious = true;
        }

        if (newHp <= 0)
        {
            result.DefenderHpAfter = 0;
            // При смерти = 0 ПЗ + серьёзная рана (новая или уже имевшаяся)
            if (defender.HasMajorWound || result.TriggeredMajorWound)
            {
                result.DefenderDying = true;
            }
            else
            {
                // 0 ПЗ без серьёзной раны → без сознания, не умрёт
                result.DefenderKnockedUnconscious = true;
            }
        }

        result.Summary = BuildAttackSummary(result);
    }

    /// <summary>
    /// Расчёт урона контратаки: защитник победил при fight back и наносит урон атакующему.
    /// </summary>
    private static void CalculateCounterAttackDamage(CombatActionResult result, AttackSetup setup, Combatant defender, Combatant attacker)
    {
        var formula = setup.CounterAttackDamageFormula;
        if (string.IsNullOrWhiteSpace(formula)) formula = "1D3";
        result.CounterAttackDamageFormula = formula;

        result.CounterDamageRolled = setup.ManualCounterWeaponDamageRoll ?? RollDiceFormula(formula);
        result.CounterBonusDamage = setup.ManualCounterDamageBonusRoll ?? RollDamageBonus(defender.DamageBonus);

        result.CounterRawDamage = Math.Max(0, result.CounterDamageRolled + result.CounterBonusDamage);
        result.CounterArmorReduction = Math.Min(attacker.Armor, result.CounterRawDamage);
        result.CounterTotalDamage = result.CounterRawDamage - result.CounterArmorReduction;

        // Мгновенная смерть от контратаки: урон одной атакой ≥ максимума ПЗ (стр. 118)
        if (result.CounterTotalDamage >= attacker.MaxHitPoints)
        {
            result.AttackerDead = true;
            result.AttackerHpAfter = 0;
            result.Summary += $" {defender.Name} наносит {attacker.Name} {result.CounterTotalDamage} урона — " +
                              $"это не меньше его максимума ПЗ ({attacker.MaxHitPoints}). {attacker.Name} убит мгновенно!";
            return;
        }

        int newHp = attacker.CurrentHitPoints - result.CounterTotalDamage;
        result.AttackerHpAfter = Math.Max(0, newHp);

        // Серьёзная рана для атакующего
        if (result.CounterTotalDamage * 2 >= attacker.MaxHitPoints)
        {
            result.AttackerTriggeredMajorWound = true;
            result.AttackerFallsProne = true;

            result.AttackerMajorWoundConRoll = setup.ManualCounterMajorWoundConRoll ?? RollD100();
            result.AttackerMajorWoundConRollSuccess = result.AttackerMajorWoundConRoll <= attacker.ConstitutionValue;
            if (!result.AttackerMajorWoundConRollSuccess)
                result.AttackerKnockedUnconscious = true;
        }

        if (newHp <= 0)
        {
            result.AttackerHpAfter = 0;
            if (attacker.HasMajorWound || result.AttackerTriggeredMajorWound)
                result.AttackerDying = true;
            else
                result.AttackerKnockedUnconscious = true;
        }

        result.Summary += $" {defender.Name} наносит урон {attacker.Name}: " +
                          $"{formula}={result.CounterDamageRolled}" +
                          (result.CounterBonusDamage != 0 ? $" + БкУ {result.CounterBonusDamage}" : "") +
                          (result.CounterArmorReduction > 0 ? $" − броня {result.CounterArmorReduction}" : "") +
                          $" = {result.CounterTotalDamage} (ОЗ: {result.AttackerHpBefore}→{result.AttackerHpAfter}).";

        if (result.AttackerTriggeredMajorWound)
        {
            var conRes = result.AttackerMajorWoundConRollSuccess ? "успех" : "провал";
            result.Summary += $" Серьёзная рана! Проверка ВЫН: {result.AttackerMajorWoundConRoll} — {conRes}.";
        }
        if (result.AttackerKnockedUnconscious) result.Summary += $" {attacker.Name} теряет сознание!";
        if (result.AttackerDying) result.Summary += $" {attacker.Name} при смерти!";
    }

    // ───────────────────── Проверка рассудка ─────────────────────

    /// <summary>
    /// Разрешение проверки рассудка. Поддерживает ручной ввод броска.
    /// </summary>
    public CombatActionResult ResolveSanityCheck(SanityCheckSetup setup)
    {
        var target = Combatants.First(c => c.Id == setup.TargetId);

        var result = new CombatActionResult
        {
            Round = CurrentRound,
            AttackerId = target.Id,
            AttackerName = target.Name,
            ActionType = CombatActionType.SanityCheck,
            SanityBefore = target.CurrentSanity
        };

        // Бонусные и штрафные кости к проверкам Рассудка не применяются (стр. 152)
        var roll = setup.ManualRoll ?? RollD100();
        result.AttackerRoll = roll;
        result.AttackerSkillValue = target.CurrentSanity;
        result.AttackerSuccessLevel = CalculateSuccessLevel(roll, target.CurrentSanity);

        var isFumble = result.AttackerSuccessLevel == SuccessLevel.Fumble;
        var success = result.AttackerSuccessLevel >= SuccessLevel.RegularSuccess;
        result.SanityFumble = isFumble;

        // Крах — теряется максимум возможных пунктов (стр. 153)
        var sanLoss = isFumble
            ? MaximizeDiceFormula(setup.FailureLoss)
            : RollDiceFormula(success ? setup.SuccessLoss : setup.FailureLoss);

        result.SanityLoss = sanLoss;
        var sanityAfter = Math.Max(0, target.CurrentSanity - sanLoss);
        result.SanityAfter = sanityAfter;

        var lostToday = target.SanityLostToday + sanLoss;
        result.SanityLostToday = lostToday;

        var notes = new List<string>();

        // Потеря 5+ пунктов за раз — проверка ИНТ; безумие наступает при УСПЕХЕ (стр. 153)
        if (sanLoss >= 5)
        {
            var intRoll = setup.ManualIntRoll ?? RollD100();
            result.IntelligenceRoll = intRoll;
            result.IntelligenceValue = target.IntelligenceValue;

            var realizedTheHorror = intRoll <= target.IntelligenceValue;
            result.TriggeredTemporaryInsanity = realizedTheHorror;

            if (realizedTheHorror)
            {
                result.TemporaryInsanityHours = setup.ManualInsanityDurationRoll ?? RollDice(10);
                notes.Add($"Потеряно 5+ пунктов за раз. Проверка ИНТ: {intRoll}/{target.IntelligenceValue} — " +
                          $"успех, сыщик осознал ужас. Временное безумие на {result.TemporaryInsanityHours} ч.");
            }
            else
            {
                notes.Add($"Потеряно 5+ пунктов за раз. Проверка ИНТ: {intRoll}/{target.IntelligenceValue} — " +
                          "провал, рассудок отгородился от ужаса. Безумие не наступает.");
            }
        }

        // Бессрочное безумие: потеря не менее ⅕ текущего рассудка за игровой день (стр. 153)
        if (target.CurrentSanity > 0 && lostToday * 5 >= target.CurrentSanity && !target.HasIndefiniteInsanity)
        {
            result.TriggeredIndefiniteInsanity = true;
            notes.Add($"За игровой день потеряно {lostToday} из {target.CurrentSanity} — это не меньше ⅕. Бессрочное безумие.");
        }

        // Неизлечимое безумие при нулевом рассудке (стр. 153)
        if (sanityAfter == 0)
        {
            result.TriggeredPermanentInsanity = true;
            notes.Add($"{target.Name} теряет рассудок полностью — неизлечимое безумие, персонаж выбывает из игры.");
        }

        var successText = GetSuccessLevelText(result.AttackerSuccessLevel);
        result.Summary = $"{target.Name}: проверка рассудка — бросок {roll} против {target.CurrentSanity} ({successText}). " +
                         (isFumble ? $"Крах — максимальная потеря: {sanLoss} ед. " : $"Потеря: {sanLoss} ед. ") +
                         $"РАС: {target.CurrentSanity}→{sanityAfter}." +
                         (notes.Count > 0 ? " " + string.Join(" ", notes) : "");

        return result;
    }

    /// <summary>
    /// Сбрасывает счётчик потерянного за день рассудка — вызывать, когда сыщики
    /// добрались до безопасного места и игровой день закончился (стр. 153).
    /// </summary>
    public void StartNewGameDay()
    {
        foreach (var c in Combatants)
            c.SanityLostToday = 0;

        NotifyStateChanged();
    }

    // ───────────────────── Применение результата ─────────────────────

    public void SetPendingResult(CombatActionResult result)
    {
        PendingResult = result;
        NotifyStateChanged();
    }

    public void ApplyResult(CombatActionResult result)
    {
        // Урон защитнику
        if (result.DefenderId.HasValue)
        {
            var defender = Combatants.FirstOrDefault(c => c.Id == result.DefenderId);
            if (defender != null)
            {
                var tookDamage = result.DefenderHpAfter < result.DefenderHpBefore;

                defender.CurrentHitPoints = result.DefenderHpAfter;
                if (result.DefenderFallsProne) defender.IsProne = true;
                if (result.DefenderKnockedUnconscious) defender.IsUnconscious = true;
                if (result.TriggeredMajorWound) defender.HasMajorWound = true;
                if (result.DefenderDying) defender.IsDying = true;
                if (result.DefenderDead) defender.IsDead = true;

                // Новое ранение — первую помощь можно пытаться оказать заново,
                // а прежняя стабилизация утрачена (стр. 118)
                if (tookDamage)
                {
                    defender.FirstAidAttempted = false;
                    defender.IsStabilized = false;
                    defender.TemporaryHitPoints = 0;
                }

                // Вырвался из захвата при манёвре
                if (result.ActionType == CombatActionType.Maneuver && result.ManeuverSucceeded)
                {
                    ApplyManeuverEffect(result, defender);
                }
            }
        }

        // Контратакующий урон по атакующему (fight back)
        if (result.CounterTotalDamage > 0)
        {
            var attacker = Combatants.FirstOrDefault(c => c.Id == result.AttackerId);
            if (attacker != null)
            {
                attacker.CurrentHitPoints = result.AttackerHpAfter;
                if (result.AttackerFallsProne) attacker.IsProne = true;
                if (result.AttackerKnockedUnconscious) attacker.IsUnconscious = true;
                if (result.AttackerTriggeredMajorWound) attacker.HasMajorWound = true;
                if (result.AttackerDying) attacker.IsDying = true;
                if (result.AttackerDead) attacker.IsDead = true;

                attacker.FirstAidAttempted = false;
                attacker.IsStabilized = false;
                attacker.TemporaryHitPoints = 0;
            }
        }

        // Проверка рассудка
        if (result.ActionType == CombatActionType.SanityCheck)
        {
            var target = Combatants.FirstOrDefault(c => c.Id == result.AttackerId);
            if (target != null)
            {
                target.CurrentSanity = result.SanityAfter ?? target.CurrentSanity;
                target.SanityLostToday = result.SanityLostToday ?? target.SanityLostToday;

                if (result.TriggeredTemporaryInsanity == true)
                {
                    target.HasTemporaryInsanity = true;
                    target.TemporaryInsanityHours = result.TemporaryInsanityHours ?? 0;
                }

                if (result.TriggeredIndefiniteInsanity)
                    target.HasIndefiniteInsanity = true;

                if (result.TriggeredPermanentInsanity)
                    target.HasPermanentInsanity = true;

                // Синхронизация с исходным персонажем (для листа сыщика)
                if (target.CharacterSource is { } src)
                {
                    src.DerivedAttributes.Sanity.Value = target.CurrentSanity;
                    var loss = result.SanityLoss ?? 0;
                    if (loss > 0)
                        src.State.SanityLossEpisode += loss;
                    if (result.TriggeredTemporaryInsanity == true && !src.State.HasTemporaryInsanity)
                    {
                        src.State.HasTemporaryInsanity = true;
                        src.State.TemporaryInsanityStartedAt = DateTime.UtcNow;
                    }
                    if (result.TriggeredIndefiniteInsanity)
                        src.State.HasIndefiniteInsanity = true;
                }
            }
        }

        CombatLog.Insert(0, result);
        PendingResult = null;
        NotifyStateChanged();
    }

    private static void ApplyManeuverEffect(CombatActionResult result, Combatant defender)
    {
        switch (result.ManeuverType)
        {
            case ManeuverType.KnockDown:
            case ManeuverType.Push:
                defender.IsProne = true;
                break;
            case ManeuverType.Grapple:
                defender.IsGrappled = true;
                defender.GrappledBy = result.AttackerId;
                break;
            case ManeuverType.BreakFree:
                defender.IsGrappled = false;
                defender.GrappledBy = null;
                break;
            case ManeuverType.Disarm:
            case ManeuverType.Disadvantage:
                // Визуально — в журнале
                break;
        }
    }

    /// <summary>
    /// Добавляет результат первой помощи в лог боя (без изменения состояния бойцов —
    /// оно уже применено в FirstAidPanel).
    /// </summary>
    public void ApplyFirstAidLog(CombatActionResult result)
    {
        CombatLog.Insert(0, result);
        NotifyStateChanged();
    }

    public void CancelPendingResult()
    {
        PendingResult = null;
        NotifyStateChanged();
    }

    // ───────────────────── Вспомогательные методы ─────────────────────

    /// <summary>
    /// Поиск значения навыка персонажа по имени (частичное совпадение).
    /// </summary>
    public static int FindSkillValue(Character character, string skillName)
    {
        if (character.Skills?.SkillGroups == null) return 0;

        foreach (var group in character.Skills.SkillGroups)
        {
            foreach (var skill in group.Skills)
            {
                if (skill.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase)
                    || skill.Name.Contains(skillName, StringComparison.OrdinalIgnoreCase))
                {
                    return skill.Value.Regular;
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// Проверяет, является ли оружие проникающим (пронзающим).
    /// Проникающее оружие при чрезвычайном успехе наносит дополнительный бросок урона.
    /// </summary>
    public static bool IsImpalingWeapon(Weapon weapon)
    {
        // Если явный флаг установлен — доверяем ему
        if (weapon.IsImpaling) return true;

        if (weapon.Type != WeaponType.Melee)
            return true; // Всё огнестрельное — проникающее

        var name = weapon.Name.ToLowerInvariant();
        return name.Contains("нож") || name.Contains("кинжал") || name.Contains("меч")
               || name.Contains("рапир") || name.Contains("копь") || name.Contains("пик")
               || name.Contains("knife") || name.Contains("sword") || name.Contains("spear")
               || name.Contains("rapier") || name.Contains("dagger") || name.Contains("штык")
               || name.Contains("шпаг") || name.Contains("сабл") || name.Contains("клинок");
    }

    /// <summary>
    /// Парсит и бросает бонус к урону: "+1D4", "0", "-1", "+1D6"
    /// </summary>
    public static int RollDamageBonus(string damageBonus)
    {
        if (string.IsNullOrWhiteSpace(damageBonus) || damageBonus.Trim() == "0")
            return 0;

        return RollDiceFormula(damageBonus);
    }

    /// <summary>Возвращает описание уровня успеха на русском.</summary>
    public static string GetSuccessLevelText(SuccessLevel level) => level switch
    {
        SuccessLevel.CriticalSuccess => "критический успех",
        SuccessLevel.ExtremeSuccess => "чрезвычайный успех",
        SuccessLevel.HardSuccess => "трудный успех",
        SuccessLevel.RegularSuccess => "обычный успех",
        SuccessLevel.Failure => "провал",
        SuccessLevel.Fumble => "крах",
        _ => "неизвестно"
    };

    /// <summary>
    /// Короткая расшифровка броска с дополнительными костями, например
    /// « [кости 24, 44 — бонусная]». Для броска без модификаторов возвращает пустую строку.
    /// </summary>
    public static string FormatRollDetail(DiceRollResult? detail)
    {
        if (detail is null || !detail.HasExtraDice) return string.Empty;

        var kind = detail.BonusDice > 0
            ? DiceWord(detail.BonusDice, "бонусная", "бонусные")
            : DiceWord(detail.PenaltyDice, "штрафная", "штрафные");

        return $" [кости {string.Join(", ", detail.Candidates)} — {kind}]";
    }

    private static string DiceWord(int count, string one, string many) =>
        count == 1 ? $"{one} кость" : $"{count} {many} кости";

    /// <summary>«1 бонусная кость», «2 штрафные кости» — для подписей в интерфейсе.</summary>
    public static string DescribeDice(int count, bool isBonus) =>
        isBonus ? DiceWord(count, "бонусная", "бонусные") : DiceWord(count, "штрафная", "штрафные");

    /// <summary>Возвращает название манёвра на русском.</summary>
    public static string GetManeuverName(ManeuverType type) => type switch
    {
        ManeuverType.Disarm => "Разоружить",
        ManeuverType.KnockDown => "Сбить с ног",
        ManeuverType.Grapple => "Захват",
        ManeuverType.Push => "Толкнуть",
        ManeuverType.BreakFree => "Вырваться",
        ManeuverType.Disadvantage => "Невыгодное положение",
        _ => "Манёвр"
    };

    private static string BuildAttackSummary(CombatActionResult result)
    {
        var parts = new List<string>();

        var atkLevel = GetSuccessLevelText(result.AttackerSuccessLevel);
        parts.Add($"{result.AttackerName} атакует ({result.WeaponName}): {result.AttackerRoll}/{result.AttackerSkillValue} — {atkLevel}{FormatRollDetail(result.AttackerRollDetail)}.");

        if (result.SurpriseMode == SurpriseMode.AutoHit)
            parts.Add($"{result.DefenderName} застигнут врасплох — атака проходит автоматически.");
        else if (result.SurpriseMode == SurpriseMode.BonusDie)
            parts.Add($"{result.DefenderName} застигнут врасплох — не защищается.");

        if (result.ActionType == CombatActionType.MeleeAttack && result.DefenderId.HasValue && !result.DefenderSuccessLevel.Equals(SuccessLevel.Failure) || result.DefenderRoll > 0)
        {
            var defAction = result.DefenderReaction == CombatActionType.FightBack ? "ответный удар" : "уклонение";
            var defLevel = GetSuccessLevelText(result.DefenderSuccessLevel);
            parts.Add($"{result.DefenderName} ({defAction}): {result.DefenderRoll}/{result.DefenderSkillValue} — {defLevel}{FormatRollDetail(result.DefenderRollDetail)}.");
        }

        if (result.AttackerWins && result.TotalDamage > 0)
        {
            var dmgParts = new List<string> { $"{result.DamageFormula}={result.DamageRolled}" };
            if (result.BonusDamage != 0) dmgParts.Add($"БкУ {result.BonusDamage}");
            if (result.ExtraDamage > 0) dmgParts.Add($"доп. {result.ExtraDamage}");
            var dmgStr = $"Урон: {string.Join(" + ", dmgParts)} = {result.RawDamage}";
            if (result.ArmorReduction > 0) dmgStr += $" − броня {result.ArmorReduction} = {result.TotalDamage}";
            parts.Add(dmgStr + $". ОЗ: {result.DefenderHpBefore}→{result.DefenderHpAfter}.");

            if (result.IsCritical) parts.Add("Критический удар!");
            else if (result.IsExtreme)
            {
                parts.Add(result.IsImpalingWeapon ? "Чрезвычайный успех — проникающая рана!" : "Чрезвычайный успех — максимальный урон!");
            }
        }
        else if (!result.AttackerWins && result.TotalDamage == 0 && result.CounterTotalDamage == 0)
        {
            parts.Add("Атака не нанесла урона.");
        }

        if (result.IsInstantDeath)
        {
            parts.Add($"{result.DefenderName} убит мгновенно!");
        }
        else
        {
            if (result.TriggeredMajorWound)
            {
                var conRes = result.MajorWoundConRollSuccess ? "успех" : "провал";
                parts.Add($"Серьёзная рана! ВЫН: {result.MajorWoundConRoll} — {conRes}. {result.DefenderName} падает.");
            }
            if (result.DefenderKnockedUnconscious) parts.Add($"{result.DefenderName} теряет сознание!");
            if (result.DefenderDead) parts.Add($"{result.DefenderName} мёртв!");
            else if (result.DefenderDying) parts.Add($"{result.DefenderName} при смерти!");
        }

        return string.Join(" ", parts);
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
