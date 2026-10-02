using System.Net;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Identity;
using Xunit;

namespace CampaignManager.Server.Tests.Campaigns;

/// <summary>Журнал встреч: кто читает и пишет, заметки Хранителя, прохождения, номер следующей встречи.</summary>
public sealed class JournalApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static CampaignSessionInput Session(int number, string? notes = null, Guid? runId = null, bool completed = false) =>
        new(new DateOnly(2026, 9, number), number, $"Встреча {number}", "Сыщики нашли дневник.", notes, runId, completed);

    // Паритет 2: Хранитель записывает встречу. Игрок читает хронику, но заметок Хранителя не получает —
    // их вырезает сервер, а не разметка. Посторонний не отличит журнал от несуществующего (404).
    [Fact]
    public async Task Keeper_writes_players_read_without_keeper_notes()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync();
        var outsider = await app.AddUserAsync(UserRole.Keeper);
        var campaign = await app.AddCampaignAsync(keeper, player);

        var saved = await app.Api(keeper).AddSessionAsync(campaign.Id, Session(1, "Культ знает о сыщиках"), Cancellation);
        Assert.Equal("Культ знает о сыщиках", saved.KeeperNotes);

        var keeperView = await app.Api(keeper).GetJournalAsync(campaign.Id, Cancellation);
        Assert.True(keeperView.CanEdit);
        Assert.Equal("Культ знает о сыщиках", Assert.Single(keeperView.Sessions).KeeperNotes);

        var playerView = await app.Api(player).GetJournalAsync(campaign.Id, Cancellation);
        Assert.False(playerView.CanEdit);
        var session = Assert.Single(playerView.Sessions);
        Assert.Equal("Сыщики нашли дневник.", session.Summary);
        Assert.Null(session.KeeperNotes);
        Assert.Empty(playerView.Runs);
        var raw = await app.Http(player).GetStringAsync(CampaignsRoutes.Journal(campaign.Id), Cancellation);
        Assert.DoesNotContain("Культ знает", raw);

        await ApiAssert.FailsWith(HttpStatusCode.Forbidden, () => app.Api(player).AddSessionAsync(campaign.Id, Session(2), Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.NotFound, () => app.Api(outsider).GetJournalAsync(campaign.Id, Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.NotFound, () => app.Api(outsider).AddSessionAsync(campaign.Id, Session(2), Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.NotFound, () => app.Api(keeper).GetJournalAsync(Guid.CreateVersion7(), Cancellation));
    }

    // Администратор ведёт и чужой журнал (в v1 тоже) и видит заметки.
    [Fact]
    public async Task Admin_keeps_any_journal()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var admin = await app.AddUserAsync(UserRole.Admin);
        var campaign = await app.AddCampaignAsync(keeper);

        await app.Api(admin).AddSessionAsync(campaign.Id, Session(1, "Секрет"), Cancellation);

        Assert.Equal("Секрет", Assert.Single((await app.Api(admin).GetJournalAsync(campaign.Id, Cancellation)).Sessions).KeeperNotes);
    }

    // Номер предлагается следующим; новые встречи сверху — по дате, затем по номеру.
    [Fact]
    public async Task Next_number_follows_the_highest_and_newest_go_first()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var campaign = await app.AddCampaignAsync(keeper);
        var api = app.Api(keeper);

        Assert.Equal(1, (await api.GetJournalAsync(campaign.Id, Cancellation)).NextNumber);
        await api.AddSessionAsync(campaign.Id, Session(3), Cancellation);
        await api.AddSessionAsync(campaign.Id, Session(7), Cancellation);
        await api.AddSessionAsync(campaign.Id, Session(5), Cancellation);

        var journal = await api.GetJournalAsync(campaign.Id, Cancellation);
        Assert.Equal(8, journal.NextNumber);
        Assert.Equal([7, 5, 3], journal.Sessions.Select(s => s.Number));
    }

    // Встреча ссылается на прохождение своей кампании; чужое — 400.
    [Fact]
    public async Task Session_links_only_a_run_of_its_campaign()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var campaign = await app.AddCampaignAsync(keeper);
        var other = await app.AddCampaignAsync(keeper);
        var run = await app.AddRunAsync(campaign.Id, "Эликсир жизни");
        var foreignRun = await app.AddRunAsync(other.Id, "Чужой");
        var api = app.Api(keeper);

        var option = Assert.Single((await api.GetJournalAsync(campaign.Id, Cancellation)).Runs);
        Assert.Equal(run.Id, option.RunId);
        Assert.Equal("Эликсир жизни", option.ScenarioName);

        var saved = await api.AddSessionAsync(campaign.Id, Session(1, runId: run.Id, completed: true), Cancellation);
        Assert.Equal("Эликсир жизни", saved.ScenarioName);
        Assert.Equal(run.ScenarioId, saved.ScenarioId);
        Assert.True(saved.ScenarioCompleted);

        await ApiAssert.FailsWith(HttpStatusCode.BadRequest,
            () => api.AddSessionAsync(campaign.Id, Session(2, runId: foreignRun.Id), Cancellation));
    }

    [Fact]
    public async Task Edit_and_delete_stay_inside_the_campaign()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync();
        var campaign = await app.AddCampaignAsync(keeper, player);
        var other = await app.AddCampaignAsync(keeper);
        var api = app.Api(keeper);
        var saved = await api.AddSessionAsync(campaign.Id, Session(1), Cancellation);

        var edited = await api.UpdateSessionAsync(campaign.Id, saved.Id, Session(1) with { Title = "Дом на Френч-Хилл" }, Cancellation);
        Assert.Equal("Дом на Френч-Хилл", edited.Title);

        await ApiAssert.FailsWith(HttpStatusCode.NotFound, () => api.UpdateSessionAsync(other.Id, saved.Id, Session(1), Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.NotFound, () => api.DeleteSessionAsync(other.Id, saved.Id, Cancellation));
        await ApiAssert.FailsWith(HttpStatusCode.Forbidden, () => app.Api(player).DeleteSessionAsync(campaign.Id, saved.Id, Cancellation));

        await api.DeleteSessionAsync(campaign.Id, saved.Id, Cancellation);
        Assert.Empty((await api.GetJournalAsync(campaign.Id, Cancellation)).Sessions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10000)]
    public async Task Session_number_is_checked(int number)
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var campaign = await app.AddCampaignAsync(keeper);

        await ApiAssert.FailsWith(HttpStatusCode.BadRequest,
            () => app.Api(keeper).AddSessionAsync(campaign.Id, Session(1) with { Number = number }, Cancellation));
    }

    // Ссылки для фазы развития: Хранителю — все активные листы игроков кампании, игроку — только свой.
    [Fact]
    public async Task Investigators_for_development_phase()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var harvey = await app.AddUserAsync(UserRole.Player, "Дмитрий");
        var anna = await app.AddUserAsync(UserRole.Player, "Анна");
        var campaign = await app.AddCampaignAsync(keeper, harvey, anna);
        await app.AddCharacterAsync(CharacterKind.Player, "Харви Уолтерс", harvey.Id, campaign.Id);
        await app.AddCharacterAsync(CharacterKind.Player, "Старый лист", harvey.Id, campaign.Id, status: CharacterStatus.Retired);
        await app.AddCharacterAsync(CharacterKind.Player, "Лидия", anna.Id, campaign.Id);
        await app.AddCharacterAsync(CharacterKind.Npc, "Джексон Элиас", campaign: campaign.Id);

        var all = (await app.Api(keeper).GetJournalAsync(campaign.Id, Cancellation)).Investigators;
        Assert.Equal(["Лидия", "Харви Уолтерс"], all.Select(i => i.Name));
        Assert.Equal(["Анна", "Дмитрий"], all.Select(i => i.PlayerName));

        var own = (await app.Api(harvey).GetJournalAsync(campaign.Id, Cancellation)).Investigators;
        Assert.Equal("Харви Уолтерс", Assert.Single(own).Name);
    }
}
