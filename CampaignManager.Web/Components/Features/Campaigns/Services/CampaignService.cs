using CampaignManager.Web.Components.Features.Campaigns.Models;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Characters.Services;
using CampaignManager.Web.Components.Shared.Model;
using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities;
using CampaignManager.Web.Utilities.Authorization;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampaignManager.Web.Components.Features.Campaigns.Services;

public sealed class CampaignService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IdentityService identityService,
    CharacterService characterService,
    IHttpContextAccessor httpContextAccessor,
    UserClaimsCache userClaimsCache,
    ILogger<CampaignService> logger)
{
    /// <summary>
    ///     Всё о кампаниях, что показывает главная, — один раз на загрузку страницы.
    ///     <para>
    ///         Раньше три блока главной звали каждый свой метод, и каждый тянул
    ///         <c>Campaigns → Players → Characters</c> с полными листами (два SQL на вызов, шесть на
    ///         проход), плюс по запросу НПС на каждую кампанию Хранителя. Теперь три независимых
    ///         запроса идут параллельно: свои кампании (листы — только те, что показываются), доступные
    ///         для вступления и НПС своих кампаний без JSONB. Почта и роль вошедшего — из claims;
    ///         <c>AspNetUsers</c> читается только ради имён чужих Хранителей, и то через
    ///         <see cref="UserClaimsCache" />.
    ///     </para>
    ///     <para>
    ///         Анониму — пустой снимок без единого запроса: главная открыта без входа, а список
    ///         кампаний раньше показывал ему и названия, и почты Хранителей.
    ///     </para>
    /// </summary>
    public async Task<HomeCampaigns> GetHomeCampaignsAsync()
    {
        var email = await identityService.GetCurrentUserEmailAsync();
        var emailLower = email?.ToLower();
        if (emailLower is null)
            return new HomeCampaigns(null, false, [], []);

        var availableTask = GetAvailableForAsync(emailLower);
        var mineTask = GetMineAsync(emailLower);
        var isKeeper = await identityService.IsKeeper();
        var npcsTask = isKeeper
            ? characterService.GetKeptCampaignNpcsAsync()
            : Task.FromResult<List<CharacterService.CampaignNpc>>([]);

        await Task.WhenAll(availableTask, mineTask, npcsTask);

        var npcsByCampaign = npcsTask.Result.ToLookup(n => n.CampaignId);
        List<HomeCampaign> mine =
        [
            .. mineTask.Result.Select(c => c.KeptByMe
                ? c with { Npcs = [.. npcsByCampaign[c.Id].Select(n => new HomeCharacter(n.Id, n.Name, null, CharacterKind.Npc, n.Status))] }
                : c)
        ];

        return new HomeCampaigns(email, isKeeper, mine, availableTask.Result);
    }

    /// <summary>
    ///     Кампании, где пользователь — игрок. Листы читаются только нужные главной: у своих
    ///     кампаний Хранителя — все (таблица «Персонажи в ваших кампаниях»), у чужих — только свой
    ///     активный. Профессия лежит в JSONB и в SQL не разбирается, поэтому эти листы приходят
    ///     целиком и сворачиваются в строку уже здесь.
    /// </summary>
    private async Task<List<HomeCampaign>> GetMineAsync(string emailLower)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var rows = await dbContext.Campaigns
                .AsNoTracking()
                .Where(c => c.Players.Any(p => p.PlayerEmail.ToLower() == emailLower))
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Status,
                    c.KeeperEmail,
                    KeptByMe = (c.KeeperEmail ?? string.Empty).ToLower() == emailLower,
                    Players = c.Players
                        .OrderBy(p => p.CreatedAt)
                        .Select(p => new
                        {
                            p.PlayerName,
                            IsMe = p.PlayerEmail.ToLower() == emailLower,
                            Characters = p.Characters
                                .Where(ch => (c.KeeperEmail ?? string.Empty).ToLower() == emailLower
                                             || (p.PlayerEmail.ToLower() == emailLower && ch.Status == CharacterStatus.Active))
                                .OrderBy(ch => ch.CreatedAt)
                                .Select(ch => new { ch.Id, ch.CharacterName, ch.Kind, ch.Status, ch.Character })
                                .ToList()
                        })
                        .ToList()
                })
                .ToListAsync();

            var keeperNames = await GetKeeperNamesAsync(rows.Where(c => !c.KeptByMe).Select(c => c.KeeperEmail));

            return
            [
                .. rows.Select(c =>
                {
                    var players = c.Players
                        .Select(p => (p.IsMe, Player: new HomePlayer(p.PlayerName,
                            [.. p.Characters.Select(ch => new HomeCharacter(ch.Id, ch.CharacterName, ch.Character.PersonalInfo.Occupation, ch.Kind, ch.Status))])))
                        .ToList();

                    var myCharacter = players
                        .Where(p => p.IsMe)
                        .SelectMany(p => p.Player.Characters)
                        .FirstOrDefault(ch => ch.Status == CharacterStatus.Active);

                    return new HomeCampaign(
                        c.Id,
                        c.Name,
                        c.Status,
                        c.KeptByMe ? null : KeeperName(keeperNames, c.KeeperEmail),
                        c.KeptByMe,
                        c.Players.Count,
                        myCharacter,
                        c.KeptByMe ? [.. players.Select(p => p.Player)] : [],
                        []);
                })
            ];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading home campaigns for {UserEmail}", emailLower);
            return [];
        }
    }

    /// <summary>Незавершённые кампании, в которых пользователя ещё нет.</summary>
    private async Task<List<HomeAvailableCampaign>> GetAvailableForAsync(string emailLower)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var rows = await dbContext.Campaigns
                .AsNoTracking()
                .Where(c => c.Status != CampaignStatus.Completed
                            && !c.Players.Any(p => p.PlayerEmail.ToLower() == emailLower))
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new { c.Id, c.Name, c.CreatedAt, c.KeeperEmail })
                .ToListAsync();

            var keeperNames = await GetKeeperNamesAsync(rows.Select(c => c.KeeperEmail));
            return [.. rows.Select(c => new HomeAvailableCampaign(c.Id, c.Name, c.CreatedAt, KeeperName(keeperNames, c.KeeperEmail)))];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading campaigns available to join");
            return [];
        }
    }

    /// <summary>
    ///     Отображаемые имена Хранителей (<c>ApplicationUser.UserName</c>) по их почтам — через кэш
    ///     claims: Хранителей на главной единицы, и их строки там почти всегда уже лежат.
    /// </summary>
    private async Task<Dictionary<string, string>> GetKeeperNamesAsync(IEnumerable<string?> keeperEmails)
    {
        Dictionary<string, string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (var email in keeperEmails.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var name = (await userClaimsCache.GetAsync(email))?.DisplayName?.Trim();
            // Имя по умолчанию — почта (так его заводит вход, если провайдер не прислал name).
            // Её не показываем: ради этого имя и подставляется.
            if (!string.IsNullOrEmpty(name) && !name.Contains('@'))
                names[email] = name;
        }

        return names;
    }

    private static string? KeeperName(Dictionary<string, string> names, string? keeperEmail) =>
        keeperEmail is not null && names.TryGetValue(keeperEmail, out var name) ? name : null;

    /// <summary>
    ///     Retrieves a campaign player associated with the current user if they are authenticated.
    /// </summary>
    /// <param name="campaignId">Identifies the specific campaign for which the player information is being retrieved.</param>
    /// <returns>Returns the campaign player details or null if the user is not authenticated.</returns>
    public async Task<CampaignPlayer?> GetCampaignPlayerAsync(Guid campaignId)
    {
        // Почта — из claims: полная строка AspNetUsers ради неё не нужна.
        var userEmail = (await identityService.GetCurrentUserEmailAsync())?.ToLower();
        if (userEmail is null) return null;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.CampaignPlayers
            .Include(p => p.Characters)
            .Where(p => p.PlayerEmail.ToLower() == userEmail && p.CampaignId == campaignId)
            .SingleOrDefaultAsync();
    }

    public async Task<Result<Campaign>> CreateCampaignAsync(string name, CampaignStatus status = CampaignStatus.Planning, Eras era = Eras.Classic)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result<Campaign>.Fail("Название кампании не может быть пустым");

            var userEmail = identityService.GetCurrentUserEmail();
            if (userEmail is null)
                return Result<Campaign>.Fail("Пользователь не авторизован");

            logger.LogInformation("Пользователь {UserEmail} создаёт кампанию '{Name}'", userEmail, name);

            Campaign campaign = new() { Name = name, Status = status, Era = era, KeeperEmail = userEmail };
            campaign.Init();

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            dbContext.Campaigns.Add(campaign);
            await dbContext.SaveChangesAsync();

            logger.LogInformation("Кампания '{Name}' успешно создана пользователем {UserEmail}", name, userEmail);
            return Result<Campaign>.Ok(campaign);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при создании кампании '{Name}'", name);
            return Result<Campaign>.Fail("Не удалось создать кампанию");
        }
    }

    /// <summary>
    ///     Method to get available companies (campaigns)
    /// </summary>
    public async Task<List<Campaign>> GetAvailableCompaniesAsync()
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Campaigns
            .AsNoTracking()
            .Where(p => p.Status != CampaignStatus.Completed)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Campaign>> GetAllCampaignsAsync()
    {
        var userEmail = identityService.GetCurrentUserEmail();
        if (string.IsNullOrEmpty(userEmail))
            return [];

        var userEmailLower = userEmail.ToLower();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Campaigns
            .Include(c => c.Players)
            .ThenInclude(p => p.Characters)
            .AsSplitQuery()
            .Where(c => (c.KeeperEmail ?? string.Empty).ToLower() == userEmailLower || c.Players.Any(p => p.PlayerEmail.ToLower() == userEmailLower))
            .ToListAsync();
    }

    /// <summary>
    ///     Method for a user to apply to a campaign
    /// </summary>
    public async Task<bool> JoinCampaignAsync(Guid campaignId, string userName)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var campaign = await dbContext.Campaigns
                .Include(c => c.Players)
                .FirstOrDefaultAsync(c => c.Id == campaignId);

            if (campaign == null)
            {
                logger.LogWarning("Campaign with ID {CampaignId} not found.", campaignId);
                return false;
            }

            var user = await identityService.GetUserAsync();

            if (user == null)
            {
                var externalPrincipal = httpContextAccessor.HttpContext?.User;
                var email = externalPrincipal?.FindFirst(ClaimTypes.Email)?.Value;
                var name = externalPrincipal?.FindFirst(ClaimTypes.Name)?.Value;
                user = new ApplicationUser { Email = email ?? string.Empty, UserName = name ?? string.Empty, Role = PlayerRole.Player };
                user = await identityService.CreateUserAsync(user);
            }

            CampaignPlayer campaignPlayers = new() { CampaignId = campaign.Id, PlayerEmail = user?.Email ?? string.Empty, PlayerName = userName };
            campaignPlayers.Init();

            if (user?.Email != null && !campaign.Players.Any(p => p.PlayerEmail == user.Email))
            {
                dbContext.CampaignPlayers.Add(campaignPlayers);
                await dbContext.SaveChangesAsync();
                logger.LogInformation("User {UserId} successfully applied to campaign {CampaignId}.", user.Email, campaignId);
                return true;
            }

            logger.LogWarning("User {UserId} has already applied to campaign {CampaignId}.", user?.Email ?? "unknown", campaignId);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying to the campaign.");
            return false;
        }
    }

    /// <summary>
    ///     Gets a campaign by ID with all players and their characters (for admins/keepers)
    /// </summary>
    /// <param name="campaignId">Campaign ID</param>
    /// <returns>Campaign with full player and character data, or null if not found</returns>
    public async Task<Campaign?> GetCampaignWithCharactersAsync(Guid campaignId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var campaign = await dbContext.Campaigns
                .Include(c => c.Players)
                .ThenInclude(p => p.Characters)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.Id == campaignId);

            return campaign;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving campaign {CampaignId} with characters", campaignId);
            return null;
        }
    }

    /// <summary>
    ///     Returns all campaigns where the current user is the Keeper, including player counts.
    /// </summary>
    public async Task<List<Campaign>> GetKeeperCampaignsAsync()
    {
        // Асинхронный вариант: ширма Хранителя зовёт этот метод уже из живого circuit, без
        // пререндера, а синхронный читает HttpContext и там может вернуть null.
        var userEmail = await identityService.GetCurrentUserEmailAsync();
        if (string.IsNullOrEmpty(userEmail)) return [];

        var userEmailLower = userEmail.ToLower();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Campaigns
            .Include(c => c.Players)
            .Where(c => (c.KeeperEmail ?? string.Empty).ToLower() == userEmailLower)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    ///     Updates the name and status of a campaign owned by the current keeper.
    /// </summary>
    public async Task<Result> UpdateCampaignAsync(Guid id, string name, CampaignStatus status, Eras? era = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result.Fail("Название кампании не может быть пустым");

            var userEmail = identityService.GetCurrentUserEmail();
            if (string.IsNullOrEmpty(userEmail))
                return Result.Fail("Пользователь не авторизован");

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var campaign = await dbContext.Campaigns.FirstOrDefaultAsync(c => c.Id == id);

            if (campaign is null)
            {
                logger.LogWarning("Campaign {CampaignId} not found for update", id);
                return Result.Fail("Кампания не найдена");
            }

            if (!string.Equals(campaign.KeeperEmail, userEmail, StringComparison.OrdinalIgnoreCase))
                return Result.Fail("Недостаточно прав для изменения кампании");

            campaign.Name = name;
            campaign.Status = status;
            if (era.HasValue)
                campaign.Era = era.Value;
            campaign.LastUpdated = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();
            logger.LogInformation("Campaign {CampaignId} updated by {UserEmail}", id, userEmail);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating campaign {CampaignId}", id);
            return Result.Fail("Не удалось обновить кампанию");
        }
    }

    /// <summary>
    ///     Creates a dedicated campaign for a one-shot scenario announcement. Used when a keeper publishes
    ///     a scenario that has no campaign yet — gives reservations a place to attach players.
    /// </summary>
    public async Task<Campaign> CreateCampaignForOneShotAsync(string scenarioName)
    {
        var userEmail = identityService.GetCurrentUserEmail()
            ?? throw new UnauthorizedAccessException("Пользователь не авторизован");

        var campaign = new Campaign
        {
            Name = $"{scenarioName} (Ваншот)",
            Status = CampaignStatus.Planning,
            Era = Eras.Classic,
            KeeperEmail = userEmail,
        };
        campaign.Init();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("One-shot campaign {CampaignId} ('{Name}') created by {UserEmail}",
            campaign.Id, campaign.Name, userEmail);
        return campaign;
    }

    /// <summary>
    ///     Deletes a campaign and all associated players/characters (cascade).
    /// </summary>
    public async Task<Result> DeleteCampaignAsync(Guid id)
    {
        try
        {
            var userEmail = identityService.GetCurrentUserEmail();
            if (string.IsNullOrEmpty(userEmail))
                return Result.Fail("Пользователь не авторизован");

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var campaign = await dbContext.Campaigns.FirstOrDefaultAsync(c => c.Id == id);

            if (campaign is null)
            {
                logger.LogWarning("Campaign {CampaignId} not found for deletion", id);
                return Result.Fail("Кампания не найдена");
            }

            if (!string.Equals(campaign.KeeperEmail, userEmail, StringComparison.OrdinalIgnoreCase))
                return Result.Fail("Недостаточно прав для удаления кампании");

            dbContext.Campaigns.Remove(campaign);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Campaign {CampaignId} deleted by {UserEmail}", id, userEmail);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting campaign {CampaignId}", id);
            return Result.Fail("Не удалось удалить кампанию");
        }
    }
}