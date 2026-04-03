namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
/// Уровень дальности для стрельбы — влияет на сложность проверки навыка
/// </summary>
public enum RangeLevel
{
    /// <summary>Базовая дальность — обычный успех (навык в полном размере)</summary>
    Base,

    /// <summary>Большая дальность — трудный успех (навык ÷ 2)</summary>
    Long,

    /// <summary>Сверхбольшая дальность — чрезвычайный успех (навык ÷ 5). Проникающая рана только при 01.</summary>
    Extreme
}
