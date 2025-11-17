using System.Security.Claims;
using CampaignManager.Web.Utilities.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampaignManager.Web.Utilities.Api;

/// <summary>
/// API endpoints for Minio object storage operations
/// </summary>
public static class MinioApi
{
    /// <summary>
    /// Maps Minio API endpoints to the application's routing
    /// </summary>
    /// <param name="routes">The endpoint route builder</param>
    public static void MapMinioEndpoints(this IEndpointRouteBuilder routes)
    {
        var minioGroup = routes.MapGroup("/api/minio")
            .WithTags("Minio Storage");

        minioGroup.MapGet("/image/{*objectPath}", GetImageAsync)
            .WithName("GetImage")
            .WithSummary("Retrieve an image from Minio storage")
            .WithDescription("Fetches an image file from Minio object storage and returns it with the appropriate content type");

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
        try
        {
            if (string.IsNullOrEmpty(objectPath)) 
                return TypedResults.BadRequest("Object path is required");

            // Check if the object exists
            var exists = await minioService.DoesObjectExistAsync(objectPath);
            if (!exists) 
                return TypedResults.NotFound($"Image {objectPath} not found");

            // Get the object stream
            var stream = await minioService.GetObjectAsync(objectPath);

            // Determine content type based on file extension
            var contentType = GetContentType(objectPath);

            return TypedResults.File(stream, contentType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving image {ObjectPath}", objectPath);
            return TypedResults.StatusCode(500);
        }
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