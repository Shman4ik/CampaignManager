using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampaignManager.Web.Services;

public class CampaignCharacterService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IDbContextFactory<AppIdentityDbContext> identityDbContextFactory,
    IHttpContextAccessor httpContextAccessor,
    ILogger<CampaignCharacterService> logger)
{

    public async Task<Companies.Models.Campaign?> GetCampaignAsync(Guid campaignId)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Campaigns.Include(c => c.Players).FirstOrDefaultAsync(c => c.Id == campaignId);
    }

    public async Task<Character?> GetPlayerCharactersInCampaignAsync(Guid campaignId)
    {
        try
        {
            string? userEmail = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;

            if (string.IsNullOrEmpty(userEmail))
            {
                logger.LogWarning("Не удалось получить email пользователя");
                return null;
            }

            await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

            // Получаем запись игрока в кампании
            var campaignPlayer = await dbContext.CampaignPlayers
                .FirstOrDefaultAsync(cp => cp.CampaignId == campaignId && cp.PlayerEmail == userEmail);

            if (campaignPlayer == null)
            {
                logger.LogWarning("Игрок с email {Email} не найден в кампании {CampaignId}", userEmail, campaignId);
                return null;
            }

            // Получаем запись персонажа
            var characterStorageDto = await dbContext.CharacterStorage
                .FirstOrDefaultAsync(c => c.CampaignPlayerId == campaignPlayer.Id);

            return characterStorageDto?.Character;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении персонажа из базы данных");
            return null;
        }
    }

    private async Task<string?> GetCurrentUserEmailAsync()
    {
        return httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;
    }

    public async Task<bool> IsCurrentUserCampaignKeeper(Companies.Models.Campaign campaign)
    {
        var userEmail = await GetCurrentUserEmailAsync();
        return campaign.KeeperEmail == userEmail;
    }

    public async Task<Companies.Models.Campaign?> CreateCharacterForPlayerInCampaignAsync(Guid campaignId, string playerEmail, Character character)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

        var campaign = await dbContext.Campaigns
            .Include(c => c.Players)
            .FirstOrDefaultAsync(c => c.Id == campaignId);

        if (campaign == null)
        {
            logger.LogWarning("Campaign {CampaignId} not found", campaignId);
            return null;
        }

        // Проверяем, является ли игрок участником кампании
        var campaignPlayer = campaign.Players.FirstOrDefault(p => p.PlayerEmail == playerEmail);
        if (campaignPlayer == null)
        {
            logger.LogWarning("Player {PlayerEmail} is not a member of campaign {CampaignId}", playerEmail, campaignId);
            return null;
        }

        // Проверяем и устанавливаем значения по умолчанию для персонажа
        EnsureCharacterDefaults(character);

        // Если ID не установлен, генерируем новый
        if (character.Id == Guid.Empty)
        {
            character.Id = Guid.NewGuid();
        }

        // Создаем персонажа
        var characterStorageDto = new CharacterStorageDto
        {
            Character = character,
            CharacterName = character.PersonalInfo.Name,
            CampaignPlayerId = campaignPlayer.Id,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };

        // Инициализируем базовые поля сущности
        characterStorageDto.Init();

        dbContext.CharacterStorage.Add(characterStorageDto);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Created character {CharacterName} with ID {CharacterId} for player {PlayerEmail} in campaign {CampaignId}",
            character.PersonalInfo.Name, character.Id, playerEmail, campaignId);

        return campaign;
    }

    // Вспомогательный метод для установки значений по умолчанию
    private void EnsureCharacterDefaults(Character character)
    {
        // Проверяем имя персонажа
        if (string.IsNullOrWhiteSpace(character.PersonalInfo.Name))
        {
            character.PersonalInfo.Name = "Безымянный";
        }

        // Проверка характеристик на валидность
        if (character.DerivedAttributes.HitPoints == null)
        {
            int hitPoints = (character.Characteristics.Size.Regular + character.Characteristics.Constitution.Regular) / 10;
            character.DerivedAttributes.HitPoints = new AttributeWithMaxValue(hitPoints, hitPoints);
        }

        if (character.DerivedAttributes.MagicPoints == null)
        {
            int magicPoints = character.Characteristics.Power.Regular / 5;
            character.DerivedAttributes.MagicPoints = new AttributeWithMaxValue(magicPoints, magicPoints);
        }

        if (character.DerivedAttributes.Sanity == null)
        {
            int sanity = character.Characteristics.Power.Regular;
            character.DerivedAttributes.Sanity = new AttributeWithMaxValue(sanity, 99);
        }

        if (character.DerivedAttributes.Luck == null)
        {
            character.DerivedAttributes.Luck = new AttributeWithMaxValue(50, 99);
        }

        // Проверяем навыки
        if (character.Skills == null)
        {
            character.Skills = new SkillsModel();
        }

        // Проверяем оружие
        if (character.Weapons == null)
        {
            character.Weapons = new List<Weapon>();
        }
    }

    public async Task<List<Companies.Models.Campaign>> GetUserCampaignsAsync()
    {
        ApplicationUser user = await GetCurrentUserAsync();
        if (user == null)
        {
            return new List<Companies.Models.Campaign>();
        }

        using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Campaigns
            .Include(c => c.Players)
            .Where(c => c.KeeperEmail == user.Email || c.Players.Any(p => p.PlayerEmail == user.Email))
            .ToListAsync();
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        string? email = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            logger.LogWarning("User email not found in authentication context");
            return null;
        }

        await using AppIdentityDbContext dbContext = await identityDbContextFactory.CreateDbContextAsync();
        return await dbContext.ApplicationUsers.SingleOrDefaultAsync(p => p.Email == email);
    }
}