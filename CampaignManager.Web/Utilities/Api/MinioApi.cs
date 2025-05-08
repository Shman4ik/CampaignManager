using System.Security.Claims;
using CampaignManager.Web.Utilities.Services;

namespace CampaignManager.Web.Utilities.Api;

public static class MinioApi
{
    public static void MapMinioEndpoints(this IEndpointRouteBuilder routes)
    {
        var accountGroup = routes.MapGroup("/api/minio");

        accountGroup.MapGet("/image/{*objectPath}", async (string objectPath, MinioService minioService, ILogger<Program> logger) =>
        {
            try
            {
                if (string.IsNullOrEmpty(objectPath)) return Results.BadRequest("Object path is required");

                // Check if the object exists
                var exists = await minioService.DoesObjectExistAsync(objectPath);
                if (!exists) return Results.NotFound($"Image {objectPath} not found");

                // Get the object stream
                var stream = await minioService.GetObjectAsync(objectPath);

                // Determine content type based on file extension
                var contentType = GetContentType(objectPath);

                return Results.File(stream, contentType);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving image {ObjectPath}", objectPath);
                return Results.StatusCode(500);
            }
        });

        accountGroup.MapGet("/url/{*objectPath}", async (string objectPath, int? expirySeconds, MinioService minioService, ILogger<Program> logger, ClaimsPrincipal user) =>
        {
            // Check if user is authenticated
            if (!user.Identity?.IsAuthenticated ?? true) return Results.Unauthorized();

            try
            {
                if (string.IsNullOrEmpty(objectPath)) return Results.BadRequest("Object path is required");

                // Check if the object exists
                var exists = await minioService.DoesObjectExistAsync(objectPath);
                if (!exists) return Results.NotFound($"Image {objectPath} not found");

                // Get presigned URL
                var url = await minioService.GetPresignedUrlAsync(objectPath, expirySeconds ?? 3600);

                return Results.Ok(new { url });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error generating presigned URL for {ObjectPath}", objectPath);
                return Results.StatusCode(500);
            }
        });
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