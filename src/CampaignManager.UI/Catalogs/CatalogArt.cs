namespace CampaignManager.UI.Catalogs;

/// <summary>
/// Рамка иллюстраций справочника — по тому, как нарисованы гравюры: портрет 4:5 (существа, люди, страницы — навыки, профессии,
/// заклинания, книги, бестиарий, артефакты) и альбом 3:2 (вещи — оружие, предметы). Размеры частей — <c>Styles/catalog.css</c>.
/// </summary>
public enum CatalogArt
{
    Portrait,
    Landscape,
}

internal static class CatalogArtCss
{
    /// <summary>Рамка с пропорцией: <c>cm-art cm-art-portrait</c>.</summary>
    public static string Frame(CatalogArt art) => art == CatalogArt.Landscape ? "cm-art cm-art-landscape" : "cm-art cm-art-portrait";

    /// <summary>Сетка плиток галереи под рамку.</summary>
    public static string Tiles(CatalogArt art) => art == CatalogArt.Landscape ? "cm-tiles cm-tiles-landscape" : "cm-tiles cm-tiles-portrait";

    /// <summary>Картинка раскрытой записи под рамку.</summary>
    public static string Figure(CatalogArt art) => art == CatalogArt.Landscape ? "cm-figure cm-figure-landscape" : "cm-figure cm-figure-portrait";
}

/// <summary>Картинки записи для раскрытия у справочника с одной картинкой (предмет, книга, навык): список из неё или пустой.</summary>
public static class CatalogImages
{
    public static IReadOnlyList<CampaignManager.Contracts.Catalogs.CatalogImageDto> Single(Guid? fileId, string? url) =>
        url is null ? [] : [new(fileId ?? Guid.Empty, url, null)];
}
