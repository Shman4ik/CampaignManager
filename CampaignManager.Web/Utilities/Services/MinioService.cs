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
    
    /// <summary>
    /// Uploads a stream to Minio storage
    /// </summary>
    /// <param name="stream">The stream to upload</param>
    /// <param name="objectName">The name of the object in Minio (including path)</param>
    /// <param name="folder">Optional folder path within the bucket</param>
    /// <param name="contentType">The MIME type of the content</param>
    /// <returns>The full object name (including folder if specified)</returns>
    public async Task<string> UploadAsync(Stream stream, string objectName, string? folder = null, string contentType = "application/octet-stream")
    {
        try
        {
            // Combine folder and object name if folder is specified
            var fullObjectName = string.IsNullOrEmpty(folder) 
                ? objectName 
                : $"{folder.TrimEnd('/')}/{objectName}";

            // Ensure the bucket exists
            await EnsureBucketExistsAsync();

            // Create put object arguments
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(fullObjectName)
                .WithStreamData(stream)
                .WithObjectSize(stream.Length)
                .WithContentType(contentType);

            // Upload the file
            await _minioClient.PutObjectAsync(putObjectArgs);
            
            _logger.LogInformation("Successfully uploaded {ObjectName} to Minio", fullObjectName);
            
            return fullObjectName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading object {ObjectName} to Minio", objectName);
            throw;
        }
    }
    
    public async Task<IEnumerable<string>> ListObjectsAsync(string? prefix = null)
    {
        var objectNames = new List<string>();
        try
        {
            var listArgs = new ListObjectsArgs()
                .WithBucket(_bucketName)
                .WithRecursive(true);

            if (!string.IsNullOrEmpty(prefix))
            {
                listArgs = listArgs.WithPrefix(prefix);
            }

            await foreach (var item in _minioClient.ListObjectsEnumAsync(listArgs))
            {
                if (!item.IsDir) // We only want files, not directories
                {
                    objectNames.Add(item.Key);
                }
            }

            return objectNames;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing objects from Minio bucket {BucketName} with prefix {Prefix}", _bucketName, prefix);
            throw;
        }
    }

    /// <summary>
    /// Ensures the configured bucket exists, creating it if it doesn't
    /// </summary>
    private async Task EnsureBucketExistsAsync()
    {
        try
        {
            // Check if bucket exists
            var bucketExistsArgs = new BucketExistsArgs()
                .WithBucket(_bucketName);
                
            bool bucketExists = await _minioClient.BucketExistsAsync(bucketExistsArgs);
            
            // Create the bucket if it doesn't exist
            if (!bucketExists)
            {
                var makeBucketArgs = new MakeBucketArgs()
                    .WithBucket(_bucketName);
                    
                await _minioClient.MakeBucketAsync(makeBucketArgs);
                
                _logger.LogInformation("Created bucket {BucketName}", _bucketName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring bucket {BucketName} exists", _bucketName);
            throw;
        }
    }
}
