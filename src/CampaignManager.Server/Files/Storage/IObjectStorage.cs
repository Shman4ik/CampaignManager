namespace CampaignManager.Server.Files.Storage;

/// <summary>
/// Хранилище объектов (MinIO). Интерфейс нужен ради тестов: интеграционные тесты API идут на
/// хранилище в памяти, а <see cref="MinioObjectStorage"/> проверяется отдельно на MinIO в wslc.
/// </summary>
public interface IObjectStorage
{
    Task PutAsync(string key, Stream content, long size, string contentType, CancellationToken cancellationToken);

    /// <summary>
    /// Поток объекта целиком или отрезок <paramref name="range"/> (границы включительно).
    /// Закрывает поток вызывающий. Нет объекта — <see cref="StoredObjectNotFoundException"/>.
    /// </summary>
    Task<Stream> OpenReadAsync(string key, ByteRange? range, CancellationToken cancellationToken);

    /// <summary>Размер объекта или <c>null</c>, если его нет.</summary>
    Task<long?> GetSizeAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

/// <summary>Отрезок байтов, обе границы включительно — как в <c>Range: bytes=From-To</c>.</summary>
public readonly record struct ByteRange(long From, long To)
{
    public long Length => To - From + 1;
}

public sealed class StoredObjectNotFoundException(string key)
    : Exception($"В хранилище нет объекта «{key}».");
