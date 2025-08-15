using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Bestiary.Model;

namespace CampaignManager.Web.Components.Features.Combat.Model;

public class CombatStats
{
    public int Strength { get; set; }
    public int Constitution { get; set; }
    public int Dexterity { get; set; }
    public int Size { get; set; }
    public int HitPoints { get; set; }
    public int Initiative => Dexterity;
    public string DamageBonus { get; set; } = "0";
    public int Build { get; set; }
    public int MovementRate { get; set; }
    public bool HasReadyFirearm { get; set; }
    public Dictionary<string, int> Skills { get; set; } = new();

    public static CombatStats CalculateFromCharacteristics(Characteristics characteristics)
    {
        var strPlusSize = characteristics.Strength.Regular + characteristics.Size.Regular;

        return new CombatStats
        {
            Strength = characteristics.Strength.Regular,
            Constitution = characteristics.Constitution.Regular,
            Dexterity = characteristics.Dexterity.Regular,
            Size = characteristics.Size.Regular,
            HitPoints = (characteristics.Constitution.Regular + characteristics.Size.Regular) / 10,
            DamageBonus = CalculateDamageBonus(strPlusSize),
            Build = CalculateBuild(strPlusSize),
            MovementRate = CalculateMovementRate(characteristics.Strength.Regular, characteristics.Dexterity.Regular, characteristics.Size.Regular),
            Skills = new Dictionary<string, int>
            {
                { "Dodge", characteristics.Dexterity.Half },
                // Add other relevant combat skills from the character sheet
            }
        };
    }

    public static CombatStats CalculateFromCreatureCharacteristics(CreatureCharacteristics characteristics)
    {
        var str = characteristics.Strength?.Value ?? 50;
        var con = characteristics.Constitution?.Value ?? 50;
        var siz = characteristics.Size?.Value ?? 50;
        var dex = characteristics.Dexterity?.Value ?? 50;

        var strPlusSize = str + siz;

        return new CombatStats
        {
            Strength = str,
            Constitution = con,
            Dexterity = dex,
            Size = siz,
            HitPoints = (con + siz) / 10,
            DamageBonus = CalculateDamageBonus(strPlusSize),
            Build = CalculateBuild(strPlusSize),
            MovementRate = CalculateMovementRate(str, dex, siz),
            Skills = new Dictionary<string, int>
            {
                { "Dodge", dex / 2 },
                // Creatures might have fixed skill values
            }
        };
    }


    private static string CalculateDamageBonus(int strPlusSize)
    {
        return strPlusSize switch
        {
            >= 2 and <= 64 => "-2",
            >= 65 and <= 84 => "-1",
            >= 85 and <= 124 => "0",
            >= 125 and <= 164 => "+1d4",
            >= 165 and <= 204 => "+1d6",
            >= 205 and <= 284 => "+2d6",
            >= 285 and <= 364 => "+3d6",
            >= 365 and <= 444 => "+4d6",
            >= 445 and <= 524 => "+5d6",
            _ => $"+{(strPlusSize - 445) / 80 + 5}d6"
        };
    }

    private static int CalculateBuild(int strPlusSize)
    {
        return strPlusSize switch
        {
            >= 2 and <= 64 => -2,
            >= 65 and <= 84 => -1,
            >= 85 and <= 124 => 0,
            >= 125 and <= 164 => 1,
            >= 165 and <= 204 => 2,
            _ => (strPlusSize - 204) / 80 + 3
        };
    }

    private static int CalculateMovementRate(int str, int dex, int siz)
    {
        if (dex < siz && str < siz) return 7;
        if (dex >= siz || str >= siz) return 8;
        if (dex > siz && str > siz) return 9;
        return 8; // Default
    }


    public int GetInitiativeWithFirearm()
    {
        return HasReadyFirearm ? Initiative + 50 : Initiative;
    }

    public int GetSkillValue(string skillName)
    {
        return Skills.TryGetValue(skillName, out var value) ? value : 0;
    }
}