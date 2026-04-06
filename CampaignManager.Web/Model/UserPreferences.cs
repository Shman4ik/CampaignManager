namespace CampaignManager.Web.Model;

public class UserPreferences : BaseDataBaseEntity
{
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// Extensible key-value store for user preferences (stored as JSONB).
    /// </summary>
    public Dictionary<string, string> Preferences { get; set; } = [];
}
