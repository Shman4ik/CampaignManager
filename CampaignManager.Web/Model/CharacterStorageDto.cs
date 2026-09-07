using CampaignManager.Web.Components.Features.Campaigns.Models;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Scenarios.Model;
using System.ComponentModel.DataAnnotations;

namespace CampaignManager.Web.Model;

/// <summary>
///     Лист персонажа в базе данных: сам персонаж лежит в JSONB, а принадлежность описывают
///     колонки этого класса.
///     <para>
///         Кто это такой, говорит <see cref="Kind" />, и только он. Владелец — ровно один
///         из трёх вариантов, остальные ключи при этом пустые:
///     </para>
///     <list type="bullet">
///         <item><description><see cref="CampaignPlayerId" /> — лист игрока в кампании;</description></item>
///         <item><description><see cref="ScenarioId" /> — преген, созданный для этого сценария;</description></item>
///         <item>
///             <description>
///                 <see cref="CampaignId" /> — НПС кампании; <c>null</c> у НПС из общей библиотеки.
///             </description>
///         </item>
///     </list>
///     <para>
///         Участие НПС в сценариях описывает <see cref="ScenarioNpc" />, а не копия листа:
///         один НПС появляется в любом числе сценариев, оставаясь одной строкой.
///     </para>
/// </summary>
public class CharacterStorageDto : BaseDataBaseEntity
{
    /// <summary>
    ///     Имя персонажа
    /// </summary>
    [StringLength(100)]
    public required string CharacterName { get; set; }

    /// <summary>
    ///     Вид персонажа: игрок, преген или НПС.
    /// </summary>
    public CharacterKind Kind { get; set; } = CharacterKind.PlayerCharacter;

    /// <summary>
    ///     Состояние листа. Только один персонаж может быть активным для игрока в кампании.
    ///     Принадлежность статусом больше не кодируется — для этого есть <see cref="Kind" />.
    /// </summary>
    public CharacterStatus Status { get; set; } = CharacterStatus.Active;

    public required Character Character { get; set; }

    /// <summary>
    ///     Идентификатор игрока, которому принадлежит персонаж.
    /// </summary>
    public Guid? CampaignPlayerId { get; set; }

    /// <summary>
    ///     Навигационное свойство.
    /// </summary>
    public CampaignPlayer? CampaignPlayer { get; set; }

    /// <summary>
    ///     Кампания, которой принадлежит НПС. <c>null</c> — НПС лежит в общей библиотеке
    ///     и доступен во всех кампаниях.
    /// </summary>
    public Guid? CampaignId { get; set; }

    /// <summary>
    ///     Навигационное свойство к кампании-владельцу.
    /// </summary>
    public Campaign? Campaign { get; set; }

    /// <summary>
    ///     Сценарий, для которого создан преген.
    /// </summary>
    public Guid? ScenarioId { get; set; }

    /// <summary>
    ///     Навигационное свойство к сценарию.
    /// </summary>
    public Scenario? Scenario { get; set; }

    /// <summary>
    ///     Сценарии, в которых занят этот НПС (роль и количество — на связи).
    /// </summary>
    public ICollection<ScenarioNpc> ScenarioCasts { get; set; } = [];
}
