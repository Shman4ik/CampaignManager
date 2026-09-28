namespace CampaignManager.Web.Components.Features.Combat.Model;

public enum CombatActionType
{
    MeleeAttack,
    RangedAttack,
    Dodge,
    FightBack,
    Maneuver,
    TakeCover,
    FirstAid,
    DelayTurn,
    FleeFromMelee,
    SanityCheck,
    Medicine,

    /// <summary>Сотворение заклинания (гл. 9). Добавлено в конец: журнал в снапшоте хранит число.</summary>
    CastSpell
}
