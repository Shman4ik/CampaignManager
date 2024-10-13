using CampaignManager.Web.Model;
using Microsoft.AspNetCore.Identity;

namespace CampaignManager.Web.Authorization;

public interface IRoleService
{
    Task AssignRoleToUser(string userId, PlayerRole role);
    Task<PlayerRole?> GetUserRole(string userId);
}

public class RoleService : IRoleService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public RoleService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task AssignRoleToUser(string userId, PlayerRole role)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            user.Role = role;
            await _userManager.UpdateAsync(user);
        }
    }

    public async Task<PlayerRole?> GetUserRole(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user?.Role;
    }
}
