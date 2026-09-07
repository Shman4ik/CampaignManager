namespace CampaignManager.Web.Components.Features.Characters.Model;

/// <summary>
///     Кто этот лист персонажа. Хранится отдельной колонкой в таблице Characters и является
///     единственным признаком принадлежности: раньше вид персонажа приходилось выводить из
///     <c>CharacterType</c> внутри JSONB, статуса и набора внешних ключей одновременно.
/// </summary>
public enum CharacterKind
{
    /// <summary>
    ///     Лист игрока. Всегда привязан к <c>CampaignPlayerId</c>.
    /// </summary>
    PlayerCharacter = 0,

    /// <summary>
    ///     Готовый персонаж для ваншота. Принадлежит сценарию (<c>ScenarioId</c>); после брони
    ///     дополнительно получает <c>CampaignPlayerId</c> забронировавшего игрока.
    /// </summary>
    Pregen = 1,

    /// <summary>
    ///     НПС Хранителя. Живёт в общей библиотеке (<c>CampaignId is null</c>) или в кампании,
    ///     а в сценариях появляется через <c>ScenarioNpc</c> — одной строкой на сценарий,
    ///     без копий листа.
    /// </summary>
    Npc = 2
}
