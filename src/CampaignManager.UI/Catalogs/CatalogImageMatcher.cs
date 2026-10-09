using CampaignManager.Contracts.Catalogs;
using CampaignManager.Core.Catalogs;

namespace CampaignManager.UI.Catalogs;

/// <summary>
/// Файл картинки → запись справочника (загрузка пачкой, <see cref="CatalogImagesBulkModal{TItem}"/>). Имя файла без расширения —
/// код записи без префикса: <c>padlock.png</c> → <c>item.padlock</c> (так рисунки лежат в папках <c>*-art</c>); не нашлось —
/// название записи без учёта регистра и «ё», дефисы и подчёркивания — пробелы (<c>Карманный нож.png</c>).
/// </summary>
public sealed class CatalogImageMatcher<TItem>
    where TItem : CatalogItemDto
{
    /// <summary>Что принимает сервер как картинку (<c>FileTypes</c>); остальное в папке (подписи, json) пропускается.</summary>
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".avif" };

    private readonly Dictionary<string, TItem> _byCode = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TItem> _byName = new(StringComparer.Ordinal);

    public CatalogImageMatcher(IEnumerable<TItem> items)
    {
        foreach (var item in items)
        {
            if (item.Code is { } code && code.IndexOf('.', StringComparison.Ordinal) is var dot and > 0)
            {
                Prefix ??= code[..(dot + 1)];
                _byCode.TryAdd(code[(dot + 1)..], item);
            }

            _byName.TryAdd(CatalogCodeTable.NormalizeName(item.Name), item);
        }
    }

    /// <summary>Префикс кодов справочника («item.») — для подсказки в окне; null — кодов у записей нет.</summary>
    public string? Prefix { get; }

    public static bool IsImage(string fileName) => ImageExtensions.Contains(Path.GetExtension(fileName));

    public TItem? Find(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName).Trim();
        if (_byCode.TryGetValue(stem.ToLowerInvariant(), out var byCode))
        {
            return byCode;
        }

        return _byName.GetValueOrDefault(CatalogCodeTable.NormalizeName(stem.Replace('_', ' ').Replace('-', ' ')))
            ?? _byName.GetValueOrDefault(CatalogCodeTable.NormalizeName(stem)); // «Кольт М1911-А1» — дефис в самом названии
    }
}
