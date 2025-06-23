using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Combat.Services;

public class DiceRollerService
{
    private readonly Random _random;

    public DiceRollerService()
    {
        _random = new Random();
    }

    public DiceRoll RollPercentile(int targetValue, string description = "")
    {
        var roll = _random.Next(1, 101);
        var successLevel = DetermineSuccessLevel(roll, targetValue);

        return new DiceRoll
        {
            Result = roll,
            TargetValue = targetValue,
            SuccessLevel = successLevel,
            IsCritical = roll == 1,
            IsFumble = roll == 100,
            Description = description
        };
    }

    public DiceRoll RollSkill(AttributeValue skill, string description = "")
    {
        return RollPercentile(skill.Regular, description);
    }

    public DiceRoll RollOpposed(int attackerSkill, int defenderSkill, string description = "")
    {
        var attackerRoll = RollPercentile(attackerSkill, $"Атакующий: {description}");
        var defenderRoll = RollPercentile(defenderSkill, $"Защитник: {description}");

        // Determine winner based on success levels
        var result = new DiceRoll
        {
            Result = attackerRoll.Result,
            TargetValue = attackerSkill,
            SuccessLevel = attackerRoll.SuccessLevel,
            IsCritical = attackerRoll.IsCritical,
            IsFumble = attackerRoll.IsFumble,
            Description = $"{description} - Атакующий: {attackerRoll.Result}/{attackerSkill} ({attackerRoll.SuccessLevel.GetDisplayName()}), " +
                          $"Защитник: {defenderRoll.Result}/{defenderSkill} ({defenderRoll.SuccessLevel.GetDisplayName()})"
        };

        // Override success level based on opposed roll result
        if (attackerRoll.SuccessLevel.BeatsLevel(defenderRoll.SuccessLevel))
        {
            result.SuccessLevel = attackerRoll.SuccessLevel;
        }
        else if (defenderRoll.SuccessLevel.BeatsLevel(attackerRoll.SuccessLevel))
        {
            result.SuccessLevel = SuccessLevel.Failure;
        }
        else if (attackerRoll.SuccessLevel == defenderRoll.SuccessLevel && attackerRoll.SuccessLevel.IsSuccess())
        {
            // Tie goes to attacker in melee combat
            result.SuccessLevel = attackerRoll.SuccessLevel;
        }
        else
        {
            result.SuccessLevel = SuccessLevel.Failure;
        }

        return result;
    }

    public DamageRoll RollDamage(string damageFormula, int damageBonus = 0, bool isExtremeSuccess = false)
    {
        var damage = ParseAndRollDamage(damageFormula);

        if (isExtremeSuccess)
        {
            // Extreme success: maximum damage + damage bonus + additional roll
            damage.TotalDamage = GetMaximumDamage(damageFormula) + damageBonus + ParseAndRollDamage(damageFormula).TotalDamage;
            damage.IsMaxDamage = true;
        }
        else
        {
            damage.TotalDamage += damageBonus;
        }

        damage.DamageBonus = damageBonus;
        damage.DamageFormula = damageFormula;

        return damage;
    }

    private SuccessLevel DetermineSuccessLevel(int roll, int targetValue)
    {
        if (roll == 1)
            return SuccessLevel.CriticalSuccess;

        if (roll == 100)
            return SuccessLevel.Fumble;

        if (roll > targetValue)
            return SuccessLevel.Failure;

        if (roll <= targetValue / 5)
            return SuccessLevel.ExtremeSuccess;

        if (roll <= targetValue / 2)
            return SuccessLevel.HardSuccess;

        return SuccessLevel.RegularSuccess;
    }

    private DamageRoll ParseAndRollDamage(string formula)
    {
        var result = new DamageRoll();

        // Simple parser for common formats like "1d6", "2d6+2", "1d8+1"
        var cleanFormula = formula.Trim().ToLower().Replace(" ", "");

        if (string.IsNullOrEmpty(cleanFormula))
        {
            return result;
        }

        try
        {
            // Split by '+' or '-' to handle modifiers
            var parts = cleanFormula.Split(new[] { '+', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var isNegative = cleanFormula.Contains('-');

            var mainPart = parts[0];
            var modifier = 0;

            if (parts.Length > 1 && int.TryParse(parts[1], out var mod))
            {
                modifier = isNegative ? -mod : mod;
            }

            // Parse dice notation (e.g., "1d6", "2d8")
            if (mainPart.Contains('d'))
            {
                var diceParts = mainPart.Split('d');
                if (diceParts.Length == 2 &&
                    int.TryParse(diceParts[0], out var numberOfDice) &&
                    int.TryParse(diceParts[1], out var diceSize))
                {
                    for (int i = 0; i < numberOfDice; i++)
                    {
                        var roll = _random.Next(1, diceSize + 1);
                        result.DiceResults.Add(roll);
                        result.TotalDamage += roll;
                    }
                }
            }
            else if (int.TryParse(mainPart, out var fixedDamage))
            {
                // Fixed damage value
                result.TotalDamage = fixedDamage;
                result.DiceResults.Add(fixedDamage);
            }

            result.TotalDamage += modifier;
            result.TotalDamage = Math.Max(0, result.TotalDamage); // Damage can't be negative
        }
        catch
        {
            // If parsing fails, return 0 damage
            result.TotalDamage = 0;
        }

        return result;
    }

    private int GetMaximumDamage(string formula)
    {
        var cleanFormula = formula.Trim().ToLower().Replace(" ", "");

        if (string.IsNullOrEmpty(cleanFormula))
            return 0;

        try
        {
            var parts = cleanFormula.Split(new[] { '+', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var isNegative = cleanFormula.Contains('-');

            var mainPart = parts[0];
            var modifier = 0;

            if (parts.Length > 1 && int.TryParse(parts[1], out var mod))
            {
                modifier = isNegative ? -mod : mod;
            }

            var maxDamage = 0;

            if (mainPart.Contains('d'))
            {
                var diceParts = mainPart.Split('d');
                if (diceParts.Length == 2 &&
                    int.TryParse(diceParts[0], out var numberOfDice) &&
                    int.TryParse(diceParts[1], out var diceSize))
                {
                    maxDamage = numberOfDice * diceSize;
                }
            }
            else if (int.TryParse(mainPart, out var fixedDamage))
            {
                maxDamage = fixedDamage;
            }

            return Math.Max(0, maxDamage + modifier);
        }
        catch
        {
            return 0;
        }
    }

    public int RollInitiative()
    {
        return _random.Next(1, 101);
    }

    public DiceRoll RollConstitution(int constitution, string description = "Проверка Телосложения")
    {
        return RollPercentile(constitution, description);
    }

    public DiceRoll RollSanity(int sanity, string description = "Проверка Рассудка")
    {
        return RollPercentile(sanity, description);
    }
}