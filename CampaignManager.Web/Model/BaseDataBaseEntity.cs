namespace CampaignManager.Web.Model;

public class BaseDataBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }

    public void Init()
    {
        Id = Guid.CreateVersion7();
        CreatedAt = DateTime.Now;
        LastUpdated = DateTime.Now;
    }
}