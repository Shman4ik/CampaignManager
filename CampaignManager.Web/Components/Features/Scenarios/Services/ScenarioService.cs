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
    ILogger<ScenarioService> logger)
{
    private const string ScenariosCacheKey = "AllScenarios";
    private const string TemplatesCacheKey = "ScenarioTemplates";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);

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
                .Include(s => s.Npcs)
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
            scenario.Init();
            await dbContext.Scenarios.AddAsync(scenario);
            await dbContext.SaveChangesAsync();

            // Invalidate cache
            cache.Remove(ScenariosCacheKey);
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

            // Update the existing scenario with the new values
            dbContext.Entry(existingScenario).CurrentValues.SetValues(scenario);
            await dbContext.SaveChangesAsync();

            // Invalidate cache
            cache.Remove(ScenariosCacheKey);
            cache.Remove(TemplatesCacheKey);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating scenario {ScenarioId}", scenario.Id);
            return false;
        }
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

            dbContext.Scenarios.Remove(scenario);
            await dbContext.SaveChangesAsync();

            // Invalidate cache
            cache.Remove(ScenariosCacheKey);
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
    /// <param name="creatorEmail">The email of the user creating the scenario</param>
    /// <returns>The newly created campaign scenario</returns>
    public async Task<Scenario?> CreateScenarioFromTemplateAsync(Guid templateId, Guid campaignId, string creatorEmail)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Get the template scenario with all related entities
            var template = await dbContext.Scenarios
                .Include(s => s.Npcs)
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
                CreatorEmail = creatorEmail,
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
            if (scenario == null) return false;

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
            if (scenario == null) return false;

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
            if (scenario is null) return false;

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
            if (scenario is null) return false;

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
            if (scenario is null) return false;

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
            if (scenario is null) return false;

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

    // ── Location CRUD ──────────────────────────────────────────────

    public async Task<bool> AddLocationAsync(Guid scenarioId, ScenarioLocation location)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var scenario = await dbContext.Scenarios.FindAsync(scenarioId);
            if (scenario is null) return false;

            var locations = scenario.Locations?.ToList() ?? [];
            locations.Add(location);
            scenario.Locations = locations;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();
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
            if (scenario is null) return false;

            var locations = scenario.Locations?.ToList() ?? [];
            var index = locations.FindIndex(l => l.Id == location.Id);
            if (index < 0) return false;

            locations[index] = location;
            scenario.Locations = locations;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();
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
            if (scenario is null) return false;

            var locations = scenario.Locations?.ToList() ?? [];
            var toRemove = locations.FirstOrDefault(l => l.Id == locationId);
            if (toRemove is null) return false;

            locations.Remove(toRemove);
            scenario.Locations = locations;

            dbContext.Scenarios.Update(scenario);
            await dbContext.SaveChangesAsync();
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
            if (scenario is null) return false;

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
            if (scenario is null) return false;

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
            if (scenario is null) return false;

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

    // ── Handout CRUD ───────────────────────────────────────────────

    public async Task<bool> AddHandoutAsync(Guid scenarioId, ScenarioHandout handout)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var scenario = await dbContext.Scenarios.FindAsync(scenarioId);
            if (scenario is null) return false;

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
            if (scenario is null) return false;

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
            if (scenario is null) return false;

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