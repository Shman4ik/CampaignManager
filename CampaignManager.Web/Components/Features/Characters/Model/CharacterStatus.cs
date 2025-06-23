namespace CampaignManager.Web.Components.Features.Characters.Model;

/// <summary>
///     Статус персонажа в кампании
/// </summary>
public enum CharacterStatus
{
    /// <summary>
    /// Персонаж создан как шаблон для сюжетов
    /// </summary>
    Template,

    /// <summary>
    ///     Активный персонаж, используемый в игре
    /// </summary>
    Active,

    /// <summary>
    ///     Неактивный персонаж, сохраненный, но не используемый
    /// </summary>
    Inactive,

    /// <summary>
    ///     Персонаж выведен из игры (погиб, отошел от дел и т.д.)
    /// </summary>
    Retired,

    /// <summary>
    ///     Персонаж архивирован и скрыт из основного списка
    /// </summary>
    Archived
}