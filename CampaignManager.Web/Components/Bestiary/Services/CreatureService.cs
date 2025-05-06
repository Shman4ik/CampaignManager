using CampaignManager.Web.Components.Bestiary.Model;
using CampaignManager.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Linq;

namespace CampaignManager.Web.Components.Bestiary.Services;

/// <summary>
/// Service for managing creatures in the system
/// </summary>
public sealed class CreatureService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    ILogger<CreatureService> logger,
    IWebHostEnvironment env)
{
    private const string CreaturesCacheKey = "AllCreatures";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);
    private const int DefaultPageSize = 6;

    /// <summary>
    /// Gets all creatures in the system
    /// </summary>
    /// <param name="searchText">Optional search text to filter creatures</param>
    /// <param name="page">Optional page number for pagination</param>
    /// <param name="pageSize">Optional page size for pagination</param>
    /// <returns>A list of all creatures</returns>
    public async Task<List<Creature>> GetAllCreaturesAsync(string searchText = "", int page = 1, int pageSize = DefaultPageSize)
    {
        try
        {
            if (cache.TryGetValue(CreaturesCacheKey, out List<Creature>? creatures) && creatures is not null)
            {
                return creatures
                    .Where(c => string.IsNullOrWhiteSpace(searchText) || c.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
            }

            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            creatures = await dbContext.Creatures
                .Where(c => string.IsNullOrWhiteSpace(searchText) || c.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Name)
                .ToListAsync();

            MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CacheExpiration);

            cache.Set(CreaturesCacheKey, creatures, cacheOptions);

            return creatures
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving creatures");
            return [];
        }
    }

    /// <summary>
    /// Gets the total count of creatures
    /// </summary>
    /// <param name="searchText">Optional search text to filter creatures</param>
    /// <returns>The total count of creatures</returns>
    public async Task<int> GetCreatureCountAsync(string searchText = "")
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Creatures
                .Where(c => string.IsNullOrWhiteSpace(searchText) || c.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .CountAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving creature count");
            return 0;
        }
    }

    /// <summary>
    /// Gets a creature by its ID
    /// </summary>
    /// <param name="id">The ID of the creature</param>
    /// <returns>The creature if found, null otherwise</returns>
    public async Task<Creature?> GetCreatureByIdAsync(Guid id)
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Creatures.FindAsync(id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving creature with ID {CreatureId}", id);
            return null;
        }
    }

    /// <summary>
    /// Gets creatures by type
    /// </summary>
    /// <param name="type">The type to filter by</param>
    /// <returns>A list of creatures of the specified type</returns>
    public async Task<List<Creature>> GetCreaturesByTypeAsync(CreatureType type)
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Creatures
                .Where(c => c.Type == type)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving creatures by type {Type}", type);
            return [];
        }
    }

    /// <summary>
    /// Searches for creatures by name
    /// </summary>
    /// <param name="searchTerm">The search term to look for in creature names</param>
    /// <returns>A list of creatures matching the search term</returns>
    public async Task<List<Creature>> SearchCreaturesAsync(string searchTerm)
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Creatures
                .Where(c => c.Name.Contains(searchTerm) ||
                            (c.Description != null && c.Description.Contains(searchTerm)))
                .OrderBy(c => c.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching creatures with term {SearchTerm}", searchTerm);
            return [];
        }
    }

    /// <summary>
    /// Creates a new creature
    /// </summary>
    /// <param name="creature">The creature to create</param>
    /// <returns>The created creature with its assigned ID</returns>
    public async Task<Creature?> CreateCreatureAsync(Creature creature)
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

            // Check if a creature with the same name already exists
            bool exists = await dbContext.Creatures.AnyAsync(c => c.Name == creature.Name);
            if (exists)
            {
                logger.LogWarning("Creature with name {CreatureName} already exists", creature.Name);
                return null;
            }

            await dbContext.Creatures.AddAsync(creature);
            await dbContext.SaveChangesAsync();

            // Invalidate cache
            cache.Remove(CreaturesCacheKey);

            return creature;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating creature {CreatureName}", creature.Name);
            return null;
        }
    }

    /// <summary>
    /// Updates an existing creature
    /// </summary>
    /// <param name="creature">The creature with updated values</param>
    /// <returns>True if the update was successful, false otherwise</returns>
    public async Task<bool> UpdateCreatureAsync(Creature creature)
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

            Creature? existingCreature = await dbContext.Creatures.FindAsync(creature.Id);
            if (existingCreature is null)
            {
                logger.LogWarning("Creature with ID {CreatureId} not found for update", creature.Id);
                return false;
            }

            // Check if the name is being changed and if the new name already exists
            if (existingCreature.Name != creature.Name)
            {
                bool nameExists = await dbContext.Creatures
                    .AnyAsync(c => c.Name == creature.Name && c.Id != creature.Id);

                if (nameExists)
                {
                    logger.LogWarning("Cannot update creature {CreatureId}: another creature with name {CreatureName} already exists",
                        creature.Id, creature.Name);
                    return false;
                }
            }

            dbContext.Entry(existingCreature).CurrentValues.SetValues(creature);
            await dbContext.SaveChangesAsync();

            // Invalidate cache
            cache.Remove(CreaturesCacheKey);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating creature {CreatureId}", creature.Id);
            return false;
        }
    }

    /// <summary>
    /// Deletes a creature by its ID
    /// </summary>
    /// <param name="id">The ID of the creature to delete</param>
    /// <returns>True if the deletion was successful, false otherwise</returns>
    public async Task<bool> DeleteCreatureAsync(Guid id)
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

            Creature? creature = await dbContext.Creatures.FindAsync(id);
            if (creature is null)
            {
                return false;
            }

            // Check if the creature is used in any scenarios
            bool isUsed = await dbContext.ScenarioCreatures.AnyAsync(sc => sc.CreatureId == id);
            if (isUsed)
            {
                logger.LogWarning("Cannot delete creature {CreatureId}: it is used in one or more scenarios", id);
                return false;
            }

            dbContext.Creatures.Remove(creature);
            await dbContext.SaveChangesAsync();

            // Invalidate cache
            cache.Remove(CreaturesCacheKey);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting creature {CreatureId}", id);
            return false;
        }
    }

    /// <summary>
    /// Imports creatures from JSON files in the Data directory
    /// </summary>
    /// <returns>The number of creatures imported</returns>
    public async Task<int> ImportCreaturesFromJsonFilesAsync()
    {
        try
        {
            string dataDirectory = Path.Combine(env.ContentRootPath, "Data");
            if (!Directory.Exists(dataDirectory))
            {
                logger.LogWarning("Data directory not found at {DataDirectory}", dataDirectory);
                return 0;
            }

            string[] jsonFiles = Directory.GetFiles(dataDirectory, "*.json");
            if (jsonFiles.Length == 0)
            {
                logger.LogInformation("No JSON files found in the Data directory");
                return 0;
            }

            int totalImported = 0;
            JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };

            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();

            foreach (string jsonFile in jsonFiles)
            {
                logger.LogInformation("Processing file: {FileName}", Path.GetFileName(jsonFile));

                try
                {
                    string jsonContent = await File.ReadAllTextAsync(jsonFile);
                    List<Creature>? importedCreatures = JsonSerializer.Deserialize<List<Creature>>(jsonContent, options);

                    if (importedCreatures == null || importedCreatures.Count == 0)
                    {
                        logger.LogWarning("No creatures found in file {FileName}", Path.GetFileName(jsonFile));
                        continue;
                    }

                    foreach (Creature importedCreature in importedCreatures)
                    {
                        await dbContext.Creatures.AddAsync(importedCreature);
                        totalImported++;
                    }
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Error deserializing JSON from file {FileName}", Path.GetFileName(jsonFile));
                }
            }

            // Save all changes to the database
            if (totalImported > 0)
            {
                await dbContext.SaveChangesAsync();

                // Clear cache
                cache.Remove(CreaturesCacheKey);

                logger.LogInformation("Successfully imported {TotalImported} creatures", totalImported);
            }

            return totalImported;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error importing creatures from JSON files");
            return 0;
        }
    }
}