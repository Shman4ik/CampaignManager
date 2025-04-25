using CampaignManager.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Weapons;

public class WeaponService
{
    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private const string WeaponsKey = "AllWeapons";

    public WeaponService(AppDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

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
    /// Асинхронно получает список всех видов оружия с возможностью фильтрации по типу
    /// </summary>
    /// <param name="type">Опциональный параметр для фильтрации по типу оружия</param>
    /// <returns>Список оружия, отфильтрованный по типу (если указан)</returns>
    public async Task<List<Weapon>> GetAllWeaponsAsync(WeaponType? type = null)
    {
        string cacheKey = type.HasValue ? $"{WeaponsKey}_{type}" : WeaponsKey;

        if (_cache.TryGetValue(cacheKey, out List<Weapon> cachedWeapons))
        {
            return cachedWeapons;
        }

        var query = _dbContext.Weapons.AsQueryable();

        if (type.HasValue)
        {
            query = query.Where(w => w.Type == type.Value);
        }

        List<Weapon> weapons = await query.OrderBy(w => w.Name).ToListAsync();
        _cache.Set(cacheKey, weapons, TimeSpan.FromMinutes(10));
        return weapons;
    }

    /// <summary>
    /// Синхронно получает список всех видов оружия с возможностью фильтрации по типу
    /// </summary>
    /// <param name="type">Опциональный параметр для фильтрации по типу оружия</param>
    /// <returns>Список оружия, отфильтрованный по типу (если указан)</returns>
    public List<Weapon> GetAllWeapons(WeaponType? type = null)
    {
        return GetAllWeaponsAsync(type).GetAwaiter().GetResult();
    }



    /// <summary>
    /// Асинхронно получает оружие по его идентификатору
    /// </summary>
    /// <param name="id">Идентификатор оружия</param>
    /// <returns>Оружие или null, если не найдено</returns>
    public async Task<Weapon?> GetWeaponByIdAsync(Guid id)
    {
        return await _dbContext.Weapons.FindAsync(id);
    }

    /// <summary>
    /// Асинхронно получает оружие по его названию
    /// </summary>
    /// <param name="name">Название оружия</param>
    /// <param name="type">Опциональный параметр для фильтрации по типу оружия</param>
    /// <returns>Оружие или null, если не найдено</returns>
    public async Task<Weapon?> GetWeaponByNameAsync(string name, WeaponType? type = null)
    {
        var query = _dbContext.Weapons.AsQueryable();

        if (type.HasValue)
        {
            query = query.Where(w => w.Type == type.Value);
        }

        return await query.FirstOrDefaultAsync(w => w.Name == name);
    }

    /// <summary>
    /// Асинхронно добавляет новое оружие в базу данных
    /// </summary>
    /// <param name="weapon">Оружие для добавления</param>
    public async Task AddWeaponAsync(Weapon weapon)
    {
        weapon.Id = Guid.NewGuid();
        _dbContext.Weapons.Add(weapon);
        await _dbContext.SaveChangesAsync();
        ClearCache();
    }

    /// <summary>
    /// Асинхронно обновляет существующее оружие в базе данных
    /// </summary>
    /// <param name="weapon">Оружие для обновления</param>
    public async Task UpdateWeaponAsync(Weapon weapon)
    {
        _dbContext.Weapons.Update(weapon);
        await _dbContext.SaveChangesAsync();
        ClearCache();
    }

    /// <summary>
    /// Асинхронно удаляет оружие по его идентификатору
    /// </summary>
    /// <param name="id">Идентификатор оружия для удаления</param>
    public async Task DeleteWeaponAsync(Guid id)
    {
        Weapon? weapon = await _dbContext.Weapons.FindAsync(id);
        if (weapon != null)
        {
            _dbContext.Weapons.Remove(weapon);
            await _dbContext.SaveChangesAsync();
            ClearCache();
        }
    }

    /// <summary>
    /// Получает список оружия указанного типа
    /// </summary>
    /// <param name="type">Тип оружия для фильтрации</param>
    /// <returns>Список оружия указанного типа</returns>
    public async Task<List<Weapon>> GetWeaponsByTypeAsync(WeaponType type)
    {
        return await GetAllWeaponsAsync(type);
    }

    /// <summary>
    /// Очищает кэш оружия
    /// </summary>
    private void ClearCache()
    {
        _cache.Remove(WeaponsKey);

        // Clear cache for each weapon type
        foreach (WeaponType type in Enum.GetValues(typeof(WeaponType)))
        {
            _cache.Remove($"{WeaponsKey}_{type}");
        }
    }
}
