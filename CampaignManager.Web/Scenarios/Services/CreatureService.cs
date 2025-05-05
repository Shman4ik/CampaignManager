using CampaignManager.Web.Scenarios.Models;
using CampaignManager.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Scenarios.Services;

/// <summary>
/// Service for managing creatures in the system
/// </summary>
public sealed class CreatureService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    ILogger<CreatureService> logger)
{
    private const string CreaturesCacheKey = "AllCreatures";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets all creatures in the system
    /// </summary>
    /// <returns>A list of all creatures</returns>
    public async Task<List<Creature>> GetAllCreaturesAsync()
    {
        try
        {
            if (cache.TryGetValue(CreaturesCacheKey, out List<Creature>? creatures) && creatures is not null)
            {
                return creatures;
            }
            
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            creatures = await dbContext.Creatures.OrderBy(c => c.Name).ToListAsync();
            
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CacheExpiration);
                
            cache.Set(CreaturesCacheKey, creatures, cacheOptions);
            
            return creatures;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving creatures");
            return [];
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
    /// Gets creatures by category
    /// </summary>
    /// <param name="category">The category to filter by</param>
    /// <returns>A list of creatures in the specified category</returns>
    public async Task<List<Creature>> GetCreaturesByCategoryAsync(string category)
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Creatures
                .Where(c => c.Categories != null && c.Categories.Contains(category))
                .OrderBy(c => c.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving creatures by category {Category}", category);
            return [];
        }
    }
    
    /// <summary>
    /// Gets creatures by type
    /// </summary>
    /// <param name="type">The type to filter by</param>
    /// <returns>A list of creatures of the specified type</returns>
    public async Task<List<Creature>> GetCreaturesByTypeAsync(string type)
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
                .Where(c => EF.Functions.ILike(c.Name, $"%{searchTerm}%") || 
                            (c.Description != null && EF.Functions.ILike(c.Description, $"%{searchTerm}%")))
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
            bool exists = await dbContext.Creatures
                .AnyAsync(c => c.Name.Equals(creature.Name, StringComparison.OrdinalIgnoreCase));
                
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
                return false;
            }
            
            // Check if the name is being changed and if the new name already exists
            if (!string.Equals(existingCreature.Name, creature.Name, StringComparison.OrdinalIgnoreCase))
            {
                bool nameExists = await dbContext.Creatures
                    .AnyAsync(c => c.Id != creature.Id && 
                                  c.Name.Equals(creature.Name, StringComparison.OrdinalIgnoreCase));
                                  
                if (nameExists)
                {
                    logger.LogWarning("Cannot update creature: name {CreatureName} already exists", creature.Name);
                    return false;
                }
            }
            
            // Update the existing creature with the new values
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
    /// Gets all distinct creature types in the system
    /// </summary>
    /// <returns>A list of distinct creature types</returns>
    public async Task<List<string>> GetAllCreatureTypesAsync()
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Creatures
                .Where(c => c.Type != null)
                .Select(c => c.Type!)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving creature types");
            return [];
        }
    }
    
    /// <summary>
    /// Gets all distinct creature categories in the system
    /// </summary>
    /// <returns>A list of distinct creature categories</returns>
    public async Task<List<string>> GetAllCreatureCategoriesAsync()
    {
        try
        {
            using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
            
            // Get all categories (comma-separated in the Categories field)
            List<string?> allCategoriesRaw = await dbContext.Creatures
                .Where(c => c.Categories != null)
                .Select(c => c.Categories)
                .ToListAsync();
                
            // Split and flatten the categories
            HashSet<string> uniqueCategories = new(StringComparer.OrdinalIgnoreCase);
            
            foreach (string? categoriesString in allCategoriesRaw)
            {
                if (string.IsNullOrWhiteSpace(categoriesString))
                {
                    continue;
                }
                
                string[] categories = categoriesString.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (string category in categories)
                {
                    uniqueCategories.Add(category.Trim());
                }
            }
            
            return uniqueCategories.OrderBy(c => c).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving creature categories");
            return [];
        }
    }
}
