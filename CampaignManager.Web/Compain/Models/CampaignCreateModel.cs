namespace CampaignManager.Web.Compain.Models
{
    public class CampaignCreateModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Название кампании обязательно")]
        [System.ComponentModel.DataAnnotations.StringLength(100, ErrorMessage = "Название кампании должно быть не более 100 символов")]
        public string Name { get; set; } = string.Empty;

        public CampaignStatus Status { get; set; } = CampaignStatus.Planning;
    }
}