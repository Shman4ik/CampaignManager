using System.ComponentModel.DataAnnotations;
namespace CampaignManager.Web.Companies.Models;

public class CampaignCreateModel
{
    [Required(ErrorMessage = "Название кампании обязательно")]
    [StringLength(100, ErrorMessage = "Название кампании должно быть не более 100 символов")]
    public string Name { get; set; } = string.Empty;

    public CampaignStatus Status { get; set; } = CampaignStatus.Planning;
}