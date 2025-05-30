using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Utilities.DataBase;

/// <summary>
/// Инициализатор базы данных для заполнения начальных данных
/// </summary>
public class DbInitializer(IDbContextFactory<AppDbContext> dbContextFactory, ILogger<DbInitializer> logger)
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<DbInitializer> _logger = logger;

    /// <summary>
    /// Инициализировать базу данных начальными данными
    /// </summary>
    /// <returns>Асинхронная задача</returns>
    public async Task InitializeDatabaseAsync()
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            _logger.LogInformation("База данных успешно инициализирована");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации базы данных");
            throw;
        }
    }


}
