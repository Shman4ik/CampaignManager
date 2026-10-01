using System.Text.Json;
using CampaignManager.Core.Campaigns;
using CampaignManager.Data;
using CampaignManager.Data.Campaigns;
using CampaignManager.Data.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CampaignManager.Server.Tests.Schema;

/// <summary>
/// Своя одноразовая база на сервере из <c>CM_TEST_DB</c>: схема <c>cm</c> поднимается миграциями,
/// как на ветке Neon, а после тестов база удаляется. Общую базу <c>CM_TEST_DB</c> тесты схемы не трогают.
/// </summary>
public sealed class SchemaDatabase : IAsyncLifetime
{
    private string? _connectionString;

    public string ConnectionString => _connectionString
        ?? throw new InvalidOperationException("База не поднята: тест должен начинаться с TestDatabase.SkipIfMissing().");

    public async ValueTask InitializeAsync()
    {
        if (TestDatabase.ConnectionString is not { } server)
        {
            return;
        }

        var builder = new NpgsqlConnectionStringBuilder(server) { Database = $"cm_schema_{Guid.NewGuid():N}" };
        await using (var admin = new NpgsqlConnection(server))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {builder.Database}", admin);
            await create.ExecuteNonQueryAsync();
        }

        _connectionString = builder.ConnectionString;
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connectionString is null || TestDatabase.ConnectionString is not { } server)
        {
            return;
        }

        var database = new NpgsqlConnectionStringBuilder(_connectionString).Database;
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(server);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    public CmDbContext CreateContext(TimeProvider? time = null)
    {
        var options = new DbContextOptionsBuilder<CmDbContext>();
        CmDatabase.Configure(options, ConnectionString, time);
        return new CmDbContext(options.Options);
    }

    public async Task<User> AddUserAsync(string name = "Сыщик")
    {
        await using var context = CreateContext();
        var user = new User { Email = $"{Guid.NewGuid():N}@example.test", DisplayName = name };
        context.Users.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    /// <summary>Кампания с Хранителем и, если задан, игроком-участником.</summary>
    public async Task<Campaign> AddCampaignAsync(User keeper, User? player = null)
    {
        await using var context = CreateContext();
        var campaign = new Campaign { Name = "Маски Ньярлатхотепа" };
        campaign.Members.Add(new CampaignMember { UserId = keeper.Id, Role = CampaignRole.Keeper });
        if (player is not null)
        {
            campaign.Members.Add(new CampaignMember { UserId = player.Id, Role = CampaignRole.Player });
        }

        context.Campaigns.Add(campaign);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return campaign;
    }

    public static JsonDocument Sheet(string name) =>
        JsonDocument.Parse(JsonSerializer.Serialize(new { personal = new { name, occupation = "Антиквар" } }));
}
