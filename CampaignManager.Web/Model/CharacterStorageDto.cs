using System.ComponentModel.DataAnnotations;
using CampaignManager.Web.Components.Features.Campaigns.Models;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Scenarios.Model;

namespace CampaignManager.Web.Model;

/// <summary>
///     Класс для хранения данных персонажа в базе данных
/// </summary>
public class CharacterStorageDto : BaseDataBaseEntity
{
    /// <summary>
    ///     Имя персонажа
    /// </summary>
    [StringLength(100)]
    public required string CharacterName { get; set; }
    
    /// <summary>
    ///     Статус персонажа в кампании. Только один персонаж может быть активным для игрока в кампании.
    /// </summary>
    public CharacterStatus Status { get; set; } = CharacterStatus.Template;

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
    ///     Идентификатор сценария, к которому привязан шаблон персонажа.
    /// </summary>
    public Guid? ScenarioId { get; set; }
    
    /// <summary>
    ///     Навигационное свойство к сценарию.
    /// </summary>
    public Scenario? Scenario { get; set; }
}