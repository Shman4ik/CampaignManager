using System.Net.Http.Json;
using CampaignManager.Contracts;
using CampaignManager.Contracts.Files;

namespace CampaignManager.ApiClient.Files;

public sealed class FilesApiClient(HttpClient http) : IFilesApi
{
    private static ContractsJsonContext Json => ContractsJsonContext.Default;

    public async Task<StoredFileDto> UploadAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        // Тип содержимого сервер определяет сам, по расширению: заявленному клиентом не верит.
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(content), FilesRoutes.UploadField, fileName);

        using var response = await http.PostAsync(FilesRoutes.Upload, form, cancellationToken);
        return await ReadAsync(response, Json.StoredFileDto, cancellationToken);
    }

    public async Task<StoredFileDto> AddExternalAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(FilesRoutes.External, new AddExternalFileRequest(url),
            Json.AddExternalFileRequest, cancellationToken);
        return await ReadAsync(response, Json.StoredFileDto, cancellationToken);
    }

    public async Task<OrphanFilesReport> GetOrphansAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(FilesRoutes.Orphans, cancellationToken);
        return await ReadAsync(response, Json.OrphanFilesReport, cancellationToken);
    }

    public async Task<DeleteOrphansResponse> DeleteOrphansAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(FilesRoutes.DeleteOrphans, new DeleteOrphansRequest(ids),
            Json.DeleteOrphansRequest, cancellationToken);
        return await ReadAsync(response, Json.DeleteOrphansResponse, cancellationToken);
    }

    private static Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken) =>
        ApiResponses.ReadAsync(response, typeInfo, cancellationToken);
}
