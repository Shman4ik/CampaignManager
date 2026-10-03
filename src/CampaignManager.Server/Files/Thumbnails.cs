using CampaignManager.Server.Files.Storage;
using Microsoft.Extensions.Primitives;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

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

    public static bool Requested(StringValues value, string? contentType) =>
        int.TryParse(value.ToString(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var width)
        && width is >= MinWidth and <= MaxWidth
        && contentType is "image/jpeg" or "image/png" or "image/webp";

    public static async Task<byte[]?> TryCreateAsync(IObjectStorage storage, string key, string contentType, int width, CancellationToken cancellationToken)
    {
        try
        {
            await using var source = await storage.OpenReadAsync(key, null, cancellationToken);
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length > MaxSourceBytes)
            {
                return null;
            }

            buffer.Position = 0;
            using var image = await Image.LoadAsync(buffer, cancellationToken);
            if (image.Width <= width)
            {
                return null;
            }

            image.Mutate(x => x.Resize(width, 0));
            using var output = new MemoryStream();
            await image.SaveAsync(output, Encoder(contentType), cancellationToken);
            return output.ToArray();
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or ImageFormatException)
        {
            return null; // не картинка (или битая) — отдаём оригинал как есть
        }
    }

    private static IImageEncoder Encoder(string contentType) => contentType switch
    {
        "image/png" => new PngEncoder(),
        "image/webp" => new WebpEncoder { Quality = 80 },
        _ => new JpegEncoder { Quality = 80 },
    };
}
