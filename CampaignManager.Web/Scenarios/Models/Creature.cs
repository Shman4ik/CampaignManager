using CampaignManager.Web.Model;

namespace CampaignManager.Web.Scenarios.Models;

/// <summary>
/// Model for monsters and supernatural entities
/// </summary>
public sealed class Creature : BaseDataBaseEntity
{
    public required string Name { get; set; }


    public CreatureType Type { get; set; }

    /// <summary>
    /// Detailed description of the creature
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Simplified statistics for the creature (stored as JSON)
    /// </summary>
    public CreatureCharacteristics CreatureCharacteristics { get; set; }

    /// <summary>
    /// Бой
    /// </summary>
    public Dictionary<string, string> CombatDescriptions { get; set; }

    /// <summary>
    /// Особые умения
    /// </summary>
    public Dictionary<string, string> SpecialAbilities { get; set; }

    /// <summary>
    /// Optional URL to an image of the creature
    /// </summary>
    public string? ImageUrl { get; set; }


    /// <summary>
    /// Collection of scenario-creature relationships
    /// </summary>
    public ICollection<ScenarioCreature> ScenarioCreatures { get; set; } = [];
}


public class CreatureCharacteristicModel
{
    /// <summary>
    /// Name of the characteristic
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Numeric value of the characteristic
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// Dice roll description (e.g. "2d6+5")
    /// </summary>
    public string? DiceRoll { get; set; }
}


public class CreatureCharacteristics
{
    public CreatureCharacteristicModel Strength { get; set; }
    public CreatureCharacteristicModel Dexterity { get; set; }
    public CreatureCharacteristicModel Intelligence { get; set; }
    public CreatureCharacteristicModel Constitution { get; set; }
    public CreatureCharacteristicModel Appearance { get; set; }
    public CreatureCharacteristicModel Power { get; set; }
    public CreatureCharacteristicModel Size { get; set; }
    public CreatureCharacteristicModel Education { get; set; }

    /// <summary>
    /// Initiative value (ИН. В)
    /// </summary>
    public int Initiative { get; set; }

    /// <summary>
    /// Average bonus to damage (Средний бонус к урону)
    /// </summary>
    public int AverageBonusToHit { get; set; }

    /// <summary>
    /// Average complexity/constitution rating (Средняя Комплекция)
    /// </summary>
    public int AverageComplexity { get; set; }

    /// <summary>
    /// Movement speed (Скорость)
    /// </summary>
    public int Speed { get; set; }

    /// <summary>
    /// ПЗ пункты здоровья
    /// </summary>
    public int HealPoint { get; set; }

    /// <summary>
    /// ПМ пункты магии
    /// </summary>
    public int ManaPoint { get; set; }

    /// <summary>
    /// Комплекция
    /// </summary>
    public int Constitutions { get; set; }
}

public enum CreatureType
{
    Other,
    MythicMonsters,
    MythicGods,
    Monsters
}
