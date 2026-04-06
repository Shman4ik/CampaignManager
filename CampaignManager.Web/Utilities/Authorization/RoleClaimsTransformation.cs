using System.Security.Claims;
using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.DataBase;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Utilities.Authorization;

public sealed class RoleClaimsTransformation(
    IDbContextFactory<AppIdentityDbContext> identityDbContextFactory) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return principal;

        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        if (email is null)
            return principal;

        // Skip if role claim already exists
        if (principal.HasClaim(c => c.Type == ClaimTypes.Role))
            return principal;

        await using var db = await identityDbContextFactory.CreateDbContextAsync();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower());

        if (user is null)
            return principal;

        var identity = principal.Identity as ClaimsIdentity;
        identity?.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));

        return principal;
    }
}
