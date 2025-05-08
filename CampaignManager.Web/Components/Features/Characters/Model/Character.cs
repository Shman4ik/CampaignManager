using CampaignManager.Web.Components.Features.Spells.Model;
using CampaignManager.Web.Components.Features.Weapons.Model;

namespace CampaignManager.Web.Components.Features.Characters.Model;

public class Character
{
    public Guid Id { get; set; }

    /// <summary>
    ///     Персональная информация о персонаже
    /// </summary>
    public PersonalInfo PersonalInfo { get; set; } = new();

    /// <summary>
    ///     Характеристики персонажа
    /// </summary>
    public Characteristics Characteristics { get; set; } = new();

    /// <summary>
    ///     Производные характеристики персонажа
    /// </summary>
    public DerivedAttributes DerivedAttributes { get; set; } = new();

    /// <summary>
    ///     Навыки персонажа
    /// </summary>
    public SkillsModel Skills { get; set; } = new();

    /// <summary>
    ///     Предыстория персонажа
    /// </summary>
    public string Backstory { get; set; } = "";

    /// <summary>
    ///     Состояние персонажа
    /// </summary>
    public CharacterState State { get; set; } = new();

    /// <summary>
    ///     Биография персонажа
    /// </summary>
    public BiographyInfo Biography { get; set; } = new();

    /// <summary>
    ///     Снаряжение и имущество персонажа
    /// </summary>
    public Equipment Equipment { get; set; } = new();

    /// <summary>
    ///     Финансы персонажа
    /// </summary>
    public Finances Finances { get; set; } = new();

    /// <summary>
    ///     Список оружия персонажа
    /// </summary>
    public List<Weapon> Weapons { get; set; } = new();

    /// <summary>
    ///     Список заклинаний персонажа
    /// </summary>
    public List<Spell> Spells { get; set; } = new();

    /// <summary>
    ///     Заметки
    /// </summary>
    public string Notes { get; set; } = "";

    /// <summary>
    ///     Тип персонажа (игровой персонаж или NPC)
    /// </summary>
    public CharacterType CharacterType { get; set; } = CharacterType.PlayerCharacter;
}