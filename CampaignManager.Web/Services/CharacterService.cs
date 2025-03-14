using CampaignManager.Web.Model;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace CampaignManager.Web.Services;

public class CharacterService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IHttpContextAccessor httpContextAccessor,
    ILogger<CharacterService> logger)
{
    public async Task<Character> CreateCharacterAsync(Character character)
    {
        try
        {
            string userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("User must be authenticated to create a character");
            }

            // Если ID не установлен, генерируем новый
            if (character.Id == Guid.Empty)
            {
                character.Id = Guid.NewGuid();
            }

            // Создаем DTO для хранения с дублированием ключевых полей
            CharacterStorageDto storageDto = new() { Id = character.Id, CharacterName = character.PersonalInfo.Name, CreatedAt = DateTime.UtcNow, Character = character };

            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

            // Устанавливаем связь с игроком
            dbContext.Entry(storageDto).Property("PlayerId").CurrentValue = userId;

            dbContext.CharacterStorage.Add(storageDto);
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

    public async Task<Character> GetCharacterByIdAsync(Guid id)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        CharacterStorageDto? storageDto = await dbContext.CharacterStorage.FindAsync(id);
        return storageDto.Character;
    }

    public async Task<Character> UpdateCharacterAsync(Character character)
    {
        try
        {
            string userEmail = GetCurrentUserId(); // Теперь возвращает email
            logger.LogInformation($"Attempting to update character {character.Id} by user email {userEmail}");

            if (string.IsNullOrEmpty(userEmail))
            {
                throw new UnauthorizedAccessException("User must be authenticated to update a character");
            }

            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();


            // Получаем существующую запись
            CharacterStorageDto? storageDto = await dbContext.CharacterStorage.FindAsync(character.Id);
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
            return character;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error updating character: {ex.Message}");
            throw;
        }
    }

    private string? GetCurrentUserId()
    {
        return httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;
    }
}