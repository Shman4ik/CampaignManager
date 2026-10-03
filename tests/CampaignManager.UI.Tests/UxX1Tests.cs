using Bunit;
using CampaignManager.Contracts.Admin;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Contracts.Profile;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Identity;
using CampaignManager.UI.Admin;
using CampaignManager.UI.Campaigns;
using CampaignManager.UI.Identity;
using CampaignManager.UI.Profile;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>Волна UX-2, группа X1: страница кампании (сыщики, НПС, прохождения, приглашение), кабинет, заявки, пользователи.</summary>
public sealed class UxX1Tests : KitContext
{
    private static readonly Guid CampaignId = Guid.Parse("0199b000-0000-7000-8000-000000000401");
    private static readonly Guid PlayerId = Guid.Parse("0199b000-0000-7000-8000-000000000402");
    private static readonly Guid KeeperId = Guid.Parse("0199b000-0000-7000-8000-000000000403");

    private static CampaignSummaryDto Summary(bool canEdit, CampaignStatus status = CampaignStatus.Active) =>
        new(CampaignId, "Маски", CampaignKind.Campaign, status, CampaignManager.Core.Era.Classic, DateTimeOffset.UnixEpoch,
            canEdit ? CampaignRole.Keeper : CampaignRole.Player, null, 1, canEdit, canEdit);

    private static CampaignDetailsDto Details(bool keeper, CampaignStatus status = CampaignStatus.Active) => new(
        Summary(keeper, status),
        [
            new(KeeperId, "Хранитель", null, CampaignRole.Keeper, DateTimeOffset.UnixEpoch, keeper, false, false, []),
            new(PlayerId, "Алиса", null, CampaignRole.Player, DateTimeOffset.UnixEpoch, !keeper, !keeper, keeper,
                [new HomeCharacterDto(Guid.NewGuid(), "Харви Уолтерс", "Журналист", CharacterKind.Player, CharacterStatus.Active)]),
        ],
        CanLeave: !keeper,
        keeper ? [new HomeCharacterDto(Guid.NewGuid(), "Старый Нед", null, CharacterKind.Npc, CharacterStatus.Active)] : [],
        keeper ? [new CampaignRunDto(Guid.NewGuid(), Guid.NewGuid(), "Дом с привидениями", ScenarioRunStatus.Running, null, false)] : [],
        new CampaignLastSessionDto(Guid.NewGuid(), 3, new DateOnly(2026, 9, 20), "Подвал"));

    private void UseCampaign(CampaignDetailsDto details) =>
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.GetCampaignAsync)] = _ => Task.FromResult(details),
        }));

    // ── Страница кампании ───────────────────────────────────────────────────

    [Fact]
    public void Keeper_page_shows_player_sheets_npcs_runs_last_session_and_invite()
    {
        UseCampaign(Details(keeper: true));

        var cut = Render<CampaignPage>(p => p.Add(c => c.CampaignId, CampaignId));
        cut.WaitForElement("[data-testid=campaign-run]");

        Assert.Contains("Харви Уолтерс", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Старый Нед", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Дом с привидениями", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Последняя встреча", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("№3", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Пригласить", cut.Markup, StringComparison.Ordinal);
        // Играть открывает режим игры именно этого прохождения.
        Assert.Contains("mode=play", cut.Find("[data-testid=campaign-run] a.cm-btn").GetAttribute("href"), StringComparison.Ordinal);
    }

    [Fact]
    public void Player_page_has_only_own_sheet_and_no_keeper_sections_or_invite()
    {
        UseCampaign(Details(keeper: false));

        var cut = Render<CampaignPage>(p => p.Add(c => c.CampaignId, CampaignId));
        cut.WaitForElement("[aria-label=Участники]");

        Assert.Contains("Харви Уолтерс", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid=campaign-run]"));
        Assert.DoesNotContain("НПС кампании", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Прохождения", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Пригласить", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Completed_campaign_cannot_be_invited_to()
    {
        UseCampaign(Details(keeper: true, CampaignStatus.Completed));

        var cut = Render<CampaignPage>(p => p.Add(c => c.CampaignId, CampaignId));
        cut.WaitForElement("[aria-label=Участники]");

        Assert.DoesNotContain("Пригласить", cut.Markup, StringComparison.Ordinal);
    }

    // Завершённой кампании не нужны действия живой игры: «Добавить НПС», подсказка про начало игры, «Выйти» (журнал — архив).
    [Fact]
    public void Completed_campaign_has_no_live_game_actions_for_keeper_or_player()
    {
        UseCampaign(Details(keeper: true, CampaignStatus.Completed) with { Runs = [] });
        var keeper = Render<CampaignPage>(p => p.Add(c => c.CampaignId, CampaignId));
        keeper.WaitForElement("[aria-label=Участники]");

        Assert.DoesNotContain("Добавить НПС", keeper.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("К сценариям", keeper.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Начать можно из сценария", keeper.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Completed_campaign_does_not_offer_the_player_to_leave()
    {
        UseCampaign(Details(keeper: false, CampaignStatus.Completed));
        var cut = Render<CampaignPage>(p => p.Add(c => c.CampaignId, CampaignId));
        cut.WaitForElement("[aria-label=Участники]");

        Assert.DoesNotContain("Выйти из кампании", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Active_campaign_keeper_sees_add_npc()
    {
        UseCampaign(Details(keeper: true));
        var keeper = Render<CampaignPage>(p => p.Add(c => c.CampaignId, CampaignId));
        keeper.WaitForElement("[data-testid=campaign-run]");
        Assert.Contains("Добавить НПС", keeper.Markup, StringComparison.Ordinal);
    }

    // В шапке длинное название обрезается, поэтому страница показывает его целиком и строку вида и эпохи; статус и роль —
    // только в подзаголовке шапки, второй раз их не повторяем.
    [Fact]
    public void Long_campaign_name_is_shown_in_full_under_the_header_with_kind_and_era()
    {
        var details = Details(keeper: true);
        var longName = "Очень длинное название кампании для проверки переноса строки в шапке";
        UseCampaign(details with { Campaign = details.Campaign with { Name = longName } });

        var cut = Render<CampaignPage>(p => p.Add(c => c.CampaignId, CampaignId));
        cut.WaitForElement("[data-testid=campaign-full-name]");

        Assert.Equal(longName, cut.Find("[data-testid=campaign-full-name]").TextContent);
        var facts = cut.Find("[data-testid=campaign-facts]").TextContent;
        Assert.Contains("Кампания", facts, StringComparison.Ordinal);
        Assert.DoesNotContain("вы — Хранитель", facts, StringComparison.Ordinal);
    }

    [Fact]
    public void Invite_modal_shows_absolute_link_and_copies_it()
    {
        UseCampaign(Details(keeper: true));
        var cut = Render<CampaignPage>(p => p.Add(c => c.CampaignId, CampaignId));
        cut.WaitForElement("[data-testid=campaign-run]");

        cut.FindAll("button").Single(b => b.TextContent.Contains("Пригласить", StringComparison.Ordinal)).Click();

        var link = cut.WaitForElement("[data-testid=invite-link]").GetAttribute("value");
        Assert.EndsWith($"/join/{CampaignId}", link, StringComparison.Ordinal);
        Assert.StartsWith("http", link, StringComparison.Ordinal);

        cut.FindAll("button").Single(b => b.TextContent.Contains("Скопировать ссылку", StringComparison.Ordinal)).Click();

        Assert.Contains("Ссылка скопирована", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(JSInterop.Invocations, i => i.Identifier == "navigator.clipboard.writeText" && Equals(i.Arguments[0], link));
    }

    // ── Ссылка-приглашение ──────────────────────────────────────────────────

    [Fact]
    public void Join_page_joins_with_the_chosen_name_and_opens_the_campaign()
    {
        AddAuthorization().SetAuthorized("Алиса");
        JoinCampaignRequest? sent = null;
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.GetInviteAsync)] = _ => Task.FromResult(
                new CampaignInviteDto(CampaignId, "Маски", CampaignKind.Campaign, CampaignStatus.Active, CampaignManager.Core.Era.Classic, "Иван", 2, false, true)),
            [nameof(ICampaignsApi.JoinAsync)] = args =>
            {
                sent = (JoinCampaignRequest)args![1]!;
                return Task.FromResult(Details(keeper: false));
            },
        }));

        var cut = Render<JoinCampaignPage>(p => p.Add(j => j.CampaignId, CampaignId));
        Assert.Equal("Маски", cut.WaitForElement("[data-testid=invite-name]").TextContent);
        Assert.Contains("Иван", cut.Markup, StringComparison.Ordinal);

        cut.FindAll("button").Single(b => b.TextContent.Contains("Вступить", StringComparison.Ordinal)).Click();
        cut.WaitForElement("#member-name-form").Submit();

        cut.WaitForAssertion(() => Assert.NotNull(sent));
        Assert.EndsWith($"/campaigns/{CampaignId}", Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Join_page_sends_a_member_straight_to_the_campaign_and_closed_invite_is_empty()
    {
        AddAuthorization().SetAuthorized("Алиса");
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.GetInviteAsync)] = _ => Task.FromResult(
                new CampaignInviteDto(CampaignId, "Маски", CampaignKind.Campaign, CampaignStatus.Active, CampaignManager.Core.Era.Classic, null, 1, true, false)),
        }));

        Render<JoinCampaignPage>(p => p.Add(j => j.CampaignId, CampaignId));

        Assert.EndsWith($"/campaigns/{CampaignId}",
            Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Join_page_for_a_missing_campaign_says_the_invite_is_invalid()
    {
        AddAuthorization().SetAuthorized("Алиса");
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.GetInviteAsync)] = _ => throw new HttpRequestException("нет", null, System.Net.HttpStatusCode.NotFound),
        }));

        var cut = Render<JoinCampaignPage>(p => p.Add(j => j.CampaignId, CampaignId));

        cut.WaitForAssertion(() => Assert.Contains("Приглашение недействительно", cut.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("Вступить", cut.Markup, StringComparison.Ordinal);
    }

    // ── Кабинет ─────────────────────────────────────────────────────────────

    private static ProfileDto PendingProfile() => new(Guid.NewGuid(), "a@example.test", "Алиса", UserRole.Player, 0, 0, 0,
        new MyKeeperApplicationDto(Guid.NewGuid(), KeeperApplicationStatus.Pending, "Хочу вести", DateTimeOffset.UnixEpoch, null, null),
        CanApply: false);

    private sealed class ProfileSpy
    {
        public SubmitKeeperApplicationRequest? Edit;
        public int Withdrawals;
        public ProfileDto Profile = PendingProfile();
    }

    private ProfileSpy UseProfile()
    {
        var spy = new ProfileSpy();
        Services.AddSingleton(Fake.Of<IProfileApi>(new()
        {
            [nameof(IProfileApi.GetProfileAsync)] = _ => Task.FromResult(spy.Profile),
            [nameof(IProfileApi.UpdateKeeperApplicationAsync)] = args =>
            {
                spy.Edit = (SubmitKeeperApplicationRequest)args![0]!;
                spy.Profile = spy.Profile with { LatestApplication = spy.Profile.LatestApplication! with { Message = spy.Edit.Message! } };
                return Task.FromResult(spy.Profile);
            },
            [nameof(IProfileApi.WithdrawKeeperApplicationAsync)] = _ =>
            {
                spy.Withdrawals++;
                spy.Profile = spy.Profile with { LatestApplication = null, CanApply = true };
                return Task.FromResult(spy.Profile);
            },
        }));
        Services.AddSingleton<IUserSession>(new NoSession());
        return spy;
    }

    private sealed class NoSession : IUserSession
    {
        public Task RefreshAsync() => Task.CompletedTask;
    }

    [Fact]
    public void Pending_application_can_be_edited_and_saved()
    {
        var spy = UseProfile();
        var cut = Render<ProfilePage>();
        var form = cut.WaitForElement("[data-testid=application-edit]");

        // Текст не менялся — сохранять нечего.
        Assert.True(form.QuerySelector("button[type=submit]")!.HasAttribute("disabled"));

        form.QuerySelector("textarea")!.Input("Хочу вести «Маски», играем по пятницам");
        cut.Find("[data-testid=application-edit]").Submit();

        cut.WaitForAssertion(() => Assert.Equal("Хочу вести «Маски», играем по пятницам", spy.Edit?.Message));
    }

    [Fact]
    public void Withdrawing_the_application_asks_first_and_then_offers_a_new_one()
    {
        var spy = UseProfile();
        var host = Render<CampaignManager.UI.Shared.DialogHost>();
        var cut = Render<ProfilePage>();
        cut.WaitForElement("[data-testid=application-edit]");

        cut.FindAll("button").Single(b => b.TextContent.Contains("Отозвать заявку", StringComparison.Ordinal)).Click();
        Assert.Equal(0, spy.Withdrawals);
        host.WaitForElement("dialog");
        host.Find("[data-testid='confirm-dialog-ok']").Click();

        cut.WaitForAssertion(() => Assert.Equal(1, spy.Withdrawals));
        cut.WaitForAssertion(() => Assert.Contains("Подать заявку на Хранителя", cut.Markup, StringComparison.Ordinal));
        Assert.Empty(cut.FindAll("[data-testid=application-edit]"));
    }

    [Fact]
    public void Reviewed_application_has_no_edit_form()
    {
        var spy = UseProfile();
        spy.Profile = spy.Profile with
        {
            LatestApplication = spy.Profile.LatestApplication! with { Status = KeeperApplicationStatus.Rejected, ReviewComment = "Позже" },
            CanApply = true,
        };

        var cut = Render<ProfilePage>();
        cut.WaitForElement("[data-testid=application-status]");

        Assert.Empty(cut.FindAll("[data-testid=application-edit]"));
        Assert.DoesNotContain("Отозвать заявку", cut.Markup, StringComparison.Ordinal);
    }

    // ── Заявки и пользователи ───────────────────────────────────────────────

    [Fact]
    public void Application_of_a_player_is_approved_with_the_button_named_after_the_result()
    {
        Services.AddSingleton(Fake.Of<IAdminApi>(new()
        {
            [nameof(IAdminApi.GetApplicationsAsync)] = _ => Task.FromResult<IReadOnlyList<KeeperApplicationDto>>(
            [
                new(Guid.NewGuid(), Guid.NewGuid(), "Алиса", "a@example.test", UserRole.Player, "Хочу", KeeperApplicationStatus.Pending,
                    DateTimeOffset.UnixEpoch, null, null, null),
                new(Guid.NewGuid(), Guid.NewGuid(), "Админ", "b@example.test", UserRole.Admin, "", KeeperApplicationStatus.Pending,
                    DateTimeOffset.UnixEpoch, null, null, null),
            ]),
            [nameof(IAdminApi.GetSummaryAsync)] = _ => Task.FromResult(new AdminSummaryDto(2)),
        }));

        var cut = Render<AdminApplicationsPage>();
        cut.WaitForElement("[data-application-id]");

        var rows = cut.FindAll("[data-application-id]");
        Assert.Contains("Назначить Хранителем", rows[0].TextContent, StringComparison.Ordinal);
        // Администратору Хранитель не нужен: у него «Одобрить», роль не меняется.
        Assert.DoesNotContain("Назначить Хранителем", rows[1].TextContent, StringComparison.Ordinal);
        Assert.Contains("Одобрить", rows[1].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Users_table_has_campaign_sheet_and_registration_columns()
    {
        Services.AddSingleton<IUserSession>(new NoSession());
        Services.AddSingleton(Fake.Of<IAdminApi>(new()
        {
            [nameof(IAdminApi.GetUsersAsync)] = _ => Task.FromResult<IReadOnlyList<AdminUserDto>>(
            [
                new(Guid.NewGuid(), "a@example.test", "Алиса", UserRole.Player, new DateTimeOffset(2026, 3, 14, 12, 0, 0, TimeSpan.Zero),
                    null, false, false, CampaignCount: 3, CharacterCount: 5),
            ]),
            [nameof(IAdminApi.GetSummaryAsync)] = _ => Task.FromResult(new AdminSummaryDto(0)),
        }));

        var cut = Render<AdminUsersPage>();
        cut.WaitForElement("table");

        var headers = cut.FindAll("th").Select(h => h.TextContent.Trim()).ToList();
        Assert.Contains("Кампании", headers);
        Assert.Contains("Листы", headers);
        Assert.Contains("Регистрация", headers);
        var cells = cut.FindAll("tbody tr td").Select(c => c.TextContent.Trim()).ToList();
        Assert.Contains("3", cells);
        Assert.Contains("5", cells);
        Assert.Contains(cells, c => c.Contains("14.03.2026", StringComparison.Ordinal));
    }

    // «Регистрация» перенесённого без записей в v1 — пусто, а не день переноса.
    [Fact]
    public void Registration_is_a_dash_when_the_user_has_no_known_date()
    {
        Services.AddSingleton<IUserSession>(new NoSession());
        Services.AddSingleton(Fake.Of<IAdminApi>(new()
        {
            [nameof(IAdminApi.GetUsersAsync)] = _ => Task.FromResult<IReadOnlyList<AdminUserDto>>(
            [
                new(Guid.NewGuid(), "a@example.test", "Алиса", UserRole.Player, null, null, false, false, CampaignCount: 0, CharacterCount: 0),
            ]),
            [nameof(IAdminApi.GetSummaryAsync)] = _ => Task.FromResult(new AdminSummaryDto(0)),
        }));

        var cut = Render<AdminUsersPage>();
        cut.WaitForElement("table");

        var cells = cut.FindAll("tbody tr td").Select(c => c.TextContent.Trim()).ToList();
        Assert.Contains("—", cells);
        Assert.DoesNotContain(cells, c => c.Contains("2026", StringComparison.Ordinal));
    }
}
