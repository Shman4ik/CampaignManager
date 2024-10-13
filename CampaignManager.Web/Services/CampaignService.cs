using CampaignManager.Web.Model;
using Microsoft.EntityFrameworkCore;

public class CampaignService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CampaignService(ApplicationDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
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
}