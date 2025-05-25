using CampaignManager.Web.Components.Features.Items.Model;
using CampaignManager.Web.Components.Shared.Model;
using CampaignManager.Web.Utilities.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Components.Features.Items.Services;

/// <summary>
///     Service for managing items and artifacts in the system
/// </summary>
public sealed class ItemService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    ILogger<ItemService> logger)
{
    private const string ItemsCacheKey = "AllItems";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);

    /// <summary>
    ///     Gets all items in the system
    /// </summary>
    /// <returns>A list of all items</returns>
    public async Task<List<Item>> GetAllItemsAsync()
    {
        try
        {
            if (cache.TryGetValue(ItemsCacheKey, out List<Item>? items) && items is not null) return items;

            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            items = await dbContext.Items.OrderBy(i => i.Name).ToListAsync();

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CacheExpiration);

            cache.Set(ItemsCacheKey, items, cacheOptions);

            return items;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving items");
            return [];
        }
    }

    /// <summary>
    ///     Gets an item by its ID
    /// </summary>
    /// <param name="id">The ID of the item</param>
    /// <returns>The item if found, null otherwise</returns>
    public async Task<Item?> GetItemByIdAsync(Guid id)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Items.FindAsync(id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving item with ID {ItemId}", id);
            return null;
        }
    }

    // Categories property has been removed from Item model
    // This method is kept as a stub for backward compatibility
    /// <summary>
    ///     Gets items by category (deprecated)
    /// </summary>
    /// <param name="category">The category to filter by</param>
    /// <returns>An empty list as Categories are no longer supported</returns>
    public Task<List<Item>> GetItemsByCategoryAsync(string category)
    {
        logger.LogWarning("GetItemsByCategoryAsync called but Categories property has been removed from Item model");
        return Task.FromResult<List<Item>>([]);
    }

    /// <summary>
    ///     Gets items by type
    /// </summary>
    /// <param name="type">The type to filter by</param>
    /// <returns>A list of items of the specified type</returns>
    public async Task<List<Item>> GetItemsByTypeAsync(string type)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Items
                .Where(i => i.Type == type)
                .OrderBy(i => i.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving items by type {Type}", type);
            return [];
        }
    }

    /// <summary>
    ///     Gets items by era
    /// </summary>
    /// <param name="era">The era to filter by</param>
    /// <returns>A list of items of the specified era</returns>
    public async Task<List<Item>> GetItemsByEraAsync(Eras era)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Items
                .Where(i => (i.Era & era) != 0)
                .OrderBy(i => i.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving items by era {Era}", era);
            return [];
        }
    }

    // Rarity property has been removed from Item model
    // This method is kept as a stub for backward compatibility
    /// <summary>
    ///     Gets items by rarity (deprecated)
    /// </summary>
    /// <param name="rarity">The rarity to filter by</param>
    /// <returns>An empty list as Rarity is no longer supported</returns>
    public Task<List<Item>> GetItemsByRarityAsync(string rarity)
    {
        logger.LogWarning("GetItemsByRarityAsync called but Rarity property has been removed from Item model");
        return Task.FromResult<List<Item>>([]);
    }

    /// <summary>
    ///     Searches for items by name or description
    /// </summary>
    /// <param name="searchTerm">The search term to look for in item names or descriptions</param>
    /// <returns>A list of items matching the search term</returns>
    public async Task<List<Item>> SearchItemsAsync(string searchTerm)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Items
                .Where(i => EF.Functions.ILike(i.Name, $"%{searchTerm}%") ||
                            (i.Description != null && EF.Functions.ILike(i.Description, $"%{searchTerm}%")))
                .OrderBy(i => i.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching items with term {SearchTerm}", searchTerm);
            return [];
        }
    }

    /// <summary>
    ///     Creates a new item
    /// </summary>
    /// <param name="item">The item to create</param>
    /// <returns>The created item with its assigned ID</returns>
    public async Task<Item?> CreateItemAsync(Item item)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Check if an item with the same name already exists
            var exists = await dbContext.Items
                .AnyAsync(i => i.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

            if (exists)
            {
                logger.LogWarning("Item with name {ItemName} already exists", item.Name);
                return null;
            }

            await dbContext.Items.AddAsync(item);
            await dbContext.SaveChangesAsync();

            // Invalidate cache
            cache.Remove(ItemsCacheKey);

            return item;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating item {ItemName}", item.Name);
            return null;
        }
    }

    /// <summary>
    ///     Updates an existing item
    /// </summary>
    /// <param name="item">The item with updated values</param>
    /// <returns>True if the update was successful, false otherwise</returns>
    public async Task<bool> UpdateItemAsync(Item item)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var existingItem = await dbContext.Items.FindAsync(item.Id);
            if (existingItem is null) return false;

            // Check if the name is being changed and if the new name already exists
            if (!string.Equals(existingItem.Name, item.Name, StringComparison.OrdinalIgnoreCase))
            {
                var nameExists = await dbContext.Items
                    .AnyAsync(i => i.Id != item.Id &&
                                   i.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

                if (nameExists)
                {
                    logger.LogWarning("Cannot update item: name {ItemName} already exists", item.Name);
                    return false;
                }
            }

            // Update the existing item with the new values
            dbContext.Entry(existingItem).CurrentValues.SetValues(item);
            await dbContext.SaveChangesAsync();

            // Invalidate cache
            cache.Remove(ItemsCacheKey);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating item {ItemId}", item.Id);
            return false;
        }
    }

    /// <summary>
    ///     Deletes an item by its ID
    /// </summary>
    /// <param name="id">The ID of the item to delete</param>
    /// <returns>True if the deletion was successful, false otherwise</returns>
    public async Task<bool> DeleteItemAsync(Guid id)
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var item = await dbContext.Items.FindAsync(id);
            if (item is null) return false;


            dbContext.Items.Remove(item);
            await dbContext.SaveChangesAsync();

            // Invalidate cache
            cache.Remove(ItemsCacheKey);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting item {ItemId}", id);
            return false;
        }
    }

    /// <summary>
    ///     Gets all distinct item types in the system
    /// </summary>
    /// <returns>A list of distinct item types</returns>
    public async Task<List<string>> GetAllItemTypesAsync()
    {
        try
        {
            using var dbContext = await dbContextFactory.CreateDbContextAsync();
            return await dbContext.Items
                .Where(i => i.Type != null)
                .Select(i => i.Type!)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving item types");
            return [];
        }
    }

    // Rarity property has been removed from Item model
    // This method is kept as a stub for backward compatibility
    /// <summary>
    ///     Gets all distinct item rarities in the system (deprecated)
    /// </summary>
    /// <returns>An empty list as Rarity is no longer supported</returns>
    public Task<List<string>> GetAllItemRaritiesAsync()
    {
        logger.LogWarning("GetAllItemRaritiesAsync called but Rarity property has been removed from Item model");
        return Task.FromResult<List<string>>([]);
    }

    // Categories property has been removed from Item model
    // This method is kept as a stub for backward compatibility
    /// <summary>
    ///     Gets all distinct item categories in the system (deprecated)
    /// </summary>
    /// <returns>An empty list as Categories are no longer supported</returns>
    public Task<List<string>> GetAllItemCategoriesAsync()
    {
        logger.LogWarning("GetAllItemCategoriesAsync called but Categories property has been removed from Item model");
        return Task.FromResult<List<string>>([]);
    }
}