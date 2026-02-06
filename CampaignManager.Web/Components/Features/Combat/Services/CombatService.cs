using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Weapons.Model;

namespace CampaignManager.Web.Components.Features.Combat.Services;

/// <summary>
/// Сервис управления боевыми столкновениями по правилам Call of Cthulhu 7e
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

    public void SortByInitiative()
    {
        Combatants = Combatants
            .OrderByDescending(c => c.Initiative)
            .ThenByDescending(c => c.Dexterity)
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
        }

        NotifyStateChanged();
    }

    public void NextRound()
    {
        CurrentRound++;
        CurrentTurnIndex = 0;
        NotifyStateChanged();
    }

    public Combatant? GetActiveCombatant()
    {
        if (Combatants.Count == 0) return null;
        if (CurrentTurnIndex >= Combatants.Count) return Combatants[0];
        return Combatants[CurrentTurnIndex];
    }

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
    /// Максимизирует бросок по формуле (все кости на максимум) — для критических ударов
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

    // ───────────────────── Правила CoC 7e ─────────────────────

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
    /// Разрешение встречного броска. Возвращает true если атакующий побеждает.
    /// </summary>
    public static bool ResolveOpposedRoll(
        SuccessLevel attackerLevel, int attackerSkill,
        SuccessLevel defenderLevel, int defenderSkill)
    {
        if (attackerLevel > defenderLevel) return true;
        if (defenderLevel > attackerLevel) return false;
        return attackerSkill >= defenderSkill;
    }

    // ───────────────────── Разрешение ближнего боя ─────────────────────

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
            WeaponName = setup.SelectedWeapon?.Name ?? setup.CreatureAttackName ?? "Безоружная атака"
        };

        // 1. Бросок атакующего
        result.AttackerSkillValue = setup.AttackSkillValue;
        result.AttackerRoll = RollD100();
        result.AttackerSuccessLevel = CalculateSuccessLevel(result.AttackerRoll, result.AttackerSkillValue);

        // 2. Бросок защитника
        result.DefenderSkillValue = setup.DefenderSkillValue;
        result.DefenderRoll = RollD100();
        result.DefenderSuccessLevel = CalculateSuccessLevel(result.DefenderRoll, result.DefenderSkillValue);

        // 3. Если атакующий провалил — промах
        if (result.AttackerSuccessLevel <= SuccessLevel.Failure)
        {
            result.AttackerWins = false;
            result.DefenderHpAfter = defender.CurrentHitPoints;
            result.Summary = $"{attacker.Name} промахивается ({result.AttackerRoll} против {result.AttackerSkillValue})!";
            return result;
        }

        // 4. Если защитник провалил — попадание
        if (result.DefenderSuccessLevel <= SuccessLevel.Failure)
        {
            result.AttackerWins = true;
        }
        else
        {
            // 5. Оба преуспели — встречный бросок
            result.AttackerWins = ResolveOpposedRoll(
                result.AttackerSuccessLevel, result.AttackerSkillValue,
                result.DefenderSuccessLevel, result.DefenderSkillValue);
        }

        if (!result.AttackerWins)
        {
            result.DefenderHpAfter = defender.CurrentHitPoints;
            result.Summary = setup.DefenderReaction == CombatActionType.FightBack
                ? $"{defender.Name} отбивает атаку {attacker.Name}!"
                : $"{defender.Name} уклоняется от атаки {attacker.Name}!";
            return result;
        }

        // 6. Расчёт урона
        CalculateDamage(result, setup, attacker, defender);

        return result;
    }

    // ───────────────────── Разрешение дальнего боя ─────────────────────

    public CombatActionResult ResolveRangedAttack(AttackSetup setup)
    {
        var attacker = Combatants.First(c => c.Id == setup.AttackerId);
        var defender = Combatants.First(c => c.Id == setup.DefenderId);

        var result = new CombatActionResult
        {
            Round = CurrentRound,
            AttackerId = attacker.Id,
            AttackerName = attacker.Name,
            ActionType = CombatActionType.RangedAttack,
            DefenderId = defender.Id,
            DefenderName = defender.Name,
            DefenderHpBefore = defender.CurrentHitPoints,
            WeaponName = setup.SelectedWeapon?.Name ?? setup.CreatureAttackName ?? "Дальняя атака"
        };

        // Бросок атакующего
        result.AttackerSkillValue = setup.AttackSkillValue;
        result.AttackerRoll = RollD100();
        result.AttackerSuccessLevel = CalculateSuccessLevel(result.AttackerRoll, result.AttackerSkillValue);

        // Дальний бой — без встречного броска
        if (result.AttackerSuccessLevel <= SuccessLevel.Failure)
        {
            result.AttackerWins = false;
            result.DefenderHpAfter = defender.CurrentHitPoints;
            result.Summary = $"{attacker.Name} промахивается ({result.AttackerRoll} против {result.AttackerSkillValue})!";
            return result;
        }

        result.AttackerWins = true;
        result.IsImpalingWeapon = true; // Все огнестрельные = пронзающие

        CalculateDamage(result, setup, attacker, defender);

        return result;
    }

    // ───────────────────── Расчёт урона ─────────────────────

    private static void CalculateDamage(CombatActionResult result, AttackSetup setup, Combatant attacker, Combatant defender)
    {
        result.IsCritical = result.AttackerSuccessLevel == SuccessLevel.CriticalSuccess;
        result.IsExtreme = result.AttackerSuccessLevel == SuccessLevel.ExtremeSuccess;

        var damageFormula = setup.SelectedWeapon?.Damage ?? setup.CreatureAttackDamage ?? "1D3";
        result.DamageFormula = damageFormula;

        // Бросок урона
        result.DamageRolled = result.IsCritical
            ? MaximizeDiceFormula(damageFormula)
            : RollDiceFormula(damageFormula);

        // Бонус к урону (только ближний бой)
        if (setup.IsMelee)
        {
            result.BonusDamage = RollDamageBonus(attacker.DamageBonus);
        }

        // Пронзающее оружие (для ближнего — по типу оружия)
        if (setup.SelectedWeapon != null && setup.IsMelee)
        {
            result.IsImpalingWeapon = IsImpalingWeapon(setup.SelectedWeapon);
        }

        if (result.IsExtreme && result.IsImpalingWeapon)
        {
            result.ExtraDamage = MaximizeDiceFormula(damageFormula);
        }

        result.TotalDamage = Math.Max(0, result.DamageRolled + result.BonusDamage + result.ExtraDamage);

        // Расчёт HP
        var newHp = defender.CurrentHitPoints - result.TotalDamage;
        result.DefenderHpAfter = newHp;

        // Тяжёлая рана: урон ≥ половины макс. HP
        if (result.TotalDamage >= defender.MaxHitPoints / 2)
        {
            result.TriggeredMajorWound = true;
            result.MajorWoundConRoll = RollD100();
            result.MajorWoundConRollSuccess = result.MajorWoundConRoll <= defender.ConstitutionValue;
            if (!result.MajorWoundConRollSuccess)
            {
                result.DefenderKnockedUnconscious = true;
            }
        }

        // При смерти / мёртв
        if (newHp <= 0)
        {
            result.DefenderDying = true;
            if (newHp <= -defender.MaxHitPoints)
            {
                result.DefenderDead = true;
            }
        }

        result.Summary = BuildAttackSummary(result);
    }

    // ───────────────────── Проверка рассудка ─────────────────────

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

        var roll = RollD100();
        result.AttackerRoll = roll;
        result.AttackerSkillValue = target.CurrentSanity;

        var success = roll <= target.CurrentSanity;
        result.AttackerSuccessLevel = success ? SuccessLevel.RegularSuccess : SuccessLevel.Failure;

        var sanLoss = success
            ? RollDiceFormula(setup.SuccessLoss)
            : RollDiceFormula(setup.FailureLoss);

        result.SanityLoss = sanLoss;
        result.SanityAfter = Math.Max(0, target.CurrentSanity - sanLoss);

        // Временное безумие: потеря ≥5 за одну проверку
        if (sanLoss >= 5)
        {
            result.TriggeredTemporaryInsanity = true;
        }

        var successText = success ? "успех" : "неудача";
        result.Summary = $"{target.Name}: проверка рассудка — бросок {roll} против {target.CurrentSanity} ({successText}). " +
                         $"Потеря: {sanLoss} ед." +
                         (sanLoss >= 5 ? " Временное безумие!" : "");

        return result;
    }

    // ───────────────────── Применение результата ─────────────────────

    public void SetPendingResult(CombatActionResult result)
    {
        PendingResult = result;
        NotifyStateChanged();
    }

    public void ApplyResult(CombatActionResult result)
    {
        // Применение урона к защитнику
        if (result.DefenderId.HasValue)
        {
            var defender = Combatants.FirstOrDefault(c => c.Id == result.DefenderId);
            if (defender != null)
            {
                defender.CurrentHitPoints = result.DefenderHpAfter;
                if (result.DefenderKnockedUnconscious) defender.IsUnconscious = true;
                if (result.TriggeredMajorWound) defender.HasMajorWound = true;
                if (result.DefenderDying) defender.IsDying = true;
                if (result.DefenderDead) defender.IsDead = true;
            }
        }

        // Проверка рассудка — применяется к цели
        if (result.ActionType == CombatActionType.SanityCheck)
        {
            var target = Combatants.FirstOrDefault(c => c.Id == result.AttackerId);
            if (target != null)
            {
                target.CurrentSanity = result.SanityAfter ?? target.CurrentSanity;
                if (result.TriggeredTemporaryInsanity == true)
                    target.HasTemporaryInsanity = true;
            }
        }

        CombatLog.Insert(0, result);
        PendingResult = null;
        NotifyStateChanged();
    }

    public void CancelPendingResult()
    {
        PendingResult = null;
        NotifyStateChanged();
    }

    // ───────────────────── Вспомогательные методы ─────────────────────

    /// <summary>
    /// Поиск значения навыка персонажа по имени
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
    /// Определяет, является ли оружие пронзающим
    /// </summary>
    public static bool IsImpalingWeapon(Weapon weapon)
    {
        if (weapon.Type != WeaponType.Melee)
            return true;

        var name = weapon.Name.ToLowerInvariant();
        return name.Contains("нож") || name.Contains("кинжал") || name.Contains("меч")
               || name.Contains("рапир") || name.Contains("копь") || name.Contains("knife")
               || name.Contains("sword") || name.Contains("spear") || name.Contains("rapier")
               || name.Contains("dagger") || name.Contains("штык");
    }

    /// <summary>
    /// Парсит и бросает бонус к урону: "+1D4", "0", "-1", "-2", "+1D6"
    /// </summary>
    public static int RollDamageBonus(string damageBonus)
    {
        if (string.IsNullOrWhiteSpace(damageBonus) || damageBonus.Trim() == "0")
            return 0;

        return RollDiceFormula(damageBonus);
    }

    private static string BuildAttackSummary(CombatActionResult result)
    {
        var parts = new List<string>();

        var atkLevel = GetSuccessLevelText(result.AttackerSuccessLevel);
        parts.Add($"{result.AttackerName} атакует ({result.WeaponName}): бросок {result.AttackerRoll} против {result.AttackerSkillValue} — {atkLevel}.");

        if (result.ActionType == CombatActionType.MeleeAttack && result.DefenderId.HasValue)
        {
            var defAction = result.DefenderReaction == CombatActionType.FightBack ? "ответный удар" : "уклонение";
            var defLevel = GetSuccessLevelText(result.DefenderSuccessLevel);
            parts.Add($"{result.DefenderName} ({defAction}): бросок {result.DefenderRoll} против {result.DefenderSkillValue} — {defLevel}.");
        }

        if (result.AttackerWins && result.TotalDamage > 0)
        {
            var dmgParts = new List<string> { $"{result.DamageFormula}={result.DamageRolled}" };
            if (result.BonusDamage != 0) dmgParts.Add($"бонус {result.BonusDamage}");
            if (result.ExtraDamage > 0) dmgParts.Add($"доп. {result.ExtraDamage}");
            parts.Add($"Урон: {string.Join(" + ", dmgParts)} = {result.TotalDamage}.");

            if (result.IsCritical) parts.Add("Критический удар!");
            if (result.IsExtreme && result.IsImpalingWeapon) parts.Add("Пронзание!");
        }

        if (result.TriggeredMajorWound)
        {
            var conResult = result.MajorWoundConRollSuccess ? "успех" : "провал";
            parts.Add($"Тяжёлая рана! Проверка ТЕЛ: {result.MajorWoundConRoll} — {conResult}.");
        }

        if (result.DefenderKnockedUnconscious) parts.Add($"{result.DefenderName} теряет сознание!");
        if (result.DefenderDead) parts.Add($"{result.DefenderName} мёртв!");
        else if (result.DefenderDying) parts.Add($"{result.DefenderName} при смерти!");

        return string.Join(" ", parts);
    }

    public static string GetSuccessLevelText(SuccessLevel level) => level switch
    {
        SuccessLevel.CriticalSuccess => "критический успех",
        SuccessLevel.ExtremeSuccess => "экстремальный успех",
        SuccessLevel.HardSuccess => "сложный успех",
        SuccessLevel.RegularSuccess => "обычный успех",
        SuccessLevel.Failure => "неудача",
        SuccessLevel.Fumble => "провал",
        _ => "неизвестно"
    };

    private void NotifyStateChanged() => OnChange?.Invoke();
}
