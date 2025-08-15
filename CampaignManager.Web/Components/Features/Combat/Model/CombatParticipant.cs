using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Combat.Model;

public class CombatParticipant : BaseDataBaseEntity
{
    public CombatParticipant()
    {
        Init();
    }

    public Guid CombatEncounterId { get; set; }
    public Guid? CharacterId { get; set; }
    public Guid? CreatureId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Character"; // Character, Creature, NPC
    public string? SourceId { get; set; } // For tracking original source in UI

    // Combat Status
    public int CurrentHitPoints { get; set; }
    public int MaxHitPoints { get; set; }
    public int CurrentMagicPoints { get; set; }
    public int MaxMagicPoints { get; set; }
    public int CurrentSanity { get; set; }
    public int MaxSanity { get; set; }

    // Combat Stats
    public CombatStats CombatStats { get; set; } = new();
    public List<CombatCondition> Conditions { get; set; } = new();
    public List<CombatAction> Actions { get; set; } = new();

    // Initiative and Turn Order
    public int TotalInitiative => CombatStats.Initiative;
    public bool HasActedThisRound { get; set; }

    // Round tracking
    public int TimesUsedFightBack { get; set; } = 0;
    public int TimesUsedDodge { get; set; } = 0;

    // Positioning (for future use)
    public int PositionX { get; set; } = 0;
    public int PositionY { get; set; } = 0;

    public bool IsConscious => !HasCondition(CombatConditionType.Unconscious) &&
                               !HasCondition(CombatConditionType.Dead) &&
                               CurrentHitPoints > 0;

    public bool CanAct => IsConscious &&
                          !HasCondition(CombatConditionType.Stunned) &&
                          !HasCondition(CombatConditionType.Dying);

    public bool HasCondition(CombatConditionType conditionType)
    {
        return Conditions.Any(c => c.Type == conditionType && !c.IsExpired);
    }

    public void AddCondition(CombatCondition condition)
    {
        // Remove existing condition of same type
        Conditions.RemoveAll(c => c.Type == condition.Type);
        Conditions.Add(condition);
    }

    public void RemoveCondition(CombatConditionType conditionType)
    {
        Conditions.RemoveAll(c => c.Type == conditionType);
    }

    public void TakeDamage(int damage)
    {
        CurrentHitPoints = Math.Max(0, CurrentHitPoints - damage);

        // Check for Major Wound (damage >= half max HP)
        if (damage >= MaxHitPoints / 2)
        {
            AddCondition(new CombatCondition
            {
                Type = CombatConditionType.MajorWound,
                Description = $"Серьезная рана от {damage} урона"
            });
        }

        // Check for unconsciousness
        if (CurrentHitPoints <= 0)
        {
            AddCondition(new CombatCondition
            {
                Type = CombatConditionType.Unconscious,
                Description = "Потерял сознание от ран"
            });

            // Check for dying condition
            if (HasCondition(CombatConditionType.MajorWound))
            {
                AddCondition(new CombatCondition
                {
                    Type = CombatConditionType.Dying,
                    Description = "Умирает от серьезных ран"
                });
            }
        }
    }

    public void Heal(int amount)
    {
        CurrentHitPoints = Math.Min(MaxHitPoints, CurrentHitPoints + amount);

        // Remove unconscious condition if healed above 0
        if (CurrentHitPoints > 0 && HasCondition(CombatConditionType.Unconscious))
        {
            RemoveCondition(CombatConditionType.Unconscious);
        }
    }

    public void ResetRoundCounters()
    {
        HasActedThisRound = false;
        TimesUsedFightBack = 0;
        TimesUsedDodge = 0;

        // Update condition durations
        foreach (var condition in Conditions.Where(c => c.Duration > 0))
        {
            condition.RoundsRemaining--;
        }

        // Remove expired conditions
        Conditions.RemoveAll(c => c.IsExpired);
    }

    public static CombatParticipant FromCharacter(Character character)
    {
        var participant = new CombatParticipant
        {
            CharacterId = character.Id,
            Name = character.PersonalInfo.Name,
            Type = "Character",
            CurrentHitPoints = character.DerivedAttributes.HitPoints.Value,
            MaxHitPoints = character.DerivedAttributes.HitPoints.MaxValue,
            CurrentMagicPoints = character.DerivedAttributes.MagicPoints.Value,
            MaxMagicPoints = character.DerivedAttributes.MagicPoints.MaxValue,
            CurrentSanity = character.DerivedAttributes.Sanity.Value,
            MaxSanity = character.DerivedAttributes.Sanity.MaxValue,
            CombatStats = CombatStats.CalculateFromCharacteristics(character.Characteristics)
        };

        return participant;
    }

    public static CombatParticipant FromCreature(Creature creature)
    {
        var combatStats = CombatStats.CalculateFromCreatureCharacteristics(creature.CreatureCharacteristics);
        var participant = new CombatParticipant
        {
            CreatureId = creature.Id,
            Name = creature.Name,
            Type = "Creature",
            CurrentHitPoints = combatStats.HitPoints,
            MaxHitPoints = combatStats.HitPoints,
            CurrentMagicPoints = creature.CreatureCharacteristics?.Power?.Value ?? 0,
            MaxMagicPoints = creature.CreatureCharacteristics?.Power?.Value ?? 0,
            CurrentSanity = 0, // Creatures typically don't have SAN
            MaxSanity = 0,
            CombatStats = combatStats
        };

        return participant;
    }
}