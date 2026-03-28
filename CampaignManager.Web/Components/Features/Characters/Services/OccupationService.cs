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
    /// Заполняет таблицу профессий начальными данными, если она пуста
    /// </summary>
    public async Task SeedDefaultOccupationsAsync()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            if (await dbContext.Occupations.AnyAsync())
            {
                logger.LogInformation("Таблица профессий уже содержит данные, сидирование пропущено");
                return;
            }

            var defaults = Occupation.GetDefaultOccupations();
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
}
