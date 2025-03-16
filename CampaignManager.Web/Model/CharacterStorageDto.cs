using CampaignManager.Web.Companies.Models;
using System.ComponentModel.DataAnnotations;

namespace CampaignManager.Web.Model;

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

    public Guid? CampaignPlayerId { get; set; }
    public CampaignPlayer CampaignPlayer { get; set; }
}