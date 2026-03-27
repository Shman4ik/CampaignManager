using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Utilities.Services;

/// <summary>
/// Reusable static helpers for common CRUD operations across entity services.
/// Eliminates ~400 lines of near-identical boilerplate.
/// </summary>
public static class CrudServiceHelper
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Loads all entities of type T, caching the result with a 15-minute absolute expiration.
    /// Entities are ordered by name.
    /// </summary>
    public static async Task<List<T>> GetAllCachedAsync<T>(
        IDbContextFactory<AppDbContext> factory,
        IMemoryCache cache,
        string cacheKey,
        ILogger logger)
        where T : BaseDataBaseEntity, INamedEntity
    {
        try
        {
            if (cache.TryGetValue(cacheKey, out List<T>? cached) && cached is not null)
                return cached;

            await using var dbContext = await factory.CreateDbContextAsync();
            var list = await dbContext.Set<T>().OrderBy(e => e.Name).ToListAsync();

            cache.Set(cacheKey, list, new MemoryCacheEntryOptions().SetAbsoluteExpiration(DefaultExpiration));
            return list;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving {EntityType} list", typeof(T).Name);
            return [];
        }
    }

    /// <summary>
    /// Finds a single entity by primary key.
    /// </summary>
    public static async Task<T?> GetByIdAsync<T>(
        IDbContextFactory<AppDbContext> factory,
        Guid id,
        ILogger logger)
        where T : BaseDataBaseEntity
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            return await dbContext.Set<T>().FindAsync(id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving {EntityType} with ID {Id}", typeof(T).Name, id);
            return null;
        }
    }

    /// <summary>
    /// Checks for a duplicate name, calls Init(), persists the entity, and removes the cache entry.
    /// Returns the entity on success, null on duplicate or error.
    /// </summary>
    public static async Task<T?> CreateAsync<T>(
        IDbContextFactory<AppDbContext> factory,
        IMemoryCache cache,
        string cacheKey,
        T entity,
        ILogger logger)
        where T : BaseDataBaseEntity, INamedEntity
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();

            if (await dbContext.Set<T>().AnyAsync(e => e.Name == entity.Name))
            {
                logger.LogWarning("{EntityType} with name {Name} already exists", typeof(T).Name, entity.Name);
                return null;
            }

            entity.Init();
            await dbContext.Set<T>().AddAsync(entity);
            await dbContext.SaveChangesAsync();
            cache.Remove(cacheKey);
            return entity;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating {EntityType} {Name}", typeof(T).Name, entity.Name);
            return null;
        }
    }

    /// <summary>
    /// Finds the existing entity, copies values from the updated entity, saves, and removes the cache entry.
    /// Returns true on success.
    /// </summary>
    public static async Task<bool> UpdateAsync<T>(
        IDbContextFactory<AppDbContext> factory,
        IMemoryCache cache,
        string cacheKey,
        T entity,
        ILogger logger)
        where T : BaseDataBaseEntity
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var existing = await dbContext.Set<T>().FindAsync(entity.Id);
            if (existing is null) return false;

            dbContext.Entry(existing).CurrentValues.SetValues(entity);
            await dbContext.SaveChangesAsync();
            cache.Remove(cacheKey);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating {EntityType} {Id}", typeof(T).Name, entity.Id);
            return false;
        }
    }

    /// <summary>
    /// Finds the entity by ID, removes it, saves, and removes the cache entry.
    /// Supports an optional additional cache key (e.g. for services with dual caches).
    /// Returns true on success.
    /// </summary>
    public static async Task<bool> DeleteAsync<T>(
        IDbContextFactory<AppDbContext> factory,
        IMemoryCache cache,
        string cacheKey,
        Guid id,
        ILogger logger,
        string? additionalCacheKey = null)
        where T : BaseDataBaseEntity
    {
        try
        {
            await using var dbContext = await factory.CreateDbContextAsync();
            var entity = await dbContext.Set<T>().FindAsync(id);
            if (entity is null) return false;

            dbContext.Set<T>().Remove(entity);
            await dbContext.SaveChangesAsync();
            cache.Remove(cacheKey);
            if (additionalCacheKey is not null) cache.Remove(additionalCacheKey);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting {EntityType} {Id}", typeof(T).Name, id);
            return false;
        }
    }
}
