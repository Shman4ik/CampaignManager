using CampaignManager.Data;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Platform;

/// <summary>
/// <c>dotnet CampaignManager.Server.dll migrate</c> — накатить миграции схемы <c>cm</c> и выйти, без веб-хоста. Тот же образ,
/// что у сервера, запускает её Job хука Argo CD <c>PreSync</c> (dmnet-gitops, <c>workloads/campaign-manager</c>): упала
/// миграция — синхронизация останавливается, и прод остаётся на прежнем образе. Строка — та же
/// <c>ConnectionStrings:DefaultConnection</c>; одновременный запуск разводит блокировка миграций EF.
/// </summary>
public static class MigrateCommand
{
    public const string Name = "migrate";

    /// <summary>Миграция с данными на всю таблицу дольше стандартных 30 с, а холодный старт Neon добавляет ещё секунды.</summary>
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(10);

    public static async Task<int> RunAsync(IConfiguration configuration, TextWriter output, CancellationToken cancellation = default)
    {
        if (configuration.GetConnectionString(CmDatabase.ConnectionStringName) is not { Length: > 0 } connectionString)
        {
            await output.WriteLineAsync($"Нет строки подключения ConnectionStrings:{CmDatabase.ConnectionStringName}.");
            return 1;
        }

        var options = new DbContextOptionsBuilder<CmDbContext>();
        CmDatabase.Configure(options, connectionString);
        await using var db = new CmDbContext(options.Options);
        db.Database.SetCommandTimeout(CommandTimeout);

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellation)).ToList();
        if (pending.Count == 0)
        {
            await output.WriteLineAsync("Миграции: схема cm актуальна.");
            return 0;
        }

        await output.WriteLineAsync($"Миграции к накату ({pending.Count}): {string.Join(", ", pending)}");
        await db.Database.MigrateAsync(cancellation);
        await output.WriteLineAsync("Миграции накатаны.");
        return 0;
    }
}
