using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Weapons.Model;

namespace CampaignManager.Web.Components.Features.Combat.Services;

public class CombatEngineService
{
    private readonly DiceRollerService _diceRoller;
    private readonly CombatCalculationService _calculator;
    private readonly ILogger<CombatEngineService> _logger;

    public CombatEngineService(
        DiceRollerService diceRoller,
        CombatCalculationService calculator,
        ILogger<CombatEngineService> logger)
    {
        _diceRoller = diceRoller;
        _calculator = calculator;
        _logger = logger;
    }

    public Task<CombatEncounter> StartCombat(CombatEncounter encounter)
    {
        _logger.LogInformation("Starting combat encounter");

        encounter.StartCombat();

        _logger.LogInformation("Combat started with {ParticipantCount} participants",
            encounter.Participants.Count);

        return Task.FromResult(encounter);
    }

    public async Task<CombatActionResult> ExecuteAction(CombatEncounter encounter, CombatAction action)
    {
        var result = new CombatActionResult { Action = action };

        try
        {
            var actor = encounter.Participants.FirstOrDefault(p => p.Id == action.ActorId);
            if (actor == null)
            {
                result.Success = false;
                result.ErrorMessage = "Участник не найден";
                return result;
            }

            if (!actor.CanAct)
            {
                result.Success = false;
                result.ErrorMessage = "Участник не может действовать";
                return result;
            }

            result = action.ActionType switch
            {
                CombatActionType.Attack => await ExecuteAttack(encounter, action),
                // NOTE: FightBack and Dodge are now handled within ExecuteAttack as reactions.
                // CombatActionType.FightBack => await ExecuteFightBack(encounter, action),
                // CombatActionType.Dodge => await ExecuteDodge(encounter, action),
                CombatActionType.CastSpell => await ExecuteSpellCast(encounter, action),
                CombatActionType.Maneuver => await ExecuteManeuver(encounter, action),
                CombatActionType.Move => await ExecuteMove(encounter, action),
                CombatActionType.Ready => await ExecuteReady(encounter, action),
                CombatActionType.Flee => await ExecuteFlee(encounter, action),
                CombatActionType.DoNothing => await ExecuteDoNothing(encounter, action),
                _ => throw new ArgumentException($"Unknown action type: {action.ActionType}")
            };

            if (result.Success)
            {
                encounter.AddToCombatLog(action);
                action.IsResolved = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing combat action: {ActionType}", action.ActionType);
            result.Success = false;
            result.ErrorMessage = "Ошибка при выполнении действия";
        }

        return result;
    }

    private Task<CombatActionResult> ExecuteAttack(CombatEncounter encounter, CombatAction action)
    {
        var result = new CombatActionResult { Action = action };

        var attacker = encounter.Participants.First(p => p.Id == action.ActorId);
        var defender = encounter.Participants.FirstOrDefault(p => p.Id == action.TargetId);

        if (defender == null)
        {
            result.Success = false;
            result.ErrorMessage = "Цель не найдена";
            return Task.FromResult(result);
        }

        // If UI specified a defender reaction, resolve opposed; otherwise do a single attack resolution
        if (action.DefenderReactionType is CombatActionType.Dodge or CombatActionType.FightBack)
        {
            var defAction = new CombatAction
            {
                ActorId = defender.Id,
                ActionType = action.DefenderReactionType!.Value,
                SkillName = action.DefenderReactionType == CombatActionType.Dodge ? "Dodge" : "Fighting (Brawl)",
                SkillValue = action.DefenderReactionType == CombatActionType.Dodge
                    ? defender.CombatStats.GetSkillValue("Dodge")
                    : defender.CombatStats.GetSkillValue("Fighting (Brawl)"),
                AttackerProvidedSuccess = action.DefenderProvidedSuccess,
                AttackerProvidedRoll = action.DefenderProvidedRoll
            };

            // Enforce: cannot Fight Back vs firearms
            if (action.Weapon != null && action.Weapon.Type != WeaponType.Melee && defAction.ActionType == CombatActionType.FightBack)
            {
                defAction.ActionType = CombatActionType.Dodge;
                defAction.SkillName = "Dodge";
                defAction.SkillValue = defender.CombatStats.GetSkillValue("Dodge");
            }

            return ResolveOpposedRoll(encounter, action, defAction);
        }


        // Suggested modifiers (for log). We still let GM provide outcome.
        var attackSkill = action.SkillValue;
        var rangeModifier = action.Weapon != null ? _calculator.CalculateRangeModifier(action.Weapon, 0) : 0;
        var outnumberedPenalty = _calculator.CalculateOutnumberedPenalty(defender);
        var modifiedSkill = Math.Clamp(attackSkill + rangeModifier - outnumberedPenalty, 1, 100);

        // Use manual success if provided; fallback to roll
        action.Roll = action.AttackerProvidedSuccess.HasValue
            ? new DiceRoll
            {
                Result = action.AttackerProvidedRoll ?? 0,
                TargetValue = modifiedSkill,
                SuccessLevel = action.AttackerProvidedSuccess.Value,
                IsCritical = action.AttackerProvidedSuccess == SuccessLevel.CriticalSuccess,
                IsFumble = action.AttackerProvidedSuccess == SuccessLevel.Fumble,
                Description = $"Ввод мастера: атака {attacker.Name} по {defender.Name} (нав. {modifiedSkill}%)"
            }
            : _diceRoller.RollPercentile(modifiedSkill, $"Атака {attacker.Name} на {defender.Name}");

        if (action.Roll.SuccessLevel.IsSuccess())
        {
            // Calculate damage with CoC 7e extreme rules
            var damageFormula = action.Weapon?.Damage ?? "1d3";
            var damageBonus = attacker.CombatStats.DamageBonus;
            var isExtremeSuccess = action.Roll.SuccessLevel == SuccessLevel.ExtremeSuccess;

            // Manual override if GM provided damage
            if (action.ProvidedDamageTotal.HasValue)
            {
                action.Damage = new DamageRoll
                {
                    TotalDamage = action.ProvidedDamageTotal.Value,
                    DiceResults = ParseDiceList(action.ProvidedDamageDiceList),
                    DamageBonus = 0,
                    DamageFormula = damageFormula,
                    IsMaxDamage = false
                };
            }
            else
            {
                if (isExtremeSuccess && action.Weapon != null)
                {
                    var isFirearm = action.Weapon.Type != WeaponType.Melee;
                    if (isFirearm)
                    {
                        var max = _diceRoller.GetMaximumDamage(damageFormula);
                        var bonus = _diceRoller.RollDamage(damageBonus, "0");
                        action.Damage = new DamageRoll { TotalDamage = max + bonus.TotalDamage, DamageBonus = bonus.TotalDamage, DamageFormula = damageFormula, IsMaxDamage = true };
                    }
                    else
                    {
                        var max = _diceRoller.GetMaximumDamage(damageFormula);
                        var extra = _diceRoller.RollDamage(damageFormula, "0");
                        var bonus = _diceRoller.RollDamage(damageBonus, "0");
                        action.Damage = new DamageRoll { TotalDamage = max + extra.TotalDamage + bonus.TotalDamage, DiceResults = extra.DiceResults, DamageBonus = bonus.TotalDamage, DamageFormula = damageFormula, IsMaxDamage = true };
                    }
                }
                else
                {
                    action.Damage = _diceRoller.RollDamage(damageFormula, damageBonus, false);
                }
            }

            // Apply armor reduction
            var armorReduction = _calculator.GetArmorReduction(defender);
            var finalDamage = Math.Max(0, action.Damage.TotalDamage - armorReduction);

            // Apply damage and conditions
            defender.TakeDamage(finalDamage);

            var woundEffects = _calculator.DetermineWoundEffects(defender, finalDamage);
            foreach (var condition in woundEffects)
            {
                defender.AddCondition(condition);
                action.InflictedConditions.Add(condition);
            }

            // Check for CON roll if Major Wound
            if (_calculator.RequiresConstitutionRoll(defender, finalDamage))
            {
                var conRoll = _diceRoller.RollConstitution(defender.CombatStats.Constitution, $"Проверка телосложения для {defender.Name}");
                if (!conRoll.SuccessLevel.IsSuccess())
                {
                    var unconscious = new CombatCondition { Type = CombatConditionType.Unconscious, Description = "Потерял сознание от серьезной раны" };
                    defender.AddCondition(unconscious);
                    action.InflictedConditions.Add(unconscious);
                }
            }

            result.DamageDealt = finalDamage;
            result.Description = $"{attacker.Name} атакует {defender.Name} и наносит {finalDamage} урона";
        }
        else
        {
            result.Description = $"{attacker.Name} промахивается по {defender.Name}";
        }

        result.Success = true;
        return Task.FromResult(result);
    }


    private Task<CombatActionResult> ResolveOpposedRoll(CombatEncounter encounter, CombatAction attackAction, CombatAction defenseAction)
    {
        var result = new CombatActionResult { Action = attackAction };
        var attacker = encounter.Participants.First(p => p.Id == attackAction.ActorId);
        var defender = encounter.Participants.First(p => p.Id == defenseAction.ActorId);

        // Use manual outcomes if provided; fallback to rolling
        attackAction.Roll = attackAction.AttackerProvidedSuccess.HasValue
            ? new DiceRoll
            {
                Result = attackAction.AttackerProvidedRoll ?? 0,
                TargetValue = attackAction.SkillValue,
                SuccessLevel = attackAction.AttackerProvidedSuccess.Value,
                IsCritical = attackAction.AttackerProvidedSuccess == SuccessLevel.CriticalSuccess,
                IsFumble = attackAction.AttackerProvidedSuccess == SuccessLevel.Fumble,
                Description = $"Ввод мастера: атака {attacker.Name}"
            }
            : _diceRoller.RollPercentile(attackAction.SkillValue, $"Атака {attacker.Name}");

        defenseAction.Roll = defenseAction.AttackerProvidedSuccess.HasValue
            ? new DiceRoll
            {
                Result = defenseAction.AttackerProvidedRoll ?? 0,
                TargetValue = defenseAction.SkillValue,
                SuccessLevel = defenseAction.AttackerProvidedSuccess.Value,
                IsCritical = defenseAction.AttackerProvidedSuccess == SuccessLevel.CriticalSuccess,
                IsFumble = defenseAction.AttackerProvidedSuccess == SuccessLevel.Fumble,
                Description = $"Ввод мастера: {defenseAction.ActionType} {defender.Name}"
            }
            : _diceRoller.RollPercentile(defenseAction.SkillValue, $"{defenseAction.ActionType} от {defender.Name}");

        var aLvl = attackAction.Roll.SuccessLevel;
        var dLvl = defenseAction.Roll.SuccessLevel;

        if (aLvl > dLvl)
        {
            // Attacker wins -> attacker deals damage
            var damageFormula = attackAction.Weapon?.Damage ?? "1d3";
            var damageBonus = attacker.CombatStats.DamageBonus;
            var isExtremeSuccess = attackAction.Roll.SuccessLevel == SuccessLevel.ExtremeSuccess;
            if (attackAction.ProvidedDamageTotal.HasValue)
            {
                attackAction.Damage = new DamageRoll
                {
                    TotalDamage = attackAction.ProvidedDamageTotal.Value,
                    DiceResults = ParseDiceList(attackAction.ProvidedDamageDiceList),
                    DamageBonus = 0,
                    DamageFormula = damageFormula,
                    IsMaxDamage = false
                };
            }
            else
            {
                if (isExtremeSuccess && attackAction.Weapon != null)
                {
                    var isFirearm = attackAction.Weapon.Type != WeaponType.Melee;
                    if (isFirearm)
                    {
                        var max = _diceRoller.GetMaximumDamage(damageFormula);
                        var bonus = _diceRoller.RollDamage(damageBonus, "0");
                        attackAction.Damage = new DamageRoll { TotalDamage = max + bonus.TotalDamage, DamageBonus = bonus.TotalDamage, DamageFormula = damageFormula, IsMaxDamage = true };
                    }
                    else
                    {
                        var max = _diceRoller.GetMaximumDamage(damageFormula);
                        var extra = _diceRoller.RollDamage(damageFormula, "0");
                        var bonus = _diceRoller.RollDamage(damageBonus, "0");
                        attackAction.Damage = new DamageRoll { TotalDamage = max + extra.TotalDamage + bonus.TotalDamage, DiceResults = extra.DiceResults, DamageBonus = bonus.TotalDamage, DamageFormula = damageFormula, IsMaxDamage = true };
                    }
                }
                else
                {
                    attackAction.Damage = _diceRoller.RollDamage(damageFormula, damageBonus, false);
                }
            }
            var armorReduction = _calculator.GetArmorReduction(defender);
            var finalDamage = Math.Max(0, attackAction.Damage.TotalDamage - armorReduction);
            defender.TakeDamage(finalDamage);
            result.DamageDealt = finalDamage;
            result.Description = $"{attacker.Name} побеждает в противоборстве и наносит {finalDamage} урона {defender.Name}.";
        }
        else if (dLvl > aLvl)
        {
            // Defender wins
            if (defenseAction.ActionType == CombatActionType.FightBack)
            {
                // Defender deals damage on successful fight back
                var damageFormula = defenseAction.Weapon?.Damage ?? "1d3";
                var damageBonus = defender.CombatStats.DamageBonus;
                defenseAction.Damage = _diceRoller.RollDamage(damageFormula, damageBonus, false);
                var armorReduction = _calculator.GetArmorReduction(attacker);
                var finalDamage = Math.Max(0, defenseAction.Damage.TotalDamage - armorReduction);
                attacker.TakeDamage(finalDamage);
                result.Description = $"{defender.Name} даёт отпор и наносит {finalDamage} урона {attacker.Name}.";
            }
            else
            {
                result.Description = $"{defender.Name} успешно уклоняется от атаки {attacker.Name}.";
            }
        }
        else
        {
            // Tie
            if (aLvl.IsSuccess() && dLvl.IsSuccess())
            {
                if (defenseAction.ActionType == CombatActionType.FightBack)
                {
                    // Tie vs Fight Back -> attacker wins
                    var damageFormula = attackAction.Weapon?.Damage ?? "1d3";
                    var damageBonus = attacker.CombatStats.DamageBonus;
                    var isExtremeSuccess = attackAction.Roll.SuccessLevel == SuccessLevel.ExtremeSuccess;
                    if (attackAction.ProvidedDamageTotal.HasValue)
                    {
                        attackAction.Damage = new DamageRoll
                        {
                            TotalDamage = attackAction.ProvidedDamageTotal.Value,
                            DiceResults = ParseDiceList(attackAction.ProvidedDamageDiceList),
                            DamageBonus = 0,
                            DamageFormula = damageFormula,
                            IsMaxDamage = false
                        };
                    }
                    else
                    {
                        attackAction.Damage = _diceRoller.RollDamage(damageFormula, damageBonus, isExtremeSuccess);
                    }
                    var armorReduction = _calculator.GetArmorReduction(defender);
                    var finalDamage = Math.Max(0, attackAction.Damage.TotalDamage - armorReduction);
                    defender.TakeDamage(finalDamage);
                    result.DamageDealt = finalDamage;
                    result.Description = $"Ничья уровней успеха: преимущество у атакующего. {attacker.Name} наносит {finalDamage} урона.";
                }
                else
                {
                    // Tie vs Dodge -> defender avoids
                    result.Description = $"Ничья уровней успеха: {defender.Name} избегает удара.";
                }
            }
            else
            {
                // Both failed or both not success -> no effect
                result.Description = $"{attacker.Name} атакует, но {defender.Name} избегает урона.";
            }
        }

        // Increment defender's action counters
        if (defenseAction.ActionType == CombatActionType.FightBack) defender.TimesUsedFightBack++;
        if (defenseAction.ActionType == CombatActionType.Dodge) defender.TimesUsedDodge++;

        result.Success = true;
        return Task.FromResult(result);
    }


    private Task<CombatActionResult> ExecuteFightBack(CombatEncounter encounter, CombatAction action)
    {
        var result = new CombatActionResult { Action = action };

        var defender = encounter.Participants.First(p => p.Id == action.ActorId);
        var attacker = encounter.Participants.FirstOrDefault(p => p.Id == action.TargetId);

        if (attacker == null)
        {
            result.Success = false;
            result.ErrorMessage = "Атакующий не найден";
            return Task.FromResult(result);
        }

        // This is part of an opposed roll - the actual resolution would happen
        // when both actions are declared and resolved together
        defender.TimesUsedFightBack++;

        result.Success = true;
        result.Description = $"{defender.Name} готовится дать отпор {attacker.Name}";

        return Task.FromResult(result);
    }

    private Task<CombatActionResult> ExecuteDodge(CombatEncounter encounter, CombatAction action)
    {
        var result = new CombatActionResult { Action = action };

        var defender = encounter.Participants.First(p => p.Id == action.ActorId);

        defender.TimesUsedDodge++;

        result.Success = true;
        result.Description = $"{defender.Name} готовится уклониться";

        return Task.FromResult(result);
    }

    private async Task<CombatActionResult> ExecuteSpellCast(CombatEncounter encounter, CombatAction action)
    {
        var result = new CombatActionResult { Action = action };

        var caster = encounter.Participants.First(p => p.Id == action.ActorId);

        if (action.Spell == null)
        {
            result.Success = false;
            result.ErrorMessage = "Заклинание не указано";
            return result;
        }

        // Check MP cost
        var mpCost = ParseMagicPointCost(action.Spell.Cost ?? "");
        if (caster.CurrentMagicPoints < mpCost)
        {
            result.Success = false;
            result.ErrorMessage = "Недостаточно пунктов магии";
            return result;
        }

        // Manual input preferred; fallback to rolling
        action.Roll = action.AttackerProvidedSuccess.HasValue
            ? new DiceRoll
            {
                Result = action.AttackerProvidedRoll ?? 0,
                TargetValue = action.SkillValue,
                SuccessLevel = action.AttackerProvidedSuccess.Value,
                IsCritical = action.AttackerProvidedSuccess == SuccessLevel.CriticalSuccess,
                IsFumble = action.AttackerProvidedSuccess == SuccessLevel.Fumble,
                Description = $"Ввод мастера: произнесение заклинания {action.Spell.Name}"
            }
            : _diceRoller.RollPercentile(action.SkillValue, $"Произнесение заклинания {action.Spell.Name}");

        if (action.Roll.SuccessLevel.IsSuccess())
        {
            caster.CurrentMagicPoints -= mpCost;
            result.Description = $"{caster.Name} успешно произносит {action.Spell.Name}";

            // Apply spell effects (this would be spell-specific)
            await ApplySpellEffects(encounter, action);
        }
        else
        {
            result.Description = $"{caster.Name} не смог произнести {action.Spell.Name}";
        }

        result.Success = true;
        return result;
    }

    private Task<CombatActionResult> ExecuteManeuver(CombatEncounter encounter, CombatAction action)
    {
        var result = new CombatActionResult { Action = action };

        if (action is not CombatManeuver maneuver)
        {
            result.Success = false;
            result.ErrorMessage = "Неверный тип маневра";
            return Task.FromResult(result);
        }

        var attacker = encounter.Participants.First(p => p.Id == action.ActorId);
        var defender = encounter.Participants.FirstOrDefault(p => p.Id == action.TargetId);

        if (defender == null)
        {
            result.Success = false;
            result.ErrorMessage = "Цель не найдена";
            return Task.FromResult(result);
        }

        // Check if maneuver is possible
        if (!_calculator.CanPerformManeuver(attacker, defender, maneuver.ManeuverType))
        {
            result.Success = false;
            result.ErrorMessage = "Маневр невозможен из-за разницы в телосложении";
            return Task.FromResult(result);
        }

        // Apply build difference penalty
        var penalty = _calculator.GetManeuverPenalty(attacker, defender, maneuver.ManeuverType);
        var modifiedSkill = action.SkillValue - penalty;

        action.Roll = action.AttackerProvidedSuccess.HasValue
            ? new DiceRoll
            {
                Result = action.AttackerProvidedRoll ?? 0,
                TargetValue = modifiedSkill,
                SuccessLevel = action.AttackerProvidedSuccess.Value,
                IsCritical = action.AttackerProvidedSuccess == SuccessLevel.CriticalSuccess,
                IsFumble = action.AttackerProvidedSuccess == SuccessLevel.Fumble,
                Description = $"Ввод мастера: маневр {maneuver.ManeuverType} от {attacker.Name}"
            }
            : _diceRoller.RollPercentile(modifiedSkill, $"Маневр {maneuver.ManeuverType} от {attacker.Name}");

        if (action.Roll.SuccessLevel.IsSuccess())
        {
            // Apply maneuver effects
            var condition = maneuver.ManeuverType switch
            {
                ManeuverType.KnockDown => new CombatCondition
                {
                    Type = CombatConditionType.Prone,
                    Description = "Сбит с ног"
                },
                ManeuverType.Grapple => new CombatCondition
                {
                    Type = CombatConditionType.Grappled,
                    Description = "Схвачен"
                },
                ManeuverType.Pin => new CombatCondition
                {
                    Type = CombatConditionType.Pinned,
                    Description = "Пригвожден"
                },
                _ => null
            };

            if (condition != null)
            {
                defender.AddCondition(condition);
                action.InflictedConditions.Add(condition);
            }

            result.Description = $"{attacker.Name} успешно выполняет маневр {maneuver.ManeuverType} против {defender.Name}";
        }
        else
        {
            result.Description = $"{attacker.Name} не смог выполнить маневр против {defender.Name}";
        }

        result.Success = true;
        return Task.FromResult(result);
    }

    private Task<CombatActionResult> ExecuteMove(CombatEncounter encounter, CombatAction action)
    {
        var result = new CombatActionResult { Action = action };
        var participant = encounter.Participants.First(p => p.Id == action.ActorId);

        // Simple movement - in a full implementation this would handle positioning
        result.Success = true;
        result.Description = $"{participant.Name} перемещается";

        return Task.FromResult(result);
    }

    private Task<CombatActionResult> ExecuteReady(CombatEncounter encounter, CombatAction action)
    {
        var result = new CombatActionResult { Action = action };
        var participant = encounter.Participants.First(p => p.Id == action.ActorId);

        // Ready action - prepare weapon, reload, etc.
        if (action.Weapon != null && action.Weapon.Type != WeaponType.Melee)
        {
            participant.CombatStats.HasReadyFirearm = true;
        }

        result.Success = true;
        result.Description = $"{participant.Name} готовится к действию";

        return Task.FromResult(result);
    }

    private Task<CombatActionResult> ExecuteFlee(CombatEncounter encounter, CombatAction action)
    {
        var result = new CombatActionResult { Action = action };
        var participant = encounter.Participants.First(p => p.Id == action.ActorId);

        // Remove participant from combat
        encounter.RemoveParticipant(participant.Id);

        result.Success = true;
        result.Description = $"{participant.Name} убегает из боя";

        return Task.FromResult(result);
    }

    private Task<CombatActionResult> ExecuteDoNothing(CombatEncounter encounter, CombatAction action)
    {
        var result = new CombatActionResult { Action = action };
        var participant = encounter.Participants.First(p => p.Id == action.ActorId);

        result.Success = true;
        result.Description = $"{participant.Name} ничего не делает";

        return Task.FromResult(result);
    }

    private Task ApplySpellEffects(CombatEncounter encounter, CombatAction action)
    {
        // This would contain spell-specific logic
        // For now, just a placeholder
        _logger.LogInformation("Applying effects of spell: {SpellName}", action.Spell?.Name);
        return Task.CompletedTask;
    }

    private int ParseMagicPointCost(string costString)
    {
        // Simple parser for MP costs like "3 MP", "1d4 MP", etc.
        if (string.IsNullOrEmpty(costString))
            return 0;

        var parts = costString.ToLower().Split(' ');
        foreach (var part in parts)
        {
            if (int.TryParse(part, out var cost))
                return cost;
        }

        return 1; // Default MP cost
    }

    public Task<CombatEncounter> ProcessRoundEnd(CombatEncounter encounter)
    {
        // Handle end-of-round effects
        foreach (var participant in encounter.Participants)
        {
            // Process dying participants
            if (participant.HasCondition(CombatConditionType.Dying))
            {
                // Roll CON to avoid death
                var conRoll = _diceRoller.RollConstitution(participant.CombatStats.Constitution,
                    $"Проверка на смерть для {participant.Name}");

                if (!conRoll.SuccessLevel.IsSuccess())
                {
                    participant.AddCondition(new CombatCondition
                    {
                        Type = CombatConditionType.Dead,
                        Description = "Умер от ран"
                    });
                }
            }

            // Process bleeding
            if (participant.HasCondition(CombatConditionType.Bleeding))
            {
                participant.TakeDamage(1); // 1 HP per round
            }
        }

        encounter.NextRound();
        return Task.FromResult(encounter);
    }

    private static List<int> ParseDiceList(string? list)
    {
        var results = new List<int>();
        if (string.IsNullOrWhiteSpace(list)) return results;
        var parts = list.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (int.TryParse(part.Trim(), out var val)) results.Add(val);
        }
        return results;
    }
}

public class CombatActionResult
{
    public CombatAction Action { get; set; } = null!;
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DamageDealt { get; set; }
    public List<CombatCondition> ConditionsInflicted { get; set; } = new();
}