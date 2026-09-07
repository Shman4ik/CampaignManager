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

    /// <summary>
    ///     Создаёт лист персонажа. Вид (<paramref name="kind" />) и владелец задаются явно —
    ///     ровно один из <paramref name="campaignPlayerId" />, <paramref name="campaignId" />,
    ///     <paramref name="scenarioId" /> или ни одного (НПС в общей библиотеке).
    /// </summary>
    public async Task<CharacterStorageDto> CreateCharacterAsync(
        Character character,
        CharacterKind kind,
        Guid? campaignPlayerId = null,
        Guid? campaignId = null,
        Guid? scenarioId = null,
        CharacterStatus status = CharacterStatus.Active)
    {
        try
        {
            var userEmail = await identityService.GetCurrentUserEmailAsync();
            if (string.IsNullOrEmpty(userEmail))
                throw new UnauthorizedAccessException("User must be authenticated to create a character");
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // A character may only be attached to a player slot the caller actually controls, and only a
            // keeper may add unbound rows (NPC and pregen templates) to the shared library.
            if (!await CanAccessCharacterAsync(dbContext, campaignPlayerId, CharacterAccess.Write))
                throw new UnauthorizedAccessException("Недостаточно прав для создания этого персонажа");

            CharacterStorageDto storageDto = new()
            {
                CharacterName = character.PersonalInfo.Name,
                Character = character,
                Kind = kind,
                CampaignPlayerId = campaignPlayerId,
                CampaignId = campaignId,
                ScenarioId = scenarioId,
                Status = status
            };
            storageDto.Init();

            // Идентификатор строки и идентификатор внутри JSONB всегда совпадают: их
            // расхождение раньше приводило к тому, что правка листа создавала новую строку.
            character.Id = storageDto.Id;
            dbContext.CharacterStorage.Add(storageDto);

            // Находим и деактивируем все активные персонажи этого игрока в этой кампании
            if (campaignPlayerId.HasValue && status == CharacterStatus.Active)
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
            cache.Remove(PublishedScenariosCacheKey);

            logger.LogInformation(
                "Character {CharacterId} ({Kind}) created by user {UserEmail}", storageDto.Id, kind, userEmail);
            return storageDto;
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

            // Если устанавливаем статус Active, деактивируем остальных персонажей этого игрока в этой кампании.
            // Библиотечных НПС и прегенов это не касается — они ничей слот не занимают.
            if (newStatus == CharacterStatus.Active && character.CampaignPlayerId.HasValue)
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
    ///     НПС, доступные Хранителю: общая библиотека плюс НПС конкретной кампании.
    /// </summary>
    /// <param name="campaignId">
    ///     Кампания, чьих НПС нужно добавить к библиотечным. <c>null</c> — без фильтра по владельцу,
    ///     то есть все НПС (так их показывает страница списка НПС).
    /// </param>
    /// <param name="includeArchived">Показать и архивные листы.</param>
    public async Task<List<CharacterStorageDto>> GetNpcsAsync(Guid? campaignId = null, bool includeArchived = false)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var query = dbContext.CharacterStorage
                .Where(c => c.Kind == CharacterKind.Npc);

            if (campaignId.HasValue)
                query = query.Where(c => c.CampaignId == null || c.CampaignId == campaignId.Value);

            if (!includeArchived)
                query = query.Where(c => c.Status != CharacterStatus.Archived);

            return await query
                .OrderBy(c => c.CharacterName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving NPCs for campaign {CampaignId}", campaignId);
            return [];
        }
    }

    /// <summary>
    ///     Преген-заготовки: листы, ещё не отданные ни одному сценарию.
    /// </summary>
    public async Task<List<CharacterStorageDto>> GetPregenTemplatesAsync(bool includeArchived = false)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var query = dbContext.CharacterStorage
                .Where(c => c.Kind == CharacterKind.Pregen && c.ScenarioId == null);

            if (!includeArchived)
                query = query.Where(c => c.Status != CharacterStatus.Archived);

            return await query
                .OrderBy(c => c.CharacterName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving pregen templates");
            return [];
        }
    }

    /// <summary>
    ///     Прегены, принадлежащие сценарию (ростер ваншота).
    /// </summary>
    public async Task<List<CharacterStorageDto>> GetScenarioPregensAsync(Guid scenarioId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.CharacterStorage
                .Where(c => c.ScenarioId == scenarioId
                            && c.Kind == CharacterKind.Pregen
                            && c.Status != CharacterStatus.Archived)
                .OrderBy(c => c.CharacterName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving pregens for scenario {ScenarioId}", scenarioId);
            return [];
        }
    }

    /// <summary>
    ///     Копирует преген-заготовку в сценарий. Копия здесь осмысленна и потому оставлена
    ///     явной: преген расходуется — его бронирует игрок, — поэтому каждому ваншоту нужен
    ///     свой лист. НПС, в отличие от прегена, не копируется никогда
    ///     (см. <c>ScenarioService.AddNpcToScenarioAsync</c>).
    /// </summary>
    public async Task<CharacterStorageDto> CopyPregenToScenarioAsync(Guid pregenId, Guid scenarioId)
    {
        try
        {
            var userEmail = await identityService.GetCurrentUserEmailAsync();
            if (string.IsNullOrEmpty(userEmail))
                throw new UnauthorizedAccessException("User must be authenticated to add a pregen to a scenario");

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var source = await dbContext.CharacterStorage
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == pregenId && c.Kind == CharacterKind.Pregen);

            if (source is null)
                throw new KeyNotFoundException($"Pregen with ID {pregenId} not found");

            if (!await CanAccessCharacterAsync(dbContext, source.CampaignPlayerId, CharacterAccess.Write))
                throw new UnauthorizedAccessException("Недостаточно прав для добавления прегена в сценарий");

            source.Init();
            source.Status = CharacterStatus.Active;
            source.ScenarioId = scenarioId;
            source.Scenario = null;
            source.CampaignPlayerId = null;
            source.CampaignPlayer = null;
            source.CampaignId = null;
            source.Campaign = null;
            source.ScenarioCasts = [];
            // Идентификатор внутри JSONB должен совпадать с идентификатором новой строки,
            // иначе правка копии создаст ещё одну строку вместо обновления.
            source.Character.Id = source.Id;

            dbContext.CharacterStorage.Add(source);
            await dbContext.SaveChangesAsync();

            cache.Remove(PublishedScenariosCacheKey);

            logger.LogInformation(
                "Pregen {PregenId} copied to scenario {ScenarioId} as {CharacterId} by {UserEmail}",
                pregenId, scenarioId, source.Id, userEmail);
            return source;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error copying pregen {PregenId} to scenario {ScenarioId}", pregenId, scenarioId);
            throw;
        }
    }

    /// <summary>
    ///     Переносит НПС между общей библиотекой и кампанией.
    /// </summary>
    /// <param name="characterId">Лист НПС.</param>
    /// <param name="campaignId">Кампания-владелец или <c>null</c>, чтобы вернуть НПС в библиотеку.</param>
    public async Task MoveNpcToCampaignAsync(Guid characterId, Guid? campaignId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var character = await dbContext.CharacterStorage.FindAsync(characterId);
        if (character is null)
            throw new KeyNotFoundException($"Character with ID {characterId} not found");

        if (character.Kind != CharacterKind.Npc)
            throw new InvalidOperationException("Владельца можно менять только у НПС");

        if (!await CanAccessCharacterAsync(dbContext, character.CampaignPlayerId, CharacterAccess.Write))
        {
            logger.LogWarning("Denied owner change on character {CharacterId}", characterId);
            throw new UnauthorizedAccessException("Недостаточно прав для изменения владельца этого НПС");
        }

        character.CampaignId = campaignId;
        character.LastUpdated = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        logger.LogInformation("NPC {CharacterId} moved to campaign {CampaignId}", characterId, campaignId);
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
        if (pregen.Kind != CharacterKind.Pregen)
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
    ///     Убирает преген из сценария. Забронированный игроком преген не трогаем — сначала
    ///     его должен освободить игрок или Хранитель (<see cref="ReleasePregenAsync" />).
    ///     Лист не удаляется, а уходит в архив, поэтому «удалил и потерял навсегда» больше нет.
    /// </summary>
    public async Task<bool> RemovePregenFromScenarioAsync(Guid pregenId)
    {
        try
        {
            var userEmail = await identityService.GetCurrentUserEmailAsync();
            if (string.IsNullOrEmpty(userEmail))
                throw new UnauthorizedAccessException("User must be authenticated to remove a pregen from a scenario");

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var pregen = await dbContext.CharacterStorage
                .FirstOrDefaultAsync(c => c.Id == pregenId);

            if (pregen is null)
                throw new KeyNotFoundException($"Pregen with ID {pregenId} not found");

            if (!await CanAccessCharacterAsync(dbContext, pregen.CampaignPlayerId, CharacterAccess.Write))
            {
                logger.LogWarning("Denied removal of pregen {CharacterId} from its scenario", pregenId);
                throw new UnauthorizedAccessException("Недостаточно прав для изменения этого персонажа");
            }

            if (pregen.CampaignPlayerId.HasValue)
                throw new InvalidOperationException("Преген забронирован игроком — сначала освободите его");

            pregen.ScenarioId = null;
            pregen.Scenario = null;
            pregen.Status = CharacterStatus.Archived;
            pregen.LastUpdated = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            cache.Remove(PublishedScenariosCacheKey);

            logger.LogInformation("Pregen {CharacterId} removed from scenario by user {UserEmail}", pregenId, userEmail);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing pregen {CharacterId} from its scenario", pregenId);
            return false;
        }
    }
}
