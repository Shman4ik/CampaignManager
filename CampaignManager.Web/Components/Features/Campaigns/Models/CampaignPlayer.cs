using CampaignManager.Web.Model;

namespace CampaignManager.Web.Companies.Models;

public class CampaignPlayer : BaseDataBaseEntity
{
    /// <summary>
    ///     Collection of characters for this player in this campaign
    /// </summary>
    public ICollection<CharacterStorageDto> Characters { get; set; } = [];

    /// <summary>
    ///     Reference to the campaign
    /// </summary>
    public Guid CampaignId { get; set; }

    /// <summary>
    ///     Represents a campaign associated with the object. It provides access to campaign details and properties.
    /// </summary>
    public Campaign Campaign { get; set; }

    /// <summary>
    ///     Player's email
    /// </summary>
    public required string PlayerEmail { get; set; }

    /// <summary>
    ///     Player's name
    /// </summary>
    public required string PlayerName { get; set; }
}