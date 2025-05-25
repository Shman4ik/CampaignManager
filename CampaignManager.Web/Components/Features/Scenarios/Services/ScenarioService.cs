using CampaignManager.Web.Components.Features.Scenarios.Model;
using CampaignManager.Web.Utilities.DataBase;
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

            using var dbContext = await dbContextFactory.CreateDbContextAsync();
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
            using var dbContext = await dbContextFactory.CreateDbContextAsync();
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
    /// <param name="id">The ID of the scenario</param>
    /// <returns>The scenario if found, null otherwise</returns>
    public async Task<Scenario?> GetScenarioByIdAsync(Guid id)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Scenarios
                .Include(s => s.Npcs)
                .Include(s => s.ScenarioCreatures)
                .Include(s => s.ScenarioItems)
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
            using var dbContext = await dbContextFactory.CreateDbContextAsync();

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
            using var dbContext = await dbContextFactory.CreateDbContextAsync();

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
            using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Get the template scenario with all related entities
            var template = await dbContext.Scenarios
                .Include(s => s.Npcs)
                .Include(s => s.ScenarioCreatures)
                .Include(s => s.ScenarioItems)
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
            
            // Copy creatures
            if (template.ScenarioCreatures != null)
            {
                newScenario.ScenarioCreatures = template.ScenarioCreatures
                    .Select(sc => new ScenarioCreature
                    {
                        Id = Guid.NewGuid(),
                        ScenarioId = newScenario.Id,
                        Name = sc.Name,
                        Type = sc.Type,
                        CreatureCharacteristics = sc.CreatureCharacteristics,
                        CombatDescriptions = sc.CombatDescriptions,
                        SpecialAbilities = sc.SpecialAbilities,
                        Notes = sc.Notes
                    })
                    .ToList();
            }

            // Copy items
            if (template.ScenarioItems != null)
            {
                newScenario.ScenarioItems = template.ScenarioItems
                    .Select(si => new ScenarioItem
                    {
                        Id = Guid.NewGuid(),
                        ScenarioId = newScenario.Id,
                        Name = si.Name,
                        Era = si.Era,
                        Type = si.Type,
                        Description = si.Description,
                        ImageUrl = si.ImageUrl,
                        Notes = si.Notes
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
            using var dbContext = await dbContextFactory.CreateDbContextAsync();
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
            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            
            // Verify the scenario exists
            var scenario = await dbContext.Scenarios.FindAsync(scenarioCreature.ScenarioId);
            if (scenario == null) return false;
            
            // Initialize the entity
            scenarioCreature.Init();
            
            // If the scenario doesn't have a ScenarioCreatures collection, create one
            if (scenario.ScenarioCreatures == null)
            {
                scenario.ScenarioCreatures = new List<ScenarioCreature>();
            }
            
            // Add the creature to the scenario's collection
            scenario.ScenarioCreatures.Add(scenarioCreature);
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
            using var dbContext = await dbContextFactory.CreateDbContextAsync();
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
            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            
            // Verify the scenario exists
            var scenario = await dbContext.Scenarios.FindAsync(scenarioItem.ScenarioId);
            if (scenario == null) return false;
            
            // Initialize the entity
            scenarioItem.Init();
            
            // If the scenario doesn't have a ScenarioItems collection, create one
            if (scenario.ScenarioItems == null)
            {
                scenario.ScenarioItems = new List<ScenarioItem>();
            }
            
            // Add the item to the scenario's collection
            scenario.ScenarioItems.Add(scenarioItem);
            await dbContext.SaveChangesAsync();
            
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding item to scenario {ScenarioId}", scenarioItem.ScenarioId);
            return false;
        }
    }
}