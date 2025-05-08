using CampaignManager.Web.Components.Features.Weapons.Model;
using CampaignManager.Web.Utilities.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Components.Features.Weapons.Services;

public class WeaponService(IDbContextFactory<AppDbContext> dbContextFactory, IMemoryCache cache)
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


    /// <summary>
    ///     Асинхронно получает список всех видов оружия с возможностью фильтрации по типу
    /// </summary>
    /// <param name="type">Опциональный параметр для фильтрации по типу оружия</param>
    /// <returns>Список оружия, отфильтрованный по типу (если указан)</returns>
    public async Task<List<Weapon>> GetAllWeaponsAsync(WeaponType? type = null)
    {
        var cacheKey = type.HasValue ? $"{WeaponsKey}_{type}" : WeaponsKey;

        if (cache.TryGetValue(cacheKey, out List<Weapon> cachedWeapons)) return cachedWeapons;

        using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var query = dbContext.Weapons.AsQueryable();

        if (type.HasValue) query = query.Where(w => w.Type == type.Value);

        var weapons = await query.OrderBy(w => w.Name).ToListAsync();
        cache.Set(cacheKey, weapons, TimeSpan.FromMinutes(10));
        return weapons;
    }

    /// <summary>
    ///     Асинхронно добавляет новое оружие в базу данных
    /// </summary>
    /// <param name="weapon">Оружие для добавления</param>
    public async Task AddWeaponAsync(Weapon weapon)
    {
        using var dbContext = await dbContextFactory.CreateDbContextAsync();
        weapon.Id = Guid.NewGuid();
        dbContext.Weapons.Add(weapon);
        await dbContext.SaveChangesAsync();
        ClearCache();
    }

    /// <summary>
    ///     Асинхронно обновляет существующее оружие в базе данных
    /// </summary>
    /// <param name="weapon">Оружие для обновления</param>
    public async Task UpdateWeaponAsync(Weapon weapon)
    {
        using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var existingWeapon = await dbContext.Weapons.FindAsync(weapon.Id);
        if (existingWeapon == null) throw new InvalidOperationException($"Оружие с ID {weapon.Id} не найдено");

        // Обновляем свойства существующего оружия
        dbContext.Entry(existingWeapon).CurrentValues.SetValues(weapon);
        await dbContext.SaveChangesAsync();
        ClearCache();
    }

    /// <summary>
    ///     Асинхронно удаляет оружие по его идентификатору
    /// </summary>
    /// <param name="id">Идентификатор оружия для удаления</param>
    public async Task DeleteWeaponAsync(Guid id)
    {
        using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var weapon = await dbContext.Weapons.FindAsync(id);
        if (weapon != null)
        {
            dbContext.Weapons.Remove(weapon);
            await dbContext.SaveChangesAsync();
            ClearCache();
        }
    }

    /// <summary>
    ///     Очищает кэш оружия
    /// </summary>
    private void ClearCache()
    {
        cache.Remove(WeaponsKey);

        // Clear cache for each weapon type
        foreach (WeaponType type in Enum.GetValues(typeof(WeaponType))) cache.Remove($"{WeaponsKey}_{type}");
    }
}