using CampaignManager.Web.Scenarios.Models;
using CampaignManager.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Scenarios.Services;

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
                .ThenInclude(sc => sc.Creature)
                .Include(s => s.ScenarioItems)
                .ThenInclude(si => si.Item)
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
            using var dbContext = await dbContextFactory.CreateDbContextAsync();

            await dbContext.Scenarios.AddAsync(scenario);
            await dbContext.SaveChangesAsync();

            // Invalidate cache
            cache.Remove(ScenariosCacheKey);
            if (scenario.IsTemplate) cache.Remove(TemplatesCacheKey);

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

            // Clone NPCs
            foreach (var npc in template.Npcs)
            {
                ScenarioNpc newNpc = new()
                {
                    Name = npc.Name,
                    Description = npc.Description,
                    Role = npc.Role,
                    Location = npc.Location,
                    Notes = npc.Notes,
                    ScenarioId = newScenario.Id
                };

                await dbContext.ScenarioNpcs.AddAsync(newNpc);
            }

            // Clone creature relationships
            foreach (var sc in template.ScenarioCreatures)
            {
                ScenarioCreature newSc = new()
                {
                    ScenarioId = newScenario.Id,
                    CreatureId = sc.CreatureId,
                    Location = sc.Location,
                    Notes = sc.Notes,
                    Quantity = sc.Quantity
                };

                await dbContext.ScenarioCreatures.AddAsync(newSc);
            }

            // Clone item relationships
            foreach (var si in template.ScenarioItems)
            {
                ScenarioItem newSi = new()
                {
                    ScenarioId = newScenario.Id,
                    ItemId = si.ItemId,
                    Location = si.Location,
                    Notes = si.Notes,
                    Quantity = si.Quantity
                };

                await dbContext.ScenarioItems.AddAsync(newSi);
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
}