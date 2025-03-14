using CampaignManager.Web.Compain.Models;

namespace CampaignManager.Web.Model;

public class CampaignPlayer : BaseDataBaseEntity
{
    // Коллекция персонажей этого игрока в этой кампании
    public ICollection<CharacterStorageDto> Characters { get; set; } = [];

    // Навигационные свойства
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; }

    public string PlayerEmail { get; set; }
    public ApplicationUser Player { get; set; }
}