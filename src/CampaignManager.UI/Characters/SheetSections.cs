namespace CampaignManager.UI.Characters;

/// <summary>Секция листа: якорь, заголовок, иконка и короткая подпись для меню на телефоне.</summary>
public sealed record SheetSectionInfo(string Id, string Title, string Icon, string ShortTitle);

/// <summary>
/// Секции листа — <b>один список</b>: по нему строятся и сами секции, и меню над листом. В v1 меню было
/// написано руками дважды (телефон и планшет), и «Рассудка» не было ни в одном (AUDIT, «Дубли»).
/// </summary>
public static class SheetSections
{
    public static readonly SheetSectionInfo Personal = new("personal", "Личные данные", "fa-user", "Личные данные");
    public static readonly SheetSectionInfo Skills = new("skills", "Навыки", "fa-graduation-cap", "Навыки");
    public static readonly SheetSectionInfo Combat = new("combat", "Оружие и магия", "fa-gun", "Оружие и магия");
    public static readonly SheetSectionInfo Equipment = new("equipment", "Вещи и деньги", "fa-suitcase", "Вещи и деньги");
    public static readonly SheetSectionInfo Sanity = new("sanity", "Рассудок и Мифы", "fa-brain", "Рассудок и Мифы");
    public static readonly SheetSectionInfo Biography = new("biography", "Биография", "fa-book", "Биография");

    public static IReadOnlyList<SheetSectionInfo> All { get; } = [Personal, Skills, Combat, Equipment, Sanity, Biography];

    /// <summary>Якорь секции в документе — по нему меню прокручивает к ней.</summary>
    public static string Anchor(SheetSectionInfo section) => $"sheet-{section.Id}";
}
