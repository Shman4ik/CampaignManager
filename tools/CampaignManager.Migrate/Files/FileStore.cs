using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace CampaignManager.Migrate.Files;

/// <summary>Объект хранилища после переноса: размер и тип для строки <c>cm.files</c>.</summary>
public sealed record StoredObject(long? Size, string? ContentType);

/// <summary>
/// Куда смотрят строки <c>cm.files</c> после переноса. Объект v1 копируется из боевого бакета в бакет
/// среды (<c>campaign-manager-dev</c> на ветке dev) под тем же ключом; боевой бакет только читается.
/// </summary>
public interface IFileStore
{
    /// <summary>Есть объект в целевом бакете (скопирован) — его размер и тип; нет в источнике — null.</summary>
    Task<StoredObject?> EnsureAsync(string key, CancellationToken cancellationToken);
}

/// <summary>Без хранилища (<c>--skip-files</c>, тесты): строки заводятся, объекты не трогаются, размера нет.</summary>
public sealed class NoFileStore : IFileStore
{
    public Task<StoredObject?> EnsureAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult<StoredObject?>(new StoredObject(null, null));
}

public sealed record MinioSettings(string Endpoint, string AccessKey, string SecretKey, bool Secure, string SourceBucket, string TargetBucket);

/// <summary>
/// MinIO: копия на стороне сервера (<c>CopyObject</c>) — 660 МБ v1 не идут через машину, где запущен перенос.
/// Уже скопированный объект второй раз не копируется: повторный прогон (<c>--reset</c>) только сверяет.
/// Если источник и цель — один бакет, ничего не копируется (так будет на проде, T3.2).
/// </summary>
public sealed class MinioFileStore(MinioSettings settings) : IFileStore, IDisposable
{
    private readonly IMinioClient _client = new MinioClient()
        .WithEndpoint(settings.Endpoint)
        .WithCredentials(settings.AccessKey, settings.SecretKey)
        .WithSSL(settings.Secure)
        .Build();

    public int Copied { get; private set; }

    public long CopiedBytes { get; private set; }

    /// <summary>Уже лежали в бакете среды (скопированы прошлым прогоном).</summary>
    public int Present { get; private set; }

    public long PresentBytes { get; private set; }

    public async Task<StoredObject?> EnsureAsync(string key, CancellationToken cancellationToken)
    {
        if (await StatAsync(settings.TargetBucket, key, cancellationToken) is { } existing)
        {
            Present++;
            PresentBytes += existing.Size ?? 0;
            return existing;
        }

        if (string.Equals(settings.SourceBucket, settings.TargetBucket, StringComparison.Ordinal)
            || await StatAsync(settings.SourceBucket, key, cancellationToken) is null)
        {
            return null;
        }

        await _client.CopyObjectAsync(new CopyObjectArgs()
            .WithBucket(settings.TargetBucket)
            .WithObject(key)
            .WithCopyObjectSource(new CopySourceObjectArgs().WithBucket(settings.SourceBucket).WithObject(key)),
            cancellationToken);

        var copied = await StatAsync(settings.TargetBucket, key, cancellationToken);
        Copied++;
        CopiedBytes += copied?.Size ?? 0;
        return copied;
    }

    public void Dispose() => _client.Dispose();

    private async Task<StoredObject?> StatAsync(string bucket, string key, CancellationToken cancellationToken)
    {
        try
        {
            var stat = await _client.StatObjectAsync(new StatObjectArgs().WithBucket(bucket).WithObject(key), cancellationToken);
            return new StoredObject(stat.Size, stat.ContentType);
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
    }
}

/// <summary>Тип по расширению — тот же список, что у загрузки (<c>Server/Files/FileTypes</c>).</summary>
public static class FileContentTypes
{
    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".avif"] = "image/avif",
        [".mp3"] = "audio/mpeg",
        [".ogg"] = "audio/ogg",
        [".opus"] = "audio/ogg",
        [".m4a"] = "audio/mp4",
        [".aac"] = "audio/aac",
        [".wav"] = "audio/wav",
        [".flac"] = "audio/flac",
    };

    public static string? Of(string key) => ByExtension.GetValueOrDefault(Path.GetExtension(key));
}
