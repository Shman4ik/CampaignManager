using CampaignManager.Web.Components.Features.Campaigns.Models;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Shared.Model;
using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Components.Features.Characters.Services;

public sealed class CharacterService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    ILogger<CharacterService> logger,
    IMemoryCache cache)
{
    private const string PublishedScenariosCacheKey = "PublishedScenarios";

    /// <summary>
    ///     What a caller wants to do with a stored character.
    /// </summary>
    private enum CharacterAccess
    {
        Read,
        Write
    }

    /// <summary>
    ///     Checks whether the current user may read or modify a character occupying the given player slot.
    ///     A character bound to a campaign player belongs to that player and to the keeper of their campaign.
    ///     An unbound row (NPC or pregen template) is shared library content: anyone signed in may read it,
    ///     only keepers may change it. Administrators may do everything.
    /// </summary>
    private async Task<bool> CanAccessCharacterAsync(AppDbContext dbContext, Guid? campaignPlayerId, CharacterAccess access)
    {
        var userEmail = await identityService.GetCurrentUserEmailAsync();
        if (string.IsNullOrEmpty(userEmail))
            return false;

        var role = await identityService.GetCurrentUserRole();
        if (role is PlayerRole.Administrator)
            return true;

        if (campaignPlayerId is null)
            return access is CharacterAccess.Read || role is PlayerRole.GameMaster;

        var owner = await dbContext.CampaignPlayers
            .Where(p => p.Id == campaignPlayerId.Value)
            .Select(p => new { p.PlayerEmail, p.Campaign.KeeperEmail })
            .FirstOrDefaultAsync();

        if (owner is null)
            return false;

        return string.Equals(owner.PlayerEmail, userEmail, StringComparison.OrdinalIgnoreCase)
               || string.Equals(owner.KeeperEmail, userEmail, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Character> CreateCharacterAsync(Character character, Guid? campaignPlayerId, CharacterStatus status = CharacterStatus.Active)
    {
        try
        {
            var userId = await identityService.GetCurrentUserEmailAsync();
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("User must be authenticated to create a character");
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // A character may only be attached to a player slot the caller actually controls, and only a
            // keeper may add unbound rows (NPC and pregen templates) to the shared library.
            if (!await CanAccessCharacterAsync(dbContext, campaignPlayerId, CharacterAccess.Write))
                throw new UnauthorizedAccessException("Недостаточно прав для создания этого персонажа");

            // Если ID не установлен, генерируем новый
            if (character.Id == Guid.Empty)
                character.Id = Guid.CreateVersion7();

            // Создаем DTO для хранения с дублированием ключевых полей
            CharacterStorageDto storageDto = new()
            {
                CharacterName = character.PersonalInfo.Name,
                Character = character,
                CampaignPlayerId = campaignPlayerId,
                Status = status
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

            logger.LogInformation("Character {CharacterId} created by user {UserEmail}", character.Id, userId);
            return character;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating character");
            throw;
        }
    }

    /// <summary>
    ///     Loads a character by id. Returns null when it does not exist or the current user has no access
    ///     to it — the caller cannot tell the two apart, which is deliberate.
    /// </summary>
    public async Task<CharacterStorageDto?> GetCharacterByIdAsync(Guid id)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var character = await dbContext.CharacterStorage.FindAsync(id);
        if (character is null)
            return null;

        if (!await CanAccessCharacterAsync(dbContext, character.CampaignPlayerId, CharacterAccess.Read))
        {
            logger.LogWarning("Denied read access to character {CharacterId}", id);
            return null;
        }

        return character;
    }

    /// <summary>
    ///     Loads a campaign player slot. Only the player themselves, the keeper of that campaign and
    ///     administrators may see it.
    /// </summary>
    public async Task<CampaignPlayer?> GetCampaignPlayerAsync(Guid id)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var player = await dbContext.CampaignPlayers
            .Include(p => p.Campaign)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (player is null)
            return null;

        if (!await CanAccessCharacterAsync(dbContext, id, CharacterAccess.Read))
        {
            logger.LogWarning("Denied access to campaign player {CampaignPlayerId}", id);
            return null;
        }

        return player;
    }

    public async Task UpdateCharacterAsync(Character character)
    {
        try
        {
            var userEmail = await identityService.GetCurrentUserEmailAsync();
            logger.LogInformation("Attempting to update character {CharacterId} by user {UserEmail}", character.Id, userEmail);

            if (string.IsNullOrEmpty(userEmail))
                throw new UnauthorizedAccessException("User must be authenticated to update a character");

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            // Получаем существующую запись
            var storageDto = await dbContext.CharacterStorage.FindAsync(character.Id);
            if (storageDto == null)
            {
                logger.LogWarning("Character {CharacterId} not found during update", character.Id);
                throw new KeyNotFoundException($"Character with ID {character.Id} not found");
            }

            if (!await CanAccessCharacterAsync(dbContext, storageDto.CampaignPlayerId, CharacterAccess.Write))
            {
                logger.LogWarning("Denied write access to character {CharacterId}", character.Id);
                throw new UnauthorizedAccessException("Недостаточно прав для изменения этого персонажа");
            }

            storageDto.LastUpdated = DateTime.UtcNow;
            storageDto.Character = character;
            storageDto.CharacterName = character.PersonalInfo.Name;
            dbContext.Update(storageDto);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating character {CharacterId}", character.Id);
            throw;
        }
    }

    public async Task SetCharacterStatusAsync(Guid characterId, CharacterStatus newStatus)
    {
        try
        {
            var userEmail = await identityService.GetCurrentUserEmailAsync();
            if (string.IsNullOrEmpty(userEmail))
                throw new UnauthorizedAccessException("User must be authenticated to change character status");

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Получаем персонажа
            var character = await dbContext.CharacterStorage
                .Include(c => c.CampaignPlayer)
                .FirstOrDefaultAsync(c => c.Id == characterId);

            if (character == null) throw new KeyNotFoundException($"Character with ID {characterId} not found");

            // Being a keeper somewhere is not enough — it has to be this character's campaign.
            if (!await CanAccessCharacterAsync(dbContext, character.CampaignPlayerId, CharacterAccess.Write))
            {
                logger.LogWarning("Denied status change on character {CharacterId}", characterId);
                throw new UnauthorizedAccessException("Недостаточно прав для изменения статуса этого персонажа");
            }

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
            logger.LogInformation("Character {CharacterId} status changed to {Status} by user {UserEmail}", characterId, newStatus, userEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error changing status of character {CharacterId}", characterId);
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
            return [];
        }
    }

    /// <summary>
    ///     Creates a copy of an existing character template and links it to a scenario
    /// </summary>
    /// <param name="characterId">ID of the character template to copy</param>
    /// <param name="scenarioId">ID of the scenario to link the template to</param>
    /// <param name="npcRole">Role of the NPC in the scenario</param>
    /// <returns>The newly created character template with scenario link</returns>
    public async Task<CharacterStorageDto> SaveCharacterTemplateWithScenarioAsync(Guid characterId, Guid scenarioId, NpcRole npcRole = NpcRole.Neutral)
    {
        try
        {
            var userEmail = await identityService.GetCurrentUserEmailAsync();
            if (string.IsNullOrEmpty(userEmail))
                throw new UnauthorizedAccessException("User must be authenticated to create a template with scenario link");

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Load template as detached entity so we can persist it as a new row.
            var character = await dbContext.CharacterStorage
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == characterId && c.Status == CharacterStatus.Template);

            if (character is null)
                throw new KeyNotFoundException($"Character template with ID {characterId} not found");

            if (!await CanAccessCharacterAsync(dbContext, character.CampaignPlayerId, CharacterAccess.Write))
                throw new UnauthorizedAccessException("Недостаточно прав для привязки этого персонажа к сценарию");

            // Create scenario-bound copy.
            character.Init();
            character.Status = CharacterStatus.Active;
            character.ScenarioId = scenarioId;
            character.Scenario = null;
            character.CampaignPlayerId = null;
            character.CampaignPlayer = null;
            character.NpcRole = npcRole;

            dbContext.CharacterStorage.Add(character);
            await dbContext.SaveChangesAsync();

            cache.Remove(PublishedScenariosCacheKey);

            logger.LogInformation(
                "Character template {TemplateId} copied to scenario {ScenarioId} as character {CharacterId} by user {UserEmail}",
                characterId,
                scenarioId,
                character.Id,
                userEmail);
            return character;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error creating character template from template {TemplateId} for scenario {ScenarioId}",
                characterId,
                scenarioId);
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
                .Where(c => c.ScenarioId == scenarioId)
                .OrderBy(c => c.CharacterName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving character templates for scenario {ScenarioId}", scenarioId);
            return [];
        }
    }

    public async Task UpdateNpcRoleAsync(Guid characterId, NpcRole role)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var character = await dbContext.CharacterStorage.FindAsync(characterId);
        if (character is null)
            throw new KeyNotFoundException($"Character with ID {characterId} not found");

        if (!await CanAccessCharacterAsync(dbContext, character.CampaignPlayerId, CharacterAccess.Write))
        {
            logger.LogWarning("Denied NPC role change on character {CharacterId}", characterId);
            throw new UnauthorizedAccessException("Недостаточно прав для изменения роли этого NPC");
        }

        character.NpcRole = role;
        character.LastUpdated = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    ///     Reserves a pregen character for the current user: the pregen template is transformed into the
    ///     user's active character in the scenario's campaign. Called when a player clicks "Забронировать"
    ///     on the home page one-shot announcement.
    /// </summary>
    /// <param name="pregenId">ID of the pregen template to reserve.</param>
    /// <exception cref="UnauthorizedAccessException">User is not authenticated.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Pregen is not available, scenario has no campaign, or the user already reserved a pregen in this
    ///     scenario.
    /// </exception>
    public async Task<CharacterStorageDto> ReservePregenAsync(Guid pregenId)
    {
        var user = await identityService.GetUserAsync()
            ?? throw new UnauthorizedAccessException("Пользователь не авторизован");
        var userEmail = user.Email
            ?? throw new UnauthorizedAccessException("У пользователя нет email");

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var pregen = await dbContext.CharacterStorage
            .Include(c => c.Scenario)
            .FirstOrDefaultAsync(c => c.Id == pregenId);

        if (pregen is null)
            throw new InvalidOperationException("Персонаж не найден");
        if (pregen.CampaignPlayerId.HasValue)
            throw new InvalidOperationException("Этот персонаж уже забронирован");
        if (pregen.ScenarioId is null || pregen.Scenario is null)
            throw new InvalidOperationException("Персонаж не привязан к сценарию");
        if (pregen.Character?.CharacterType != CharacterType.PlayerCharacter)
            throw new InvalidOperationException("Этот персонаж не является играбельным");

        Guid campaignId;
        if (pregen.Scenario.CampaignId.HasValue)
        {
            campaignId = pregen.Scenario.CampaignId.Value;
        }
        else
        {
            // Auto-create a one-shot campaign and link it to the scenario.
            var campaign = new Campaign
            {
                Name = $"{pregen.Scenario.Name} (Ваншот)",
                Status = CampaignStatus.Planning,
                Era = Eras.Classic,
                KeeperEmail = pregen.Scenario.CreatorEmail ?? userEmail,
            };
            campaign.Init();
            dbContext.Campaigns.Add(campaign);
            pregen.Scenario.CampaignId = campaign.Id;
            await dbContext.SaveChangesAsync();
            campaignId = campaign.Id;
            logger.LogInformation(
                "Auto-created one-shot campaign {CampaignId} for scenario {ScenarioId}",
                campaign.Id, pregen.ScenarioId);
        }

        // Find-or-create CampaignPlayer for the current user in the one-shot campaign.
        var player = await dbContext.CampaignPlayers
            .FirstOrDefaultAsync(cp => cp.CampaignId == campaignId && cp.PlayerEmail == userEmail);

        if (player is null)
        {
            player = new CampaignPlayer
            {
                CampaignId = campaignId,
                PlayerEmail = userEmail,
                PlayerName = user.UserName ?? userEmail
            };
            player.Init();
            dbContext.CampaignPlayers.Add(player);
            await dbContext.SaveChangesAsync();
        }
        else
        {
            // Block double-booking in the same scenario.
            var alreadyReserved = await dbContext.CharacterStorage
                .AnyAsync(c => c.CampaignPlayerId == player.Id
                               && c.ScenarioId == pregen.ScenarioId
                               && c.Status == CharacterStatus.Active);
            if (alreadyReserved)
                throw new InvalidOperationException("Вы уже забронировали персонажа на эту игру");
        }

        // Transform the pregen into the player's active character.
        pregen.Status = CharacterStatus.Active;
        pregen.CampaignPlayerId = player.Id;
        pregen.LastUpdated = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        cache.Remove(PublishedScenariosCacheKey);

        logger.LogInformation(
            "Pregen {PregenId} reserved by {UserEmail} for scenario {ScenarioId}",
            pregenId, userEmail, pregen.ScenarioId);

        return pregen;
    }

    /// <summary>
    ///     Releases a reserved pregen so it becomes available again.
    ///     Allowed for: the player who owns the pregen, global Keeper/Admin.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">User is not authenticated or lacks permission.</exception>
    /// <exception cref="InvalidOperationException">Pregen not found or not reserved.</exception>
    public async Task ReleasePregenAsync(Guid pregenId)
    {
        var user = await identityService.GetUserAsync()
            ?? throw new UnauthorizedAccessException("Пользователь не авторизован");
        var userEmail = user.Email
            ?? throw new UnauthorizedAccessException("У пользователя нет email");

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var pregen = await dbContext.CharacterStorage
            .Include(c => c.CampaignPlayer)
            .FirstOrDefaultAsync(c => c.Id == pregenId);

        if (pregen is null)
            throw new InvalidOperationException("Персонаж не найден");
        if (!pregen.CampaignPlayerId.HasValue)
            throw new InvalidOperationException("Персонаж не забронирован");

        var isOwner = string.Equals(pregen.CampaignPlayer?.PlayerEmail, userEmail, StringComparison.OrdinalIgnoreCase);
        var isKeeperOrAdmin = user.Role is PlayerRole.GameMaster or PlayerRole.Administrator;

        if (!isOwner && !isKeeperOrAdmin)
            throw new UnauthorizedAccessException("Недостаточно прав для освобождения персонажа");

        pregen.CampaignPlayerId = null;
        pregen.LastUpdated = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        cache.Remove(PublishedScenariosCacheKey);

        logger.LogInformation("Pregen {PregenId} released by {UserEmail}", pregenId, userEmail);
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
            var userEmail = await identityService.GetCurrentUserEmailAsync();
            if (string.IsNullOrEmpty(userEmail))
                throw new UnauthorizedAccessException("User must be authenticated to unlink a character template from a scenario");

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Get the character template
            var character = await dbContext.CharacterStorage
                .FirstOrDefaultAsync(c => c.Id == characterId);

            if (character == null)
                throw new KeyNotFoundException($"Character template with ID {characterId} not found");

            if (!await CanAccessCharacterAsync(dbContext, character.CampaignPlayerId, CharacterAccess.Write))
            {
                logger.LogWarning("Denied unlink of character {CharacterId} from its scenario", characterId);
                throw new UnauthorizedAccessException("Недостаточно прав для отвязки этого персонажа от сценария");
            }

            // Unlink the character template from the scenario
            character.ScenarioId = null;
            character.Scenario = null;
            character.LastUpdated = DateTime.UtcNow;

            dbContext.Update(character);
            await dbContext.SaveChangesAsync();

            cache.Remove(PublishedScenariosCacheKey);

            logger.LogInformation("Character template {CharacterId} unlinked from scenario by user {UserEmail}", characterId, userEmail);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unlinking character template {CharacterId} from scenario", characterId);
            return false;
        }
    }
}