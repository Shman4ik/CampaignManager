using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace CampaignManager.Server.Files.Storage;

/// <summary>Секция <c>Minio</c> — те же ключи, что у v1, чтобы настройки переносились как есть.</summary>
public sealed class MinioOptions
{
    public const string Section = "Minio";

    public string? Endpoint { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public bool Secure { get; set; } = true;
    public string? BucketName { get; set; }

    /// <summary>
    /// Регион для подписи ссылок. Пусто — SDK один раз спросит его у бакета и запомнит.
    /// </summary>
    public string? Region { get; set; }
}

/// <summary>
/// MinIO. Синглтон: один клиент и один <see cref="HttpClient"/> на приложение (в v1 клиент
/// создавался на каждый scope и перед каждой загрузкой звал <c>BucketExists</c>). Бакет должен
/// существовать заранее — создаёт его администратор, а не загрузка.
/// <para>
/// Таймаута у <see cref="HttpClient"/> нет намеренно: длинный трек не должен обрываться через
/// 100 (или, как у resilience-обработчика v1, 10) секунд. Запросы отменяет токен — для отдачи это
/// <c>RequestAborted</c>, то есть ушедший со страницы браузер.
/// </para>
/// </summary>
public sealed class MinioObjectStorage : IObjectStorage, IDisposable
{
    /// <summary>Ссылка нужна на один запрос и наружу не выходит — минуты хватает с запасом.</summary>
    private const int PresignedExpirySeconds = 60;

    private readonly HttpClient _http = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

    private readonly Lazy<(IMinioClient Client, string Bucket)> _minio;

    /// <summary>
    /// Клиент собирается при первом обращении: без настроек MinIO сервер стартует и работает,
    /// а запрос к файлам падает с понятным сообщением о недостающей настройке.
    /// </summary>
    public MinioObjectStorage(IOptions<MinioOptions> options) =>
        _minio = new Lazy<(IMinioClient, string)>(() => Build(options.Value, _http));

    private IMinioClient Minio => _minio.Value.Client;

    private string Bucket => _minio.Value.Bucket;

    public async Task PutAsync(string key, Stream content, long size, string contentType, CancellationToken cancellationToken) =>
        await Minio.PutObjectAsync(new PutObjectArgs()
            .WithBucket(Bucket)
            .WithObject(key)
            .WithStreamData(content)
            .WithObjectSize(size)
            .WithContentType(contentType), cancellationToken);

    public async Task<Stream> OpenReadAsync(string key, ByteRange? range, CancellationToken cancellationToken)
    {
        // Обычный HTTP по короткоживущей presigned-ссылке, а не GetObjectAsync SDK. Тому нужен
        // колбэк, в котором поток надо дочитать до выхода (v1 копировал его в MemoryStream), а
        // с WithOffsetAndLength он ещё и делает StatObject с тем же Range, получает 206 и падает
        // с PartialContentException. Здесь Range уходит один раз, а ResponseHeadersRead отдаёт
        // поток как есть — байты идут из хранилища в ответ без копии в памяти.
        var url = await Minio.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(Bucket)
            .WithObject(key)
            .WithExpiry(PresignedExpirySeconds));

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (range is { } r)
        {
            request.Headers.Range = new RangeHeaderValue(r.From, r.To);
        }

        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response.Dispose();
            throw new StoredObjectNotFoundException(key);
        }

        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new HttpRequestException($"MinIO ответил {(int)response.StatusCode} на «{key}».", null, response.StatusCode);
        }

        // Dispose потока закрывает ответ и возвращает соединение в пул.
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    public async Task<long?> GetSizeAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var stat = await Minio.StatObjectAsync(new StatObjectArgs().WithBucket(Bucket).WithObject(key), cancellationToken);
            return stat.Size;
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        Minio.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(Bucket).WithObject(key), cancellationToken);

    public void Dispose()
    {
        if (_minio.IsValueCreated)
        {
            _minio.Value.Client.Dispose();
        }

        _http.Dispose();
    }

    private static (IMinioClient, string) Build(MinioOptions settings, HttpClient http)
    {
        var builder = new MinioClient()
            .WithEndpoint(Required(settings.Endpoint, nameof(MinioOptions.Endpoint)))
            .WithCredentials(
                Required(settings.AccessKey, nameof(MinioOptions.AccessKey)),
                Required(settings.SecretKey, nameof(MinioOptions.SecretKey)))
            .WithSSL(settings.Secure)
            .WithHttpClient(http);
        if (!string.IsNullOrWhiteSpace(settings.Region))
        {
            builder = builder.WithRegion(settings.Region);
        }

        return (builder.Build(), Required(settings.BucketName, nameof(MinioOptions.BucketName)));
    }

    private static string Required(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Нет настройки {MinioOptions.Section}:{name} — файлы недоступны.")
            : value;
}
