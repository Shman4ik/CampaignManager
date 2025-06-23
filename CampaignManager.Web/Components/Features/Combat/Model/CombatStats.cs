using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Combat.Model;

public class CombatStats
{
    public int Initiative { get; set; }
    public int DamageBonus { get; set; }
    public int Build { get; set; }
    public int MovementRate { get; set; }
    public bool HasReadyFirearm { get; set; }

    public static CombatStats CalculateFromCharacteristics(Characteristics characteristics)
    {
        var stats = new CombatStats();

        // Initiative = DEX (+ 50 if ready firearm)
        stats.Initiative = characteristics.Dexterity.Regular;

        // Damage Bonus calculation based on STR + SIZ
        var strPlusSize = characteristics.Strength.Regular + characteristics.Size.Regular;
        stats.DamageBonus = CalculateDamageBonus(strPlusSize);

        // Build = SIZE in 10s (rounded down)
        stats.Build = characteristics.Size.Regular / 10;

        // Movement Rate = (STR + DEX) / 10 (rounded down)
        stats.MovementRate = (characteristics.Strength.Regular + characteristics.Dexterity.Regular) / 10;

        return stats;
    }

    private static int CalculateDamageBonus(int strPlusSize)
    {
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

    public int GetInitiativeWithFirearm()
    {
        return HasReadyFirearm ? Initiative + 50 : Initiative;
    }
}