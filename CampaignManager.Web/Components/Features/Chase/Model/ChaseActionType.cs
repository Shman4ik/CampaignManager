namespace CampaignManager.Web.Components.Features.Chase.Model;

public enum ChaseActionType
{
    SpeedCheck,
    MovementAction,
    HazardCheck,
    BarrierCheck,
    BarrierDestroy,
    MeleeAttack,
    RangedAttack,
    CombatManeuver,
    SkipAction,
    CaughtEvent,
    EscapedEvent,
    Other,

    // Часть 4 — столкновения транспорта
    VehicleCollision,
    TyreShot,
    DriverControlCheck,

    // Часть 5 — необязательные правила
    FloorIt,
    RandomHazardRoll,
    SuddenHazard,
    TrackingCheck,
    HideAttempt,
    CreateObstacle,
    ModeChange,
    JoinChase
}
