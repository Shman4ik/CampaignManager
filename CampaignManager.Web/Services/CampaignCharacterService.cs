using CampaignManager.Web.Model;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Services;

public class CampaignCharacterService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CampaignCharacterService> _logger;
    private readonly CharacterService _characterService;

    public CampaignCharacterService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CampaignCharacterService> logger,
        CharacterService characterService)
    {
        _dbContextFactory = dbContextFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _characterService = characterService;
    }

    public async Task<Campaign> GetCampaignAsync(Guid campaignId)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.Campaigns
            .Include(c => c.Keeper)
            .Include(c => c.Players)
            .FirstOrDefaultAsync(c => c.Id == campaignId);
    }

    public async Task<List<Character>> GetPlayerCharactersInCampaignAsync(Guid campaignId, string playerId)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var campaign = await dbContext.Campaigns
            .Include(c => c.Players)
            .FirstOrDefaultAsync(c => c.Id == campaignId);

        if (campaign == null)
        {
            _logger.LogWarning($"Campaign with ID {campaignId} not found");
            return new List<Character>();
        }

        var player = campaign.Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null)
        {
            _logger.LogWarning($"Player with ID {playerId} not found in campaign {campaignId}");
            return new List<Character>();
        }

        // In a real implementation, we would filter characters by player ID and campaign ID from the database
        // For now, we'll use the in-memory character service
        var allCharacters = await _characterService.GetAllCharactersAsync();
        return allCharacters.Where(c => c.PersonalInfo.PlayerName == player.UserName).ToList();
    }

    public async Task<Character> CreateCharacterForPlayerInCampaignAsync(Guid campaignId, string playerId, Character character)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var campaign = await dbContext.Campaigns
            .Include(c => c.Keeper)
            .Include(c => c.Players)
            .FirstOrDefaultAsync(c => c.Id == campaignId);

        if (campaign == null)
        {
            throw new Exception($"Campaign with ID {campaignId} not found");
        }

        var player = campaign.Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null)
        {
            throw new Exception($"Player with ID {playerId} not found in campaign {campaignId}");
        }

        // Set the player name in the character
        character.PersonalInfo.PlayerName = player.UserName;

        // Save the character
        return await _characterService.CreateCharacterAsync(character);
    }

    public async Task<List<Campaign>> GetUserCampaignsAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return new List<Campaign>();

        using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.Campaigns
            .Include(c => c.Keeper)
            .Include(c => c.Players)
            .Where(c => c.KeeperId == user.Id || c.Players.Any(p => p.Id == user.Id))
            .ToListAsync();
    }

    private async Task<ApplicationUser> GetCurrentUserAsync()
    {
        var email = _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
            return null;

        using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.Users.SingleOrDefaultAsync(p => p.Email == email);
    }
}