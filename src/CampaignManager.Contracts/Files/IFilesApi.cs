namespace CampaignManager.Contracts.Files;

/// <summary>
/// Файлы: загрузка, внешние адреса, сироты. Содержимое читается не через этот интерфейс, а
/// обычным <c>GET</c> по <see cref="StoredFileDto.Url"/> — его отдают в <c>src</c> картинки и плеера.
/// </summary>
public interface IFilesApi
{
    /// <summary>Загрузить файл. Тот же файл повторно не плодит объект — вернётся прежняя строка.</summary>
    Task<StoredFileDto> UploadAsync(Stream content, string fileName, CancellationToken cancellationToken = default);

    Task<StoredFileDto> AddExternalAsync(string url, CancellationToken cancellationToken = default);

    Task<OrphanFilesReport> GetOrphansAsync(CancellationToken cancellationToken = default);

    Task<DeleteOrphansResponse> DeleteOrphansAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);
}
