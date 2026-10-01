using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CampaignManager.Data;

/// <summary>
/// Контекст для <c>dotnet ef</c>: без него инструменты поднимали бы <c>Program.cs</c> сервера
/// вместе с Auth0 и требовали бы всю его конфигурацию. Строка подключения — из <c>CM_DB</c>;
/// без неё — локальный Postgres (для <c>migrations add</c> база не нужна вовсе).
/// </summary>
public sealed class DesignTimeCmDbContextFactory : IDesignTimeDbContextFactory<CmDbContext>
{
    public const string ConnectionStringVariable = "CM_DB";

    private const string LocalDatabase =
        "Host=localhost;Port=5432;Database=campaignmanager;Username=postgres;Password=postgres";

    public CmDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable) ?? LocalDatabase;
        var options = new DbContextOptionsBuilder<CmDbContext>();
        CmDatabase.Configure(options, connectionString);
        return new CmDbContext(options.Options);
    }
}
