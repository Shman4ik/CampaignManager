using CampaignManager.Server.Files.Storage;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.Net.Http.Headers;

namespace CampaignManager.Server.Files;

/// <summary>
/// <c>GET</c>/<c>HEAD /api/v1/files/{id}</c> — содержимое файла <b>со своего origin и с Range</b>.
/// <list type="bullet">
/// <item>Свой origin, а не presigned-ссылка на S3: плеер ведёт звук через Web Audio (на iPad громкость
/// иначе не меняется), а <c>createMediaElementSource</c> на чужом origin без CORS отдаёт тишину.</item>
/// <item>Range разбирается здесь, а не <c>enableRangeProcessing</c>: тому нужен перематываемый поток,
/// то есть объект целиком. Здесь отрезок запрашивается у хранилища и идёт в ответ потоком, поэтому
/// перемотка стоит одного короткого запроса, а не выкачки трека. Без <c>Accept-Ranges</c> браузер
/// не считает источник перематываемым, а Safari ещё и капризничает с воспроизведением.</item>
/// <item>Ключи неизменяемы (содержимое строки не меняется никогда), поэтому <c>immutable</c> и
/// <c>ETag</c> по id. В v1 картинки шли без кэша и целиком через <c>MemoryStream</c>.</item>
/// </list>
/// </summary>
public static class FileContentEndpoint
{
    // private: ответ зависит от входа, общим кэшам его держать нельзя.
    private const string CacheControl = "private, max-age=31536000, immutable";

    public static async Task<IResult> HandleAsync(
        Guid id,
        HttpContext http,
        FileService files,
        IObjectStorage storage)
    {
        // Читать — любой вошедший: эндпоинт под RequireAuthorization (FilesModule).
        var cancellationToken = http.RequestAborted;
        var file = await files.FindAsync(id, cancellationToken);
        if (file is null)
        {
            return TypedResults.NotFound();
        }

        if (file.ExternalUrl is { } externalUrl)
        {
            return TypedResults.Redirect(externalUrl);
        }

        var key = file.StorageKey!;

        // Размер есть в строке; у перенесённых без него (T1.3 берёт его StatObject) — спросим хранилище.
        var size = file.SizeBytes ?? await storage.GetSizeAsync(key, cancellationToken);
        if (size is not { } length)
        {
            return TypedResults.NotFound();
        }

        var request = http.Request.GetTypedHeaders();
        var response = http.Response;
        var etag = new EntityTagHeaderValue($"\"{file.Id:N}\"");

        response.Headers.ETag = etag.ToString();
        response.Headers.CacheControl = CacheControl;
        response.Headers.AcceptRanges = "bytes";
        // Файл с нашего origin не должен ни угадываться браузером, ни исполняться, если его
        // открыть отдельной вкладкой.
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";

        if (request.IfNoneMatch.Any(tag => tag.Equals(EntityTagHeaderValue.Any) || tag.Compare(etag, useStrongComparison: false)))
        {
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        var range = ParseRange(request, etag, length);
        if (range is RangeRequest.Unsatisfiable)
        {
            response.Headers.ContentRange = $"bytes */{length}";
            return TypedResults.StatusCode(StatusCodes.Status416RangeNotSatisfiable);
        }

        var slice = (range as RangeRequest.Slice)?.Range;
        response.ContentType = file.ContentType ?? "application/octet-stream";
        response.ContentLength = slice?.Length ?? length;
        if (slice is { } partial)
        {
            response.StatusCode = StatusCodes.Status206PartialContent;
            response.Headers.ContentRange = $"bytes {partial.From}-{partial.To}/{length}";
        }

        if (HttpMethods.IsHead(http.Request.Method))
        {
            return TypedResults.Empty;
        }

        Stream content;
        try
        {
            content = await storage.OpenReadAsync(key, slice, cancellationToken);
        }
        catch (StoredObjectNotFoundException)
        {
            // Заголовки успеха сбрасываются: 404 с immutable браузер запомнил бы на год.
            response.Headers.ContentRange = default;
            response.Headers.CacheControl = "no-store";
            response.Headers.ETag = default;
            response.ContentLength = null;
            response.StatusCode = StatusCodes.Status200OK;
            return TypedResults.NotFound();
        }

        try
        {
            await using (content)
            {
                await content.CopyToAsync(response.Body, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException && cancellationToken.IsCancellationRequested)
        {
            // Перемотка: браузер бросает прежний запрос и шлёт новый Range. Это не ошибка.
        }

        return TypedResults.Empty;
    }

    /// <summary>
    /// <c>Range: bytes=…</c> — один отрезок в любой из трёх форм: <c>1024-</c> (перемотка),
    /// <c>0-1</c> (так Safari узнаёт размер) и <c>-N</c> (хвост с метаданными). Несколько отрезков
    /// и чужие единицы игнорируются — ответ целиком, это разрешено RFC 9110. <c>If-Range</c> с
    /// чужим ETag или датой — тоже целиком: Last-Modified мы не отдаём.
    /// </summary>
    internal static RangeRequest ParseRange(RequestHeaders request, EntityTagHeaderValue etag, long length)
    {
        if (request.Range is not { } header
            || !string.Equals(header.Unit.Value, "bytes", StringComparison.OrdinalIgnoreCase)
            || header.Ranges.Count != 1)
        {
            return RangeRequest.Whole.Instance;
        }

        if (request.IfRange is { } ifRange
            && (ifRange.EntityTag is not { } tag || !tag.Compare(etag, useStrongComparison: true)))
        {
            return RangeRequest.Whole.Instance;
        }

        var item = header.Ranges.Single();
        long from, to;
        if (item.From is null)
        {
            // bytes=-N: последние N байт.
            if (item.To is not > 0)
            {
                return RangeRequest.Unsatisfiable.Instance;
            }

            from = Math.Max(0, length - item.To.Value);
            to = length - 1;
        }
        else
        {
            from = item.From.Value;
            to = Math.Min(item.To ?? length - 1, length - 1);
        }

        return from >= length || from > to
            ? RangeRequest.Unsatisfiable.Instance
            : new RangeRequest.Slice(new ByteRange(from, to));
    }

    internal abstract record RangeRequest
    {
        public sealed record Whole : RangeRequest
        {
            public static readonly Whole Instance = new();
        }

        public sealed record Unsatisfiable : RangeRequest
        {
            public static readonly Unsatisfiable Instance = new();
        }

        public sealed record Slice(ByteRange Range) : RangeRequest;
    }
}
