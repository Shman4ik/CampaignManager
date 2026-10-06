using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Files;
using CampaignManager.Data;
using CampaignManager.Data.Catalogs;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Catalogs.Stores;

/// <summary>
/// Картинки записи справочника — одна логика на бестиарий, оружие, профессии и заклинания: файлы <c>cm.files</c> по порядку, первая — обложка.
/// Таблицы разные (<c>creature_images</c>, <c>weapon_images</c>, <c>occupation_images</c>, <c>spell_images</c>), ключ — (запись, порядок).
/// </summary>
internal static class CatalogImages
{
    public static List<CatalogImageDto> ToDtos(IEnumerable<ICatalogImage> images) =>
        images.OrderBy(i => i.Ord).Select(i => new CatalogImageDto(i.FileId, FilesRoutes.Content(i.FileId), i.Caption)).ToList();

    /// <summary>
    /// Файлы картинок есть в этой базе. Из файла обмена другой базы их может не оказаться — импорт пропускает такие
    /// с предупреждением, обычная запись отказывает.
    /// </summary>
    public static async Task<List<CatalogImageDto>> CheckAsync(
        CmDbContext db,
        IEnumerable<CatalogImageDto>? incoming,
        CatalogWrite write,
        CancellationToken cancellationToken)
    {
        var images = (incoming ?? []).ToList();
        if (images.Count == 0)
        {
            return images;
        }

        var ids = images.Select(i => i.FileId).Distinct().ToList();
        var known = await db.Files.Where(f => ids.Contains(f.Id)).Select(f => f.Id).ToListAsync(cancellationToken);
        if (known.Count == ids.Count)
        {
            return images;
        }

        if (!write.IsImport)
        {
            throw ApiProblemException.Invalid("Картинка не найдена: загрузите её заново.");
        }

        write.Warnings.Add("часть картинок из файла в этой базе не найдена — они пропущены.");
        return images.Where(i => known.Contains(i.FileId)).ToList();
    }

    /// <summary>Привести строки записи к списку: ключ — (запись, порядок), поэтому правим на месте, лишние убираем.</summary>
    public static void Apply<TImage>(CmDbContext db, List<TImage> current, IReadOnlyList<CatalogImageDto> images, Func<int, TImage> create)
        where TImage : class, ICatalogImage
    {
        for (var ord = 0; ord < images.Count; ord++)
        {
            var row = current.FirstOrDefault(i => i.Ord == ord);
            if (row is null)
            {
                row = create(ord);
                current.Add(row);
                db.Add(row);
            }

            row.FileId = images[ord].FileId;
            row.Caption = string.IsNullOrWhiteSpace(images[ord].Caption) ? null : images[ord].Caption!.Trim();
        }

        current.RemoveAll(i => i.Ord >= images.Count);
    }
}
