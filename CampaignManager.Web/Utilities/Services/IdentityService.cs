using CampaignManager.Web.Components.Features.Admin.Model;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.DataBase;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace CampaignManager.Web.Utilities.Services;

public class IdentityService(
    IHttpContextAccessor httpContextAccessor,
    AuthenticationStateProvider authenticationStateProvider,
    IDbContextFactory<AppIdentityDbContext> appIdentityDbContextFactory,
    IDbContextFactory<AppDbContext> appDbContextFactory)
{
    private ApplicationUser? _cachedUser;
    private bool _userCacheLoaded;
    /// <summary>
    /// Sync — works during SSR/prerender (HttpContext available).
    /// Returns null during interactive WebSocket rendering.
    /// Prefer <see cref="GetCurrentUserEmailAsync"/> in interactive components.
    /// </summary>
    public string? GetCurrentUserEmail()
    {
        return httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;
    }

    /// <summary>
    /// Async — works in both SSR prerender and interactive Blazor Server rendering.
    /// Falls back to AuthenticationStateProvider when HttpContext is unavailable.
    /// </summary>
    public async Task<string?> GetCurrentUserEmailAsync()
    {
        var email = GetCurrentUserEmail();
        if (email is not null) return email;

        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.FindFirst(ClaimTypes.Email)?.Value;
    }

    public async Task<bool> IsKeeper()
    {
        return await GetCurrentUserRole() is PlayerRole.GameMaster or PlayerRole.Administrator;
    }

    public async Task<bool> IsAdministrator()
    {
        return await GetCurrentUserRole() is PlayerRole.Administrator;
    }

    public async Task<PlayerRole> GetCurrentUserRole()
    {
        // RoleClaimsTransformation already adds ClaimTypes.Role to the principal on every request.
        // Read it from there to avoid an extra round-trip to the database.
        var principal = await GetCurrentPrincipalAsync();
        var roleStr = principal?.FindFirst(ClaimTypes.Role)?.Value;
        if (roleStr is not null && Enum.TryParse<PlayerRole>(roleStr, out var roleFromClaims))
            return roleFromClaims;

        // Fallback: user not yet authenticated or claims not yet transformed.
        var user = await GetUserAsync();
        return user?.Role ?? PlayerRole.Player;
    }

    private async Task<ClaimsPrincipal?> GetCurrentPrincipalAsync()
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser?.Identity?.IsAuthenticated == true)
            return httpUser;

        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.Identity?.IsAuthenticated == true ? state.User : null;
    }

    public async Task<bool> HasPendingKeeperApplicationAsync()
    {
        var email = await GetCurrentUserEmailAsync();
        if (email is null) return false;

        await using var db = await appDbContextFactory.CreateDbContextAsync();
        return await db.KeeperApplications
            .AnyAsync(a => a.UserEmail.ToLower() == email.ToLower()
                           && a.Status == KeeperApplicationStatus.Pending);
    }

    public async Task<ApplicationUser?> GetUserAsync()
    {
        if (_userCacheLoaded) return _cachedUser;

        var userEmail = await GetCurrentUserEmailAsync();
        _cachedUser = await GetUserByEmailFromDb(userEmail);
        _userCacheLoaded = true;
        return _cachedUser;
    }

    public void InvalidateUserCache()
    {
        _cachedUser = null;
        _userCacheLoaded = false;
    }

    public async Task<ApplicationUser?> GetUserAsync(string? email)
    {
        if (email == null) return null;

        var currentEmail = await GetCurrentUserEmailAsync();
        if (_userCacheLoaded && string.Equals(currentEmail, email, StringComparison.OrdinalIgnoreCase))
            return _cachedUser;

        return await GetUserByEmailFromDb(email);
    }

    private async Task<ApplicationUser?> GetUserByEmailFromDb(string? email)
    {
        if (email == null) return null;

        await using var dbContext = await appIdentityDbContextFactory.CreateDbContextAsync();
        return await dbContext.Users.SingleOrDefaultAsync(p => p.Email != null && p.Email.ToLower() == email.ToLower());
    }

    public async Task<ApplicationUser?> CreateUserAsync(ApplicationUser user)
    {
        await using var dbContext = await appIdentityDbContextFactory.CreateDbContextAsync();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }
}
