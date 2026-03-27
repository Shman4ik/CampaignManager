using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
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

    /// <summary>
    ///     Gets all creatures in the system, with optional filtering and pagination
    /// </summary>
    public async Task<List<Creature>> GetAllCreaturesAsync(string searchText = "", string? creatureTypeStr = null, int page = 1, int pageSize = DefaultPageSize)
    {
        var all = await CrudServiceHelper.GetAllCachedAsync<Creature>(dbContextFactory, cache, CreaturesCacheKey, logger);
        return FilterAndPage(all);

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
    /// <param name="creatureTypeStr"></param>
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
    ///     Gets a creature by its ID, ensuring all characteristic sub-objects are non-null
    /// </summary>
    public async Task<Creature?> GetCreatureByIdAsync(Guid id)
    {
        var result = await CrudServiceHelper.GetByIdAsync<Creature>(dbContextFactory, id, logger);

        if (result?.CreatureCharacteristics != null)
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

    /// <summary>
    ///     Creates a new creature, rejecting duplicates by name
    /// </summary>
    public Task<Creature?> CreateCreatureAsync(Creature creature) =>
        CrudServiceHelper.CreateAsync(dbContextFactory, cache, CreaturesCacheKey, creature, logger);

    /// <summary>
    ///     Updates an existing creature
    /// </summary>
    public Task<bool> UpdateCreatureAsync(Creature creature) =>
        CrudServiceHelper.UpdateAsync(dbContextFactory, cache, CreaturesCacheKey, creature, logger);

    /// <summary>
    ///     Deletes a creature by its ID
    /// </summary>
    public Task<bool> DeleteCreatureAsync(Guid id) =>
        CrudServiceHelper.DeleteAsync<Creature>(dbContextFactory, cache, CreaturesCacheKey, id, logger);

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

            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

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