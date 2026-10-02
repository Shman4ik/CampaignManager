// Перенос v1 → v2 (docs/v2/SCHEMA.md, «Перенос данных»; T1.3). Как запускать — tools/CampaignManager.Migrate/CLAUDE.md.
//
//   dotnet run --project tools/CampaignManager.Migrate -- --settings <файл> [--reset] [--report <путь>]
//                                                           [--skip-files] [--keeper-time-zone Europe/Moscow]
//                                                           [--note «строка в начало отчёта»]
//
// Строка подключения — CM_DB (как у dotnet ef), иначе ConnectionStrings:Cm из файла настроек. Настройки MinIO —
// секция Minio файла: Endpoint, AccessKey, SecretKey, Secure, SourceBucket (боевой, только чтение), TargetBucket.

using System.Text.Json;
using CampaignManager.Migrate;
using CampaignManager.Migrate.Files;

var reset = args.Contains("--reset");
var skipFiles = args.Contains("--skip-files");
string? Arg(string name) => Array.IndexOf(args, name) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : null;

JsonElement? settings = Arg("--settings") is { } path
    ? JsonDocument.Parse(await File.ReadAllTextAsync(path)).RootElement
    : null;
string? Setting(string section, string key) =>
    settings is { } root && root.TryGetProperty(section, out var s) && s.TryGetProperty(key, out var v)
        ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText()
        : null;

var connectionString = Environment.GetEnvironmentVariable("CM_DB") is { Length: > 0 } fromEnv
    ? fromEnv
    : Setting("ConnectionStrings", "Cm");
if (connectionString is null)
{
    Console.Error.WriteLine("Нет строки подключения: CM_DB или ConnectionStrings:Cm в --settings.");
    return 2;
}

IFileStore files;
if (skipFiles)
{
    files = new NoFileStore();
}
else if (Setting("Minio", "Endpoint") is { } endpoint)
{
    files = new MinioFileStore(new MinioSettings(
        endpoint,
        Setting("Minio", "AccessKey") ?? "",
        Setting("Minio", "SecretKey") ?? "",
        !string.Equals(Setting("Minio", "Secure"), "false", StringComparison.OrdinalIgnoreCase),
        Setting("Minio", "SourceBucket") ?? throw new InvalidOperationException("Нет Minio:SourceBucket."),
        Setting("Minio", "TargetBucket") ?? throw new InvalidOperationException("Нет Minio:TargetBucket.")));
}
else
{
    Console.Error.WriteLine("Нет настроек Minio в --settings (или --skip-files, чтобы не трогать файлы).");
    return 2;
}

var options = new MigrationOptions { KeeperTimeZone = Arg("--keeper-time-zone") ?? new MigrationOptions().KeeperTimeZone };
var started = DateTimeOffset.UtcNow;
try
{
    var report = await new Migrator(connectionString, files, options).RunAsync(reset, CancellationToken.None);
    var header = files is MinioFileStore minio
        ? $"Файлы: в бакете среды {minio.Copied + minio.Present} объектов, {(minio.CopiedBytes + minio.PresentBytes) / 1_000_000.0:0.0} МБ "
          + $"(в этом прогоне скопировано {minio.Copied}, остальные — прошлыми прогонами)."
        : "Файлы: без хранилища (--skip-files).";
    if (Arg("--note") is { } note)
    {
        header = note + "\n\n" + header;
    }

    var markdown = report.ToMarkdown(started, header);
    if (Arg("--report") is { } reportPath)
    {
        await File.WriteAllTextAsync(reportPath, markdown);
        Console.WriteLine($"Отчёт: {reportPath}");
    }
    else
    {
        Console.WriteLine(markdown);
    }

    foreach (var row in report.Counts)
    {
        Console.WriteLine($"{row.Source,-28} {row.V1,6} → {row.Target,-22} {row.V2,6}  {row.Note}");
    }

    Console.WriteLine($"Готово за {(DateTimeOffset.UtcNow - started).TotalSeconds:0} с.");
    return 0;
}
finally
{
    (files as IDisposable)?.Dispose();
}
