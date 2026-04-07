namespace CampaignManager.Web.Components.Features.Characters.Model;

public enum InsanityConditionKind
{
    Phobia,
    Mania
}

/// <summary>
///     Структурированная запись фобии/мании сыщика (см. главу 8, табл. IX/X).
/// </summary>
public class InsanityCondition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public InsanityConditionKind Kind { get; set; }

    /// <summary>
    ///     Название (например, "Клаустрофобия").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Описание/обстоятельства появления.
    /// </summary>
    public string? Description { get; set; }

    public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Активна ли (false — вылечена психоанализом).
    /// </summary>
    public bool Active { get; set; } = true;
}
