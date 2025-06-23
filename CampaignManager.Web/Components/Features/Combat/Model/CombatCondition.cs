namespace CampaignManager.Web.Components.Features.Combat.Model;

public enum CombatConditionType
{
    None = 0,
    Prone = 1,
    Stunned = 2,
    Grappled = 3,
    Pinned = 4,
    Unconscious = 5,
    Dying = 6,
    Dead = 7,
    TemporaryInsanity = 8,
    MajorWound = 9,
    Bleeding = 10
}

public class CombatCondition
{
    public CombatConditionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Duration { get; set; } = -1; // -1 = permanent until removed
    public int RoundsRemaining { get; set; }
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public string AppliedBy { get; set; } = string.Empty;

    public bool IsExpired => Duration > 0 && RoundsRemaining <= 0;
    public bool IsPermanent => Duration == -1;
}

public static class CombatConditionExtensions
{
    public static string GetDisplayName(this CombatConditionType type)
    {
        return type switch
        {
            CombatConditionType.None => "Норма",
            CombatConditionType.Prone => "Лежит",
            CombatConditionType.Stunned => "Оглушен",
            CombatConditionType.Grappled => "Схвачен",
            CombatConditionType.Pinned => "Пригвожден",
            CombatConditionType.Unconscious => "Без сознания",
            CombatConditionType.Dying => "Умирает",
            CombatConditionType.Dead => "Мертв",
            CombatConditionType.TemporaryInsanity => "Временное безумие",
            CombatConditionType.MajorWound => "Серьезная рана",
            CombatConditionType.Bleeding => "Кровотечение",
            _ => "Неизвестно"
        };
    }

    public static string GetDescription(this CombatConditionType type)
    {
        return type switch
        {
            CombatConditionType.Prone => "Персонаж лежит на земле. Штраф к атакам ближнего боя.",
            CombatConditionType.Stunned => "Персонаж оглушен и не может действовать.",
            CombatConditionType.Grappled => "Персонаж схвачен противником.",
            CombatConditionType.Pinned => "Персонаж пригвожден и не может двигаться.",
            CombatConditionType.Unconscious => "Персонаж без сознания (0 НР).",
            CombatConditionType.Dying => "Персонаж умирает. Требуется проверка Телосложения.",
            CombatConditionType.Dead => "Персонаж мертв.",
            CombatConditionType.TemporaryInsanity => "Персонаж временно безумен.",
            CombatConditionType.MajorWound => "Серьезная рана (урон ≥ половины макс. НР).",
            CombatConditionType.Bleeding => "Персонаж теряет НР каждый раунд.",
            _ => string.Empty
        };
    }

    public static bool PreventsAction(this CombatConditionType type)
    {
        return type is CombatConditionType.Unconscious or
            CombatConditionType.Dying or
            CombatConditionType.Dead or
            CombatConditionType.Stunned;
    }

    public static bool AffectsMovement(this CombatConditionType type)
    {
        return type is CombatConditionType.Prone or
            CombatConditionType.Grappled or
            CombatConditionType.Pinned or
            CombatConditionType.Unconscious or
            CombatConditionType.Dying or
            CombatConditionType.Dead;
    }

    public static bool AffectsCombat(this CombatConditionType type)
    {
        return type is CombatConditionType.Prone or
            CombatConditionType.Grappled or
            CombatConditionType.MajorWound;
    }
}