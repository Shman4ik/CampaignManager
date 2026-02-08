using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Web.Components.Features.Chase.Model;

public class ChaseParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public ChaseRole Role { get; set; }
    public bool IsPlayer { get; set; }

    // Транспорт
    public bool IsInVehicle { get; set; }
    public string? VehicleName { get; set; }
    public int VehicleSpeed { get; set; }

    // Характеристики
    public int MovementRate { get; set; }
    public int Dexterity { get; set; }
    public int ConstitutionValue { get; set; }
    public int MaxHitPoints { get; set; }
    public int CurrentHitPoints { get; set; }
    public int DrivingSkill { get; set; }

    public int EffectiveSpeed => IsInVehicle ? VehicleSpeed : MovementRate;

    // Позиция и состояние
    public int CurrentLocation { get; set; }
    public int SpeedMovesThisRound { get; set; }
    public int ExtraMovesAttempted { get; set; }
    public bool HasActedThisRound { get; set; }
    public bool IsExhausted { get; set; }
    public bool IsEliminated { get; set; }
    public bool HasEscaped { get; set; }
    public bool IsCaught { get; set; }

    // Ссылки на источник
    public Character? CharacterSource { get; set; }
    public Creature? CreatureSource { get; set; }

    public bool IsActive => !IsEliminated && !HasEscaped && !IsCaught;

    public ChaseParticipant() { }

    public ChaseParticipant(Character character)
    {
        Id = character.Id;
        Name = character.PersonalInfo.Name;
        IsPlayer = true;
        CharacterSource = character;

        MovementRate = character.PersonalInfo.MoveSpeed;
        Dexterity = character.Characteristics.Dexterity.Regular;
        ConstitutionValue = character.Characteristics.Constitution.Regular;
        MaxHitPoints = character.DerivedAttributes.HitPoints.MaxValue;
        CurrentHitPoints = character.DerivedAttributes.HitPoints.Value;
        DrivingSkill = CombatService.FindSkillValue(character, "Вождение");
    }

    public ChaseParticipant(Creature creature)
    {
        Id = Guid.NewGuid();
        Name = creature.Name;
        IsPlayer = false;
        CreatureSource = creature;

        MovementRate = creature.CreatureCharacteristics.Speed;
        Dexterity = creature.CreatureCharacteristics.Dexterity.Value;
        ConstitutionValue = creature.CreatureCharacteristics.Constitution.Value;
        MaxHitPoints = creature.CreatureCharacteristics.HealPoint;
        CurrentHitPoints = creature.CreatureCharacteristics.HealPoint;
    }
}
