using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Components.Features.Characters.Services;

public sealed class OccupationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    ILogger<OccupationService> logger)
{
    private const string OccupationsCacheKey = "AllOccupations";

    public Task<List<Occupation>> GetAllOccupationsAsync() =>
        CrudServiceHelper.GetAllCachedAsync<Occupation>(dbContextFactory, cache, OccupationsCacheKey, logger);

    public Task<Occupation?> GetByIdAsync(Guid id) =>
        CrudServiceHelper.GetByIdAsync<Occupation>(dbContextFactory, id, logger);

    public Task<Occupation?> CreateAsync(Occupation occupation) =>
        CrudServiceHelper.CreateAsync(dbContextFactory, cache, OccupationsCacheKey, occupation, logger);

    public Task<bool> UpdateAsync(Occupation occupation) =>
        CrudServiceHelper.UpdateAsync(dbContextFactory, cache, OccupationsCacheKey, occupation, logger);

    public Task<bool> DeleteAsync(Guid id) =>
        CrudServiceHelper.DeleteAsync<Occupation>(dbContextFactory, cache, OccupationsCacheKey, id, logger);

    /// <summary>
    /// Заполняет таблицу профессий начальными данными, если она пуста.
    /// Для существующих профессий обновляет теги, если они не заданы.
    /// </summary>
    public async Task SeedDefaultOccupationsAsync()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var defaults = Occupation.GetDefaultOccupations();

            if (await dbContext.Occupations.AnyAsync())
            {
                logger.LogInformation("Таблица профессий уже содержит данные, сидирование пропущено");

                // Идемпотентный upgrade: проставить теги существующим профессиям без тегов
                await UpgradeOccupationTagsAsync(dbContext, defaults);
                return;
            }

            foreach (var occupation in defaults)
                occupation.Init();

            await dbContext.Occupations.AddRangeAsync(defaults);
            await dbContext.SaveChangesAsync();
            cache.Remove(OccupationsCacheKey);

            logger.LogInformation("Загружено {Count} профессий по умолчанию", defaults.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при заполнении таблицы профессий");
        }
    }

    private async Task UpgradeOccupationTagsAsync(AppDbContext dbContext, List<Occupation> defaults)
    {
        var defaultsByName = defaults
            .ToDictionary(d => d.Name, d => d.Tags);

        var occupationsWithoutTags = await dbContext.Occupations
            .Where(o => o.Tags == OccupationTag.None)
            .ToListAsync();

        var updated = 0;
        foreach (var occupation in occupationsWithoutTags)
        {
            if (defaultsByName.TryGetValue(occupation.Name, out var tags) && tags != OccupationTag.None)
            {
                occupation.Tags = tags;
                updated++;
            }
        }

        if (updated > 0)
        {
            await dbContext.SaveChangesAsync();
            cache.Remove(OccupationsCacheKey);
            logger.LogInformation("Обновлены теги у {Count} профессий", updated);
        }
    }
}
