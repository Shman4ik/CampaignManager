namespace CampaignManager.UI.Characters;

/// <summary>Секция листа: якорь, заголовок, иконка и короткая подпись для меню на телефоне.</summary>
public sealed record SheetSectionInfo(string Id, string Title, string Icon, string ShortTitle);

/// <summary>
/// Секции листа — <b>один список</b>: по нему строятся и сами секции, и меню над листом. В v1 меню было
/// написано руками дважды (телефон и планшет), и «Рассудка» не было ни в одном (AUDIT, «Дубли»).
/// </summary>
public static class SheetSections
{
    public static readonly SheetSectionInfo Personal = new("personal", "Личные данные", "fa-user", "Сыщик");
    public static readonly SheetSectionInfo Skills = new("skills", "Навыки", "fa-graduation-cap", "Навыки");
    public static readonly SheetSectionInfo Combat = new("combat", "Оружие и заклинания", "fa-gun", "Оружие");
    public static readonly SheetSectionInfo Equipment = new("equipment", "Снаряжение и финансы", "fa-suitcase", "Вещи");
    public static readonly SheetSectionInfo Sanity = new("sanity", "Рассудок и безумие", "fa-brain", "Рассудок");
    public static readonly SheetSectionInfo Biography = new("biography", "Биография и заметки", "fa-book", "Биография");

    public static IReadOnlyList<SheetSectionInfo> All { get; } = [Personal, Skills, Combat, Equipment, Sanity, Biography];

    /// <summary>Якорь секции в документе — по нему меню прокручивает к ней.</summary>
    public static string Anchor(SheetSectionInfo section) => $"sheet-{section.Id}";
}
