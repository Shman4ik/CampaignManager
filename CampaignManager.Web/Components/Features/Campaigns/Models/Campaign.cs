using CampaignManager.Web.Components.Shared.Model;
using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Campaigns.Models;

public class Campaign : BaseDataBaseEntity
{
    public required string Name { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Planning;
    public string? KeeperEmail { get; set; }
    public Eras Era { get; set; } = Eras.Classic;
    public List<CampaignPlayer> Players { get; set; } = [];
}