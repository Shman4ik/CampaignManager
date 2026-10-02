using System.Net.Http.Json;
using System.Text.Json;
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

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(await ReadProblemAsync(response, cancellationToken), null, response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken)
            ?? throw new InvalidOperationException($"Пустой ответ от {response.RequestMessage?.RequestUri}.");
    }

    // ProblemDetails читается без типа: нужен только текст для пользователя.
    private static async Task<string> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var fallback = $"{(int)response.StatusCode} {response.ReasonPhrase}";
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var problem = JsonDocument.Parse(body);
            foreach (var name in (ReadOnlySpan<string>)["detail", "title"])
            {
                if (problem.RootElement.TryGetProperty(name, out var value) && value.GetString() is { Length: > 0 } text)
                {
                    return text;
                }
            }
        }
        catch (JsonException)
        {
        }

        return fallback;
    }
}
