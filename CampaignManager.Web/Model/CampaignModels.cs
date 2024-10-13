namespace CampaignManager.Web.Model
{
    public class Campaign
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string KeeperId { get; set; }
        public ApplicationUser Keeper { get; set; }
        public List<ApplicationUser> Players { get; set; } = new List<ApplicationUser>();
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class Keeper : ApplicationUser
    {
        public List<Campaign> Campaigns { get; set; } = [];
    }

    public class Player : ApplicationUser
    {
        public Character Character { get; set; }
        public Campaign Campaign { get; set; }
    }
}