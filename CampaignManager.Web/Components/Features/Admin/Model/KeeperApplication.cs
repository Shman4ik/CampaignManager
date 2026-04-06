using System.ComponentModel.DataAnnotations;
using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Admin.Model;

public sealed class KeeperApplication : BaseDataBaseEntity
{
    [Required]
    [StringLength(256)]
    public string UserEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    public KeeperApplicationStatus Status { get; set; } = KeeperApplicationStatus.Pending;

    [StringLength(256)]
    public string? ReviewedByEmail { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    [StringLength(1000)]
    public string? ReviewComment { get; set; }
}
