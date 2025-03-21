﻿using CampaignManager.Web.Model;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampaignManager.Web.Utilities.Services;

public class UserInformationService(
    IHttpContextAccessor httpContextAccessor,
    IDbContextFactory<AppIdentityDbContext> appIdentityDbContextFactory)
{
    public string? GetCurrentUserEmail()
    {
        return httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;
    }

    public async Task<ApplicationUser?> GetUserAsync(string email)
    {
        await using AppIdentityDbContext dbContext = await appIdentityDbContextFactory.CreateDbContextAsync();
        return await dbContext.Users
            .SingleOrDefaultAsync(p => p.Email.ToLower() == email.ToLower());
    }
}