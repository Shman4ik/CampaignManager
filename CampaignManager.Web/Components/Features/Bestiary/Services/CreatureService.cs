using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Utilities.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace CampaignManager.Web.Components.Features.Bestiary.Services;

/// <summary>
///     Service for managing creatures in the system
/// </summary>
public sealed class CreatureService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    ILogger<CreatureService> logger,
    IWebHostEnvironment env)
{
    private const string CreaturesCacheKey = "AllCreatures";
    private const int DefaultPageSize = 6;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);

    /// <summary>
    ///     Gets all creatures in the system
    /// </summary>
    /// <param name="searchText">Optional search text to filter creatures</param>
    /// <param name="page">Optional page number for pagination</param>
    /// <param name="pageSize">Optional page size for pagination</param>
    /// <returns>A list of all creatures</returns>
    public async Task<List<Creature>> GetAllCreaturesAsync(string searchText = "", string? creatureTypeStr = null, int page = 1, int pageSize = DefaultPageSize)
    {
        try
        {
            if (cache.TryGetValue(CreaturesCacheKey, out List<Creature>? creatures) && creatures is not null) return FilterAndPage(creatures);

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            creatures = await dbContext.Creatures
                .OrderBy(c => c.Name)
                .ToListAsync();

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CacheExpiration);

            cache.Set(CreaturesCacheKey, creatures, cacheOptions);

            return FilterAndPage(creatures);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving creatures");
            return [];
        }

        List<Creature> FilterAndPage(List<Creature> creatures)
        {
            var isTypeFilter = Enum.TryParse(creatureTypeStr, out CreatureType creatureType);
            return creatures
                .Where(c => string.IsNullOrWhiteSpace(searchText) || c.Name.ToLower().Contains(searchText.ToLower()))
                .Where(p => isTypeFilter == false || p.Type == creatureType)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
    }

    /// <summary>
    ///     Gets the total count of creatures
    /// </summary>
    /// <param name="searchText">Optional search text to filter creatures</param>
    /// <returns>The total count of creatures</returns>
    public async Task<int> GetCreatureCountAsync(string searchText = "", string? creatureTypeStr = null)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var isTypeFilter = Enum.TryParse(creatureTypeStr, out CreatureType creatureType);
            return await dbContext.Creatures
                .Where(c => string.IsNullOrWhiteSpace(searchText) || c.Name.ToLower().Contains(searchText.ToLower()))
                .Where(p => isTypeFilter == false || p.Type == creatureType)
                .CountAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving creature count");
            return 0;
        }
    }

    /// <summary>
    ///     Gets a creature by its ID
    /// </summary>
    /// <param name="id">The ID of the creature</param>
    /// <returns>The creature if found, null otherwise</returns>
    public async Task<Creature?> GetCreatureByIdAsync(Guid id)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var result = await dbContext.Creatures.FindAsync(id);

            if (result != null && result.CreatureCharacteristics != null)
            {
                result.CreatureCharacteristics.Appearance ??= new CreatureCharacteristicModel();
                result.CreatureCharacteristics.Constitution ??= new CreatureCharacteristicModel();
                result.CreatureCharacteristics.Intelligence ??= new CreatureCharacteristicModel();
                result.CreatureCharacteristics.Strength ??= new CreatureCharacteristicModel();
                result.CreatureCharacteristics.Dexterity ??= new CreatureCharacteristicModel();
                result.CreatureCharacteristics.Size ??= new CreatureCharacteristicModel();
                result.CreatureCharacteristics.Education ??= new CreatureCharacteristicModel();
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving creature with ID {CreatureId}", id);
            return null;
        }
    }

    /// <summary>
    ///     Creates a new creature
    /// </summary>
    /// <param name="creature">The creature to create</param>
    /// <returns>The created creature with its assigned ID</returns>
    public async Task<Creature?> CreateCreatureAsync(Creature creature)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Check if a creature with the same name already exists
            var exists = await dbContext.Creatures.AnyAsync(c => c.Name == creature.Name);
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
    ///     Updates an existing creature
    /// </summary>
    /// <param name="creature">The creature with updated values</param>
    /// <returns>True if the update was successful, false otherwise</returns>
    public async Task<bool> UpdateCreatureAsync(Creature creature)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var existingCreature = await dbContext.Creatures.FindAsync(creature.Id);
            if (existingCreature is null)
            {
                logger.LogWarning("Creature with ID {CreatureId} not found for update", creature.Id);
                return false;
            }

            // Check if the name is being changed and if the new name already exists
            if (existingCreature.Name != creature.Name)
            {
                var nameExists = await dbContext.Creatures
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
    ///     Deletes a creature by its ID
    /// </summary>
    /// <param name="id">The ID of the creature to delete</param>
    /// <returns>True if the deletion was successful, false otherwise</returns>
    public async Task<bool> DeleteCreatureAsync(Guid id)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var creature = await dbContext.Creatures.FindAsync(id);
            if (creature is null) return false;


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
    ///     Imports creatures from JSON files in the Data directory
    /// </summary>
    /// <returns>The number of creatures imported</returns>
    public async Task<int> ImportCreaturesFromJsonFilesAsync()
    {
        try
        {
            var dataDirectory = Path.Combine(env.ContentRootPath, "Data");
            if (!Directory.Exists(dataDirectory))
            {
                logger.LogWarning("Data directory not found at {DataDirectory}", dataDirectory);
                return 0;
            }

            var jsonFiles = Directory.GetFiles(dataDirectory, "*.json");
            if (jsonFiles.Length == 0)
            {
                logger.LogInformation("No JSON files found in the Data directory");
                return 0;
            }

            var totalImported = 0;
            JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };

            using var dbContext = await dbContextFactory.CreateDbContextAsync();

            foreach (var jsonFile in jsonFiles)
            {
                logger.LogInformation("Processing file: {FileName}", Path.GetFileName(jsonFile));

                try
                {
                    var jsonContent = await File.ReadAllTextAsync(jsonFile);
                    var importedCreatures = JsonSerializer.Deserialize<List<Creature>>(jsonContent, options);

                    if (importedCreatures == null || importedCreatures.Count == 0)
                    {
                        logger.LogWarning("No creatures found in file {FileName}", Path.GetFileName(jsonFile));
                        continue;
                    }

                    foreach (var importedCreature in importedCreatures)
                    {
                        importedCreature.Init();
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