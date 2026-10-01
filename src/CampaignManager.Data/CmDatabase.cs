using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CampaignManager.Data;

public static class CmDatabase
{
    /// <summary>Имя строки подключения в конфигурации сервера (<c>ConnectionStrings:DefaultConnection</c>).</summary>
    public const string ConnectionStringName = "DefaultConnection";

    /// <summary>
    /// Настройки контекста — одни для сервера, <c>dotnet ef</c> и тестов.
    /// Журнал миграций — свой, <c>cm.__ef_migrations_history</c>: общий <c>public</c> v1 делит
    /// между двумя контекстами, и чистка «чужих» строк в нём уже ломала <c>database update</c>.
    /// </summary>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CmDbContext.Schema));

    /// <summary>Контекст на запрос (scoped): в API, в отличие от circuit v1, фабрика не нужна.</summary>
    public static IServiceCollection AddCmData(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<CmDbContext>(options => Configure(options, connectionString));
}
