using CampaignManager.Web.Components.Features.Items.Model;
using CampaignManager.Web.Components.Shared.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Components.Features.Items.Services;

/// <summary>
///     Справочник предметов. Читают все вошедшие, правят только Хранитель и администратор —
///     проверка стоит в каждом методе записи, а не только в разметке страницы.
/// </summary>
public sealed class ItemService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    IdentityService identityService,
    ILogger<ItemService> logger)
{
    private const string ItemsCacheKey = "AllItems";

    /// <summary>
    ///     Gets all items in the system
    /// </summary>
    public Task<List<Item>> GetAllItemsAsync() =>
        CrudServiceHelper.GetAllCachedAsync<Item>(dbContextFactory, cache, ItemsCacheKey, logger);

    /// <summary>
    ///     Creates a new item, rejecting duplicates by name
    /// </summary>
    public async Task<Item?> CreateItemAsync(Item item)
    {
        await identityService.EnsureKeeperAsync("добавление предмета в справочник");
        return await CrudServiceHelper.CreateAsync(dbContextFactory, cache, ItemsCacheKey, item, logger);
    }

    /// <summary>
    ///     Updates an existing item
    /// </summary>
    public async Task<bool> UpdateItemAsync(Item item)
    {
        await identityService.EnsureKeeperAsync("изменение предмета в справочнике");
        return await CrudServiceHelper.UpdateAsync(dbContextFactory, cache, ItemsCacheKey, item, logger);
    }

    /// <summary>
    ///     Deletes an item by its ID
    /// </summary>
    public async Task<bool> DeleteItemAsync(Guid id)
    {
        await identityService.EnsureKeeperAsync("удаление предмета из справочника");
        return await CrudServiceHelper.DeleteAsync<Item>(dbContextFactory, cache, ItemsCacheKey, id, logger);
    }

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
