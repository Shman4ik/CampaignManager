namespace CampaignManager.Web.Components.Features.Bestiary.Model;

/// <summary>
///     Одна иллюстрация существа. У многих тварей одной картинки мало: у оборотня две формы,
///     шоггота-владыку книга описывает человеком и шогготом, а богу нужен и облик, и аватара.
/// </summary>
public class CreatureImage
{
    /// <summary>
    ///     Путь к объекту в MinIO («images/beasts/img-4.jpeg») или полная ссылка https://.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Подпись: «форма шоггота», «рядом с человеком для масштаба».</summary>
    public string? Caption { get; set; }

    /// <summary>
    ///     Адрес для тега img: объект хранилища отдаёт свой эндпоинт, внешняя ссылка идёт как есть.
    /// </summary>
    public static string ToSrc(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? url
            : $"/api/minio/image/{url}";
}
