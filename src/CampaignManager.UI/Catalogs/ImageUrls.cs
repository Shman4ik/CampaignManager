namespace CampaignManager.UI.Catalogs;

/// <summary>
/// Адреса картинок для плиток и миниатюр. Сервер отдаёт файл целиком (до 300 КБ на плитку бестиария, 20 плиток —
/// ~6 МБ на страницу); параметр <c>w</c> просит у него версию нужной ширины (<c>FileContentEndpoint</c>). Внешний адрес
/// (чужой сайт) и неизвестный вид ссылки остаются как есть — им параметр ничего не скажет.
/// </summary>
public static class ImageUrls
{
    /// <summary>Миниатюра плитки: 480px хватает на 232px при плотности пикселей 2.</summary>
    public const int TileWidth = 480;

    public static string? Thumb(string? url, int width = TileWidth)
    {
        if (string.IsNullOrEmpty(url) || url.Contains('?', StringComparison.Ordinal) || !IsOwnFile(url))
        {
            return url;
        }

        return $"{url}?w={width}";
    }

    // Свои файлы — относительный адрес /api/v1/files/{id}; всё с хостом — внешнее.
    private static bool IsOwnFile(string url) => url.Contains("/files/", StringComparison.Ordinal) && !url.Contains("://", StringComparison.Ordinal);
}
