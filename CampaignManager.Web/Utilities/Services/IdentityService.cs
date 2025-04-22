using CampaignManager.Web.Model;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampaignManager.Web.Utilities.Services;

public class IdentityService(
    IHttpContextAccessor httpContextAccessor,
    IDbContextFactory<AppIdentityDbContext> appIdentityDbContextFactory)
{
    public string? GetCurrentUserEmail()
    {
        return httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;
    }

    public async Task<bool> IsKeeper()
    {
        return await GetCurrentUserRole() is PlayerRole.GameMaster or PlayerRole.Administrator;
    }

    public async Task<PlayerRole> GetCurrentUserRole()
    {
        ApplicationUser? user = await GetUserAsync();
        return user?.Role ?? PlayerRole.Player;
    }

    public async Task<ApplicationUser?> GetUserAsync()
    {
        string? userEmail = GetCurrentUserEmail();
        return await GetUserAsync(userEmail);
    }

    public async Task<ApplicationUser?> GetUserAsync(string? email)
    {
        if (email == null)
        {
            return null;
        }

        await using AppIdentityDbContext dbContext = await appIdentityDbContextFactory.CreateDbContextAsync();
        return await dbContext.Users.SingleOrDefaultAsync(p => p.Email.ToLower() == email.ToLower());
    }

    public async Task<ApplicationUser?> CreateUserAsync(ApplicationUser user)
    {
        await using AppIdentityDbContext dbContext = await appIdentityDbContextFactory.CreateDbContextAsync();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }
}