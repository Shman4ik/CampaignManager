using CampaignManager.Web.Model;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CampaignManager.Web.Services;

public class CharacterService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CharacterService> _logger;

    public CharacterService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CharacterService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

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
            var storageDto = new CharacterStorageDto
            {
                Id = character.Id,
                CharacterName = character.PersonalInfo.Name,
                Occupation = character.PersonalInfo.Occupation,
                PlayerName = character.PersonalInfo.PlayerName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CharacterData = JsonSerializer.Serialize(
                    character,
                    new JsonSerializerOptions { WriteIndented = false })
            };

            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Устанавливаем связь с игроком
            dbContext.Entry(storageDto).Property("PlayerId").CurrentValue = userId;

            dbContext.Characters.Add(storageDto);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation($"Character {character.Id} created by user {userId}");
            return character;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating character: {ex.Message}");
            throw;
        }
    }

    public async Task<Character> GetCharacterByIdAsync(Guid id)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var storageDto = await dbContext.Characters.FindAsync(id);

            if (storageDto == null)
            {
                _logger.LogWarning($"Character with ID {id} not found");
                return null;
            }

            // Десериализуем данные персонажа
            var character = JsonSerializer.Deserialize<Character>(
                storageDto.CharacterData,
                new JsonSerializerOptions());

            return character;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting character {id}: {ex.Message}");
            throw;
        }
    }

    public async Task<List<Character>> GetAllCharactersAsync()
    {
        try
        {
            string userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return new List<Character>();
            }

            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var storageDtos = await dbContext.Characters
                .Where(c => EF.Property<string>(c, "PlayerId") == userId)
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();

            // Десериализуем все персонажи
            var characters = storageDtos
                .Select(dto => JsonSerializer.Deserialize<Character>(
                    dto.CharacterData,
                    new JsonSerializerOptions()))
                .ToList();

            return characters;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting all characters: {ex.Message}");
            throw;
        }
    }

    public async Task<List<Character>> GetCharactersByCampaignIdAsync(Guid campaignId)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var storageDtos = await dbContext.Characters
                .Where(c => EF.Property<Guid?>(c, "CampaignId") == campaignId)
                .OrderBy(c => c.CharacterName)
                .ToListAsync();

            // Десериализуем все персонажи
            var characters = storageDtos
                .Select(dto => JsonSerializer.Deserialize<Character>(
                    dto.CharacterData,
                    new JsonSerializerOptions()))
                .ToList();

            return characters;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting characters for campaign {campaignId}: {ex.Message}");
            throw;
        }
    }

    public async Task<Character> UpdateCharacterAsync(Character character)
    {
        try
        {
            string userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("User must be authenticated to update a character");
            }

            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Получаем существующую запись
            var storageDto = await dbContext.Characters.FindAsync(character.Id);
            if (storageDto == null)
            {
                throw new KeyNotFoundException($"Character with ID {character.Id} not found");
            }

            // Проверяем, принадлежит ли персонаж текущему пользователю
            string characterUserId = dbContext.Entry(storageDto).Property<string>("PlayerId").CurrentValue;
            if (characterUserId != userId)
            {
                throw new UnauthorizedAccessException("Cannot update a character that belongs to another user");
            }

            // Сохраняем кампанию
            var campaignId = dbContext.Entry(storageDto).Property<Guid?>("CampaignId").CurrentValue;

            // Обновляем данные персонажа
            storageDto.CharacterName = character.PersonalInfo.Name;
            storageDto.Occupation = character.PersonalInfo.Occupation;
            storageDto.PlayerName = character.PersonalInfo.PlayerName;
            storageDto.UpdatedAt = DateTime.UtcNow;
            storageDto.CharacterData = JsonSerializer.Serialize(
                character,
                new JsonSerializerOptions { WriteIndented = false });

            // Убеждаемся, что связи сохраняются
            dbContext.Entry(storageDto).Property("PlayerId").CurrentValue = userId;
            dbContext.Entry(storageDto).Property("CampaignId").CurrentValue = campaignId;

            dbContext.Update(storageDto);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation($"Character {character.Id} updated by user {userId}");
            return character;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating character: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> DeleteCharacterAsync(Guid id)
    {
        try
        {
            string userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("User must be authenticated to delete a character");
            }

            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var storageDto = await dbContext.Characters.FindAsync(id);
            if (storageDto == null)
            {
                return false;
            }

            string characterUserId = dbContext.Entry(storageDto).Property<string>("PlayerId").CurrentValue;
            if (characterUserId != userId)
            {
                throw new UnauthorizedAccessException("Cannot delete a character that belongs to another user");
            }

            dbContext.Characters.Remove(storageDto);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation($"Character {id} deleted by user {userId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting character: {ex.Message}");
            throw;
        }
    }

    private string GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }
}