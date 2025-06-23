using CampaignManager.Web.Components.Features.Weapons.Model;
using CampaignManager.Web.Components.Features.Spells.Model;

namespace CampaignManager.Web.Components.Features.Combat.Model;

public enum CombatActionType
{
    Attack = 1,
    FightBack = 2,
    Dodge = 3,
    CastSpell = 4,
    Maneuver = 5,
    Move = 6,
    Ready = 7,
    Flee = 8,
    DoNothing = 9
}

public class CombatAction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorId { get; set; }
    public Guid? TargetId { get; set; }
    public CombatActionType ActionType { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public int SkillValue { get; set; }
    public Weapon? Weapon { get; set; }
    public Spell? Spell { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
    public DiceRoll? Roll { get; set; }
    public DamageRoll? Damage { get; set; }
    public List<CombatCondition> InflictedConditions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int Round { get; set; }
}

public enum ManeuverType
{
    Disarm = 1,
    KnockDown = 2,
    Pin = 3,
    Grapple = 4,
    Trip = 5,
    Shove = 6
}

public class CombatManeuver : CombatAction
{
    public ManeuverType ManeuverType { get; set; }
    public int BuildDifference { get; set; }
    public bool IsImpossible { get; set; }
}

public static class CombatActionExtensions
{
    public static string GetDisplayName(this CombatActionType type)
    {
        return type switch
        {
            CombatActionType.Attack => "Атака",
            CombatActionType.FightBack => "Дать отпор",
            CombatActionType.Dodge => "Увернуться",
            CombatActionType.CastSpell => "Заклинание",
            CombatActionType.Maneuver => "Маневр",
            CombatActionType.Move => "Движение",
            CombatActionType.Ready => "Приготовиться",
            CombatActionType.Flee => "Бежать",
            CombatActionType.DoNothing => "Ничего не делать",
            _ => "Неизвестно"
        };
    }

    public static bool RequiresTarget(this CombatActionType type)
    {
        return type is CombatActionType.Attack or
            CombatActionType.FightBack or
            CombatActionType.CastSpell or
            CombatActionType.Maneuver;
    }

    public static bool RequiresSkillRoll(this CombatActionType type)
    {
        return type is CombatActionType.Attack or
            CombatActionType.FightBack or
            CombatActionType.Dodge or
            CombatActionType.CastSpell or
            CombatActionType.Maneuver;
    }
}