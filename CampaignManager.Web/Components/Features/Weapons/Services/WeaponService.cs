using CampaignManager.Web.Components.Features.Weapons.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Components.Features.Weapons.Services;

public sealed class WeaponService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    ILogger<WeaponService> logger)
{
    private const string WeaponsKey = "AllWeapons";

    public async Task<List<Weapon>> GetAllRangeWeaponsAsync()
    {
        return await GetAllWeaponsAsync(
            WeaponType.Pistols |
            WeaponType.Rifles |
            WeaponType.Shotguns |
            WeaponType.AssaultRifles |
            WeaponType.SubmachineGuns |
            WeaponType.MachineGuns |
            WeaponType.ExplosivesAndHeavyWeapons |
            WeaponType.Other);
    }

    public async Task<List<Weapon>> GetAllWeaponsAsync(WeaponType? type = null)
    {
        var cacheKey = type.HasValue ? $"{WeaponsKey}_{type}" : WeaponsKey;

        if (cache.TryGetValue(cacheKey, out List<Weapon>? cachedWeapons) && cachedWeapons is not null)
            return cachedWeapons;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var query = dbContext.Weapons.AsQueryable();

        if (type.HasValue) query = query.Where(w => w.Type == type.Value);

        var weapons = await query.OrderBy(w => w.Name).ToListAsync();

        cache.Set(cacheKey, weapons, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
        });

        return weapons;
    }

    public async Task<bool> AddWeaponAsync(Weapon weapon) =>
        await CrudServiceHelper.CreateAsync(dbContextFactory, cache, WeaponsKey, weapon, logger) is not null;

    public async Task<bool> UpdateWeaponAsync(Weapon weapon)
    {
        var result = await CrudServiceHelper.UpdateAsync(dbContextFactory, cache, WeaponsKey, weapon, logger);
        if (result) ClearCache();
        return result;
    }

    public async Task<bool> DeleteWeaponAsync(Guid id)
    {
        var result = await CrudServiceHelper.DeleteAsync<Weapon>(dbContextFactory, cache, WeaponsKey, id, logger);
        if (result) ClearCache();
        return result;
    }

    private void ClearCache()
    {
        cache.Remove(WeaponsKey);
        foreach (WeaponType type in Enum.GetValues(typeof(WeaponType))) cache.Remove($"{WeaponsKey}_{type}");
    }
}