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
    public async Task<ApplicationUser?> GetUserAsync()
    {
        var userEmail = GetCurrentUserEmail();
        return await GetUserAsync(userEmail);
    }
    public async Task<ApplicationUser?> GetUserAsync(string? email)
    {
        if (email == null)
            return null;

        await using AppIdentityDbContext dbContext = await appIdentityDbContextFactory.CreateDbContextAsync();
        return await dbContext.Users.SingleOrDefaultAsync(p => p.Email.ToLower() == email.ToLower());
    }
}