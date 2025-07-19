using CampaignManager.Web.Components.Features.Campaigns.Models;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Components.Features.Characters.Services;

public class CharacterService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    ILogger<CharacterService> logger)
{
    public async Task<Character> CreateCharacterAsync(Character character, Guid? campaignPlayerId)
    {
        try
        {
            var userId = identityService.GetCurrentUserEmail();
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("User must be authenticated to create a character");
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            // Если ID не установлен, генерируем новый
            if (character.Id == Guid.Empty)
                character.Id = Guid.CreateVersion7();

            // Создаем DTO для хранения с дублированием ключевых полей
            CharacterStorageDto storageDto = new()
            {
                CharacterName = character.PersonalInfo.Name,
                Character = character,
                CampaignPlayerId = campaignPlayerId,
                Status = CharacterStatus.Active
            };
            // Инициализируем базовые поля сущности
            storageDto.Init();
            storageDto.Id = character.Id;
            dbContext.CharacterStorage.Add(storageDto);

            // Находим и деактивируем все активные персонажи этого игрока в этой кампании
            if (campaignPlayerId.HasValue)
            {
                var existingActiveCharacters = await dbContext.CharacterStorage
                    .Where(c => c.CampaignPlayerId == campaignPlayerId && c.Status == CharacterStatus.Active)
                    .ToListAsync();

                foreach (var existingChar in existingActiveCharacters)
                {
                    existingChar.Status = CharacterStatus.Inactive;
                    existingChar.LastUpdated = DateTime.UtcNow;
                    dbContext.Update(existingChar);
                }
            }

            await dbContext.SaveChangesAsync();

            logger.LogInformation($"Character {character.Id} created by user {userId}");
            return character;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error creating character: {ex.Message}");
            throw;
        }
    }

    public async Task<CharacterStorageDto?> GetCharacterByIdAsync(Guid id)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.CharacterStorage.FindAsync(id);
    }

    public async Task<CampaignPlayer?> GetCampaignPlayerAsync(Guid id)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.CampaignPlayers.FindAsync(id);
    }

    public async Task UpdateCharacterAsync(Character character)
    {
        try
        {
            var userEmail = identityService.GetCurrentUserEmail();
            logger.LogInformation($"Attempting to update character {character.Id} by user email {userEmail}");

            if (string.IsNullOrEmpty(userEmail))
                throw new UnauthorizedAccessException("User must be authenticated to update a character");

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            // Получаем существующую запись
            var storageDto = await dbContext.CharacterStorage.FindAsync(character.Id);
            if (storageDto == null)
            {
                logger.LogWarning($"Character with ID {character.Id} not found during update");
                throw new KeyNotFoundException($"Character with ID {character.Id} not found");
            }

            storageDto.LastUpdated = DateTime.UtcNow;
            storageDto.Character = character;
            storageDto.CharacterName = character.PersonalInfo.Name;
            dbContext.Update(storageDto);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error updating character: {ex.Message}");
            throw;
        }
    }

    public async Task SetCharacterStatusAsync(Guid characterId, CharacterStatus newStatus)
    {
        try
        {
            var userEmail = identityService.GetCurrentUserEmail();
            if (string.IsNullOrEmpty(userEmail))
                throw new UnauthorizedAccessException("User must be authenticated to change character status");

            // Check if user has permission to change character status (Admin or GameMaster)
            var isKeeper = await identityService.IsKeeper();
            if (!isKeeper)
                throw new UnauthorizedAccessException("Only administrators and game masters can change character status");

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Получаем персонажа
            var character = await dbContext.CharacterStorage
                .Include(c => c.CampaignPlayer)
                .FirstOrDefaultAsync(c => c.Id == characterId);

            if (character == null) throw new KeyNotFoundException($"Character with ID {characterId} not found");

            // Если устанавливаем статус Active, деактивируем остальных персонажей этого игрока в этой кампании
            if (newStatus == CharacterStatus.Active)
            {
                var otherActiveCharacters = await dbContext.CharacterStorage
                    .Where(c => c.CampaignPlayerId == character.CampaignPlayerId
                                && c.Status == CharacterStatus.Active
                                && c.Id != characterId)
                    .ToListAsync();

                foreach (var otherChar in otherActiveCharacters)
                {
                    otherChar.Status = CharacterStatus.Inactive;
                    otherChar.LastUpdated = DateTime.UtcNow;
                    dbContext.Update(otherChar);
                }
            }

            // Обновляем статус указанного персонажа
            character.Status = newStatus;
            character.LastUpdated = DateTime.UtcNow;
            dbContext.Update(character);

            await dbContext.SaveChangesAsync();
            logger.LogInformation($"Character {characterId} status changed to {newStatus} by user {userEmail}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error changing character status: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Gets all character templates (CharacterStorageDto with Status = CharacterStatus.Template)
    /// </summary>
    /// <returns>A list of character templates</returns>
    public async Task<List<CharacterStorageDto>> GetAllCharacterTemplatesAsync()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.CharacterStorage
                .Where(c => c.Status == CharacterStatus.Template)
                .OrderBy(c => c.CharacterName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving character templates");
            return new List<CharacterStorageDto>();
        }
    }

    /// <summary>
    ///     Creates a copy of an existing character template and links it to a scenario
    /// </summary>
    /// <param name="characterId">ID of the character template to copy</param>
    /// <param name="scenarioId">ID of the scenario to link the template to</param>
    /// <returns>The newly created character template with scenario link</returns>
    public async Task<CharacterStorageDto> SaveCharacterTemplateWithScenarioAsync(Guid characterId, Guid scenarioId)
    {
        try
        {
            var userEmail = identityService.GetCurrentUserEmail();
            if (string.IsNullOrEmpty(userEmail))
                throw new UnauthorizedAccessException("User must be authenticated to create a template with scenario link");

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Get the original template
            var character = await dbContext.CharacterStorage
                .FirstOrDefaultAsync(c => c.Id == characterId && c.Status == CharacterStatus.Template);

            if (character == null)
                throw new KeyNotFoundException($"Character template with ID {characterId} not found");


            // Initialize the new template with a new ID
            character.Init();
            character.Status = CharacterStatus.Active;
            character.ScenarioId = scenarioId;
            dbContext.CharacterStorage.Add(character);
            await dbContext.SaveChangesAsync();

            logger.LogInformation($"Character template {character.Id} created with scenario link {scenarioId} by user {userEmail}");
            return character;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error creating character template with scenario link: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Gets all character templates linked to a specific scenario
    /// </summary>
    /// <param name="scenarioId">ID of the scenario</param>
    /// <returns>A list of character templates linked to the specified scenario</returns>
    public async Task<List<CharacterStorageDto>> GetCharacterTemplatesByScenarioIdAsync(Guid scenarioId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Get character templates that are linked to the specified scenario
            return await dbContext.CharacterStorage
                .Include(c => c.Scenario)
                .Where(c => c.ScenarioId == scenarioId)
                .OrderBy(c => c.CharacterName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error retrieving character templates for scenario {scenarioId}: {ex.Message}");
            return new List<CharacterStorageDto>();
        }
    }

    /// <summary>
    ///     Unlinks a character template from a scenario
    /// </summary>
    /// <param name="characterId">ID of the character template to unlink</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> UnlinkCharacterTemplateFromScenarioAsync(Guid characterId)
    {
        try
        {
            var userEmail = identityService.GetCurrentUserEmail();
            if (string.IsNullOrEmpty(userEmail))
                throw new UnauthorizedAccessException("User must be authenticated to unlink a character template from a scenario");

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Get the character template
            var character = await dbContext.CharacterStorage
                .FirstOrDefaultAsync(c => c.Id == characterId);

            if (character == null)
                throw new KeyNotFoundException($"Character template with ID {characterId} not found");

            // Unlink the character template from the scenario
            character.ScenarioId = null;
            character.Scenario = null;
            character.LastUpdated = DateTime.UtcNow;

            dbContext.Update(character);
            await dbContext.SaveChangesAsync();

            logger.LogInformation($"Character template {characterId} unlinked from scenario by user {userEmail}");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error unlinking character template from scenario: {ex.Message}");
            return false;
        }
    }
}