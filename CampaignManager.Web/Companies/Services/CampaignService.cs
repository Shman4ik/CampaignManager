using CampaignManager.Web.Companies.Models;
using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Companies.Services;

public class CampaignService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    ILogger<CampaignService> logger)
{
    /// <summary>
    /// Retrieves a list of campaigns associated with the current user, including related players and characters.
    /// </summary>
    /// <returns>Returns a list of Campaign objects or an empty list if no campaigns are found.</returns>
    public async Task<List<Campaign>> GetUserCampaignsAsync()
    {
        ApplicationUser? user = await identityService.GetUserAsync();
        if (user == null)
            return [];


        using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Campaigns
            .Include(c => c.Players)
            .ThenInclude(cp => cp.Characters)
            .AsSplitQuery()
            .Where(c => c.Players.Any(p => p.PlayerEmail == user.Email))
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves a campaign player associated with the current user if they are authenticated.
    /// </summary>
    /// <param name="campaignId">Identifies the specific campaign for which the player information is being retrieved.</param>
    /// <returns>Returns the campaign player details or null if the user is not authenticated.</returns>
    public async Task<CampaignPlayer?> GetCampaignPlayerAsync(Guid campaignId)
    {
        ApplicationUser? user = await identityService.GetUserAsync();
        if (user == null)
            return null;

        using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.CampaignPlayers
            .Where(p => p.PlayerEmail == user.Email && p.CampaignId == campaignId)
            .SingleOrDefaultAsync();
    }

    public async Task<Campaign> CreateCampaignAsync(string name)
    {
        string? userEmail = identityService.GetCurrentUserEmail();
        if (userEmail == null)
        {
            throw new UnauthorizedAccessException("Пользователь не авторизован");
        }


        logger.LogInformation("Пользователь {userEmail} создаёт кампанию '{name}'", userEmail, name);

        Campaign campaign = new() { Name = name, KeeperEmail = userEmail, CreatedAt = DateTime.UtcNow, LastUpdated = DateTime.UtcNow };

        using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

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
    /// Method to get available companies (campaigns)
    /// </summary>
    public async Task<List<Campaign>> GetAvailableCompaniesAsync()
    {
        using AppDbContext? dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Campaigns.Where(p => p.Status != CampaignStatus.Completed).ToListAsync();
    }

    /// <summary>
    /// Method for a user to apply to a campaign
    /// </summary>   
    public async Task<bool> JoinCampaignAsync(Guid campaignId, string userName)
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            Campaign? campaign = await dbContext.Campaigns
                .Include(c => c.Players)
                .FirstOrDefaultAsync(c => c.Id == campaignId);

            if (campaign == null)
            {
                logger.LogWarning("Campaign with ID {CampaignId} not found.", campaignId);
                return false;
            }

            ApplicationUser? user = await identityService.GetUserAsync();

            if (user == null)
            {
                logger.LogWarning("User not found.");
                return false;
            }

            CampaignPlayer campaignPlayers = new() { CampaignId = campaign.Id, PlayerEmail = user.Email!, PlayerName = userName };
            campaignPlayers.Init();

            if (!campaign.Players.Any(p => p.PlayerEmail == user.Email))
            {
                dbContext.CampaignPlayers.Add(campaignPlayers);
                await dbContext.SaveChangesAsync();
                logger.LogInformation("User {UserId} successfully applied to campaign {CampaignId}.", user.Email, campaignId);
                return true;
            }

            logger.LogWarning("User {UserId} has already applied to campaign {CampaignId}.", user.Email, campaignId);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying to the campaign.");
            return false;
        }
    }
}