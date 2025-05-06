using Minio;
using Minio.DataModel.Args;

namespace CampaignManager.Web.Utilities.Services;

public class MinioService
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioService> _logger;
    private readonly string _bucketName;

    public MinioService(IConfiguration configuration, ILogger<MinioService> logger)
    {
        _logger = logger;

        var endpoint = configuration["Minio:Endpoint"] ?? "s3.shmanev.xyz";
        var accessKey = configuration["Minio:AccessKey"] ?? throw new ArgumentNullException("Minio:AccessKey");
        var secretKey = configuration["Minio:SecretKey"] ?? throw new ArgumentNullException("Minio:SecretKey");
        var secure = configuration.GetValue("Minio:Secure", true);
        _bucketName = configuration["Minio:BucketName"] ?? "campaignmanager";

        _minioClient = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(secure)
            .Build();
    }

    public async Task<Stream> GetObjectAsync(string objectName)
    {
        try
        {
            var memoryStream = new MemoryStream();
            var args = new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName)
                .WithCallbackStream(stream =>
                {
                    stream.CopyTo(memoryStream);
                    memoryStream.Position = 0;
                });

            await _minioClient.GetObjectAsync(args);
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving object {ObjectName} from Minio", objectName);
            throw;
        }
    }

    public async Task<string> GetPresignedUrlAsync(string objectName, int expiryInSeconds = 3600)
    {
        try
        {
            var args = new PresignedGetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName)
                .WithExpiry(expiryInSeconds);

            return await _minioClient.PresignedGetObjectAsync(args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating presigned URL for object {ObjectName}", objectName);
            throw;
        }
    }

    public async Task<bool> DoesObjectExistAsync(string objectName)
    {
        try
        {
            var statArgs = new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName);

            await _minioClient.StatObjectAsync(statArgs);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
