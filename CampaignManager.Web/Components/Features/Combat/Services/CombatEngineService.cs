using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Weapons.Model;
using CampaignManager.Web.Components.Features.Spells.Model;

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

        encounter.StartCombat(new Random());

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
                CombatActionType.FightBack => await ExecuteFightBack(encounter, action),
                CombatActionType.Dodge => await ExecuteDodge(encounter, action),
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

        // Apply modifiers
        var attackSkill = action.SkillValue;
        var rangeModifier = action.Weapon != null ? _calculator.CalculateRangeModifier(action.Weapon, 0) : 0; // Distance would be calculated from positioning
        var outnumberedPenalty = _calculator.CalculateOutnumberedPenalty(defender);

        var modifiedSkill = attackSkill + rangeModifier - outnumberedPenalty;

        // Roll attack
        action.Roll = _diceRoller.RollPercentile(modifiedSkill,
            $"Атака {attacker.Name} на {defender.Name}");

        if (action.Roll.SuccessLevel.IsSuccess())
        {
            // Calculate damage
            var damageFormula = action.Weapon?.Damage ?? "1d3";
            var damageBonus = attacker.CombatStats.DamageBonus;
            var isExtremeSuccess = action.Roll.SuccessLevel == SuccessLevel.ExtremeSuccess;

            action.Damage = _diceRoller.RollDamage(damageFormula, damageBonus, isExtremeSuccess);

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
                // This would trigger a CON roll - for now just log it
                _logger.LogInformation("Participant {Name} needs CON roll for Major Wound",
                    defender.Name);
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

        // Roll for spell casting (assuming we have a spell skill)
        action.Roll = _diceRoller.RollPercentile(action.SkillValue,
            $"Произнесение заклинания {action.Spell.Name}");

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

        action.Roll = _diceRoller.RollPercentile(modifiedSkill,
            $"Маневр {maneuver.ManeuverType} от {attacker.Name}");

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
                var conRoll = _diceRoller.RollConstitution(60, // TODO: Get actual CON
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