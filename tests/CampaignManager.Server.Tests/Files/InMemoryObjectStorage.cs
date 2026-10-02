using System.Collections.Concurrent;
using CampaignManager.Server.Files.Storage;

namespace CampaignManager.Server.Tests.Files;

/// <summary>
/// Хранилище в памяти для тестов API: CI поднимает только Postgres. Настоящий MinIO проверяет
/// <see cref="MinioObjectStorageTests"/>.
/// </summary>
public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, (byte[] Content, string ContentType)> _objects = new();

    public IReadOnlyCollection<string> Keys => [.. _objects.Keys];

    /// <summary>Отрезки, которые у хранилища попросили, — чтобы видеть, что Range дошёл до него.</summary>
    public ConcurrentQueue<ByteRange?> Reads { get; } = new();

    public async Task PutAsync(string key, Stream content, long size, string contentType, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length != size)
        {
            throw new InvalidOperationException($"Заявлено {size} байт, пришло {buffer.Length}.");
        }

        _objects[key] = (buffer.ToArray(), contentType);
    }

    public Task<Stream> OpenReadAsync(string key, ByteRange? range, CancellationToken cancellationToken)
    {
        if (!_objects.TryGetValue(key, out var stored))
        {
            throw new StoredObjectNotFoundException(key);
        }

        Reads.Enqueue(range);
        var bytes = range is { } r ? stored.Content[(int)r.From..(int)(r.To + 1)] : stored.Content;
        return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }

    public Task<long?> GetSizeAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(_objects.TryGetValue(key, out var stored) ? stored.Content.LongLength : (long?)null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public void Remove(string key) => _objects.TryRemove(key, out _);
}
