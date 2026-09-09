using CampaignManager.Web.Components.Features.Books.Model;

namespace CampaignManager.Web.Components.Features.Books.Services;

/// <summary>
///     Короткая подпись категории книги — для метки в строке каталога, где полное
///     «Книга по оккультизму» из <c>ToRussianString()</c> занимает половину колонки названия.
///     Полная форма остаётся в модалках и подтверждениях, короткая — только в списке.
/// </summary>
public static class BookTypeText
{
    public static string Short(BookType type) => type switch
    {
        BookType.MythosBook => "Мифы",
        BookType.OccultBook => "Оккультизм",
        _ => "—"
    };
}
