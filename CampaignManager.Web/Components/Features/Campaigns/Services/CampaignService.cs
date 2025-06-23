using System.Security.Claims;
using CampaignManager.Web.Components.Features.Campaigns.Models;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Components.Features.Campaigns.Services;

public class CampaignService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<CampaignService> logger)
{
    /// <summary>
    ///     Retrieves a list of campaigns associated with the current user, including related players and characters.
    /// </summary>
    /// <returns>Returns a list of Campaign objects or an empty list if no campaigns are found.</returns>
    public async Task<List<Campaign>> GetUserCampaignsAsync()
    {
        var user = await identityService.GetUserAsync();
        if (user == null) return [];


        using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Campaigns
            .Include(c => c.Players)
            .ThenInclude(cp => cp.Characters)
            .AsSplitQuery()
            .Where(c => c.Players.Any(p => p.PlayerEmail == user.Email))
            .ToListAsync();
    }

    /// <summary>
    ///     Retrieves a campaign player associated with the current user if they are authenticated.
    /// </summary>
    /// <param name="campaignId">Identifies the specific campaign for which the player information is being retrieved.</param>
    /// <returns>Returns the campaign player details or null if the user is not authenticated.</returns>
    public async Task<CampaignPlayer?> GetCampaignPlayerAsync(Guid campaignId)
    {
        var user = await identityService.GetUserAsync();
        if (user == null) return null;

        using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.CampaignPlayers
            .Include(p => p.Characters) // Include characters!
            .Where(p => p.PlayerEmail == user.Email && p.CampaignId == campaignId)
            .SingleOrDefaultAsync();
    }

    public async Task<Campaign> CreateCampaignAsync(string name)
    {
        var userEmail = identityService.GetCurrentUserEmail();
        if (userEmail == null) throw new UnauthorizedAccessException("Пользователь не авторизован");


        logger.LogInformation("Пользователь {userEmail} создаёт кампанию '{name}'", userEmail, name);

        Campaign campaign = new() { Name = name, KeeperEmail = userEmail, CreatedAt = DateTime.UtcNow, LastUpdated = DateTime.UtcNow };

        using var dbContext = await dbContextFactory.CreateDbContextAsync();

        dbContext.Campaigns.Add(campaign);

        try
        {
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Кампания '{name}' успешно создана пользователем {userEmail}", name, userEmail);
            return campaign;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при создании кампании '{name}' пользователем {userEmail}", name, userEmail);
            throw;
        }
    }

    /// <summary>
    ///     Method to get available companies (campaigns)
    /// </summary>
    public async Task<List<Campaign>> GetAvailableCompaniesAsync()
    {
        using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Campaigns.Where(p => p.Status != CampaignStatus.Completed).ToListAsync();
    }

    /// <summary>
    ///     Method for a user to apply to a campaign
    /// </summary>
    public async Task<bool> JoinCampaignAsync(Guid campaignId, string userName)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var campaign = await dbContext.Campaigns
                .Include(c => c.Players)
                .FirstOrDefaultAsync(c => c.Id == campaignId);

            if (campaign == null)
            {
                logger.LogWarning("Campaign with ID {CampaignId} not found.", campaignId);
                return false;
            }

            var user = await identityService.GetUserAsync();

            if (user == null)
            {
                var externalPrincipal = httpContextAccessor.HttpContext?.User;
                var email = externalPrincipal?.FindFirst(ClaimTypes.Email)?.Value;
                var name = externalPrincipal?.FindFirst(ClaimTypes.Name)?.Value;
                user = new ApplicationUser { Email = email ?? string.Empty, UserName = name ?? string.Empty, Role = PlayerRole.Player };
                user = await identityService.CreateUserAsync(user);
            }

            CampaignPlayer campaignPlayers = new() { CampaignId = campaign.Id, PlayerEmail = user?.Email ?? string.Empty, PlayerName = userName };
            campaignPlayers.Init();

            if (user?.Email != null && !campaign.Players.Any(p => p.PlayerEmail == user.Email))
            {
                dbContext.CampaignPlayers.Add(campaignPlayers);
                await dbContext.SaveChangesAsync();
                logger.LogInformation("User {UserId} successfully applied to campaign {CampaignId}.", user.Email, campaignId);
                return true;
            }

            logger.LogWarning("User {UserId} has already applied to campaign {CampaignId}.", user?.Email ?? "unknown", campaignId);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying to the campaign.");
            return false;
        }
    }

    /// <summary>
    ///     Gets a campaign by ID with all players and their characters (for admins/keepers)
    /// </summary>
    /// <param name="campaignId">Campaign ID</param>
    /// <returns>Campaign with full player and character data, or null if not found</returns>
    public async Task<Campaign?> GetCampaignWithCharactersAsync(Guid campaignId)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var campaign = await dbContext.Campaigns
                .Include(c => c.Players)
                .ThenInclude(p => p.Characters)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.Id == campaignId);

            return campaign;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving campaign {CampaignId} with characters", campaignId);
            return null;
        }
    }

    /// <summary>
    ///     Checks if the current user is an admin or keeper of the specified campaign
    /// </summary>
    /// <param name="campaignId">Campaign ID</param>
    /// <returns>True if user is admin or keeper, false otherwise</returns>
    public async Task<bool> IsUserAdminOrKeeperAsync(Guid campaignId)
    {
        try
        {
            var user = await identityService.GetUserAsync();
            if (user == null) return false;

            // Check if user is global admin
            if (user.Role == PlayerRole.Administrator) return true;

            // Check if user is campaign keeper
            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var campaign = await dbContext.Campaigns
                .FirstOrDefaultAsync(c => c.Id == campaignId);

            return campaign?.KeeperEmail?.Equals(user.Email, StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if user is admin or keeper for campaign {CampaignId}", campaignId);
            return false;
        }
    }
}