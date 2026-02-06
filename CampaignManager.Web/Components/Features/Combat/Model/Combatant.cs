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

    // Состояния
    public bool IsUnconscious { get; set; }
    public bool HasMajorWound { get; set; }
    public bool IsDying { get; set; }
    public bool IsDead { get; set; }
    public bool HasTemporaryInsanity { get; set; }
    public bool HasIndefiniteInsanity { get; set; }

    // Боевые характеристики
    public int DodgeSkill { get; set; }
    public int FightingSkill { get; set; }
    public string DamageBonus { get; set; } = "0";
    public int Build { get; set; }
    public int ConstitutionValue { get; set; }
    public int Armor { get; set; }

    // Ссылки на исходные сущности
    public Character? CharacterSource { get; set; }
    public Creature? CreatureSource { get; set; }

    public Combatant() { }

    public Combatant(Character character)
    {
        Id = character.Id;
        Name = character.PersonalInfo.Name;
        Dexterity = character.Characteristics.Dexterity.Regular;
        Initiative = character.Characteristics.Dexterity.Regular;

        MaxHitPoints = character.DerivedAttributes.HitPoints.MaxValue;
        CurrentHitPoints = character.DerivedAttributes.HitPoints.Value;

        MaxMagicPoints = character.DerivedAttributes.MagicPoints.MaxValue;
        CurrentMagicPoints = character.DerivedAttributes.MagicPoints.Value;

        MaxSanity = character.DerivedAttributes.Sanity.MaxValue;
        CurrentSanity = character.DerivedAttributes.Sanity.Value;

        IsPlayer = true;
        CharacterSource = character;

        // Боевые характеристики
        DodgeSkill = character.PersonalInfo.Dodge;
        FightingSkill = FindFightingSkill(character);
        DamageBonus = character.PersonalInfo.DamageBonus;
        _ = int.TryParse(character.PersonalInfo.Build, out var buildValue);
        Build = buildValue;
        ConstitutionValue = character.Characteristics.Constitution.Regular;

        // Состояния из персонажа
        if (character.State != null)
        {
            IsUnconscious = character.State.IsUnconscious;
            HasMajorWound = character.State.HasSeriousInjury;
            IsDying = character.State.IsDying;
            HasTemporaryInsanity = character.State.HasTemporaryInsanity;
            HasIndefiniteInsanity = character.State.HasIndefiniteInsanity;
        }
    }

    public Combatant(Creature creature)
    {
        Id = Guid.NewGuid();
        Name = creature.Name;
        Dexterity = creature.CreatureCharacteristics.Dexterity.Value;
        Initiative = creature.CreatureCharacteristics.Initiative;

        MaxHitPoints = creature.CreatureCharacteristics.HealPoint;
        CurrentHitPoints = creature.CreatureCharacteristics.HealPoint;

        MaxMagicPoints = creature.CreatureCharacteristics.ManaPoint;
        CurrentMagicPoints = creature.CreatureCharacteristics.ManaPoint;

        MaxSanity = creature.CreatureCharacteristics.Power.Value;
        CurrentSanity = creature.CreatureCharacteristics.Power.Value;

        IsPlayer = false;
        CreatureSource = creature;

        // Боевые характеристики существа
        DamageBonus = creature.CreatureCharacteristics.AverageBonusToHit;
        ConstitutionValue = creature.CreatureCharacteristics.Constitution.Value;
        Build = creature.CreatureCharacteristics.AverageComplexity;
        DodgeSkill = creature.CreatureCharacteristics.Dexterity.Value / 2; // половина ЛВК как уклонение по умолчанию
    }

    private static int FindFightingSkill(Character character)
    {
        if (character.Skills?.SkillGroups == null) return 25;

        foreach (var group in character.Skills.SkillGroups)
        {
            foreach (var skill in group.Skills)
            {
                if (skill.Name.Contains("Ближний бой", StringComparison.OrdinalIgnoreCase)
                    || skill.Name.Contains("драка", StringComparison.OrdinalIgnoreCase))
                {
                    return skill.Value.Regular;
                }
            }
        }

        return 25; // базовое значение
    }
}
