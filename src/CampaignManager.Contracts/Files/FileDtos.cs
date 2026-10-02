namespace CampaignManager.Contracts.Files;

/// <summary>
/// Строка <c>cm.files</c>: объект в хранилище или внешний адрес.
/// </summary>
/// <param name="Id">Идентификатор — его и хранят ссылающиеся таблицы (<c>*_file_id</c>).</param>
/// <param name="Url">Откуда брать содержимое: <see cref="FilesRoutes.Content"/>, для внешнего файла тоже
/// (сервер ответит переходом).</param>
/// <param name="ExternalUrl">Внешний адрес; у загруженного файла — <c>null</c>.</param>
public sealed record StoredFileDto(
    Guid Id,
    string Url,
    string? ExternalUrl,
    string? ContentType,
    long? SizeBytes,
    string? OriginalName,
    DateTimeOffset CreatedAt);

/// <param name="Url">Абсолютный <c>https://</c>-адрес картинки снаружи.</param>
public sealed record AddExternalFileRequest(string Url);

/// <summary>
/// Отчёт о сиротах. Удаление — отдельным запросом и только тех, что были в отчёте.
/// </summary>
/// <param name="CreatedBefore">Моложе этой отметки файл сиротой не считается: его могли загрузить
/// для формы, которую ещё не сохранили.</param>
public sealed record OrphanFilesReport(
    IReadOnlyList<StoredFileDto> Files,
    long TotalBytes,
    DateTimeOffset CreatedBefore);

public sealed record DeleteOrphansRequest(IReadOnlyList<Guid> Ids);

/// <param name="Deleted">Удалённые строки.</param>
/// <param name="Skipped">Идентификаторы, которые уже не сироты (на них сослались после отчёта) или
/// которых нет.</param>
/// <param name="ObjectsNotDeleted">Ключи объектов, которые хранилище не удалило: строки уже нет,
/// объект остался — в лог и сюда.</param>
public sealed record DeleteOrphansResponse(
    IReadOnlyList<Guid> Deleted,
    IReadOnlyList<Guid> Skipped,
    IReadOnlyList<string> ObjectsNotDeleted);
