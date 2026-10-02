using System.Security.Cryptography;
using CampaignManager.Contracts.Files;
using CampaignManager.Data;
using CampaignManager.Data.Files;
using CampaignManager.Server.Files.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CampaignManager.Server.Files;

/// <summary>Отказ по содержимому запроса — эндпоинт отвечает 400 с этим текстом.</summary>
public sealed class FileRejectedException(string message) : Exception(message);

/// <summary>
/// Строки <c>cm.files</c> и объекты хранилища. Права проверяет эндпоинт через
/// <see cref="FileAccessStub"/> (TODO T1.4: <c>AccessPolicy</c> прямо здесь, в методах записи).
/// </summary>
public sealed class FileService(
    CmDbContext dbContext,
    IObjectStorage storage,
    IOptions<FilesOptions> options,
    TimeProvider time,
    ILogger<FileService> logger)
{
    private const int MaxNameLength = 255;
    private const int MaxUrlLength = 2048;

    /// <summary>
    /// Загрузка. Ключ объекта — <c>&lt;папка&gt;/&lt;sha256&gt;&lt;расширение&gt;</c>: тот же файл второй
    /// раз находится по <c>sha256</c> и возвращает прежнюю строку, а одновременная загрузка двух
    /// одинаковых файлов упирается в уникальный <c>storage_key</c> и тоже получает одну строку.
    /// </summary>
    /// <param name="openRead">Поток файла; зовётся дважды — для хеша и для загрузки.</param>
    public async Task<StoredFileDto> UploadAsync(
        string fileName,
        long length,
        Func<Stream> openRead,
        CancellationToken cancellationToken)
    {
        var name = CleanName(fileName);
        var type = FileTypes.Find(name)
            ?? throw new FileRejectedException($"Такие файлы не принимаются. Можно: {FileTypes.Supported}.");
        if (length <= 0)
        {
            throw new FileRejectedException("Файл пустой.");
        }

        var limit = options.Value.MaxUploadBytes;
        if (length > limit)
        {
            throw new FileRejectedException($"Файл больше {limit / (1024 * 1024)} МБ.");
        }

        string sha256;
        await using (var hashed = openRead())
        {
            sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(hashed, cancellationToken));
        }

        if (await FindBySha256Async(sha256, cancellationToken) is { } existing)
        {
            return ToDto(existing);
        }

        var key = $"{type.Folder}/{sha256}{Path.GetExtension(name).ToLowerInvariant()}";
        await using (var content = openRead())
        {
            await storage.PutAsync(key, content, length, type.ContentType, cancellationToken);
        }

        var file = new StoredFile
        {
            StorageKey = key,
            ContentType = type.ContentType,
            SizeBytes = length,
            Sha256 = sha256,
            OriginalName = name,
            // TODO(T1.4): UploadedById = currentUser.Id.
        };
        dbContext.Files.Add(file);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Тот же файл только что загрузил кто-то ещё: объект один и тот же, строка — его.
            dbContext.ChangeTracker.Clear();
            file = await dbContext.Files.AsNoTracking().SingleAsync(f => f.StorageKey == key, cancellationToken);
        }

        logger.LogInformation("Файл {FileId} загружен: {StorageKey}, {SizeBytes} байт", file.Id, key, length);
        return ToDto(file);
    }

    /// <summary>Внешний адрес — строка без объекта. Тот же адрес второй раз даёт прежнюю строку.</summary>
    public async Task<StoredFileDto> AddExternalAsync(string url, CancellationToken cancellationToken)
    {
        if (url.Length > MaxUrlLength
            || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new FileRejectedException("Нужен полный адрес, начинающийся с https://.");
        }

        var normalized = uri.AbsoluteUri;
        var existing = await dbContext.Files.AsNoTracking()
            .FirstOrDefaultAsync(f => f.ExternalUrl == normalized, cancellationToken);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        var file = new StoredFile { ExternalUrl = normalized };
        dbContext.Files.Add(file);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(file);
    }

    public Task<StoredFile?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Files.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    /// <summary>Сироты: старше <see cref="FilesOptions.OrphanGracePeriod"/>, и никто не ссылается.</summary>
    public async Task<OrphanFilesReport> GetOrphansAsync(CancellationToken cancellationToken)
    {
        var cutoff = OrphanCutoff();
        // EF1002: в SQL склеиваются только имена таблиц и колонок из модели, значения — параметрами.
#pragma warning disable EF1002
        var orphans = await dbContext.Files
            .FromSqlRaw(
                $"select f.* from cm.files f where f.created_at < @cutoff and {FileReferences.NotReferencedSql(dbContext.Model, "f")}",
                new NpgsqlParameter("cutoff", cutoff))
            .AsNoTracking()
            .OrderBy(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
#pragma warning restore EF1002

        return new OrphanFilesReport([.. orphans.Select(ToDto)], orphans.Sum(f => f.SizeBytes ?? 0), cutoff);
    }

    /// <summary>
    /// Удаляет из присланных только тех, кто сирота и сейчас: условие отчёта повторяется в самом
    /// <c>DELETE</c>, поэтому файл, на который сослались после отчёта, останется. Сначала строки,
    /// потом объекты: оставшийся без строки объект — мусор, но не битая картинка.
    /// </summary>
    public async Task<DeleteOrphansResponse> DeleteOrphansAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new DeleteOrphansResponse([], [], []);
        }

        var requested = ids.Distinct().ToArray();
#pragma warning disable EF1002 // см. GetOrphansAsync
        var deleted = await dbContext.Files
            .FromSqlRaw(
                $"""
                 delete from cm.files f
                 where f.id = any(@ids) and f.created_at < @cutoff
                   and {FileReferences.NotReferencedSql(dbContext.Model, "f")}
                 returning f.*
                 """,
                new NpgsqlParameter("ids", requested),
                new NpgsqlParameter("cutoff", OrphanCutoff()))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
#pragma warning restore EF1002

        var notDeleted = new List<string>();
        foreach (var key in deleted.Select(f => f.StorageKey).OfType<string>())
        {
            try
            {
                await storage.DeleteAsync(key, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Строка файла удалена, а объект {StorageKey} — нет", key);
                notDeleted.Add(key);
            }
        }

        var deletedIds = deleted.Select(f => f.Id).ToHashSet();
        logger.LogInformation("Удалено сирот: {Deleted} из {Requested}", deletedIds.Count, requested.Length);
        return new DeleteOrphansResponse([.. deletedIds], [.. requested.Where(id => !deletedIds.Contains(id))], notDeleted);
    }

    public static StoredFileDto ToDto(StoredFile file) => new(
        file.Id,
        FilesRoutes.Content(file.Id),
        file.ExternalUrl,
        file.ContentType,
        file.SizeBytes,
        file.OriginalName,
        file.CreatedAt);

    private DateTimeOffset OrphanCutoff() => time.GetUtcNow() - options.Value.OrphanGracePeriod;

    private Task<StoredFile?> FindBySha256Async(string sha256, CancellationToken cancellationToken) =>
        dbContext.Files.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Sha256 == sha256 && f.StorageKey != null, cancellationToken);

    private static string CleanName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/')).Trim();
        if (name.Length == 0)
        {
            throw new FileRejectedException("У файла нет имени.");
        }

        if (name.Length <= MaxNameLength)
        {
            return name;
        }

        // Длинное имя режется так, чтобы расширение — по нему выбирается тип — уцелело.
        var extension = Path.GetExtension(name);
        return extension.Length < 16 ? name[..(MaxNameLength - extension.Length)] + extension : name[..MaxNameLength];
    }
}
