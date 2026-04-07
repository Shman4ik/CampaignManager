namespace CampaignManager.Web.Components.Features.Characters.Model;

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

    /// <summary>
    ///     Накопленная потеря рассудка в рамках текущего эпизода
    ///     (для правил "≥5 → проверка ИНТ → временное безумие" и "≥1/5 → бессрочное безумие").
    ///     Сбрасывается вручную ("Новый эпизод").
    /// </summary>
    public int SanityLossEpisode { get; set; }

    /// <summary>
    ///     Время начала временного безумия (для отсчёта 1d10 часов).
    /// </summary>
    public DateTime? TemporaryInsanityStartedAt { get; set; }

    /// <summary>
    ///     Время начала бессрочного безумия.
    /// </summary>
    public DateTime? IndefiniteInsanityStartedAt { get; set; }

    /// <summary>
    ///     Счётчик случаев безумия, связанного с Мифами Ктулху.
    ///     Первый случай даёт +5 к навыку Мифов, каждый следующий +1 (стр. 284).
    /// </summary>
    public int MythosInsanityCount { get; set; }
}