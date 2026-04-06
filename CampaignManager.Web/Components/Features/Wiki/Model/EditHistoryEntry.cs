using System.ComponentModel.DataAnnotations;
using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Wiki.Model;

public sealed class EditHistoryEntry : BaseDataBaseEntity
{
    [Required]
    [StringLength(100)]
    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public EditAction Action { get; set; }

    [Required]
    [StringLength(256)]
    public string EditorEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string EditorName { get; set; } = string.Empty;

    public string? SnapshotJson { get; set; }

    public string? PreviousSnapshotJson { get; set; }
}
