using Bunit;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.UI.Campaigns;
using CampaignManager.UI.Identity;
using CampaignManager.UI.Layout;
using CampaignManager.UI.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>Волна UX-1, группа U1: оболочка, главная, журнал, «Нет доступа» и 404.</summary>
public sealed class UxU1Tests : KitContext
{
    private static readonly Guid CampaignId = Guid.Parse("0199b000-0000-7000-8000-000000000301");

    // ── Оболочка ────────────────────────────────────────────────────────────

    // Хранителю на телефоне — подготовка: «Сценарии» на панели, «Погоня» в «Ещё» (её открывают из режима игры).
    [Fact]
    public void Phone_bar_puts_scenarios_before_chase()
    {
        var bar = NavMenu.Items.Where(i => i.OnPhoneBar).Select(i => i.Href).ToList();

        Assert.Contains("scenarios", bar);
        Assert.DoesNotContain("chase", bar);
        Assert.InRange(bar.Count, 1, 4);
    }

    // Группы рельса разделены линейкой без подписи (подпись 10px нарушала п. 2); имя группы — для диктора (S1, S2).
    [Fact]
    public void Rail_groups_are_separated_by_a_rule_and_named_for_screen_readers_only()
    {
        var cut = Render<NavRail>(p => p.Add(r => r.Items, NavMenu.Items));

        Assert.Empty(cut.FindAll(".cm-rail-group-title"));
        Assert.Equal(["Основное", "Справочники", "Система"], cut.FindAll(".cm-rail-group").Select(g => g.GetAttribute("aria-label")));
    }

    // Решение оркестратора (g1/S7, g6/Sh1): лист «Ещё» не повторяет пункты панели; сначала остальное и справочники, «Система»,
    // «О проекте», а учётная запись — последней (справочники игрока начинались на y=481 из 651).
    [Fact]
    public void More_sheet_ends_with_the_account_and_does_not_repeat_the_phone_bar()
    {
        var cut = Render<BottomNav>(p => p
            .Add(b => b.Items, NavMenu.Items)
            .Add(b => b.Account, (RenderFragment)(builder => builder.AddContent(0, "АККАУНТ")))
            .Add(b => b.Footer, (RenderFragment)(builder => builder.AddContent(0, "ПОДВАЛ"))));

        cut.Find("button[aria-haspopup='dialog']").Click();

        var markup = cut.Markup;
        Assert.True(markup.IndexOf("Справочники", StringComparison.Ordinal) < markup.IndexOf("Система", StringComparison.Ordinal));
        Assert.True(markup.IndexOf("Система", StringComparison.Ordinal) < markup.IndexOf("ПОДВАЛ", StringComparison.Ordinal));
        Assert.True(markup.IndexOf("ПОДВАЛ", StringComparison.Ordinal) < markup.IndexOf("АККАУНТ", StringComparison.Ordinal));

        var sheet = cut.Find("dialog");
        var barLabels = NavMenu.Items.Where(i => i.OnPhoneBar).Select(i => i.Label).ToList();
        Assert.All(barLabels, label => Assert.DoesNotContain(sheet.QuerySelectorAll(".cm-sheet-item").Select(a => a.TextContent.Trim()), t => t == label));
        Assert.Contains("Фонотека", sheet.TextContent, StringComparison.Ordinal);
    }

    // ── «Нет доступа» и 404 ─────────────────────────────────────────────────

    [Fact]
    public void No_access_for_admin_pages_does_not_offer_a_keeper_application()
    {
        var cut = Render<NoAccess>(p => p.Add(a => a.AdminOnly, true));

        Assert.Contains("только для администратора", cut.Find("[data-testid=no-access-text]").TextContent);
        Assert.DoesNotContain("заявк", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void No_access_for_a_player_on_a_keeper_page_points_to_the_application()
    {
        var cut = Render<NoAccess>();

        Assert.Contains("Хранителям", cut.Find("[data-testid=no-access-text]").TextContent);
        Assert.Contains("заявку", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void No_access_for_a_keeper_does_not_send_him_to_apply()
    {
        var cut = Render<NoAccess>(p => p.Add(a => a.IsKeeperOrAdmin, true));

        Assert.DoesNotContain("заявк", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Not_found_shows_the_missing_address_and_no_internal_wording()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("zzz/missing?x=1");

        var cut = Render<NotFound>();

        Assert.Contains("/zzz/missing", cut.Find("[data-testid=empty-state]").TextContent);
        Assert.Contains("Такой страницы нет", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("2.0", cut.Markup, StringComparison.Ordinal);
    }

    // ── Главная ─────────────────────────────────────────────────────────────

    private static HomeCampaignDto Mine(CampaignStatus status, CampaignRole role, HomeCharacterDto? sheet = null,
        IReadOnlyList<HomePlayerDto>? players = null) =>
        new(CampaignId, "Маски", CampaignKind.Campaign, status, role, "Хранитель", players?.Count ?? 0, sheet, players ?? [], []);

    [Fact]
    public void Completed_campaign_is_a_row_with_the_journal_and_no_creation_actions()
    {
        var cut = Render<HomeCampaignCard>(p => p.Add(c => c.Campaign, Mine(CampaignStatus.Completed, CampaignRole.Player)));

        Assert.Single(cut.FindAll("[data-testid=home-completed]"));
        Assert.Contains("Журнал", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Создать сыщика", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("article", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Active_campaign_of_a_player_without_a_sheet_offers_to_create_one()
    {
        var cut = Render<HomeCampaignCard>(p => p.Add(c => c.Campaign, Mine(CampaignStatus.Active, CampaignRole.Player)));

        Assert.Contains("Создать сыщика", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Пустой лист", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeper_card_without_players_says_so_in_one_line_and_has_no_zero_sections()
    {
        var cut = Render<HomeCampaignCard>(p => p.Add(c => c.Campaign, Mine(CampaignStatus.Active, CampaignRole.Keeper)));

        Assert.Contains("Игроков пока нет.", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("вступает сам", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Игроки (0)", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("НПС кампании (0)", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("fa-plus", cut.Find(".cm-card-footer").InnerHtml, StringComparison.Ordinal);
    }

    private static HomeOneShotDto OneShot() =>
        new(Guid.Parse("0199b000-0000-7000-8000-000000000302"), Guid.Empty, "Вечер поэзии", Guid.Empty, "Эликсир жизни", null, "Длинный анонс", "Хранитель",
            IsMine: false,
            [new HomePregenDto(Guid.Parse("0199b000-0000-7000-8000-000000000303"), "Доктор", null, false, null, false, null, false)],
            CanReserve: true);

    [Fact]
    public void One_shot_shows_pregens_at_once_and_clamps_the_announcement_only_on_the_phone()
    {
        Services.AddSingleton(Fake.Of<IRunsApi>(new()));
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()));
        var cut = Render<OneShotCard>(p => p.Add(c => c.Run, OneShot()));

        Assert.Single(cut.FindAll("[data-testid=reserve]"));
        Assert.Contains("max-md:line-clamp-4", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("md:hidden", cut.Find("[data-testid=announcement-toggle]").ClassName, StringComparison.Ordinal);

        cut.Find("[data-testid=announcement-toggle]").Click();
        Assert.DoesNotContain("line-clamp", cut.Markup, StringComparison.Ordinal);
    }

    // ── Журнал ──────────────────────────────────────────────────────────────

    [Fact]
    public void Journal_entry_is_collapsed_until_read_in_full()
    {
        var session = new CampaignSessionDto(Guid.Parse("0199b000-0000-7000-8000-000000000304"), 1, new DateOnly(2026, 10, 2), "Дом на Френч-Хилл",
            "Что было\n\nДлинная хроника", "Тайна пастуха", null, null, null, ScenarioCompleted: false);
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.GetJournalAsync)] = _ => Task.FromResult(new CampaignJournalDto(CampaignId, "Маски", true, 2, [session], [], [])),
        }));

        var cut = Render<CampaignJournalPage>(p => p.Add(j => j.CampaignId, CampaignId));
        cut.WaitForElement("[data-testid=session-toggle]");

        Assert.Contains("line-clamp-3", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Тайна пастуха", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid=session-toggle]").Click();

        Assert.DoesNotContain("line-clamp-3", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Тайна пастуха", cut.Markup, StringComparison.Ordinal);
    }

    // «Читать полностью» — только там, где запись реально длиннее трёх строк; у короткой кнопки нет.
    [Fact]
    public void Short_journal_entry_has_no_read_more_button()
    {
        var session = new CampaignSessionDto(Guid.Parse("0199b000-0000-7000-8000-000000000305"), 1, new DateOnly(2026, 10, 2), null,
            "Короткая запись.", null, null, null, null, ScenarioCompleted: false);
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.GetJournalAsync)] = _ => Task.FromResult(new CampaignJournalDto(CampaignId, "Маски", true, 2, [session], [], [])),
        }));

        var cut = Render<CampaignJournalPage>(p => p.Add(j => j.CampaignId, CampaignId));
        cut.WaitForElement("article");

        Assert.Empty(cut.FindAll("[data-testid=session-toggle]"));
    }

    [Fact]
    public void Empty_journal_has_one_new_meeting_button_in_the_header_only()
    {
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.GetJournalAsync)] = _ => Task.FromResult(new CampaignJournalDto(CampaignId, "Маски", true, 1, [], [], [])),
        }));

        var cut = Render<CampaignJournalPage>(p => p.Add(j => j.CampaignId, CampaignId));
        cut.WaitForElement("[data-testid=empty-state]");

        Assert.Single(cut.FindAll("button"), b => b.TextContent.Contains("Новая встреча", StringComparison.Ordinal));
    }

    // ── Вход ────────────────────────────────────────────────────────────────

    [Fact]
    public void Login_after_a_redirect_explains_why_the_user_is_here()
    {
        AddAuthorization();
        Services.GetRequiredService<NavigationManager>().NavigateTo("login?returnUrl=%2Fcampaigns");

        var cut = Render<LoginPage>();

        Assert.Contains("вы вернётесь на неё", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Как войти", cut.Markup, StringComparison.Ordinal);
    }

    // ── /about и /legal ─────────────────────────────────────────────────────

    // Тела страниц (их рендерит сервер статически), тексты про 2.0: ни кодов приглашения, ни вики, ни «issue».
    [Fact]
    public void About_content_has_no_v1_leftovers()
    {
        var about = Render<AboutContent>();

        AssertShellWithoutV1Leftovers(about.Markup, about);
        Assert.Contains("ссылку-приглашение", about.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Legal_content_has_no_v1_leftovers()
    {
        var legal = Render<LegalContent>();

        AssertShellWithoutV1Leftovers(legal.Markup, legal);
        Assert.Contains("Chaosium Inc.", legal.Markup, StringComparison.Ordinal);
    }

    private static void AssertShellWithoutV1Leftovers(string text, Bunit.IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> cut)
    {
        Assert.Single(cut.FindAll("div.cm-page"));
        Assert.DoesNotContain("коду приглашения", text, StringComparison.Ordinal);
        Assert.DoesNotContain("вики", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("issue", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NPC", text, StringComparison.Ordinal);
    }
}
