using CampaignManager.Web.Components.Features.Music.Model;
using CampaignManager.Web.Utilities.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Primitives;
using Minio.Exceptions;
using System.Security.Claims;

namespace CampaignManager.Web.Utilities.Api;

/// <summary>
/// API endpoints for Minio object storage operations
/// </summary>
public static class MinioApi
{
    /// <summary>Потолок одного ответа на Range — чтобы ответ не разрастался в памяти.</summary>
    private const long MaxChunkBytes = 4L * 1024 * 1024;

    /// <summary>
    /// Maps Minio API endpoints to the application's routing
    /// </summary>
    /// <param name="routes">The endpoint route builder</param>
    public static void MapMinioEndpoints(this IEndpointRouteBuilder routes)
    {
        var minioGroup = routes.MapGroup("/api/minio")
            .WithTags("Minio Storage");

        minioGroup.MapGet("/image/{*objectPath}", GetImageAsync)
            .RequireAuthorization()
            .WithName("GetImage")
            .WithSummary("Retrieve an image from Minio storage")
            .WithDescription("Fetches an image file from Minio object storage and returns it with the appropriate content type");

        minioGroup.MapGet("/audio/{*objectPath}", GetAudioAsync)
            .RequireAuthorization()
            .WithName("GetAudio")
            .WithSummary("Retrieve an audio track from Minio storage")
            .WithDescription("Streams a music track through the app's own origin so that Web Audio can control its volume");

        minioGroup.MapGet("/url/{*objectPath}", GetPresignedUrlAsync)
            .RequireAuthorization()
            .WithName("GetPresignedUrl")
            .WithSummary("Generate a presigned URL for a Minio object")
            .WithDescription("Creates a temporary presigned URL that allows direct access to a Minio object for a specified duration");
    }

    /// <summary>
    /// Retrieves an image from Minio storage
    /// </summary>
    /// <param name="objectPath">Path to the image object in Minio storage</param>
    /// <param name="minioService">Minio service for storage operations</param>
    /// <param name="logger">Logger for operation tracking</param>
    /// <returns>
    /// <list type="bullet">
    /// <item><description>200 OK - Image file stream with appropriate content type</description></item>
    /// <item><description>400 Bad Request - Object path is missing or invalid</description></item>
    /// <item><description>404 Not Found - Image does not exist in storage</description></item>
    /// <item><description>500 Internal Server Error - Server error occurred during retrieval</description></item>
    /// </list>
    /// </returns>
    /// <response code="200">Returns the image file stream</response>
    /// <response code="400">Object path is required</response>
    /// <response code="404">Image not found in storage</response>
    /// <response code="500">Internal server error during image retrieval</response>
    private static async Task<Results<FileStreamHttpResult, BadRequest<string>, NotFound<string>, StatusCodeHttpResult>> GetImageAsync(
        string objectPath,
        MinioService minioService,
        ILogger<Program> logger)
    {
        if (string.IsNullOrEmpty(objectPath))
            return TypedResults.BadRequest("Object path is required");

        // Prevent path traversal attacks
        if (objectPath.Contains("..") || Path.IsPathRooted(objectPath))
            return TypedResults.BadRequest("Invalid object path");

        try
        {
            // Get the object stream directly — skip the separate existence check
            // (DoesObjectExistAsync made 2 extra HEAD requests before every GET).
            // ObjectNotFoundException is thrown by the SDK on HTTP 404.
            var stream = await minioService.GetObjectAsync(objectPath);
            var contentType = GetContentType(objectPath);
            return TypedResults.File(stream, contentType);
        }
        catch (ObjectNotFoundException)
        {
            return TypedResults.NotFound($"Image {objectPath} not found");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving image {ObjectPath}", objectPath);
            return TypedResults.StatusCode(500);
        }
    }

    /// <summary>
    /// Streams an audio track from Minio storage through the application's own origin.
    /// </summary>
    /// <remarks>
    /// Намеренно не presigned-ссылка прямо на хранилище: плеер пропускает файл через
    /// Web Audio, чтобы управлять громкостью (на iOS иначе никак), а
    /// <c>createMediaElementSource</c> на кросс-доменном элементе без CORS отдаёт тишину.
    /// <para>
    ///     <c>Range</c> разбирается вручную, а не через <c>enableRangeProcessing</c>: тому режиму
    ///     нужен уже вычитанный перематываемый поток, то есть объект пришлось бы каждый раз тянуть
    ///     из хранилища целиком. Здесь запрошенный отрезок читается напрямую
    ///     (<see cref="MinioService.GetObjectRangeAsync" />), поэтому сдвиг ползунка стоит одного
    ///     короткого запроса, а не повторной выкачки трека.
    /// </para>
    /// </remarks>
    private static async Task<IResult> GetAudioAsync(
        string objectPath,
        MinioService minioService,
        HttpContext http,
        ILogger<Program> logger)
    {
        if (string.IsNullOrEmpty(objectPath))
            return TypedResults.BadRequest("Object path is required");

        // Prevent path traversal attacks
        if (objectPath.Contains("..") || Path.IsPathRooted(objectPath))
            return TypedResults.BadRequest("Invalid object path");

        if (!MusicSource.IsSupportedAudioFile(objectPath))
            return TypedResults.BadRequest("Unsupported audio format");

        var contentType = MusicSource.GetAudioContentType(objectPath);

        try
        {
            var size = await minioService.GetObjectSizeAsync(objectPath);

            // Без этого заголовка браузер не считает источник перематываемым: ползунок работать
            // не будет, а Safari в придачу капризничает с воспроизведением.
            http.Response.Headers.AcceptRanges = "bytes";

            if (!TryParseRange(http.Request.Headers.Range, size, out var start, out var end))
            {
                // Заголовка нет или он неразборчив — отдаём объект целиком, как раньше.
                var whole = await minioService.GetObjectAsync(objectPath);
                return TypedResults.Stream(whole, contentType);
            }

            if (start >= size)
            {
                http.Response.Headers.ContentRange = $"bytes */{size}";
                return TypedResults.StatusCode(StatusCodes.Status416RangeNotSatisfiable);
            }

            // Отрезок режется по MaxChunkBytes: отдать меньше запрошенного сервер вправе, браузер
            // просто попросит продолжение. Иначе «bytes=0-» на пятидесятимегабайтном треке означало
            // бы те же пятьдесят мегабайт в памяти, от которых мы здесь и уходим.
            var length = Math.Min(end - start + 1, MaxChunkBytes);
            var last = start + length - 1;

            var slice = await minioService.GetObjectRangeAsync(objectPath, start, length);

            http.Response.StatusCode = StatusCodes.Status206PartialContent;
            http.Response.ContentType = contentType;
            http.Response.ContentLength = length;
            http.Response.Headers.ContentRange = $"bytes {start}-{last}/{size}";

            await using (slice)
                await slice.CopyToAsync(http.Response.Body);

            return TypedResults.Empty;
        }
        catch (ObjectNotFoundException)
        {
            return TypedResults.NotFound($"Audio {objectPath} not found");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving audio {ObjectPath}", objectPath);
            return TypedResults.StatusCode(500);
        }
    }

    /// <summary>
    ///     Разбирает <c>Range: bytes=…</c>. Поддержаны обе формы, которыми пользуются браузеры:
    ///     <c>bytes=1024-</c> — от позиции до конца, так выглядит перемотка, и <c>bytes=0-1</c> —
    ///     пробный запрос, которым Safari выясняет размер. Форма <c>bytes=-N</c> (последние N байт)
    ///     тоже разбирается: ею читают хвост с метаданными. Несколько диапазонов в одном заголовке
    ///     не поддержаны — для аудио браузеры их не шлют.
    /// </summary>
    private static bool TryParseRange(StringValues header, long size, out long start, out long end)
    {
        start = 0;
        end = size - 1;

        var raw = header.ToString();
        if (string.IsNullOrEmpty(raw) || size <= 0) return false;
        if (!raw.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase)) return false;

        var spec = raw["bytes=".Length..].Trim();
        if (spec.Contains(',')) return false;

        var dash = spec.IndexOf('-');
        if (dash < 0) return false;

        var fromText = spec[..dash];
        var toText = spec[(dash + 1)..];

        if (fromText.Length == 0)
        {
            if (!long.TryParse(toText, out var suffix) || suffix <= 0) return false;
            start = Math.Max(0, size - suffix);
            end = size - 1;
            return true;
        }

        if (!long.TryParse(fromText, out start) || start < 0) return false;

        if (toText.Length == 0)
        {
            end = size - 1;
            return true;
        }

        if (!long.TryParse(toText, out end) || end < start) return false;

        end = Math.Min(end, size - 1);
        return true;
    }

    /// <summary>
    /// Generates a presigned URL for accessing a Minio object
    /// </summary>
    /// <param name="objectPath">Path to the object in Minio storage</param>
    /// <param name="expirySeconds">URL expiry time in seconds (default: 3600 = 1 hour)</param>
    /// <param name="minioService">Minio service for storage operations</param>
    /// <param name="logger">Logger for operation tracking</param>
    /// <param name="user">Current authenticated user principal</param>
    /// <returns>
    /// <list type="bullet">
    /// <item><description>200 OK - Presigned URL for the object</description></item>
    /// <item><description>401 Unauthorized - User is not authenticated</description></item>
    /// <item><description>400 Bad Request - Object path is missing or invalid</description></item>
    /// <item><description>404 Not Found - Object does not exist in storage</description></item>
    /// <item><description>500 Internal Server Error - Server error occurred during URL generation</description></item>
    /// </list>
    /// </returns>
    /// <response code="200">Returns the presigned URL with expiration details</response>
    /// <response code="401">User must be authenticated to generate presigned URLs</response>
    /// <response code="400">Object path is required</response>
    /// <response code="404">Object not found in storage</response>
    /// <response code="500">Internal server error during URL generation</response>
    private static async Task<Results<Ok<PresignedUrlResponse>, UnauthorizedHttpResult, BadRequest<string>, NotFound<string>, StatusCodeHttpResult>> GetPresignedUrlAsync(
        string objectPath,
        int? expirySeconds,
        MinioService minioService,
        ILogger<Program> logger,
        ClaimsPrincipal user)
    {
        // Check if user is authenticated - .NET 10 will automatically return 401 for API endpoints
        if (!user.Identity?.IsAuthenticated ?? true)
            return TypedResults.Unauthorized();

        try
        {
            if (string.IsNullOrEmpty(objectPath))
                return TypedResults.BadRequest("Object path is required");

            // Prevent path traversal attacks
            if (objectPath.Contains("..") || Path.IsPathRooted(objectPath))
                return TypedResults.BadRequest("Invalid object path");

            // Check if the object exists
            var exists = await minioService.DoesObjectExistAsync(objectPath);
            if (!exists)
                return TypedResults.NotFound($"Object {objectPath} not found");

            // Get presigned URL with specified or default expiry
            var expiry = expirySeconds ?? 3600;
            var url = await minioService.GetPresignedUrlAsync(objectPath, expiry);

            return TypedResults.Ok(new PresignedUrlResponse(url, expiry));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating presigned URL for {ObjectPath}", objectPath);
            return TypedResults.StatusCode(500);
        }
    }

    /// <summary>
    /// Determines the MIME content type based on file extension
    /// </summary>
    /// <param name="fileName">File name or path</param>
    /// <returns>MIME content type string</returns>
    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".bmp" => "image/bmp",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream"
        };
    }
}

/// <summary>
/// Response model for presigned URL endpoint
/// </summary>
/// <param name="Url">The presigned URL for accessing the object</param>
/// <param name="ExpiresInSeconds">The number of seconds until the URL expires</param>
public record PresignedUrlResponse(string Url, int ExpiresInSeconds);