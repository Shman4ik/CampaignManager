using System.Security.Claims;
using CampaignManager.Web.Utilities.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampaignManager.Web.Utilities.Api;

/// <summary>
/// API endpoints for Minio object storage operations
/// </summary>
public static class MinioApi
{
    public static void MapMinioEndpoints(this IEndpointRouteBuilder routes)
    {
        var accountGroup = routes.MapGroup("/api/minio");

        accountGroup.MapGet("/image/{*objectPath}", GetImageAsync);
        accountGroup.MapGet("/url/{*objectPath}", GetPresignedUrlAsync);
    }

    /// <summary>
    /// Retrieves an image from Minio storage
    /// </summary>
    /// <param name="objectPath">Path to the image object</param>
    /// <param name="minioService">Minio service</param>
    /// <param name="logger">Logger</param>
    /// <returns>Image file stream or error result</returns>
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
    /// <param name="objectPath">Path to the object</param>
    /// <param name="expirySeconds">URL expiry time in seconds (default: 3600)</param>
    /// <param name="minioService">Minio service</param>
    /// <param name="logger">Logger</param>
    /// <param name="user">Current user principal</param>
    /// <returns>Presigned URL or error result</returns>
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
                return TypedResults.NotFound($"Image {objectPath} not found");

            // Get presigned URL
            var url = await minioService.GetPresignedUrlAsync(objectPath, expirySeconds ?? 3600);

            return TypedResults.Ok(new PresignedUrlResponse(url));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating presigned URL for {ObjectPath}", objectPath);
            return TypedResults.StatusCode(500);
        }
    }

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
            _ => "application/octet-stream"
        };
    }
}

/// <summary>
/// Response model for presigned URL endpoint
/// </summary>
/// <param name="Url">The presigned URL for accessing the object</param>
public record PresignedUrlResponse(string Url);