using System.ComponentModel.DataAnnotations;
using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Chase.Model;

/// <summary>
/// Сохранённая сцена погони. Одна активная сцена на пару «Хранитель + кампания».
/// </summary>
public class ChaseSessionDto : BaseDataBaseEntity
{
    public Guid? CampaignId { get; set; }

    [StringLength(256)]
    public required string KeeperEmail { get; set; }

    public required ChaseSnapshot State { get; set; }
}
