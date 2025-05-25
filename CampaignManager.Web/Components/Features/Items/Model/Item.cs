using CampaignManager.Web.Components.Shared.Model;
using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Items.Model;

/// <summary>
///     Model for significant items and artifacts in scenarios
/// </summary>
public class Item : BaseDataBaseEntity
{
    /// <summary>
    ///     The name of the item
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// В какой этохе доступен этот предмет
    /// </summary>
    public required Eras Era { get; init; }

    /// <summary>
    ///     The type of item (e.g., Weapon, Artifact, Tool)
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    ///     Detailed description of the item
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Optional URL to an image of the item
    /// </summary>
    public string? ImageUrl { get; set; }
}