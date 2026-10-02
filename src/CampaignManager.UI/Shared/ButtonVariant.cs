namespace CampaignManager.UI.Shared;

/// <summary>
/// Цвет кнопки несёт смысл, его не выбирают для разнообразия. Синей кнопки нет: в v1
/// <c>cm-btn-info</c> раскрашивал одно и то же действие по-разному на разных страницах.
/// </summary>
public enum ButtonVariant
{
    /// <summary>Одно главное действие страницы или диалога: сохранить, создать.</summary>
    Primary,

    /// <summary>Всё нейтральное: назад, отмена, переключить.</summary>
    Secondary,

    /// <summary>Одобрить чужую заявку.</summary>
    Success,

    /// <summary>Необратимое подтверждение в диалоге: «Удалить».</summary>
    Error,

    Warning,

    /// <summary>Правка строки или карточки.</summary>
    OutlinePrimary,

    /// <summary>Удаление или сброс строки или карточки.</summary>
    OutlineError,

    /// <summary>Без фона: крестик закрытия, шеврон строки.</summary>
    Ghost,
}
