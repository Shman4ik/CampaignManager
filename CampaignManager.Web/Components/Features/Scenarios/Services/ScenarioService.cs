using CampaignManager.Web.Components.Features.Campaigns.Models;
using CampaignManager.Web.Components.Features.Campaigns.Services;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Scenarios.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Components.Features.Scenarios.Services;

/// <summary>
///     Service for managing scenarios, including templates and campaign-specific scenarios
/// </summary>
public sealed class ScenarioService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    CampaignService campaignService,
    IdentityService identityService,
    ILogger<ScenarioService> logger)
{
    private const string ScenariosCacheKey = "AllScenarios";
    private const string TemplatesCacheKey = "ScenarioTemplates";
    private const string PublishedCacheKey = "PublishedScenarios";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);

    // ── Права ──────────────────────────────────────────────────────
    //
    // Единственное место, где живут правила доступа к сценариям (см. Scenarios/CLAUDE.md, «Права»).
    // Сценарии — общая библиотека Хранителей:
    //  • шаблон (CampaignId == null) правит любой Хранитель; удаляет автор (CreatorEmail, без учёта
    //    регистра) или администратор; старые шаблоны без автора удаляет только администратор;
    //  • сценарий кампании правит и удаляет Хранитель этой кампании (Campaign.KeeperEmail) или
    //    администратор.
    // Игрок не правит ничего. Каждый метод записи сверяется со строкой из базы, а не с объектом,
    // который прислала страница: CampaignId и CreatorEmail в нём можно подменить.

    /// <summary>Кто пишет: почта и роль текущего пользователя.</summary>
    private sealed record Caller(string Email, PlayerRole Role)
    {
        public bool IsAdministrator => Role is PlayerRole.Administrator;
        public bool IsKeeper => Role is PlayerRole.GameMaster or PlayerRole.Administrator;

        public bool Is(string? email) =>
            !string.IsNullOrWhiteSpace(email) && string.Equals(email.Trim(), Email, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Асинхронные методы <see cref="IdentityService" />, а не синхронный
    ///     <c>GetCurrentUserEmail</c>: тот в интерактивном рендере отдаёт <c>null</c>.
    /// </summary>
    private async Task<Caller?> GetCallerAsync()
    {
        var email = await identityService.GetCurrentUserEmailAsync();
        if (string.IsNullOrWhiteSpace(email)) return null;

        return new Caller(email.Trim(), await identityService.GetCurrentUserRole());
    }

    /// <summary>Сами правила. Всё остальное в сервисе только собирает для них данные.</summary>
    private static ScenarioAccess Evaluate(Caller? caller, Guid? campaignId, string? creatorEmail, string? campaignKeeperEmail)
    {
        if (caller is null || !caller.IsKeeper) return ScenarioAccess.None;
        if (caller.IsAdministrator) return new ScenarioAccess(CanEdit: true, CanDelete: true);

        if (campaignId is null)
            return new ScenarioAccess(CanEdit: true, CanDelete: caller.Is(creatorEmail));

        var keepsCampaign = caller.Is(campaignKeeperEmail);
        return new ScenarioAccess(CanEdit: keepsCampaign, CanDelete: keepsCampaign);
    }

    private async Task<string?> GetCampaignKeeperEmailAsync(Guid? campaignId, AppDbContext? dbContext = null)
    {
        if (campaignId is not { } id) return null;

        if (dbContext is not null) return await QueryAsync(dbContext);

        await using var ownContext = await dbContextFactory.CreateDbContextAsync();
        return await QueryAsync(ownContext);

        Task<string?> QueryAsync(AppDbContext db) =>
            db.Campaigns.Where(c => c.Id == id).Select(c => c.KeeperEmail).FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Права текущего пользователя на сценарий. Страницам — чтобы не показывать кнопки,
    ///     которые сервис всё равно отклонит.
    /// </summary>
    public async Task<ScenarioAccess> GetAccessAsync(Scenario scenario)
    {
        var caller = await GetCallerAsync();
        if (caller is null || !caller.IsKeeper) return ScenarioAccess.None;

        var keeperEmail = await GetCampaignKeeperEmailAsync(scenario.CampaignId);
        return Evaluate(caller, scenario.CampaignId, scenario.CreatorEmail, keeperEmail);
    }

    /// <summary>
    ///     То же для списка: Хранители кампаний читаются одним запросом, а не по запросу на карточку.
    /// </summary>
    public async Task<Dictionary<Guid, ScenarioAccess>> GetAccessAsync(IReadOnlyCollection<Scenario> scenarios)
    {
        var caller = await GetCallerAsync();
        if (caller is null || !caller.IsKeeper)
            return scenarios.ToDictionary(s => s.Id, _ => ScenarioAccess.None);

        List<Guid> campaignIds = [.. scenarios.Where(s => s.CampaignId is not null).Select(s => s.CampaignId!.Value).Distinct()];
        Dictionary<Guid, string?> keepers = [];
        if (campaignIds.Count > 0)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            keepers = await dbContext.Campaigns
                .Where(c => campaignIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.KeeperEmail);
        }

        return scenarios.ToDictionary(
            s => s.Id,
            s => Evaluate(caller, s.CampaignId, s.CreatorEmail,
                s.CampaignId is { } id ? keepers.GetValueOrDefault(id) : null));
    }

    /// <summary>
    ///     Кампании, куда текущий пользователь может положить сценарий: свои, а администратору — любые.
    ///     Для выбора в «Добавить в кампанию» — чтобы не предлагать то, что сервис отклонит.
    /// </summary>
    public async Task<List<Campaign>> GetWritableCampaignsAsync(IEnumerable<Campaign> campaigns)
    {
        var caller = await GetCallerAsync();
        return [.. campaigns.Where(c => Evaluate(caller, c.Id, creatorEmail: null, c.KeeperEmail).CanEdit)];
    }

    /// <summary>
    ///     Проверка перед записью в уже существующий сценарий. <paramref name="stored" /> — строка
    ///     из базы, а не то, что прислала страница. Отказ — предупреждение в лог и <c>false</c>:
    ///     методы записи и так сообщают о неудаче результатом, а кнопки сюда страницы уже прячут.
    /// </summary>
    private async Task<bool> MayWriteAsync(AppDbContext dbContext, Scenario stored, bool delete = false)
    {
        var caller = await GetCallerAsync();
        var keeperEmail = await GetCampaignKeeperEmailAsync(stored.CampaignId, dbContext);
        var access = Evaluate(caller, stored.CampaignId, stored.CreatorEmail, keeperEmail);

        var allowed = delete ? access.CanDelete : access.CanEdit;
        if (!allowed)
            logger.LogWarning("Denied {Operation} of scenario {ScenarioId} for {Email}",
                delete ? "delete" : "edit", stored.Id, caller?.Email ?? "<anonymous>");

        return allowed;
    }

    /// <summary>
    ///     Проверка перед созданием сценария: заводит его только Хранитель, а в кампанию — только
    ///     её Хранитель (или администратор). Возвращает вызывающего, если можно, иначе <c>null</c>.
    /// </summary>
    private async Task<Caller?> MayCreateAsync(AppDbContext dbContext, Guid? campaignId)
    {
        var caller = await GetCallerAsync();
        var keeperEmail = await GetCampaignKeeperEmailAsync(campaignId, dbContext);
        if (Evaluate(caller, campaignId, creatorEmail: null, keeperEmail).CanEdit)
            return caller;

        logger.LogWarning("Denied scenario creation in campaign {CampaignId} for {Email}",
            campaignId, caller?.Email ?? "<anonymous>");
        return null;
    }

    /// <summary>
    ///     Gets all scenarios, optionally filtered by template status
    /// </summary>
    /// <param name="templatesOnly">If true, returns only template scenarios</param>
    /// <returns>A list of scenarios</returns>
    public async Task<List<Scenario>> GetAllScenariosAsync(bool templatesOnly = false)
    {
        try
        {
            var cacheKey = templatesOnly ? TemplatesCacheKey : ScenariosCacheKey;

            if (cache.TryGetValue(cacheKey, out List<Scenario>? scenarios) && scenarios is not null) return scenarios;

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var query = dbContext.Scenarios.AsQueryable();

            if (templatesOnly) query = query.Where(s => s.IsTemplate);

            scenarios = await query.OrderBy(s => s.Name).ToListAsync();

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CacheExpiration);

            cache.Set(cacheKey, scenarios, cacheOptions);

            return scenarios;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving scenarios");
            return [];
        }
    }

    /// <summary>
    ///     Gets published one-shot scenarios for the home page announcement
    /// </summary>
    public async Task<List<Scenario>> GetPublishedScenariosAsync()
    {
        try
        {
            if (cache.TryGetValue(PublishedCacheKey, out List<Scenario>? scenarios) && scenarios is not null)
                return scenarios;

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            scenarios = await dbContext.Scenarios
                .Include(s => s.Pregens)
                .ThenInclude(n => n.CampaignPlayer)
                .Where(s => s.IsPublished)
                .OrderBy(s => s.ScheduledDate)
                .ToListAsync();

            cache.Set(PublishedCacheKey, scenarios, new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CacheExpiration));

            return scenarios;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving published scenarios");
            return [];
        }
    }

    /// <summary>
    ///     Gets scenarios for a specific campaign
    /// </summary>
    /// <param name="campaignId">The ID of the campaign</param>
    /// <returns>A list of scenarios for the campaign</returns>
    public async Task<List<Scenario>> GetCampaignScenariosAsync(Guid campaignId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Scenarios
                .Where(s => s.CampaignId == campaignId)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving scenarios for campaign {CampaignId}", campaignId);
            return [];
        }
    }

    /// <summary>
    ///     Gets a scenario by its ID
    /// </summary>
    public async Task<Scenario?> GetScenarioByIdAsync(Guid id)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Scenarios
                .Include(s => s.Cast)
                .ThenInclude(sn => sn.Character)
                .Include(s => s.Pregens)
                .FirstOrDefaultAsync(s => s.Id == id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving scenario with ID {ScenarioId}", id);
            return null;
        }
    }

    /// <summary>
    ///     Creates a new scenario
    /// </summary>
    /// <param name="scenario">The scenario to create</param>
    /// <returns>The created scenario with its assigned ID</returns>
    public async Task<Scenario?> CreateScenarioAsync(Scenario scenario)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var caller = await MayCreateAsync(dbContext, scenario.CampaignId);
            if (caller is null) return null;

            // Автор — тот, кто создаёт, а не то, что прислала страница: от него зависит, кто сможет
            // удалить шаблон. Страницы брали почту синхронным GetCurrentUserEmail, который в
            // интерактивном рендере отдаёт null, а импорт не заполнял её вовсе.
            scenario.CreatorEmail = caller.Email;
            scenario.Init();
            await dbContext.Scenarios.AddAsync(scenario);
            await dbContext.SaveChangesAsync();

            // Invalidate cache
            cache.Remove(ScenariosCacheKey);
            cache.Remove(PublishedCacheKey);
            if (scenario.IsTemplate)
                cache.Remove(TemplatesCacheKey);
            return scenario;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating scenario {ScenarioName}", scenario.Name);
            return null;
        }
    }

    /// <summary>
    ///     Updates an existing scenario
    /// </summary>
    /// <param name="scenario">The scenario with updated values</param>
    /// <returns>True if the update was successful, false otherwise</returns>
    public async Task<bool> UpdateScenarioAsync(Scenario scenario)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var existingScenario = await dbContext.Scenarios.FindAsync(scenario.Id);
            if (existingScenario is null) return false;
            if (!await MayWriteAsync(dbContext, existingScenario)) return false;

            // Автора и привязку к кампании правка не меняет: от них зависят права, и подменённый
            // CampaignId иначе увёл бы общий шаблон в свою кампанию, а CreatorEmail — дал бы право
            // его удалить. Кампанию сценарию назначает только публикация ваншота ниже.
            scenario.CreatorEmail = existingScenario.CreatorEmail;
            scenario.CampaignId = existingScenario.CampaignId;

            // Auto-create a campaign when a scenario is first published as a one-shot without one.
            // Reservations need a campaign to attach CampaignPlayer records to.
            var publishingWithoutCampaign =
                scenario.IsPublished
                && !existingScenario.IsPublished
                && existingScenario.CampaignId is null
                && scenario.CampaignId is null;

            if (publishingWithoutCampaign)
            {
                var campaign = await campaignService.CreateCampaignForOneShotAsync(scenario.Name);
                scenario.CampaignId = campaign.Id;
                logger.LogInformation(
                    "Scenario {ScenarioId} auto-linked to new one-shot campaign {CampaignId}",
                    scenario.Id, campaign.Id);
            }

            // Update the existing scenario with the new values
            dbContext.Entry(existingScenario).CurrentValues.SetValues(scenario);
            await dbContext.SaveChangesAsync();

            // Invalidate cache
            cache.Remove(ScenariosCacheKey);
            cache.Remove(TemplatesCacheKey);
            cache.Remove(PublishedCacheKey);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating scenario {ScenarioId}", scenario.Id);
            return false;
        }
    }

    /// <summary>
    ///     Drops the cached list of published one-shot scenarios so the next home-page load reflects
    ///     fresh reservation state.
    /// </summary>
    public void InvalidatePublishedCache()
    {
        cache.Remove(PublishedCacheKey);
    }

    /// <summary>
    ///     Deletes a scenario by its ID
    /// </summary>
    /// <param name="id">The ID of the scenario to delete</param>
    /// <returns>True if the deletion was successful, false otherwise</returns>
    public async Task<bool> DeleteScenarioAsync(Guid id)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var scenario = await dbContext.Scenarios.FindAsync(id);
            if (scenario is null) return false;
            if (!await MayWriteAsync(dbContext, scenario, delete: true)) return false;

            dbContext.Scenarios.Remove(scenario);
            await dbContext.SaveChangesAsync();

            // Invalidate cache
            cache.Remove(ScenariosCacheKey);
            cache.Remove(PublishedCacheKey);
            if (scenario.IsTemplate) cache.Remove(TemplatesCacheKey);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting scenario {ScenarioId}", id);
            return false;
        }
    }

    /// <summary>
    ///     Creates a new scenario in a campaign based on a template
    /// </summary>
    /// <param name="templateId">The ID of the template scenario</param>
    /// <param name="campaignId">The ID of the campaign</param>
    /// <returns>The newly created campaign scenario</returns>
    public async Task<Scenario?> CreateScenarioFromTemplateAsync(Guid templateId, Guid campaignId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Сценарий ложится в кампанию — значит, класть его может только её Хранитель.
            var caller = await MayCreateAsync(dbContext, campaignId);
            if (caller is null) return null;

            // Get the template scenario with all related entities
            var template = await dbContext.Scenarios
                .Include(s => s.Cast)
                .FirstOrDefaultAsync(s => s.Id == templateId && s.IsTemplate);

            if (template is null) return null;

            // Create a new scenario based on the template
            Scenario newScenario = new()
            {
                Name = template.Name,
                Description = template.Description,
                Location = template.Location,
                Era = template.Era,
                Journal = template.Journal,
                IsTemplate = false,
                CreatorEmail = caller.Email,
                CampaignId = campaignId
            };

            // Add the new scenario to the database
            await dbContext.Scenarios.AddAsync(newScenario);
            await dbContext.SaveChangesAsync();

            // Build old→new ID maps for cross-reference remapping
            var creatureIdMap = new Dictionary<Guid, Guid>();
            var itemIdMap = new Dictionary<Guid, Guid>();
            var handoutIdMap = new Dictionary<Guid, Guid>();

            // Copy creatures
            if (template.ScenarioCreatures is not null)
            {
                newScenario.ScenarioCreatures = template.ScenarioCreatures
                    .Select(sc =>
                    {
                        var newId = Guid.CreateVersion7();
                        creatureIdMap[sc.Id] = newId;
                        return new ScenarioCreature
                        {
                            Id = newId,
                            ScenarioId = newScenario.Id,
                            Name = sc.Name,
                            Type = sc.Type,
                            CreatureCharacteristics = sc.CreatureCharacteristics,
                            Attacks = sc.Attacks,
                            CombatDescriptions = sc.CombatDescriptions,
                            SpecialAbilities = sc.SpecialAbilities,
                            Notes = sc.Notes
                        };
                    })
                    .ToList();
            }

            // Copy items
            if (template.ScenarioItems is not null)
            {
                newScenario.ScenarioItems = template.ScenarioItems
                    .Select(si =>
                    {
                        var newId = Guid.CreateVersion7();
                        itemIdMap[si.Id] = newId;
                        return new ScenarioItem
                        {
                            Id = newId,
                            ScenarioId = newScenario.Id,
                            Name = si.Name,
                            Era = si.Era,
                            Type = si.Type,
                            Description = si.Description,
                            ImageUrl = si.ImageUrl,
                            Notes = si.Notes
                        };
                    })
                    .ToList();
            }

            // Copy handouts
            if (template.Handouts is not null)
            {
                newScenario.Handouts = template.Handouts
                    .Select(h =>
                    {
                        var newId = Guid.CreateVersion7();
                        handoutIdMap[h.Id] = newId;
                        return new ScenarioHandout
                        {
                            Id = newId,
                            Name = h.Name,
                            Description = h.Description,
                            FileUrl = h.FileUrl,
                            Order = h.Order
                        };
                    })
                    .ToList();
            }

            // Copy key facts
            if (template.KeyFacts is not null)
            {
                newScenario.KeyFacts = template.KeyFacts
                    .Select(f => new ScenarioKeyFact
                    {
                        Id = Guid.CreateVersion7(),
                        Title = f.Title,
                        Content = f.Content,
                        Type = f.Type,
                        Order = f.Order
                    })
                    .ToList();
            }

            // Copy locations with remapped references
            if (template.Locations is not null)
            {
                var locationIdMap = new Dictionary<Guid, Guid>();
                // First pass: generate new IDs
                foreach (var loc in template.Locations)
                    locationIdMap[loc.Id] = Guid.CreateVersion7();

                newScenario.Locations = template.Locations
                    .Select(loc => new ScenarioLocation
                    {
                        Id = locationIdMap[loc.Id],
                        Name = loc.Name,
                        Address = loc.Address,
                        Description = loc.Description,
                        Order = loc.Order,
                        ParentLocationId = loc.ParentLocationId.HasValue && locationIdMap.TryGetValue(loc.ParentLocationId.Value, out var newParent)
                            ? newParent
                            : null,
                        SkillChecks = loc.SkillChecks.Select(sc => new ScenarioSkillCheck
                        {
                            Id = Guid.CreateVersion7(),
                            SkillName = sc.SkillName,
                            Difficulty = sc.Difficulty,
                            SuccessResult = sc.SuccessResult,
                            FailureResult = sc.FailureResult
                        }).ToList(),
                        CreatureIds = loc.CreatureIds.Select(id => creatureIdMap.GetValueOrDefault(id, id)).ToList(),
                        ItemIds = loc.ItemIds.Select(id => itemIdMap.GetValueOrDefault(id, id)).ToList(),
                        NpcIds = [..loc.NpcIds],
                        HandoutIds = loc.HandoutIds.Select(id => handoutIdMap.GetValueOrDefault(id, id)).ToList()
                    })
                    .ToList();
            }

            // Состав НПС переносится связями: листы остаются общими, поэтому ссылки
            // NpcIds в локациях остаются валидными и после копирования сценария.
            foreach (var cast in template.Cast)
            {
                ScenarioNpc copy = new()
                {
                    ScenarioId = newScenario.Id,
                    CharacterId = cast.CharacterId,
                    Role = cast.Role,
                    Count = cast.Count,
                    Notes = cast.Notes
                };
                copy.Init();
                dbContext.Add(copy);
            }

            await dbContext.SaveChangesAsync();

            return newScenario;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating scenario from template {TemplateId} for campaign {CampaignId}",
                templateId, campaignId);
            return null;
        }
    }

    /// <summary>
    ///     Состав НПС сценария вместе с листами персонажей.
    /// </summary>
    public async Task<List<ScenarioNpc>> GetScenarioCastAsync(Guid scenarioId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.ScenarioNpcs
                .Include(sn => sn.Character)
                .Where(sn => sn.ScenarioId == scenarioId)
                .OrderBy(sn => sn.Character!.CharacterName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving cast for scenario {ScenarioId}", scenarioId);
            return [];
        }
    }

    /// <summary>
    ///     Занимает НПС в сценарии. Лист персонажа не копируется: правки НПС видны во всех
    ///     сценариях, где он занят. Повторный вызов обновляет роль, количество и заметку.
    ///     <para>
    ///         <paramref name="notes" /> оставлен необязательным: ручной ввод через
    ///         <c>AddNpcModal</c> заметку не спрашивает, а импорт сценария — переносит.
    ///     </para>
    /// </summary>
    public async Task<bool> AddNpcToScenarioAsync(Guid scenarioId, Guid characterId, NpcRole role = NpcRole.Neutral, int count = 1, string? notes = null)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var scenario = await dbContext.Scenarios.FindAsync(scenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            var existing = await dbContext.ScenarioNpcs
                .FirstOrDefaultAsync(sn => sn.ScenarioId == scenarioId && sn.CharacterId == characterId);

            if (existing is not null)
            {
                existing.Role = role;
                existing.Count = Math.Max(1, count);
                if (notes is not null) existing.Notes = notes;
                existing.LastUpdated = DateTime.UtcNow;
            }
            else
            {
                ScenarioNpc cast = new()
                {
                    ScenarioId = scenarioId,
                    CharacterId = characterId,
                    Role = role,
                    Count = Math.Max(1, count),
                    Notes = notes
                };
                cast.Init();
                dbContext.ScenarioNpcs.Add(cast);
            }

            await dbContext.SaveChangesAsync();
            cache.Remove(PublishedCacheKey);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding NPC {CharacterId} to scenario {ScenarioId}", characterId, scenarioId);
            return false;
        }
    }

    /// <summary>
    ///     Меняет роль и количество НПС в сценарии.
    /// </summary>
    public async Task<bool> UpdateScenarioNpcAsync(Guid scenarioId, Guid characterId, NpcRole role, int count)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var scenario = await dbContext.Scenarios.FindAsync(scenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            var cast = await dbContext.ScenarioNpcs
                .FirstOrDefaultAsync(sn => sn.ScenarioId == scenarioId && sn.CharacterId == characterId);

            if (cast is null) return false;

            cast.Role = role;
            cast.Count = Math.Max(1, count);
            cast.LastUpdated = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating NPC {CharacterId} in scenario {ScenarioId}", characterId, scenarioId);
            return false;
        }
    }

    /// <summary>
    ///     Убирает НПС из сценария. Удаляется только связь — сам лист остаётся в библиотеке
    ///     или в кампании.
    /// </summary>
    public async Task<bool> RemoveNpcFromScenarioAsync(Guid scenarioId, Guid characterId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var scenario = await dbContext.Scenarios.FindAsync(scenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            var cast = await dbContext.ScenarioNpcs
                .FirstOrDefaultAsync(sn => sn.ScenarioId == scenarioId && sn.CharacterId == characterId);

            if (cast is null) return false;

            dbContext.ScenarioNpcs.Remove(cast);
            await dbContext.SaveChangesAsync();
            cache.Remove(PublishedCacheKey);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing NPC {CharacterId} from scenario {ScenarioId}", characterId, scenarioId);
            return false;
        }
    }

    /// <summary>
    ///     Названия сценариев, в которых занят каждый НПС: идентификатор листа → названия.
    ///     Нужен спискам НПС, чтобы Хранитель видел, где персонаж уже задействован.
    /// </summary>
    public async Task<Dictionary<Guid, List<string>>> GetNpcScenarioNamesAsync()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var rows = await dbContext.ScenarioNpcs
                .Select(sn => new { sn.CharacterId, ScenarioName = sn.Scenario!.Name })
                .ToListAsync();

            return rows
                .GroupBy(r => r.CharacterId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.ScenarioName).OrderBy(n => n).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving NPC usage across scenarios");
            return [];
        }
    }

    /// <summary>
    ///     Gets all available creatures for adding to scenarios
    /// </summary>
    /// <returns>A list of all creatures</returns>
    public async Task<List<CampaignManager.Web.Components.Features.Bestiary.Model.Creature>> GetAllCreaturesAsync()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Creatures
                .OrderBy(c => c.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving all creatures");
            return [];
        }
    }

    /// <summary>
    ///     Adds a creature to a scenario
    /// </summary>
    /// <param name="scenarioCreature">The scenario-creature relationship to add</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> AddCreatureToScenarioAsync(ScenarioCreature scenarioCreature)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Verify the scenario exists
            var scenario = await dbContext.Scenarios.FindAsync(scenarioCreature.ScenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            // Initialize the entity
            scenarioCreature.Init();

            // JSON-backed collections need explicit reassignment for reliable change tracking.
            var creatures = scenario.ScenarioCreatures?.ToList() ?? [];
            creatures.Add(scenarioCreature);
            scenario.ScenarioCreatures = creatures;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding creature to scenario {ScenarioId}", scenarioCreature.ScenarioId);
            return false;
        }
    }

    /// <summary>
    ///     Gets all available items for adding to scenarios
    /// </summary>
    /// <returns>A list of all items</returns>
    public async Task<List<CampaignManager.Web.Components.Features.Items.Model.Item>> GetAllItemsAsync()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Items
                .OrderBy(i => i.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving all items");
            return [];
        }
    }

    /// <summary>
    ///     Adds an item to a scenario
    /// </summary>
    /// <param name="scenarioItem">The scenario-item relationship to add</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> AddItemToScenarioAsync(ScenarioItem scenarioItem)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Verify the scenario exists
            var scenario = await dbContext.Scenarios.FindAsync(scenarioItem.ScenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            // Initialize the entity
            scenarioItem.Init();

            // JSON-backed collections need explicit reassignment for reliable change tracking.
            var items = scenario.ScenarioItems?.ToList() ?? [];
            items.Add(scenarioItem);
            scenario.ScenarioItems = items;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding item to scenario {ScenarioId}", scenarioItem.ScenarioId);
            return false;
        }
    }

    /// <summary>
    ///     Removes a creature from a scenario
    /// </summary>
    public async Task<bool> RemoveCreatureFromScenarioAsync(Guid scenarioId, Guid creatureId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var scenario = await dbContext.Scenarios.FindAsync(scenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            var creatures = scenario.ScenarioCreatures?.ToList() ?? [];
            var toRemove = creatures.FirstOrDefault(c => c.Id == creatureId);
            if (toRemove is null) return false;

            creatures.Remove(toRemove);
            scenario.ScenarioCreatures = creatures;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing creature {CreatureId} from scenario {ScenarioId}", creatureId, scenarioId);
            return false;
        }
    }

    /// <summary>
    ///     Updates an existing creature entry inside a scenario (replaces by Id)
    /// </summary>
    public async Task<bool> UpdateCreatureInScenarioAsync(ScenarioCreature scenarioCreature)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var scenario = await dbContext.Scenarios.FindAsync(scenarioCreature.ScenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            var creatures = scenario.ScenarioCreatures?.ToList() ?? [];
            var index = creatures.FindIndex(c => c.Id == scenarioCreature.Id);
            if (index < 0) return false;

            creatures[index] = scenarioCreature;
            scenario.ScenarioCreatures = creatures;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating creature {CreatureId} in scenario {ScenarioId}", scenarioCreature.Id, scenarioCreature.ScenarioId);
            return false;
        }
    }

    /// <summary>
    ///     Removes an item from a scenario
    /// </summary>
    public async Task<bool> RemoveItemFromScenarioAsync(Guid scenarioId, Guid itemId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var scenario = await dbContext.Scenarios.FindAsync(scenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            var items = scenario.ScenarioItems?.ToList() ?? [];
            var toRemove = items.FirstOrDefault(i => i.Id == itemId);
            if (toRemove is null) return false;

            items.Remove(toRemove);
            scenario.ScenarioItems = items;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing item {ItemId} from scenario {ScenarioId}", itemId, scenarioId);
            return false;
        }
    }

    /// <summary>
    ///     Updates an existing item entry inside a scenario (replaces by Id)
    /// </summary>
    public async Task<bool> UpdateItemInScenarioAsync(ScenarioItem scenarioItem)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var scenario = await dbContext.Scenarios.FindAsync(scenarioItem.ScenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            var items = scenario.ScenarioItems?.ToList() ?? [];
            var index = items.FindIndex(i => i.Id == scenarioItem.Id);
            if (index < 0) return false;

            items[index] = scenarioItem;
            scenario.ScenarioItems = items;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating item {ItemId} in scenario {ScenarioId}", scenarioItem.Id, scenarioItem.ScenarioId);
            return false;
        }
    }

    /// <summary>
    ///     Сбрасывает все три списочных кэша сценариев. Локации, ключевые факты и раздатки —
    ///     jsonb-колонки самой строки сценария, поэтому их правка меняет то же, что отдаёт
    ///     <c>GetAllScenariosAsync</c>: без сброса правка не доезжает до режима игры
    ///     до истечения пятнадцатиминутного кэша.
    /// </summary>
    private void InvalidateScenarioCaches()
    {
        cache.Remove(ScenariosCacheKey);
        cache.Remove(TemplatesCacheKey);
        cache.Remove(PublishedCacheKey);
    }

    // ── Location CRUD ──────────────────────────────────────────────

    public async Task<bool> AddLocationAsync(Guid scenarioId, ScenarioLocation location)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var scenario = await dbContext.Scenarios.FindAsync(scenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            var locations = scenario.Locations?.ToList() ?? [];
            locations.Add(location);
            scenario.Locations = locations;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();
            InvalidateScenarioCaches();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding location to scenario {ScenarioId}", scenarioId);
            return false;
        }
    }

    public async Task<bool> UpdateLocationAsync(Guid scenarioId, ScenarioLocation location)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var scenario = await dbContext.Scenarios.FindAsync(scenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            var locations = scenario.Locations?.ToList() ?? [];
            var index = locations.FindIndex(l => l.Id == location.Id);
            if (index < 0) return false;

            locations[index] = location;
            scenario.Locations = locations;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();
            InvalidateScenarioCaches();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating location {LocationId} in scenario {ScenarioId}", location.Id, scenarioId);
            return false;
        }
    }

    public async Task<bool> RemoveLocationAsync(Guid scenarioId, Guid locationId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var scenario = await dbContext.Scenarios.FindAsync(scenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            var locations = scenario.Locations?.ToList() ?? [];
            var toRemove = locations.FirstOrDefault(l => l.Id == locationId);
            if (toRemove is null) return false;

            locations.Remove(toRemove);
            scenario.Locations = locations;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();
            InvalidateScenarioCaches();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing location {LocationId} from scenario {ScenarioId}", locationId, scenarioId);
            return false;
        }
    }

    // ── KeyFact CRUD ───────────────────────────────────────────────

    public async Task<bool> AddKeyFactAsync(Guid scenarioId, ScenarioKeyFact keyFact)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var scenario = await dbContext.Scenarios.FindAsync(scenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            var facts = scenario.KeyFacts?.ToList() ?? [];
            facts.Add(keyFact);
            scenario.KeyFacts = facts;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding key fact to scenario {ScenarioId}", scenarioId);
            return false;
        }
    }

    public async Task<bool> UpdateKeyFactAsync(Guid scenarioId, ScenarioKeyFact keyFact)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var scenario = await dbContext.Scenarios.FindAsync(scenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            var facts = scenario.KeyFacts?.ToList() ?? [];
            var index = facts.FindIndex(f => f.Id == keyFact.Id);
            if (index < 0) return false;

            facts[index] = keyFact;
            scenario.KeyFacts = facts;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating key fact {KeyFactId} in scenario {ScenarioId}", keyFact.Id, scenarioId);
            return false;
        }
    }

    public async Task<bool> RemoveKeyFactAsync(Guid scenarioId, Guid keyFactId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var scenario = await dbContext.Scenarios.FindAsync(scenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            var facts = scenario.KeyFacts?.ToList() ?? [];
            var toRemove = facts.FirstOrDefault(f => f.Id == keyFactId);
            if (toRemove is null) return false;

            facts.Remove(toRemove);
            scenario.KeyFacts = facts;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing key fact {KeyFactId} from scenario {ScenarioId}", keyFactId, scenarioId);
            return false;
        }
    }

    public async Task<bool> SaveAllKeyFactsAsync(Guid scenarioId, List<ScenarioKeyFact> facts)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var scenario = await dbContext.Scenarios.FindAsync(scenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            scenario.KeyFacts = facts;
            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving all key facts for scenario {ScenarioId}", scenarioId);
            return false;
        }
    }

    // ── Handout CRUD ───────────────────────────────────────────────

    public async Task<bool> AddHandoutAsync(Guid scenarioId, ScenarioHandout handout)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var scenario = await dbContext.Scenarios.FindAsync(scenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            var handouts = scenario.Handouts?.ToList() ?? [];
            handouts.Add(handout);
            scenario.Handouts = handouts;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding handout to scenario {ScenarioId}", scenarioId);
            return false;
        }
    }

    public async Task<bool> UpdateHandoutAsync(Guid scenarioId, ScenarioHandout handout)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var scenario = await dbContext.Scenarios.FindAsync(scenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            var handouts = scenario.Handouts?.ToList() ?? [];
            var index = handouts.FindIndex(h => h.Id == handout.Id);
            if (index < 0) return false;

            handouts[index] = handout;
            scenario.Handouts = handouts;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating handout {HandoutId} in scenario {ScenarioId}", handout.Id, scenarioId);
            return false;
        }
    }

    public async Task<bool> RemoveHandoutAsync(Guid scenarioId, Guid handoutId)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var scenario = await dbContext.Scenarios.FindAsync(scenarioId);
            if (scenario is null || !await MayWriteAsync(dbContext, scenario)) return false;

            var handouts = scenario.Handouts?.ToList() ?? [];
            var toRemove = handouts.FirstOrDefault(h => h.Id == handoutId);
            if (toRemove is null) return false;

            handouts.Remove(toRemove);
            scenario.Handouts = handouts;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing handout {HandoutId} from scenario {ScenarioId}", handoutId, scenarioId);
            return false;
        }
    }
}