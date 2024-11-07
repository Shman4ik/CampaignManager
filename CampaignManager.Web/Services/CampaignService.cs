using CampaignManager.Web.Model;
using Microsoft.EntityFrameworkCore;

public class CampaignService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CampaignService> _logger;

    public CampaignService(ApplicationDbContext dbContext, IHttpContextAccessor httpContextAccessor, ILogger<CampaignService> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<List<Campaign>> GetUserCampaignsAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return new List<Campaign>();

        return await _dbContext.Campaigns
            .Where(c => c.KeeperId == user.Id || c.Players.Any(p => p.Id == user.Id))
            .ToListAsync();
    }

    public async Task<Campaign> CreateCampaignAsync(string name)
    {
        var user = await GetCurrentUserAsync();
        if (user == null || user.Role != PlayerRole.Administrator)
            throw new UnauthorizedAccessException("Only administrators can create campaigns.");

        var campaign = new Campaign
        {
            Name = name,
            KeeperId = user.Id,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };

        _dbContext.Campaigns.Add(campaign);
        await _dbContext.SaveChangesAsync();

        return campaign;
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var email = _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
            return null;

        return await _dbContext.Users.SingleOrDefaultAsync(p => p.Email == email);
    }

    // Method to get available companies (campaigns)
    public async Task<List<Campaign>> GetAvailableCompaniesAsync()
    {
        try
        {
            // Assuming campaigns that are not yet full are considered available
            return await _dbContext.Campaigns
                .Include(c => c.Players)
                .Where(c => c.Players.Count < 5) // Assuming max 5 players per campaign
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while fetching available companies.");
            return new List<Campaign>();
        }
    }

    // Method for a user to apply to a campaign
    public async Task<bool> ApplyToCompanyAsync(Guid campaignId, string userEmail)
    {
        try
        {
            var campaign = await _dbContext.Campaigns
                .Include(c => c.Players)
                .FirstOrDefaultAsync(c => c.Id == campaignId);

            if (campaign == null)
            {
                _logger.LogWarning("Campaign with ID {CampaignId} not found.", campaignId);
                return false;
            }

            var user = await _dbContext.ApplicationUsers.FirstOrDefaultAsync(u => u.Email == userEmail);

            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found.", userEmail);
                return false;
            }

            // Create an application record
            if (!campaign.Players.Contains(user))
            {
                campaign.Players.Add(user);
                campaign.LastUpdated = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("User {UserId} successfully applied to campaign {CampaignId}.", userEmail, campaignId);
                return true;
            }

            _logger.LogWarning("User {UserId} has already applied to campaign {CampaignId}.", userEmail, campaignId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while applying to the campaign.");
            return false;
        }
    }
}