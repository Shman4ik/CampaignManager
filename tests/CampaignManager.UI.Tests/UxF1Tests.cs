using System.Reflection;
using Bunit;
using CampaignManager.Contracts.Admin;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Identity;
using CampaignManager.UI.Admin;
using CampaignManager.UI.Campaigns;
using CampaignManager.UI.Identity;
using CampaignManager.UI.Layout;
using CampaignManager.UI.Platform;
using CampaignManager.UI.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Исправления по ревью g1 (задача F1): тосты с объектом, плашка связи без «Сохранено», главная в две колонки, вступление без окна,
/// страница кампании, журнал, форма кампании, «Назначить Хранителем» у одобренной заявки.
/// </summary>
public sealed class UxF1Tests : KitContext
{
    private static readonly Guid CampaignId = Guid.Parse("0199b000-0000-7000-8000-000000000501");
    private static readonly Guid UserId = Guid.Parse("0199b000-0000-7000-8000-000000000502");

    // ── Тосты и плашка связи (S5, п. 7) ─────────────────────────────────────

    // Имя объекта — отдельным параметром: жирным и без обрамляющих кавычек; Message остаётся целым текстом.
    [Fact]
    public void Toast_subject_is_cleaned_of_quotes_and_rendered_bold()
    {
        var toasts = Services.GetRequiredService<ToastService>();
        toasts.Success("Кампания создана:", subject: "«Норман и сыновья»");

        var message = Assert.Single(toasts.Messages);
        Assert.Equal("Кампания создана: Норман и сыновья", message.Message);
        Assert.Equal("Норман и сыновья", message.Subject);

        var cut = Render<ToastHost>();
        Assert.Equal("Норман и сыновья", cut.Find("[data-testid=toast-subject]").TextContent);
        Assert.Contains("Кампания создана:", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Toast_without_subject_stays_a_plain_string()
    {
        var toasts = Services.GetRequiredService<ToastService>();
        toasts.Success("Кампания «Маски» сохранена.");

        var cut = Render<ToastHost>();

        Assert.Empty(cut.FindAll("[data-testid=toast-subject]"));
        Assert.Contains("Кампания «Маски» сохранена.", cut.Markup, StringComparison.Ordinal);
    }

    // Подтверждение записи одно: тост. Плашка «Сохранено» его дублировала и на телефоне лежала под ним.
    [Fact]
    public void Connection_badge_shows_saving_and_offline_but_never_saved()
    {
        var activity = Services.GetRequiredService<ApiActivity>();
        var cut = Render<ConnectionIndicator>();

        using (activity.BeginWrite())
        {
            Assert.Contains("Сохранение", cut.Markup, StringComparison.Ordinal);
        }

        activity.ReportReachable(saved: true);
        Assert.Equal(ConnectionState.Saved, activity.State);
        Assert.DoesNotContain("Сохранено", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".cm-connection"));

        activity.ReportUnreachable();
        Assert.Contains("Нет связи", cut.Markup, StringComparison.Ordinal);
    }

    // ── Оболочка (S8, R3) ───────────────────────────────────────────────────

    [Fact]
    public void Login_page_has_no_rail()
    {
        var layout = typeof(LoginPage).GetCustomAttribute<LayoutAttribute>();

        Assert.NotNull(layout);
        Assert.Equal(typeof(BareLayout), layout.LayoutType);
    }

    [Fact]
    public void Keeper_rail_has_the_screen_next_to_the_music_library()
    {
        var hrefs = NavMenu.Items.Select(i => i.Href).ToList();

        Assert.Equal(hrefs.IndexOf("music") + 1, hrefs.IndexOf("reference"));
        Assert.Equal(NavAudience.Keeper, NavMenu.Items.Single(i => i.Href == "reference").Audience);
        Assert.Equal("Ширма", NavMenu.Items.Single(i => i.Href == "reference").Label);
    }

    // ── Главная: вступление без окна (JN2), запись на место (H5) ─────────────

    private static HomeDto HomeWith(params HomeAvailableCampaignDto[] available) => new([], available, []);

    [Fact]
    public void Home_joins_at_once_with_the_profile_name_and_has_no_name_dialog()
    {
        AddAuthorization().SetAuthorized("Алиса");
        JoinCampaignRequest? sent = null;
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.GetHomeAsync)] = _ => Task.FromResult(HomeWith(
                new HomeAvailableCampaignDto(CampaignId, "Маски", CampaignKind.Campaign, CampaignStatus.Planning, DateTimeOffset.UnixEpoch, "Иван"))),
            [nameof(ICampaignsApi.JoinAsync)] = args =>
            {
                sent = (JoinCampaignRequest)args![1]!;
                return Task.FromResult<CampaignDetailsDto>(null!);
            },
        }));

        var cut = Render<HomePage>();
        cut.WaitForElement("[data-testid=join]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(sent));
        Assert.Equal("", sent!.DisplayName);
        Assert.Empty(cut.FindAll("#member-name-form"));
    }

    [Fact]
    public void Home_puts_open_signups_and_joinable_campaigns_into_the_aside_only_when_there_are_any()
    {
        AddAuthorization().SetAuthorized("Алиса");
        var withAside = HomeWith(new HomeAvailableCampaignDto(CampaignId, "Маски", CampaignKind.Campaign, CampaignStatus.Planning, DateTimeOffset.UnixEpoch, null));
        var current = withAside;
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.GetHomeAsync)] = _ => Task.FromResult(current),
        }));

        var cut = Render<HomePage>();
        cut.WaitForElement("aside");
        Assert.Contains("Можно вступить", cut.Find("aside").TextContent, StringComparison.Ordinal);
        Assert.Contains("lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task One_shot_reserve_is_secondary_and_called_taking_a_place()
    {
        var reserved = Guid.Empty;
        Services.AddSingleton(Fake.Of<IRunsApi>(new()
        {
            [nameof(IRunsApi.ReserveAsync)] = args =>
            {
                reserved = (Guid)args![1]!;
                return Task.FromResult(new ReservationDto(Guid.Empty, reserved, Guid.Empty, Guid.Empty));
            },
        }));
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()));
        var pregen = Guid.NewGuid();
        var card = Render<OneShotCard>(p => p.Add(c => c.Run,
            new HomeOneShotDto(Guid.NewGuid(), Guid.Empty, "Вечер поэзии", Guid.Empty, "Эликсир жизни", null, null, "Хранитель", false,
                [new HomePregenDto(pregen, "Доктор", null, false, null, false, null, false)], CanReserve: true)));

        var button = card.Find("[data-testid=reserve]");
        Assert.Equal("Занять", button.TextContent.Trim());
        Assert.Contains("cm-btn-secondary", button.ClassName, StringComparison.Ordinal);
        Assert.DoesNotContain("брон", card.Markup, StringComparison.OrdinalIgnoreCase);

        await button.ClickAsync(new());
        Assert.Equal(pregen, reserved);
        Assert.Equal("Вы заняли: Доктор", Services.GetRequiredService<ToastService>().Messages.Single().Message);
    }

    // ── Страница кампании ───────────────────────────────────────────────────

    private static CampaignSummaryDto Summary(bool canEdit, CampaignKind kind = CampaignKind.Campaign, int players = 1) =>
        new(CampaignId, "Маски", kind, CampaignStatus.Active, Era.Classic, DateTimeOffset.UnixEpoch,
            canEdit ? CampaignRole.Keeper : CampaignRole.Player, null, players, canEdit, canEdit);

    private static CampaignDetailsDto Details(bool keeper, ScenarioRunStatus? run = null, string? excerpt = "Штурм дома. Двое ранены.", int players = 1) => new(
        Summary(keeper, players: players),
        [new(UserId, "Алиса", null, CampaignRole.Player, DateTimeOffset.UnixEpoch, !keeper, !keeper, keeper, [])],
        CanLeave: !keeper,
        [],
        keeper && run is { } status ? [new CampaignRunDto(Guid.NewGuid(), Guid.NewGuid(), "Дом с привидениями", status, null, false)] : [],
        new CampaignLastSessionDto(Guid.NewGuid(), 3, new DateOnly(2026, 9, 20), "Подвал", excerpt));

    private void UseCampaign(CampaignDetailsDto details) =>
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.GetCampaignAsync)] = _ => Task.FromResult(details),
        }));

    // P2: тёмная «Играть» только у идущего прохождения; запланированное — «Начать» вторичной; журнал в шапке тогда главный.
    [Theory]
    [InlineData(ScenarioRunStatus.Running, "Играть", true)]
    [InlineData(ScenarioRunStatus.Planned, "Начать", false)]
    [InlineData(ScenarioRunStatus.Announced, "Начать", false)]
    public void Only_a_running_run_gets_the_dark_play_button(ScenarioRunStatus status, string label, bool primary)
    {
        UseCampaign(Details(keeper: true, status));

        var cut = Render<CampaignPage>(p => p.Add(c => c.CampaignId, CampaignId));
        var button = cut.WaitForElement("[data-testid=campaign-run] a.cm-btn");

        Assert.Equal(label, button.TextContent.Trim());
        Assert.Equal(primary, button.ClassName!.Contains("cm-btn-primary", StringComparison.Ordinal));
        var journal = cut.FindAll("header a.cm-btn").Single(a => a.TextContent.Contains("Журнал", StringComparison.Ordinal));
        Assert.Equal(!primary, journal.ClassName!.Contains("cm-btn-primary", StringComparison.Ordinal));
        Assert.Contains("fa-book-open", journal.InnerHtml, StringComparison.Ordinal);
    }

    // P7: игроку страница начинается с «Последней встречи» — номер, заголовок, первая строка и «Читать журнал».
    [Fact]
    public void Player_sees_the_last_session_card_above_the_member_list()
    {
        UseCampaign(Details(keeper: false));

        var cut = Render<CampaignPage>(p => p.Add(c => c.CampaignId, CampaignId));
        var card = cut.WaitForElement("[data-testid=last-session]");

        Assert.Contains("Встреча №3", card.TextContent, StringComparison.Ordinal);
        Assert.Contains("Подвал", card.TextContent, StringComparison.Ordinal);
        Assert.Contains("Штурм дома. Двое ранены.", card.TextContent, StringComparison.Ordinal);
        Assert.Contains("Читать журнал", card.TextContent, StringComparison.Ordinal);
        Assert.True(cut.Markup.IndexOf("last-session", StringComparison.Ordinal) < cut.Markup.IndexOf("aria-label=\"Участники\"", StringComparison.Ordinal));
    }

    // P3, P6: ни даты вступления, ни пометки про псевдоним, ни Alert-пересказа кнопки «Пригласить».
    [Fact]
    public void Member_rows_and_empty_campaign_have_no_instructions()
    {
        UseCampaign(Details(keeper: true, players: 0));

        var cut = Render<CampaignPage>(p => p.Add(c => c.CampaignId, CampaignId));
        cut.WaitForElement("[aria-label=Участники]");

        Assert.DoesNotContain("В кампании с", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("имя задано", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Можно вступить", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Нет прохождений.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("cm-btn-primary", cut.FindAll("header a, header button").Single(b => b.TextContent.Contains("Пригласить", StringComparison.Ordinal)).ClassName, StringComparison.Ordinal);
    }

    // ── Форма кампании (M1, M2) ─────────────────────────────────────────────

    [Fact]
    public void New_campaign_form_asks_name_and_kind_only_and_creates_a_planned_classic_campaign()
    {
        CampaignInput? sent = null;
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.CreateCampaignAsync)] = args =>
            {
                sent = (CampaignInput)args![0]!;
                return Task.FromResult(Summary(true));
            },
        }));

        var cut = Render<CampaignFormModal>(p => p.Add(m => m.Open, true));

        Assert.Contains("Вид", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Статус", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Эпоха", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("cm-field-required", cut.Markup, StringComparison.Ordinal);

        // Пустое название: все ошибки сразу и «Проверьте поля: 1» у кнопки, запроса нет.
        cut.Find("form").Submit();
        Assert.Contains("Проверьте поля: 1", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Введите название.", cut.Markup, StringComparison.Ordinal);
        Assert.Null(sent);

        cut.Find("input.cm-input").Input("Маски");
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => Assert.NotNull(sent));
        Assert.Equal(new CampaignInput("Маски", CampaignKind.Campaign, CampaignStatus.Planning, Era.Classic), sent);
    }

    [Fact]
    public void Edit_form_has_status_and_shows_the_kind_read_only()
    {
        CampaignInput? sent = null;
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.UpdateCampaignAsync)] = args =>
            {
                sent = (CampaignInput)args![1]!;
                return Task.FromResult(Summary(true, CampaignKind.OneShot));
            },
        }));

        var cut = Render<CampaignFormModal>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.Campaign, Summary(true, CampaignKind.OneShot)));

        Assert.Contains("Статус", cut.Markup, StringComparison.Ordinal);
        Assert.Single(cut.FindAll("select"));
        Assert.Contains("Ваншот", cut.Find("[data-testid=campaign-kind]").TextContent, StringComparison.Ordinal);

        cut.Find("form").Submit();
        cut.WaitForAssertion(() => Assert.NotNull(sent));
        Assert.Equal(CampaignKind.OneShot, sent!.Kind);
        Assert.Equal(Era.Classic, sent.Era);
    }

    [Fact]
    public void Invite_modal_title_does_not_repeat_the_campaign_name()
    {
        var cut = Render<InviteModal>(p => p.Add(m => m.Open, true).Add(m => m.Link, "https://x.test/join/1"));

        Assert.Equal("Пригласить игроков", cut.Find("h2").TextContent.Trim());
    }

    // ── Журнал ──────────────────────────────────────────────────────────────

    private static CampaignSessionDto Session(int number, string scenario, bool completed = false) => new(
        Guid.NewGuid(), number, new DateOnly(2026, 9, number), $"Встреча {number}", "Текст.", null, Guid.NewGuid(), Guid.NewGuid(), scenario, completed);

    private static CampaignJournalDto Journal(params CampaignSessionDto[] sessions) => new(
        CampaignId, "Маски", CanEdit: true, NextNumber: 9, sessions, [],
        [new JournalInvestigator(Guid.NewGuid(), "Элизабет Миллер", "Evgenii Kartoshkin")]);

    // J3: сценарий — один раз на подряд идущие встречи одного прохождения.
    [Fact]
    public void Journal_names_the_scenario_once_per_streak_of_sessions()
    {
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.GetJournalAsync)] = _ => Task.FromResult(
                Journal(Session(3, "Дом на холме"), Session(2, "Лес"), Session(1, "Лес"))),
        }));

        var cut = Render<CampaignJournalPage>(p => p.Add(c => c.CampaignId, CampaignId));
        cut.WaitForElement("article");

        Assert.Equal(2, cut.FindAll("article .cm-badge").Count(b => b.TextContent.Contains("Лес", StringComparison.Ordinal) || b.TextContent.Contains("Дом на холме", StringComparison.Ordinal)));
        Assert.Single(cut.FindAll("article .cm-badge"), b => b.TextContent.Contains("Лес", StringComparison.Ordinal));
        // Заголовок встречи — 18px, а не 24px.
        Assert.Contains("text-lg", cut.Find("article h2").ClassName, StringComparison.Ordinal);
        Assert.DoesNotContain("cm-h4", cut.Markup, StringComparison.Ordinal);
    }

    // J1, J2: короткая подсказка и кнопки листов, у которых имя игрока скрывается на телефоне.
    [Fact]
    public void Development_phase_hint_is_one_line_and_investigator_buttons_shrink_on_a_phone()
    {
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.GetJournalAsync)] = _ => Task.FromResult(Journal(Session(1, "Лес", completed: true))),
        }));

        var cut = Render<CampaignJournalPage>(p => p.Add(c => c.CampaignId, CampaignId));
        var link = cut.WaitForElement("[data-testid=investigator-link]");

        Assert.Contains("max-w-full", link.ClassName, StringComparison.Ordinal);
        Assert.Contains("hidden", link.QuerySelector("span.hidden")!.ClassName, StringComparison.Ordinal);
        Assert.Contains("md:inline", link.QuerySelector("span.hidden")!.ClassName, StringComparison.Ordinal);
        Assert.Contains("Проведите фазу развития в листе.", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Рассудок за навыки", cut.Markup, StringComparison.Ordinal);
    }

    // J5: окно встречи — две вкладки; заметки Хранителя не в конце длинной формы.
    [Fact]
    public void Session_editor_has_an_entry_tab_and_a_keeper_notes_tab()
    {
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()));
        var cut = Render<SessionEditorModal>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.CampaignId, CampaignId)
            .Add(m => m.NextNumber, 4));
        cut.WaitForElement("[role=tablist]");

        var tabs = cut.FindAll("[role=tab]").Select(t => t.TextContent.Trim()).ToList();
        Assert.Equal(["Запись", "Заметки Хранителя"], tabs);
        Assert.Contains("Хроника", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("**жирный**", cut.Markup, StringComparison.Ordinal);

        cut.FindAll("[role=tab]")[1].Click();
        Assert.Contains("Заметки Хранителя", cut.Find("textarea[placeholder^='Тайны']").ParentElement!.TextContent, StringComparison.Ordinal);
    }

    // ── Админка (A4) ────────────────────────────────────────────────────────

    private sealed class NoSession : IUserSession
    {
        public Task RefreshAsync() => Task.CompletedTask;
    }

    [Fact]
    public async Task Approved_application_of_a_still_player_offers_to_assign_the_keeper_role()
    {
        UserRole? assigned = null;
        Services.AddSingleton<IUserSession>(new NoSession());
        Services.AddSingleton(Fake.Of<IAdminApi>(new()
        {
            [nameof(IAdminApi.GetApplicationsAsync)] = _ => Task.FromResult<IReadOnlyList<KeeperApplicationDto>>(
            [
                new(Guid.NewGuid(), UserId, "Александр", "a@example.test", UserRole.Player, "", KeeperApplicationStatus.Approved,
                    DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "Админ", null),
                new(Guid.NewGuid(), Guid.NewGuid(), "Денис", "d@example.test", UserRole.Keeper, "", KeeperApplicationStatus.Approved,
                    DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "Админ", null),
            ]),
            [nameof(IAdminApi.GetSummaryAsync)] = _ => Task.FromResult(new AdminSummaryDto(0)),
            [nameof(IAdminApi.ChangeRoleAsync)] = args =>
            {
                assigned = (UserRole)args![1]!;
                return Task.FromResult(new AdminUserDto(UserId, "a@example.test", "Александр", UserRole.Keeper, null, null, false, false, 0, 0));
            },
        }));

        var cut = Render<AdminApplicationsPage>();
        cut.WaitForElement("[data-application-id]");

        // Только у того, кто всё ещё игрок.
        var buttons = cut.FindAll("[data-testid=assign-keeper]");
        Assert.Single(buttons);

        // Вопрос — тот же, что на странице пользователей; без ответа роль не меняется.
        var dialogs = Services.GetRequiredService<DialogService>();
        var click = buttons[0].ClickAsync(new());
        Assert.Equal("Назначить Хранителем?", dialogs.Current!.Title);
        Assert.Equal("Александр", dialogs.Current.Subject);
        dialogs.Complete(true);
        await click;

        cut.WaitForAssertion(() => Assert.Equal(UserRole.Keeper, assigned));
    }

    [Fact]
    public void Users_page_sorts_by_name_ascending_and_reset_is_disabled_until_a_filter_is_set()
    {
        Services.AddSingleton<IUserSession>(new NoSession());
        Services.AddSingleton(Fake.Of<IAdminApi>(new()
        {
            [nameof(IAdminApi.GetUsersAsync)] = _ => Task.FromResult<IReadOnlyList<AdminUserDto>>(
            [
                new(Guid.NewGuid(), "z@example.test", "Яков", UserRole.Player, null, null, false, false, 0, 0),
                new(Guid.NewGuid(), "a@example.test", "Алиса", UserRole.Player, null, DateTimeOffset.UnixEpoch, false, false, 0, 0),
            ]),
            [nameof(IAdminApi.GetSummaryAsync)] = _ => Task.FromResult(new AdminSummaryDto(0)),
        }));

        var cut = Render<AdminUsersPage>();
        cut.WaitForElement("table");

        var names = cut.FindAll("tbody tr td:first-child .font-semibold").Select(n => n.TextContent.Trim()).ToList();
        Assert.Equal(["Алиса", "Яков"], names);
        Assert.True(cut.FindAll("button").Single(b => b.TextContent.Contains("Сбросить фильтры", StringComparison.Ordinal)).HasAttribute("disabled"));
        Assert.DoesNotContain("в 2.0", cut.Markup, StringComparison.Ordinal);

        cut.Find("input[type=search]").Input("Али");
        Assert.False(cut.FindAll("button").Single(b => b.TextContent.Contains("Сбросить фильтры", StringComparison.Ordinal)).HasAttribute("disabled"));
    }
}
