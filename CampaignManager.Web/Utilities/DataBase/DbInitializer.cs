using CampaignManager.Web.Components.Features.Characters.Services;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Utilities.DataBase;

/// <summary>
/// Инициализатор базы данных для заполнения начальных данных
/// </summary>
public class DbInitializer(
    IDbContextFactory<AppDbContext> dbContextFactory,
    OccupationService occupationService,
    LlmKnowledgeSeeder llmKnowledgeSeeder,
    ILogger<DbInitializer> logger)
{
    /// <summary>
    /// Инициализировать базу данных начальными данными
    /// </summary>
    public async Task InitializeDatabaseAsync()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            await dbContext.Database.MigrateAsync();

            await occupationService.SeedDefaultOccupationsAsync();
            await llmKnowledgeSeeder.SeedAsync();

            logger.LogInformation("База данных успешно инициализирована");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при инициализации базы данных");
            throw;
        }
    }
}