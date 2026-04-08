using System.ComponentModel.DataAnnotations;
using CampaignManager.Web.Components.Shared.Model;

namespace CampaignManager.Web.Components.Features.Campaigns.Models;

public class CampaignCreateModel
{
    [Required(ErrorMessage = "Название кампании обязательно")]
    [StringLength(100, ErrorMessage = "Название кампании должно быть не более 100 символов")]
    public string Name { get; set; } = string.Empty;

    public CampaignStatus Status { get; set; } = CampaignStatus.Planning;

    public Eras Era { get; set; } = Eras.Classic;
}