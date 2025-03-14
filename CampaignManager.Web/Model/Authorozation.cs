using Microsoft.AspNetCore.Identity;

namespace CampaignManager.Web.Model;

public class ApplicationUser : IdentityUser
{
    public PlayerRole Role { get; set; }
}

public enum PlayerRole
{
    Player,
    GameMaster,
    Administrator
}