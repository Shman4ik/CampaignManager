using CampaignManager.Web.Companies.Models;
using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Companies.Services;

public class CampaignService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    UserInformationService userInformationService,
    ILogger<CampaignService> logger)
{
    public async Task<List<Campaign>> GetUserCampaignsAsync()
    {
        string? userEmail = userInformationService.GetCurrentUserEmail();
        if (userEmail == null)
        {
            return [];
        }

        using AppDbContext? dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Campaigns
            .Where(p => p.KeeperEmail == userEmail || p.Players.Any(p => p.PlayerEmail == userEmail))
            .ToListAsync();
    }

    public async Task<Campaign> CreateCampaignAsync(string name)
    {
        string? userEmail = userInformationService.GetCurrentUserEmail();
        if (userEmail == null)
        {
            throw new UnauthorizedAccessException("Пользователь не авторизован");
        }

        // Изменено: разрешить всем пользователям создавать кампании
        // или разрешить пользователям с ролью GameMaster или Administrator
        bool canCreateCampaign = true; // Разрешить всем пользователям
        // Или можно использовать: bool canCreateCampaign = user.Role == PlayerRole.Administrator || user.Role == PlayerRole.GameMaster;

        if (!canCreateCampaign)
        {
            throw new UnauthorizedAccessException("У вас нет прав для создания кампании");
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

    // Method to get available companies (campaigns)
    public async Task<List<Campaign>> GetAvailableCompaniesAsync()
    {
        using AppDbContext? dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Campaigns.Where(p => p.Status != CampaignStatus.Completed).ToListAsync();
    }

    // Method for a user to apply to a campaign
    public async Task<bool> ApplyToCompanyAsync(Guid campaignId, string userEmail)
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

            ApplicationUser? user = await userInformationService.GetUserAsync(userEmail);

            if (user == null)
            {
                logger.LogWarning("User with ID {UserId} not found.", userEmail);
                return false;
            }

            CampaignPlayer campaignPlayers = new() { CampaignId = campaign.Id, PlayerEmail = user.Email! };
            campaignPlayers.Init();

            // Create an application record
            if (!campaign.Players.Any(p => p.PlayerEmail == userEmail))
            {
                campaign.Players.Add(campaignPlayers);
                campaign.LastUpdated = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
                logger.LogInformation("User {UserId} successfully applied to campaign {CampaignId}.", userEmail, campaignId);
                return true;
            }

            logger.LogWarning("User {UserId} has already applied to campaign {CampaignId}.", userEmail, campaignId);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying to the campaign.");
            return false;
        }
    }
}