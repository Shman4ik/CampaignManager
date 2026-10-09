using SkiaSharp;

namespace CampaignManager.Server.Files;

/// <summary>
/// PNG и JPEG → WebP при загрузке (<c>?format=webp</c>): рисунки справочников по 2–3 МБ становятся в разы легче, и большая
/// картинка раскрытой записи на iPad появляется сразу. Размер в пикселях тот же, качество 85 — как у прежних скриптов
/// загрузки. Не картинка, битая, огромная, JPEG с поворотом в EXIF (WebP его не унесёт — фото легло бы на бок) или WebP
/// вышел не меньше оригинала — null, и сохраняется оригинал.
/// </summary>
internal static class WebpConversion
{
    public const int Quality = 85;

    public static bool Applies(string contentType) => contentType is "image/png" or "image/jpeg";

    public static byte[]? TryConvert(byte[] source)
    {
        if (source.Length > Thumbnails.MaxSourceBytes)
        {
            return null;
        }

        using var data = SKData.CreateCopy(source);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedOrigin != SKEncodedOrigin.TopLeft)
        {
            return null;
        }

        using var image = Thumbnails.Decode(codec);
        using var encoded = image?.Encode(SKEncodedImageFormat.Webp, Quality);
        var bytes = encoded?.ToArray();
        return bytes is not null && bytes.Length < source.Length ? bytes : null;
    }
}
