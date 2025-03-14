using CampaignManager.Web.Compain.Models;
using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CampaignManager.Web.Services;

public class CampaignCharacterService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IDbContextFactory<AppIdentityDbContext> identityDbContextFactory,
    IHttpContextAccessor httpContextAccessor,
    ILogger<CampaignCharacterService> logger,
    CharacterService characterService)
{
    private readonly CharacterService _characterService = characterService;

    public async Task<Campaign?> GetCampaignAsync(Guid campaignId)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Campaigns
            .Include(c => c.Keeper)
            .Include(c => c.Players)
            .FirstOrDefaultAsync(c => c.Id == campaignId);
    }

    public async Task<Character?> GetPlayerCharactersInCampaignAsync(Guid campaingPlayerId)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        var characterStorageDto = await dbContext.CharacterStorage.SingleOrDefaultAsync(p => p.CampaignPlayerId == campaingPlayerId);
        return characterStorageDto?.Character;
    }

    public async Task<Character> CreateCharacterForPlayerInCampaignAsync(Guid campaignId, string playerEmail, Character character)
    {
        try
        {
            logger.LogInformation($"Starting to create character for player email: {playerEmail} in campaign: {campaignId}");

            await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            await using AppIdentityDbContext appIdentityDbContext = await identityDbContextFactory.CreateDbContextAsync();
            

            // Проверка кампании
            Campaign? campaign = await dbContext.Campaigns
                .Include(c => c.Keeper)
                .Include(c => c.Players)
                .FirstOrDefaultAsync(c => c.Id == campaignId);

            if (campaign == null)
            {
                logger.LogError($"Campaign with ID {campaignId} not found");
                throw new Exception($"Campaign with ID {campaignId} not found");
            }
            
            ApplicationUser user = await appIdentityDbContext.Users.SingleOrDefaultAsync(u => u.Email == playerEmail);
            if(user == null)
                throw new ArgumentException($"User {playerEmail} not found");
            // Проверка, является ли пользователь ведущим или игроком в кампании
            bool isKeeper = campaign.KeeperEmail == user.Email;
            bool isPlayer = campaign.Players.Any(p => p.PlayerEmail == user.Email);

            logger.LogInformation($"User {user.UserName} (ID: {user.Id}) is keeper: {isKeeper}, is player: {isPlayer}");

            // Если пользователь не является ни ведущим, ни игроком, добавляем его как игрока
            if (!isKeeper && !isPlayer)
            {
                var campaignPlayer = new CampaignPlayer()
                {
                    CampaignId = campaignId,
                    PlayerEmail = user.Email
                };
                campaignPlayer.Init();
                campaign.Players.Add(campaignPlayer);
                await dbContext.SaveChangesAsync();
                logger.LogInformation($"User {user.UserName} added to campaign {campaignId} as a player");
            }

            // Если имя игрока не установлено, устанавливаем его
            if (string.IsNullOrEmpty(character.PersonalInfo.PlayerName))
            {
                character.PersonalInfo.PlayerName = user.UserName ?? user.Email;
                logger.LogInformation($"Set PlayerName to {character.PersonalInfo.PlayerName}");
            }

            // Проверка и настройка необходимых атрибутов персонажа перед сохранением
            EnsureCharacterDefaults(character);

            // Создаем персонажа с ID
            if (character.Id == Guid.Empty)
            {
                character.Id = Guid.NewGuid();
            }

            logger.LogInformation($"Character ID set to {character.Id}");

           

            // Создаем DTO для хранения с дополнительными полями
            CharacterStorageDto storageDto = new()
            {
                Id = character.Id,
                CharacterName = character.PersonalInfo?.Name ?? "Unnamed",
                Character = character
            };

            logger.LogInformation($"Character storage DTO created with name: {storageDto.CharacterName}");

            // Сохраняем персонажа с привязкой к кампании в отдельной транзакции
            try
            {
                dbContext.CharacterStorage.Add(storageDto);
                await dbContext.SaveChangesAsync();
                logger.LogInformation($"Character {character.Id} successfully saved to database");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error saving character to database: {ex.Message}");
                throw new Exception($"Failed to save character to database: {ex.Message}", ex);
            }

            logger.LogInformation($"Character {character.Id} created for player {user.Id} in campaign {campaignId}");
            return character;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Unhandled exception in CreateCharacterForPlayerInCampaignAsync: {ex.Message}");
            throw;
        }
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

    public async Task<List<Campaign>> GetUserCampaignsAsync()
    {
        ApplicationUser user = await GetCurrentUserAsync();
        if (user == null)
        {
            return new List<Campaign>();
        }

        using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Campaigns
            .Include(c => c.Keeper)
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