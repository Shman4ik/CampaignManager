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
    ///     Накопленная за игровой день потеря рассудка — для правила "≥1/5 текущего Рассудка
    ///     за один игровой день → бессрочное безумие" (стр. 153). Сбрасывается вручную ("Новый день").
    ///     Имя свойства оставлено прежним: по нему уже лежат сохранённые листы в JSONB.
    /// </summary>
    public int SanityLossEpisode { get; set; }

    /// <summary>
    ///     Потеря рассудка от одной причины (последняя проверка Рассудка, книга, встреча).
    ///     Правило "≥5 пунктов по одной и той же причине → проверка ИНТ" (стр. 152) считается
    ///     именно по ней, а не по накопленному за день: два отдельных провала по 3 проверку не дают.
    /// </summary>
    public int LastSanityLoss { get; set; }

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

    /// <summary>
    ///     Привыкание к ужасному (стр. 167): накопленная потеря рассудка за каждый вид тварей.
    ///     Фаза развития снижает каждое значение на 1.
    /// </summary>
    public List<MythosHabituation> MythosHabituations { get; set; } = [];
}