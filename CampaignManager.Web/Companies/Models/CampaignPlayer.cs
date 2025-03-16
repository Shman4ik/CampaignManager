using CampaignManager.Web.Model;

namespace CampaignManager.Web.Companies.Models;

public class CampaignPlayer : BaseDataBaseEntity
{
    // Коллекция персонажей этого игрока в этой кампании
    public ICollection<CharacterStorageDto> Characters { get; set; } = [];

    // Ссылка на кампанию
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; }

    // Email игрока
    public required string PlayerEmail { get; set; }
}