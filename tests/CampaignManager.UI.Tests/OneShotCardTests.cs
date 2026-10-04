using Bunit;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.UI.Campaigns;
using CampaignManager.UI.Platform;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Кнопки записи на место на карточке ваншота главной (T2.5c; «Занять» и «Снять запись», в коде — бронь) — по флагам сервера: свободного сыщика занимает тот, кому можно
/// (<see cref="HomeOneShotDto.CanReserve"/>), у своего — «Лист» и «Снять запись», ведущий снимает любую.
/// </summary>
public sealed class OneShotCardTests : KitContext
{
    private static readonly Guid RunId = Guid.Parse("0199b000-0000-7000-8000-000000000201");
    private static readonly Guid Free = Guid.Parse("0199b000-0000-7000-8000-000000000202");
    private static readonly Guid Taken = Guid.Parse("0199b000-0000-7000-8000-000000000203");
    private static readonly Guid Sheet = Guid.Parse("0199b000-0000-7000-8000-000000000204");
    private static readonly Guid CampaignId = Guid.Parse("0199b000-0000-7000-8000-000000000205");

    private readonly List<Guid> _reserved = [];
    private readonly List<Guid> _joined = [];

    public OneShotCardTests()
    {
        Services.AddSingleton(Fake.Of<IRunsApi>(new()
        {
            [nameof(IRunsApi.ReserveAsync)] = args =>
            {
                _reserved.Add((Guid)args![1]!);
                return Task.FromResult(new ReservationDto(RunId, (Guid)args[1]!, Sheet, Guid.Empty));
            },
        }));
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.JoinAsync)] = args =>
            {
                _joined.Add((Guid)args![0]!);
                return Task.FromResult<CampaignDetailsDto>(null!);
            },
        }));
    }

    private static HomeOneShotDto Run(bool canReserve, params HomePregenDto[] pregens) =>
        new(RunId, CampaignId, "Вечер поэзии", Guid.Empty, "Эликсир жизни", null, null, "Хранитель", IsMine: false, pregens, canReserve);

    [Fact]
    public async Task Player_without_reservation_reserves_a_free_pregen_and_the_page_reloads()
    {
        var reloaded = 0;
        var card = Render<OneShotCard>(p => p
            .Add(c => c.Run, Run(true,
                new HomePregenDto(Free, "Доктор", null, false, null, false, null, false),
                new HomePregenDto(Taken, "Букинист", null, true, "Анна", false, null, false)))
            .Add(c => c.OnChanged, () => reloaded++));

        Assert.Single(card.FindAll("[data-testid=reserve]"));
        Assert.Empty(card.FindAll("[data-testid=release]"));
        Assert.Contains("Занят: Анна", card.Markup, StringComparison.Ordinal);

        await card.Find("[data-testid=reserve]").ClickAsync(new());
        Assert.Equal([Free], _reserved);
        Assert.Equal(1, reloaded);
    }

    [Fact]
    public void Own_reservation_links_the_copy_and_can_be_released()
    {
        var card = Render<OneShotCard>(p => p.Add(c => c.Run, Run(false,
            new HomePregenDto(Free, "Доктор", null, false, null, false, null, false),
            new HomePregenDto(Taken, "Букинист", null, true, "Анна", true, Sheet, true))));

        Assert.Empty(card.FindAll("[data-testid=reserve]"));
        Assert.Single(card.FindAll("[data-testid=release]"));
        Assert.Single(card.FindAll($"a[href='character/{Sheet}']"));
        Assert.Contains("Свободен", card.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Datetime_input_round_trips_through_utc()
    {
        var moment = new DateTimeOffset(2026, 10, 10, 17, 0, 0, TimeSpan.Zero);
        var input = LocalTime.ToInput(moment);
        Assert.Equal(moment, LocalTime.FromInput(input));
        Assert.Equal(TimeSpan.Zero, LocalTime.FromInput(input)!.Value.Offset);
        Assert.Null(LocalTime.FromInput(null));
    }

    // ── Свой сыщик вместо готового (владелец 2026-10-04) ────────────────────

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task Create_own_joins_the_game_unless_a_member_and_opens_the_wizard(bool member, int joins)
    {
        var card = Render<OneShotCard>(p => p
            .Add(c => c.Run, Run(true, new HomePregenDto(Free, "Доктор", null, false, null, false, null, false)))
            .Add(c => c.IsMember, member));

        await card.Find("[data-testid=create-own]").ClickAsync(new());

        Assert.Equal(joins, _joined.Count);
        Assert.EndsWith($"character/wizard?campaignId={CampaignId}", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_own_is_offered_only_while_signup_is_open_for_me()
    {
        var card = Render<OneShotCard>(p => p.Add(c => c.Run, Run(false, new HomePregenDto(Free, "Доктор", null, false, null, false, null, false))));
        Assert.Empty(card.FindAll("[data-testid=create-own]"));
    }

    // ── Раскладка главной по тому, кто смотрит ──────────────────────────────

    private static HomeOneShotDto RunFor(bool leads, bool mine = false) => new(RunId, CampaignId, "Вечер поэзии", Guid.Empty, "Эликсир жизни", null, null,
        "Хранитель", leads, [new HomePregenDto(Free, "Доктор", null, mine, null, mine, mine ? Sheet : null, mine)], CanReserve: !leads && !mine);

    private static HomeCampaignDto Campaign(CampaignRole role, HomeCharacterDto? character = null) => new(CampaignId, "Вечер поэзии", CampaignKind.OneShot,
        CampaignStatus.Planning, role, null, 0, character, [], []);

    private static readonly HomeAvailableCampaignDto AvailableGame = new(CampaignId, "Вечер поэзии", CampaignKind.OneShot, CampaignStatus.Planning,
        DateTimeOffset.UnixEpoch, "Хранитель");

    [Fact]
    public void Keeper_sees_own_game_on_the_campaign_card_not_as_an_announcement()
    {
        var layout = HomeLayout.Of(new HomeDto([Campaign(CampaignRole.Keeper)], [], [RunFor(leads: true)]));

        Assert.Empty(layout.Invites);
        Assert.NotNull(layout.RunOf(CampaignId));
    }

    [Fact]
    public void Player_with_a_seat_has_no_announcement_and_others_get_it_instead_of_can_join()
    {
        var seat = new HomeCharacterDto(Sheet, "Доктор", null, CharacterKind.Player, CharacterStatus.Active);
        Assert.Empty(HomeLayout.Of(new HomeDto([Campaign(CampaignRole.Player, seat)], [], [RunFor(leads: false, mine: true)])).Invites);

        var stranger = HomeLayout.Of(new HomeDto([], [AvailableGame], [RunFor(leads: false)]));
        Assert.Single(stranger.Invites);
        Assert.Empty(stranger.Available);
        Assert.False(stranger.IsMember(CampaignId));
    }

    [Fact]
    public void Home_without_own_campaigns_puts_the_announcement_first()
    {
        AddAuthorization().SetAuthorized("Алиса");
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.GetHomeAsync)] = _ => Task.FromResult(new HomeDto([], [AvailableGame], [RunFor(leads: false) with { Announcement = "Анонс" }])),
        }));

        var cut = Render<HomePage>();
        cut.WaitForElement("[data-testid=one-shot]");

        Assert.True(cut.Markup.IndexOf("home-oneshots", StringComparison.Ordinal) < cut.Markup.IndexOf("home-mine", StringComparison.Ordinal));
        Assert.DoesNotContain("lg:order-last", cut.Find("section[aria-labelledby=home-oneshots]").ClassName, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("aside"));
    }

    [Fact]
    public void Home_with_own_campaigns_puts_the_announcement_right_under_them()
    {
        AddAuthorization().SetAuthorized("Алиса");
        var other = Campaign(CampaignRole.Player) with { Id = Guid.NewGuid(), Name = "Маски", Kind = CampaignKind.Campaign };
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.GetHomeAsync)] = _ => Task.FromResult(new HomeDto([other], [AvailableGame], [RunFor(leads: false) with { Announcement = "Анонс" }])),
        }));

        var cut = Render<HomePage>();
        cut.WaitForElement("[data-testid=one-shot]");

        Assert.True(cut.Markup.IndexOf("home-mine", StringComparison.Ordinal) < cut.Markup.IndexOf("home-oneshots", StringComparison.Ordinal));
        Assert.Contains("lg:order-last", cut.Find("section[aria-labelledby=home-oneshots]").ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeper_campaign_card_tells_that_signup_is_open()
    {
        AddAuthorization().SetAuthorized("Хранитель");
        Services.AddSingleton(Fake.Of<ICampaignsApi>(new()
        {
            [nameof(ICampaignsApi.GetHomeAsync)] = _ => Task.FromResult(new HomeDto([Campaign(CampaignRole.Keeper)], [], [RunFor(leads: true)])),
        }));

        var cut = Render<HomePage>();
        cut.WaitForElement("[data-testid=signup-line]");

        Assert.Empty(cut.FindAll("[data-testid=one-shot]"));
        Assert.Contains("свободно 1 из 1", cut.Find("[data-testid=signup-line]").TextContent, StringComparison.Ordinal);
    }
}
