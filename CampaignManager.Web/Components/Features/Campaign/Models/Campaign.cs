using CampaignManager.Web.Model;

namespace CampaignManager.Web.Companies.Models;

public class Campaign : BaseDataBaseEntity
{
    public required string Name { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Planning;
    public string? KeeperEmail { get; set; }
    public List<CampaignPlayer> Players { get; set; } = [];
}