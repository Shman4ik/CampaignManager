using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Weapons.Model;

namespace CampaignManager.Web.Components.Features.Combat.Services;

public class CombatCalculationService
{
    public CombatStats CalculateCombatStats(Characteristics characteristics)
    {
        return CombatStats.CalculateFromCharacteristics(characteristics);
    }

    public int CalculateDamageBonus(int strength, int size)
    {
        var strPlusSize = strength + size;
        return strPlusSize switch
        {
            <= 64 => -2,
            <= 84 => -1,
            <= 124 => 0,
            <= 164 => 1,
            <= 204 => 2,
            <= 284 => 3,
            <= 364 => 4,
            <= 444 => 5,
            <= 524 => 6,
            _ => (strPlusSize - 524) / 80 + 6
        };
    }

    public int CalculateBuild(int size)
    {
        return size / 10;
    }

    public int CalculateMovementRate(int strength, int dexterity)
    {
        return (strength + dexterity) / 10;
    }

    public bool CanPerformManeuver(CombatParticipant attacker, CombatParticipant defender, ManeuverType maneuverType)
    {
        var buildDifference = Math.Abs(attacker.CombatStats.Build - defender.CombatStats.Build);

        // If build difference is 3 or more, maneuver is impossible
        return buildDifference < 3;
    }

    public int GetManeuverPenalty(CombatParticipant attacker, CombatParticipant defender, ManeuverType maneuverType)
    {
        var buildDifference = defender.CombatStats.Build - attacker.CombatStats.Build;

        // Each build point difference adds penalty die (roughly -10 per die)
        return Math.Max(0, buildDifference * 10);
    }

    public int CalculateRangeModifier(Weapon weapon, int distance)
    {
        if (weapon.Type == WeaponType.Melee)
            return 0;

        // Parse weapon range - this is simplified, real implementation would need better parsing
        var range = ParseWeaponRange(weapon.Range);

        if (distance <= range.PointBlank)
            return 10; // Bonus die for point blank
        if (distance <= range.Short)
            return 0; // No modifier for short range
        if (distance <= range.Medium)
            return -10; // Penalty for medium range
        if (distance <= range.Long)
            return -20; // Penalty for long range
        return -30; // Extreme range penalty
    }

    public int CalculateOutnumberedPenalty(CombatParticipant defender)
    {
        var timesDefended = defender.TimesUsedFightBack + defender.TimesUsedDodge;

        if (timesDefended == 0)
            return 0;

        // Each additional attack after the first gives attackers bonus dice
        return timesDefended * 10; // Roughly +10 per additional attack
    }

    public List<CombatCondition> DetermineWoundEffects(CombatParticipant target, int damage)
    {
        var conditions = new List<CombatCondition>();

        // Major Wound check
        if (damage >= target.MaxHitPoints / 2)
        {
            conditions.Add(new CombatCondition
            {
                Type = CombatConditionType.MajorWound,
                Description = $"Серьезная рана от {damage} урона",
                AppliedAt = DateTime.UtcNow
            });
        }

        // Instant death check
        if (damage >= target.MaxHitPoints)
        {
            conditions.Add(new CombatCondition
            {
                Type = CombatConditionType.Dead,
                Description = $"Мгновенная смерть от {damage} урона",
                AppliedAt = DateTime.UtcNow
            });
            return conditions; // No need to check other conditions
        }

        // Unconsciousness check (when HP reaches 0)
        if (target.CurrentHitPoints - damage <= 0)
        {
            conditions.Add(new CombatCondition
            {
                Type = CombatConditionType.Unconscious,
                Description = "Потерял сознание от ран",
                AppliedAt = DateTime.UtcNow
            });

            // Dying condition check (Major Wound + 0 HP)
            if (conditions.Any(c => c.Type == CombatConditionType.MajorWound) ||
                target.HasCondition(CombatConditionType.MajorWound))
            {
                conditions.Add(new CombatCondition
                {
                    Type = CombatConditionType.Dying,
                    Description = "Умирает от серьезных ран",
                    AppliedAt = DateTime.UtcNow
                });
            }
        }

        return conditions;
    }

    public bool RequiresConstitutionRoll(CombatParticipant participant, int damage)
    {
        // CON roll required for Major Wounds to avoid unconsciousness
        return damage >= participant.MaxHitPoints / 2;
    }

    public int GetArmorReduction(CombatParticipant participant, string hitLocation = "")
    {
        // This would be expanded to handle different armor types and locations
        // For now, return a simple armor value

        // TODO: Implement proper armor system
        // This should check participant's equipment for armor items
        // and return appropriate damage reduction based on hit location

        return 0; // No armor system implemented yet
    }

    public int ApplyDamageTypeModifiers(int baseDamage, string damageType, CombatParticipant target)
    {
        // Different damage types might have different effects
        // e.g., impaling weapons do more damage on extreme success

        return damageType.ToLower() switch
        {
            "impaling" => baseDamage, // Already handled in extreme success calculation
            "piercing" => baseDamage,
            "slashing" => baseDamage,
            "bludgeoning" => baseDamage,
            "fire" => baseDamage,
            "cold" => baseDamage,
            "electrical" => baseDamage,
            _ => baseDamage
        };
    }

    private WeaponRange ParseWeaponRange(string rangeString)
    {
        // This is a simplified parser - real implementation would handle complex range strings
        if (string.IsNullOrEmpty(rangeString))
            return new WeaponRange();

        if (int.TryParse(rangeString, out var singleRange))
        {
            return new WeaponRange
            {
                PointBlank = singleRange / 10,
                Short = singleRange / 4,
                Medium = singleRange / 2,
                Long = singleRange
            };
        }

        return new WeaponRange();
    }

    private class WeaponRange
    {
        public int PointBlank { get; set; } = 5;
        public int Short { get; set; } = 25;
        public int Medium { get; set; } = 50;
        public int Long { get; set; } = 100;
    }
}