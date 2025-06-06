namespace CampaignManager.Web.Models.Navigation;

public class NavigationItem
{
    public string Title { get; set; } = string.Empty;
    public string? Href { get; set; }
    public string Icon { get; set; } = string.Empty;
    public List<NavigationItem>? Children { get; set; }
    public NavigationMatchMode MatchMode { get; set; } = NavigationMatchMode.Prefix;
    public bool IsExpanded { get; set; } = false;
}

public enum NavigationMatchMode
{
    All,
    Prefix
}
