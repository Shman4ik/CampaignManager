using CampaignManager.Core.Catalogs;

namespace CampaignManager.UI.Catalogs;

/// <summary>
/// Иллюстрации книжного оружия — статика UI (<c>wwwroot/img/weapons</c>) по коду записи: <c>weapon.thompson</c> →
/// <c>thompson.webp</c> (960×640) и <c>thompson.thumb.webp</c> (360×240). Картинка привязана к коду, а не к строке базы:
/// одна на все окружения, без загрузки в хранилище; самодельное оружие (без кода) её не получает. Список кодов —
/// <c>WeaponArt.Codes.cs</c>, его вместе с файлами пишет <c>tools/export-weapon-art.py</c>.
/// </summary>
public static partial class WeaponArt
{
    private const string Root = "_content/CampaignManager.UI/img/weapons/";

    /// <summary>Картинка для раскрытой строки; null — у записи её нет.</summary>
    public static string? Full(string? code) => Has(code) ? $"{Root}{Slug(code!)}.webp" : null;

    /// <summary>Миниатюра для строки таблицы и карточки.</summary>
    public static string? Thumb(string? code) => Has(code) ? $"{Root}{Slug(code!)}.thumb.webp" : null;

    public static bool Has(string? code) => code is not null && Codes.Contains(code);

    /// <summary>Коды с картинкой — для теста, что файлы на месте.</summary>
    public static IReadOnlyCollection<string> All => Codes;

    private static string Slug(string code) => code[WeaponCodes.Prefix.Length..];
}
