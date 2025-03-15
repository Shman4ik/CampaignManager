using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Services;

public interface IUserService
{
    Task<ApplicationUser?> GetUserByEmailAsync(string email);
    Task<ApplicationUser> CreateUserAsync(string email, string? name = null, PlayerRole role = PlayerRole.Player);
    Task<ApplicationUser> EnsureUserExistsAsync(string email, string? name = null);
    Task<bool> UpdateUserAsync(ApplicationUser user);
}

// Make class sealed per project guidelines
internal sealed class UserService(
    IDbContextFactory<AppIdentityDbContext> identityDbContextFactory,
    ILogger<UserService> logger) : IUserService
{
    // Using primary constructor injection per project guidelines

    public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return null;
        }

        await using AppIdentityDbContext dbContext = await identityDbContextFactory.CreateDbContextAsync();
        return await dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
    }

    public async Task<ApplicationUser> CreateUserAsync(string email, string? name = null, PlayerRole role = PlayerRole.Player)
    {
        if (string.IsNullOrEmpty(email))
        {
            throw new ArgumentException("Email is required", nameof(email));
        }

        await using AppIdentityDbContext dbContext = await identityDbContextFactory.CreateDbContextAsync();

        // Check if user already exists
        ApplicationUser? existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == email.ToUpperInvariant());

        if (existingUser is not null)
        {
            throw new InvalidOperationException($"User with email {email} already exists");
        }

        // Create new user with Guid.NewGuid().ToString() for Id per project guidelines
        ApplicationUser user = new()
        {
            Id = Guid.NewGuid().ToString(),
            UserName = name ?? email,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = (name ?? email).ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            Role = role
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        
        logger.LogInformation("Created new user {Email} with role {Role}", email, role);
        return user;
    }

    public async Task<ApplicationUser> EnsureUserExistsAsync(string email, string? name = null)
    {
        // Using is null check instead of == null per project guidelines
        ApplicationUser? user = await GetUserByEmailAsync(email);
        
        if (user is null)
        {
            user = await CreateUserAsync(email, name);
            logger.LogInformation("Created new user during EnsureUserExistsAsync for {Email}", email);
        }
        
        return user;
    }

    public async Task<bool> UpdateUserAsync(ApplicationUser user)
    {
        try
        {
            await using AppIdentityDbContext dbContext = await identityDbContextFactory.CreateDbContextAsync();
            dbContext.Users.Update(user);
            await dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating user {Email}", user.Email);
            return false;
        }
    }
}