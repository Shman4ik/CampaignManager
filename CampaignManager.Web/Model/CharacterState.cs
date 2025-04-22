namespace CampaignManager.Web.Model;

public class CharacterState
{
    /// <summary>
    ///     Находится ли персонаж без сознания
    /// </summary>
    public bool IsUnconscious { get; set; }

    /// <summary>
    ///     Имеет ли персонаж серьезную рану
    /// </summary>
    public bool HasSeriousInjury { get; set; }

    /// <summary>
    ///     Находится ли персонаж при смерти
    /// </summary>
    public bool IsDying { get; set; }

    /// <summary>
    ///     Страдает ли персонаж временным безумием
    /// </summary>
    public bool HasTemporaryInsanity { get; set; }

    /// <summary>
    ///     Страдает ли персонаж бессрочным безумием
    /// </summary>
    public bool HasIndefiniteInsanity { get; set; }
}