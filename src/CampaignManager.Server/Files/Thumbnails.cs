using CampaignManager.Server.Files.Storage;
using Microsoft.Extensions.Primitives;
using SkiaSharp;

namespace CampaignManager.Server.Files;

/// <summary>
/// Уменьшенная копия картинки по <c>?w=</c> (ширина в пикселях). Не хранится: ключи неизменяемы, поэтому ответ с
/// <c>immutable</c> и ETag «id-w480» браузер кладёт в свой кэш навсегда, а сервер считает копию один раз на
/// браузер. Формат тот же, что у оригинала (JPEG, PNG, WebP); GIF и AVIF отдаются как есть (анимацию и
/// прозрачность не портим). Увеличивать не умеет: оригинал не шире просимого — отдаётся он сам.
/// </summary>
internal static class Thumbnails
{
    /// <summary>Допустимая ширина — защита от «w=100000» и от перебора ширин, который забил бы кэш.</summary>
    private const int MinWidth = 64;

    private const int MaxWidth = 1600;

    /// <summary>Оригиналы больше этого не разбираем: картинка в десятки мегапикселей съест память ради миниатюры.</summary>
    private const long MaxSourceBytes = 25 * 1024 * 1024;

    /// <summary>Сжатый файл бывает мал, а распакованный — огромен (PNG-бомба): размер в пикселях проверяется до разбора.</summary>
    private const long MaxSourcePixels = 50_000_000;

    public static bool Requested(StringValues value, string? contentType) =>
        int.TryParse(value.ToString(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var width)
        && width is >= MinWidth and <= MaxWidth
        && contentType is "image/jpeg" or "image/png" or "image/webp";

    public static async Task<byte[]?> TryCreateAsync(IObjectStorage storage, string key, string contentType, int width, CancellationToken cancellationToken)
    {
        await using var source = await storage.OpenReadAsync(key, null, cancellationToken);
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > MaxSourceBytes)
        {
            return null;
        }

        using var data = SKData.CreateCopy(buffer.GetBuffer(), (ulong)buffer.Length);
        using var codec = SKCodec.Create(data);
        if (codec is null) // не картинка (или битая) — отдаём оригинал как есть
        {
            return null;
        }

        var info = codec.Info;
        if (info.Width <= width || (long)info.Width * info.Height > MaxSourcePixels)
        {
            return null;
        }

        // Не SKBitmap.Decode: обрезанный файл он «декодирует» частично, и миниатюрой стала бы пустая картинка.
        var alpha = info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul;
        using var image = new SKBitmap(info.WithColorType(SKImageInfo.PlatformColorType).WithAlphaType(alpha));
        if (codec.GetPixels(image.Info, image.GetPixels()) != SKCodecResult.Success) // битая картинка
        {
            return null;
        }

        var height = Math.Max(1, (int)Math.Round(image.Height * (double)width / image.Width));
        using var thumbnail = image.Resize(image.Info.WithSize(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));
        using var encoded = thumbnail?.Encode(Format(contentType), 80);
        return encoded?.ToArray();
    }

    private static SKEncodedImageFormat Format(string contentType) => contentType switch
    {
        "image/png" => SKEncodedImageFormat.Png,
        "image/webp" => SKEncodedImageFormat.Webp,
        _ => SKEncodedImageFormat.Jpeg,
    };
}
