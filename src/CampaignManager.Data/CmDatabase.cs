using CampaignManager.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CampaignManager.Data;

public static class CmDatabase
{
    /// <summary>Имя строки подключения в конфигурации сервера (<c>ConnectionStrings:DefaultConnection</c>).</summary>
    public const string ConnectionStringName = "DefaultConnection";

    /// <summary>
    /// Настройки контекста — одни для сервера, <c>dotnet ef</c>, переноса и тестов.
    /// Журнал миграций — свой, <c>cm.__ef_migrations_history</c>: общий <c>public</c> v1 делит
    /// между двумя контекстами, и чистка «чужих» строк в нём уже ломала <c>database update</c>.
    /// Имена — <c>snake_case</c> (SCHEMA, правило 4), поэтому сырой SQL пишется без кавычек.
    /// </summary>
    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder options,
        string connectionString,
        TimeProvider? timeProvider = null) =>
        options
            .UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CmDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new TimestampsInterceptor(timeProvider ?? TimeProvider.System));

    /// <summary>
    /// Контекст на запрос (scoped): в API, в отличие от circuit v1, фабрика не нужна. Перехватчики
    /// модулей (<see cref="ISaveChangesInterceptor"/> в DI, например журнал правок справочников) — из
    /// того же scope запроса: им нужен вошедший пользователь.
    /// </summary>
    public static IServiceCollection AddCmData(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<CmDbContext>((provider, options) =>
            Configure(options, connectionString, provider.GetService<TimeProvider>())
                .AddInterceptors(provider.GetServices<ISaveChangesInterceptor>()));
}
