using CampaignManager.Web.Components.Features.Items.Model;
using CampaignManager.Web.Components.Shared.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
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

    /// <summary>
    ///     Gets all items in the system
    /// </summary>
    public Task<List<Item>> GetAllItemsAsync() =>
        CrudServiceHelper.GetAllCachedAsync<Item>(dbContextFactory, cache, ItemsCacheKey, logger);

    /// <summary>
    ///     Gets an item by its ID
    /// </summary>
    public Task<Item?> GetItemByIdAsync(Guid id) =>
        CrudServiceHelper.GetByIdAsync<Item>(dbContextFactory, id, logger);

    /// <summary>
    ///     Gets items by type
    /// </summary>
    public async Task<List<Item>> GetItemsByTypeAsync(string type)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
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
    public async Task<List<Item>> GetItemsByEraAsync(Eras era)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
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

    /// <summary>
    ///     Searches for items by name or description
    /// </summary>
    public async Task<List<Item>> SearchItemsAsync(string searchTerm)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
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
    ///     Creates a new item, rejecting duplicates by name
    /// </summary>
    public Task<Item?> CreateItemAsync(Item item) =>
        CrudServiceHelper.CreateAsync(dbContextFactory, cache, ItemsCacheKey, item, logger);

    /// <summary>
    ///     Updates an existing item
    /// </summary>
    public Task<bool> UpdateItemAsync(Item item) =>
        CrudServiceHelper.UpdateAsync(dbContextFactory, cache, ItemsCacheKey, item, logger);

    /// <summary>
    ///     Deletes an item by its ID
    /// </summary>
    public Task<bool> DeleteItemAsync(Guid id) =>
        CrudServiceHelper.DeleteAsync<Item>(dbContextFactory, cache, ItemsCacheKey, id, logger);

    /// <summary>
    ///     Gets all distinct item types in the system
    /// </summary>
    public async Task<List<string>> GetAllItemTypesAsync()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
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
}
