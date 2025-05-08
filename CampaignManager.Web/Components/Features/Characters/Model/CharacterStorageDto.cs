using System.ComponentModel.DataAnnotations;
using CampaignManager.Web.Components.Features.Campaigns.Models;
using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Characters.Model;

/// <summary>
///     Класс для хранения данных персонажа в базе данных
/// </summary>
public class CharacterStorageDto : BaseDataBaseEntity
{
    /// <summary>
    ///     Имя персонажа, дублируется из JSON для быстрого доступа
    /// </summary>
    [StringLength(100)]
    public string CharacterName { get; set; }

    public Character Character { get; set; }

    public required Guid CampaignPlayerId { get; set; }
    public CampaignPlayer CampaignPlayer { get; set; }

    /// <summary>
    ///     Статус персонажа в кампании. Только один персонаж может быть активным для игрока в кампании.
    /// </summary>
    public CharacterStatus Status { get; set; } = CharacterStatus.Active;
}