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
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Проверяем кампанию
            var campaign = await dbContext.Campaigns
                .FirstOrDefaultAsync(c => c.Id == campaignId);

            if (campaign == null)
            {
                _logger.LogWarning($"Campaign with ID {campaignId} not found");
                return new List<Character>();
            }

            // Получаем персонажей по playerId и campaignId
            var characterDtos = await dbContext.Characters
                .AsNoTracking() // Для лучшей производительности при чтении
                .Where(c =>
                    EF.Property<string>(c, "PlayerId") == playerId &&
                    EF.Property<Guid?>(c, "CampaignId") == campaignId)
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();

            _logger.LogInformation($"Found {characterDtos.Count} characters for player {playerId} in campaign {campaignId}");

            // Десериализуем персонажей
            var characters = new List<Character>();
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true // Делаем десериализацию нечувствительной к регистру
            };

            foreach (var dto in characterDtos)
            {
                try
                {
                    if (!string.IsNullOrEmpty(dto.CharacterData))
                    {
                        var character = System.Text.Json.JsonSerializer.Deserialize<Character>(
                            dto.CharacterData, options);

                        if (character != null)
                        {
                            characters.Add(character);
                        }
                        else
                        {
                            _logger.LogWarning($"Deserialized character is null for ID {dto.Id}");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Character data is empty for ID {dto.Id}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to deserialize character {dto.Id}: {ex.Message}");
                }
            }

            return characters;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in GetPlayerCharactersInCampaignAsync: {ex.Message}");
            return new List<Character>();
        }
    }

    public async Task<Character> CreateCharacterForPlayerInCampaignAsync(Guid campaignId, string playerEmail, Character character)
    {
        try
        {
            _logger.LogInformation($"Starting to create character for player email: {playerEmail} in campaign: {campaignId}");

            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Проверка кампании
            var campaign = await dbContext.Campaigns
                .Include(c => c.Keeper)
                .Include(c => c.Players)
                .FirstOrDefaultAsync(c => c.Id == campaignId);

            if (campaign == null)
            {
                _logger.LogError($"Campaign with ID {campaignId} not found");
                throw new Exception($"Campaign with ID {campaignId} not found");
            }

            // Проверка пользователя - используем email для поиска
            ApplicationUser user = null;

            // 1. Ищем пользователя по email
            user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == playerEmail);

            // 2. Если пользователь не найден, пробуем найти через HttpContext (текущий пользователь)
            if (user == null && _httpContextAccessor.HttpContext != null)
            {
                var currentUserEmail = _httpContextAccessor.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                if (!string.IsNullOrEmpty(currentUserEmail))
                {
                    user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == currentUserEmail);
                    _logger.LogInformation($"Found user by email: {currentUserEmail}");
                }
            }

            // 3. Если пользователь всё ещё не найден, создаем нового пользователя
            if (user == null)
            {
                _logger.LogWarning($"User with email {playerEmail} not found, creating new user");

                // Получаем данные из текущего контекста или используем переданный email
                string email = !string.IsNullOrEmpty(playerEmail) ?
                    playerEmail :
                    _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

                string name = _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? email;

                if (string.IsNullOrEmpty(email))
                {
                    throw new ArgumentException("Valid email is required to create a user");
                }

                user = new ApplicationUser
                {
                    UserName = name,
                    NormalizedUserName = name?.ToUpper(),
                    Email = email,
                    NormalizedEmail = email?.ToUpper(),
                    EmailConfirmed = true,
                    LockoutEnabled = false,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    Role = PlayerRole.Player
                };

                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();
                _logger.LogInformation($"Created new user with Email: {user.Email}, Username: {user.UserName}");
            }

            // Проверка, является ли пользователь ведущим или игроком в кампании
            bool isKeeper = campaign.KeeperId == user.Id;
            bool isPlayer = campaign.Players.Any(p => p.Id == user.Id);

            _logger.LogInformation($"User {user.UserName} (ID: {user.Id}) is keeper: {isKeeper}, is player: {isPlayer}");

            // Если пользователь не является ни ведущим, ни игроком, добавляем его как игрока
            if (!isKeeper && !isPlayer)
            {
                campaign.Players.Add(user);
                await dbContext.SaveChangesAsync();
                _logger.LogInformation($"User {user.UserName} added to campaign {campaignId} as a player");
            }

            // Если имя игрока не установлено, устанавливаем его
            if (string.IsNullOrEmpty(character.PersonalInfo.PlayerName))
            {
                character.PersonalInfo.PlayerName = user.UserName ?? user.Email;
                _logger.LogInformation($"Set PlayerName to {character.PersonalInfo.PlayerName}");
            }

            // Проверка и настройка необходимых атрибутов персонажа перед сохранением
            EnsureCharacterDefaults(character);

            // Создаем персонажа с ID
            if (character.Id == Guid.Empty)
            {
                character.Id = Guid.NewGuid();
            }

            _logger.LogInformation($"Character ID set to {character.Id}");

            // Сериализуем персонажа с обработкой потенциальных ошибок
            string characterJson;
            try
            {
                var serializerOptions = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = false,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                characterJson = System.Text.Json.JsonSerializer.Serialize(character, serializerOptions);

                if (string.IsNullOrEmpty(characterJson))
                {
                    _logger.LogError("Character serialization resulted in empty JSON");
                    throw new InvalidOperationException("Character serialization failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error serializing character: {ex.Message}");
                throw new InvalidOperationException($"Failed to serialize character: {ex.Message}", ex);
            }

            // Создаем DTO для хранения с дополнительными полями
            var storageDto = new CharacterStorageDto
            {
                Id = character.Id,
                CharacterName = character.PersonalInfo?.Name ?? "Unnamed",
                Occupation = character.PersonalInfo?.Occupation ?? "Unknown",
                PlayerName = character.PersonalInfo?.PlayerName ?? user.UserName ?? user.Email,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CharacterData = characterJson
            };

            _logger.LogInformation($"Character storage DTO created with name: {storageDto.CharacterName}");

            // Сохраняем персонажа с привязкой к кампании в отдельной транзакции
            try
            {
                using var transaction = await dbContext.Database.BeginTransactionAsync();

                // Устанавливаем связи персонажа через теневые свойства
                dbContext.Entry(storageDto).Property("PlayerId").CurrentValue = user.Id;
                dbContext.Entry(storageDto).Property("CampaignId").CurrentValue = campaignId;

                dbContext.Characters.Add(storageDto);

                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Character {character.Id} successfully saved to database");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving character to database: {ex.Message}");
                throw new Exception($"Failed to save character to database: {ex.Message}", ex);
            }

            _logger.LogInformation($"Character {character.Id} created for player {user.Id} in campaign {campaignId}");
            return character;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unhandled exception in CreateCharacterForPlayerInCampaignAsync: {ex.Message}");
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
        {
            _logger.LogWarning("User email not found in authentication context");
            return null;
        }

        using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.ApplicationUsers.FirstOrDefaultAsync(p => p.Email == email);
    }
}