using CampaignManager.Server.Files.Storage;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Xunit;

namespace CampaignManager.Server.Tests.Files;

/// <summary>
/// Настоящий MinIO: <c>CM_TEST_MINIO=endpoint;accessKey;secretKey</c> (локально — MinIO в wslc, см.
/// <c>Server/Files/CLAUDE.md</c>); без переменной тесты пропускаются — в CI MinIO нет. Каждый
/// прогон заводит свой бакет и удаляет его.
/// </summary>
public sealed class MinioObjectStorageTests : IAsyncLifetime
{
    public const string Variable = "CM_TEST_MINIO";

    private static readonly string[]? Settings =
        Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } value ? value.Split(';') : null;

    private readonly string _bucket = $"cm-test-{Guid.NewGuid():N}"[..40];
    private IMinioClient? _admin;
    private MinioObjectStorage? _storage;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private MinioObjectStorage Storage => _storage!;

    public async ValueTask InitializeAsync()
    {
        if (Settings is not [var endpoint, var accessKey, var secretKey])
        {
            return;
        }

        _admin = new MinioClient().WithEndpoint(endpoint).WithCredentials(accessKey, secretKey).WithSSL(false).Build();
        await _admin.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucket));
        _storage = new MinioObjectStorage(Options.Create(new MinioOptions
        {
            Endpoint = endpoint, AccessKey = accessKey, SecretKey = secretKey, Secure = false, BucketName = _bucket,
        }));
    }

    public async ValueTask DisposeAsync()
    {
        _storage?.Dispose();
        if (_admin is null)
        {
            return;
        }

        await foreach (var item in _admin.ListObjectsEnumAsync(new ListObjectsArgs().WithBucket(_bucket).WithRecursive(true)))
        {
            await _admin.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_bucket).WithObject(item.Key));
        }

        await _admin.RemoveBucketAsync(new RemoveBucketArgs().WithBucket(_bucket));
        _admin.Dispose();
    }

    [Fact]
    public async Task Object_round_trips_whole_and_by_range()
    {
        SkipIfMissing();
        var bytes = new byte[300_000];
        Random.Shared.NextBytes(bytes);

        await Storage.PutAsync("music/test.mp3", new MemoryStream(bytes), bytes.Length, "audio/mpeg", Cancellation);

        Assert.Equal(bytes.Length, await Storage.GetSizeAsync("music/test.mp3", Cancellation));
        Assert.Equal(bytes, await ReadAsync(null));
        // Отрезок из середины — так выглядит перемотка. Через GetObjectArgs SDK тут падал
        // с PartialContentException; presigned-ссылка отдаёт 206 как есть.
        Assert.Equal(bytes[200_000..250_001], await ReadAsync(new ByteRange(200_000, 250_000)));
        Assert.Equal(bytes[^1..], await ReadAsync(new ByteRange(bytes.Length - 1, bytes.Length - 1)));

        await Storage.DeleteAsync("music/test.mp3", Cancellation);
        Assert.Null(await Storage.GetSizeAsync("music/test.mp3", Cancellation));

        async Task<byte[]> ReadAsync(ByteRange? range)
        {
            await using var stream = await Storage.OpenReadAsync("music/test.mp3", range, Cancellation);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, Cancellation);
            return buffer.ToArray();
        }
    }

    [Fact]
    public async Task Missing_object_is_reported_as_not_found()
    {
        SkipIfMissing();

        Assert.Null(await Storage.GetSizeAsync("images/нет.png", Cancellation));
        await Assert.ThrowsAsync<StoredObjectNotFoundException>(() =>
            Storage.OpenReadAsync("images/нет.png", null, Cancellation));
    }

    private static void SkipIfMissing() =>
        Assert.SkipWhen(Settings is null, $"Нет {Variable}: тест с MinIO пропущен.");
}
