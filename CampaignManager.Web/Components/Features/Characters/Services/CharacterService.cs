using CampaignManager.Web.Components.Features.Campaigns.Models;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Skills.Services;
using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Components.Features.Characters.Services;

public class CharacterService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    ILogger<CharacterService> logger,
    SkillService skillService)
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

    /// <summary>
    ///     Gets all non-template characters.
    /// </summary>
    /// <returns>A list of all characters.</returns>
    public async Task<List<Character>> GetAllCharactersAsync()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var characterDtos = await dbContext.CharacterStorage
                .Where(c => c.Status != CharacterStatus.Template)
                .ToListAsync();

            return characterDtos.Select(dto => dto.Character).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving all characters");
            return new List<Character>();
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

    /// <summary>
    /// Migrates character skills to link them with SkillModel entities by matching skill names.
    /// This is a one-time migration utility to establish links between existing character skills and the skill wiki.
    /// </summary>
    /// <returns>Migration result with statistics</returns>
    public async Task<SkillMigrationResult> MigrateCharacterSkillsToSkillModelAsync()
    {
        var result = new SkillMigrationResult();

        try
        {
            var userEmail = identityService.GetCurrentUserEmail();
            if (string.IsNullOrEmpty(userEmail))
                throw new UnauthorizedAccessException("User must be authenticated to perform migration");

            // Check if user is GameMaster or Admin
            var isKeeper = await identityService.IsKeeper();
            if (!isKeeper)
                throw new UnauthorizedAccessException("Only administrators and game masters can perform skill migration");

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Get all characters
            var allCharacters = await dbContext.CharacterStorage.ToListAsync();
            result.TotalCharacters = allCharacters.Count;

            // Get all skills from wiki
            var allSkills = await skillService.GetAllSkillsAsync(string.Empty, pageSize: int.MaxValue);

            // Create multiple lookup strategies for flexible matching
            var exactMatchDict = allSkills.ToDictionary(s => s.Name.Trim().ToLowerInvariant(), s => s.Id);
            var baseNameDict = new Dictionary<string, List<(Guid Id, string FullName)>>();

            // Build base name dictionary for skills with specializations (e.g., "выживание" → ["выживание (море)", ...])
            foreach (var skill in allSkills)
            {
                var normalizedName = skill.Name.Trim().ToLowerInvariant();

                // Extract base name if skill has parentheses (specialization)
                var parenIndex = normalizedName.IndexOf('(');
                if (parenIndex > 0)
                {
                    var baseName = normalizedName.Substring(0, parenIndex).Trim();
                    if (!baseNameDict.ContainsKey(baseName))
                        baseNameDict[baseName] = new List<(Guid, string)>();
                    baseNameDict[baseName].Add((skill.Id, skill.Name));
                }
            }

            logger.LogInformation($"Starting skill migration for {allCharacters.Count} characters with {allSkills.Count} wiki skills");

            foreach (var characterStorage in allCharacters)
            {
                var character = characterStorage.Character;
                if (character?.Skills?.SkillGroups == null)
                    continue;

                bool characterModified = false;

                foreach (var skillGroup in character.Skills.SkillGroups)
                {
                    foreach (var skill in skillGroup.Skills)
                    {
                        result.TotalSkills++;

                        // Skip if already linked
                        if (skill.SkillModelId.HasValue)
                        {
                            result.SkillsAlreadyLinked++;
                            continue;
                        }

                        var normalizedSkillName = skill.Name.Trim().ToLowerInvariant();
                        Guid? matchedSkillId = null;

                        // Strategy 1: Exact match
                        if (exactMatchDict.TryGetValue(normalizedSkillName, out var exactId))
                        {
                            matchedSkillId = exactId;
                            result.MatchStrategies["exact"] = result.MatchStrategies.GetValueOrDefault("exact") + 1;
                        }
                        // Strategy 2: Character skill might be a base name, find first matching specialized skill
                        else if (baseNameDict.TryGetValue(normalizedSkillName, out var specializedSkills))
                        {
                            // Pick the first available specialized skill
                            matchedSkillId = specializedSkills[0].Id;
                            result.MatchStrategies["base-to-specialized"] = result.MatchStrategies.GetValueOrDefault("base-to-specialized") + 1;
                            result.SpecializationMatches.Add($"{skill.Name} → {specializedSkills[0].FullName}");
                        }
                        // Strategy 3: Check if character skill has specialization and base exists in DB
                        else
                        {
                            var parenIndex = normalizedSkillName.IndexOf('(');
                            if (parenIndex > 0)
                            {
                                var baseName = normalizedSkillName.Substring(0, parenIndex).Trim();

                                // Try to find exact match for base name
                                if (exactMatchDict.TryGetValue(baseName, out var baseId))
                                {
                                    matchedSkillId = baseId;
                                    result.MatchStrategies["specialized-to-base"] = result.MatchStrategies.GetValueOrDefault("specialized-to-base") + 1;
                                }
                            }
                        }

                        if (matchedSkillId.HasValue)
                        {
                            skill.SkillModelId = matchedSkillId;
                            result.SkillsLinked++;
                            characterModified = true;
                        }
                        else
                        {
                            result.SkillsNotFound++;
                            result.UnmatchedSkills.Add(skill.Name);
                        }
                    }
                }

                if (characterModified)
                {
                    characterStorage.Character = character;
                    characterStorage.LastUpdated = DateTime.UtcNow;
                    dbContext.Update(characterStorage);
                    result.CharactersUpdated++;
                }
            }

            await dbContext.SaveChangesAsync();

            logger.LogInformation($"Skill migration completed: {result.SkillsLinked} skills linked, {result.SkillsNotFound} not found, {result.CharactersUpdated} characters updated");
            result.Success = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during skill migration");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }
}

/// <summary>
/// Result of skill migration operation
/// </summary>
public class SkillMigrationResult
{
    public bool Success { get; set; }
    public int TotalCharacters { get; set; }
    public int CharactersUpdated { get; set; }
    public int TotalSkills { get; set; }
    public int SkillsLinked { get; set; }
    public int SkillsAlreadyLinked { get; set; }
    public int SkillsNotFound { get; set; }
    public List<string> UnmatchedSkills { get; set; } = new();
    public Dictionary<string, int> MatchStrategies { get; set; } = new();
    public List<string> SpecializationMatches { get; set; } = new();
    public string? ErrorMessage { get; set; }
}