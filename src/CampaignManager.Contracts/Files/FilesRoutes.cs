namespace CampaignManager.Contracts.Files;

public static class FilesRoutes
{
    /// <summary><c>POST</c> multipart, поле <see cref="UploadField"/>: загрузить файл.</summary>
    public const string Upload = ApiRoutes.Prefix + "/files";

    /// <summary><c>POST</c> <see cref="AddExternalFileRequest"/>: завести строку с внешним адресом.</summary>
    public const string External = ApiRoutes.Prefix + "/files/external";

    /// <summary><c>GET</c>/<c>HEAD</c>: содержимое файла (с <c>Range</c>) или переход на внешний адрес.</summary>
    public const string ContentPattern = ApiRoutes.Prefix + "/files/{id:guid}";

    /// <summary><c>GET</c>: отчёт о сиротах — файлах, на которые никто не ссылается.</summary>
    public const string Orphans = ApiRoutes.Prefix + "/admin/files/orphans";

    /// <summary><c>POST</c> <see cref="DeleteOrphansRequest"/>: удалить сирот из отчёта.</summary>
    public const string DeleteOrphans = ApiRoutes.Prefix + "/admin/files/orphans/delete";

    /// <summary>
    /// Параметр загрузки <c>?format=webp</c>: картинку PNG или JPEG сервер переводит в WebP (качество 85, размер тот же).
    /// Рисунки справочников по 2–3 МБ иначе тяжелы для iPad.
    /// </summary>
    public const string FormatQuery = "format";

    public const string WebpFormat = "webp";

    /// <summary>Имя поля формы с файлом.</summary>
    public const string UploadField = "file";

    /// <summary>
    /// Адрес содержимого. Относительный: веб отдаёт его в <c>src</c> как есть, приложение
    /// складывает с адресом сервера.
    /// </summary>
    public static string Content(Guid id) => $"{ApiRoutes.Prefix}/files/{id}";
}
