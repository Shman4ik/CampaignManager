using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Combat.Model;

public class Combatant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int Dexterity { get; set; }
    public int Initiative { get; set; }
    
    public int MaxHitPoints { get; set; }
    public int CurrentHitPoints { get; set; }
    
    public int MaxMagicPoints { get; set; }
    public int CurrentMagicPoints { get; set; }
    
    public int MaxSanity { get; set; }
    public int CurrentSanity { get; set; }
    
    public bool IsPlayer { get; set; }
    
    // Reference to the original entity (optional, for saving back)
    public Character? CharacterSource { get; set; }
    public Creature? CreatureSource { get; set; }

    public Combatant() { }

    public Combatant(Character character)
    {
        Id = character.Id;
        Name = character.PersonalInfo.Name;
        Dexterity = character.Characteristics.Dexterity.Regular;
        Initiative = character.Characteristics.Dexterity.Regular; // Default initiative is DEX
        
        MaxHitPoints = character.DerivedAttributes.HitPoints.MaxValue;
        CurrentHitPoints = character.DerivedAttributes.HitPoints.Value;
        
        MaxMagicPoints = character.DerivedAttributes.MagicPoints.MaxValue;
        CurrentMagicPoints = character.DerivedAttributes.MagicPoints.Value;
        
        MaxSanity = character.DerivedAttributes.Sanity.MaxValue;
        CurrentSanity = character.DerivedAttributes.Sanity.Value;
        
        IsPlayer = true;
        CharacterSource = character;
    }

    public Combatant(Creature creature)
    {
        Id = Guid.NewGuid(); // Creatures might be instantiated multiple times, so new ID
        Name = creature.Name;
        Dexterity = creature.CreatureCharacteristics.Dexterity.Value;
        Initiative = creature.CreatureCharacteristics.Initiative;
        
        MaxHitPoints = creature.CreatureCharacteristics.HealPoint;
        CurrentHitPoints = creature.CreatureCharacteristics.HealPoint;
        
        MaxMagicPoints = creature.CreatureCharacteristics.ManaPoint;
        CurrentMagicPoints = creature.CreatureCharacteristics.ManaPoint;
        
        // Monsters usually don't track Sanity the same way, but we can keep it 0 or use Power
        MaxSanity = creature.CreatureCharacteristics.Power.Value; 
        CurrentSanity = creature.CreatureCharacteristics.Power.Value;
        
        IsPlayer = false;
        CreatureSource = creature;
    }
}
