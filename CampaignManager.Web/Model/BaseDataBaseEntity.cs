namespace CampaignManager.Web.Model;

public class BaseDataBaseEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public void Init()
    {
        Id = Guid.CreateVersion7();
        CreatedAt = DateTimeOffset.UtcNow;
        LastUpdated = DateTimeOffset.UtcNow;
    }
}