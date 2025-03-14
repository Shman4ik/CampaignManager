using CampaignManager.Web.Model;

namespace CampaignManager.Web.Compain.Models;

public class Campaign : BaseDataBaseEntity
{
    public required string Name { get; set; }

    public CampaignStatus Status { get; set; } = CampaignStatus.Planning;

    // Навигационные свойства
    public string? KeeperEmail { get; set; }
    public ApplicationUser? Keeper { get; set; }

    // Коллекция связей с игроками
    public List<CampaignPlayer> Players { get; set; } = [];
}