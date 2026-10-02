using System.Security.Claims;
using CampaignManager.Core.Identity;

namespace CampaignManager.UI.Layout;

/// <summary>
/// Пункты меню — один список на рельс планшета и на нижнюю панель с листом «Ещё» телефона. В v1 их
/// было два: <c>Sidebar</c> и <c>MobileBottomNav</c> (234 строки, на iPad мёртвая) расходились.
/// Новый раздел приложения — одна строка здесь. Кабинет, вход и выход — не разделы: их рисует
/// <c>UserMenu</c> в подвале рельса и листа.
/// </summary>
public static class NavMenu
{
    public static IReadOnlyList<NavItem> Items { get; } =
    [
        new("", "fa-house", "Главная", NavGroup.Main, NavAudience.Everyone, OnPhoneBar: true),
        new("campaigns", "fa-map", "Кампании", NavGroup.Main, NavAudience.SignedIn, OnPhoneBar: true),
        new("combat", "fa-hand-fist", "Бой", NavGroup.Main, NavAudience.SignedIn, OnPhoneBar: true),
        new("chase", "fa-person-running", "Погоня", NavGroup.Main, NavAudience.SignedIn, OnPhoneBar: true),
        new("scenarios", "fa-masks-theater", "Сценарии", NavGroup.Main, NavAudience.Keeper),
        new("npcs", "fa-user-secret", "НПС", NavGroup.Main, NavAudience.Keeper),
        new("music", "fa-music", "Музыка", NavGroup.Main, NavAudience.Keeper),

        new("weapons", "fa-gun", "Оружие", NavGroup.Reference, NavAudience.SignedIn),
        new("bestiary", "fa-skull", "Существа", NavGroup.Reference, NavAudience.SignedIn),
        new("items", "fa-box", "Предметы", NavGroup.Reference, NavAudience.SignedIn),
        new("occupations", "fa-user-tie", "Профессии", NavGroup.Reference, NavAudience.SignedIn),
        new("skills", "fa-brain", "Навыки", NavGroup.Reference, NavAudience.SignedIn),
        new("spells", "fa-hat-wizard", "Заклинания", NavGroup.Reference, NavAudience.SignedIn),
        new("books", "fa-book-open", "Книги", NavGroup.Reference, NavAudience.SignedIn),

        new("admin/users", "fa-users", "Пользователи", NavGroup.System, NavAudience.Admin, ShortLabel: "Аккаунты"),
        new("admin/applications", "fa-inbox", "Заявки", NavGroup.System, NavAudience.Admin),
    ];

    /// <summary>Подписи групп в листе «Ещё»; в рельсе группы разделяет линейка.</summary>
    public static string Title(NavGroup group) => group switch
    {
        NavGroup.Reference => "Справочники",
        NavGroup.System => "Система",
        _ => "Основное",
    };

    /// <summary>
    /// Пункты, которые видит пользователь (принципал из <c>/api/v1/me</c>). Меню только не показывает
    /// лишнего — защищает сервер. Администратор несёт и роль Хранителя, поэтому видит всё.
    /// </summary>
    public static IReadOnlyList<NavItem> VisibleTo(ClaimsPrincipal user) =>
        Items.Where(item => CanSee(item.Audience, user)).ToList();

    public static bool CanSee(NavAudience audience, ClaimsPrincipal user) => audience switch
    {
        NavAudience.Everyone => true,
        NavAudience.SignedIn => user.Identity?.IsAuthenticated == true,
        NavAudience.Keeper => user.IsInRole(nameof(UserRole.Keeper)),
        NavAudience.Admin => user.IsInRole(nameof(UserRole.Admin)),
        _ => false,
    };
}

/// <param name="Href">Адрес относительно корня, без ведущего «/»: так его понимает NavLink.</param>
/// <param name="ShortLabel">Подпись в рельсе, если <paramref name="Label"/> не влезает одной строкой в 76px
/// (не длиннее «Заклинания»).</param>
/// <param name="OnPhoneBar">На нижней панели телефона; остальное — в листе «Ещё». Влезает четыре пункта.</param>
public sealed record NavItem(
    string Href,
    string Icon,
    string Label,
    NavGroup Group,
    NavAudience Audience,
    string? ShortLabel = null,
    bool OnPhoneBar = false)
{
    public string RailLabel => ShortLabel ?? Label;
}

public enum NavGroup
{
    Main,
    Reference,
    System,
}

/// <summary>Кому виден пункт. Права решает сервер (Access); меню только не показывает лишнего.</summary>
public enum NavAudience
{
    Everyone,
    SignedIn,
    Keeper,
    Admin,
}
