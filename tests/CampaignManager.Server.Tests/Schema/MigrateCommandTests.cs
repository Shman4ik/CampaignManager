using CampaignManager.Data;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace CampaignManager.Server.Tests.Schema;

/// <summary>
/// Команда <c>migrate</c> (Job хука PreSync в Argo CD): пустую базу поднимает всеми миграциями, повторный запуск — без
/// изменений и с кодом 0, без строки подключения — код 1, а не исключение (Job должен упасть понятно).
/// </summary>
public sealed class MigrateCommandTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrates_an_empty_database_and_is_idempotent()
    {
        TestDatabase.SkipIfMissing();
        var server = TestDatabase.ConnectionString!;
        var target = new NpgsqlConnectionStringBuilder(server) { Database = $"cm_migrate_{Guid.NewGuid():N}" };
        await ExecuteAsync(server, $"CREATE DATABASE {target.Database}");
        try
        {
            var configuration = Configuration(target.ConnectionString);

            var first = new StringWriter();
            Assert.Equal(0, await MigrateCommand.RunAsync(configuration, first, Cancellation));
            Assert.Contains("Миграции к накату", first.ToString());

            var options = new DbContextOptionsBuilder<CmDbContext>();
            CmDatabase.Configure(options, target.ConnectionString);
            await using (var db = new CmDbContext(options.Options))
            {
                Assert.Empty(await db.Database.GetPendingMigrationsAsync(Cancellation));
            }

            var second = new StringWriter();
            Assert.Equal(0, await MigrateCommand.RunAsync(configuration, second, Cancellation));
            Assert.Contains("актуальна", second.ToString());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteAsync(server, $"DROP DATABASE IF EXISTS {target.Database} WITH (FORCE)");
        }
    }

    [Fact]
    public async Task Fails_with_exit_code_without_connection_string()
    {
        var output = new StringWriter();

        Assert.Equal(1, await MigrateCommand.RunAsync(new ConfigurationBuilder().Build(), output, Cancellation));
        Assert.Contains("DefaultConnection", output.ToString());
    }

    private static IConfiguration Configuration(string connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{CmDatabase.ConnectionStringName}"] = connectionString,
            })
            .Build();

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Cancellation);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Cancellation);
    }
}
