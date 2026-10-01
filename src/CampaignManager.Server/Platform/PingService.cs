using System.Diagnostics;
using System.Reflection;
using CampaignManager.Contracts.Platform;
using CampaignManager.Data;

namespace CampaignManager.Server.Platform;

public sealed class PingService(CmDbContext dbContext, IHostEnvironment environment)
{
    private static readonly string ServerVersion = ReadServerVersion();

    public async Task<PingResponse> PingAsync(CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var databaseTime = await dbContext.GetDatabaseTimeAsync(cancellationToken);
        var latency = Stopwatch.GetElapsedTime(started);

        return new PingResponse(
            DateTimeOffset.UtcNow,
            databaseTime,
            Math.Round(latency.TotalMilliseconds, 1),
            ServerVersion,
            environment.EnvironmentName);
    }

    // «1.0.0+<sha коммита>» — хватает короткого хеша.
    private static string ReadServerVersion()
    {
        var version = typeof(PingService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var plus = version.IndexOf('+');
        return plus >= 0 && version.Length > plus + 8 ? version[..(plus + 8)] : version;
    }
}
