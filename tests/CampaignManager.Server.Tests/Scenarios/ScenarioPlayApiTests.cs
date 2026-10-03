using System.Net;
using CampaignManager.ApiClient.Scenarios;
using CampaignManager.Contracts.Platform;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Identity;
using CampaignManager.Data.Identity;
using CampaignManager.Data.Music;
using CampaignManager.Server.Tests.Campaigns;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampaignManager.Server.Tests.Scenarios;

/// <summary>
/// Режим игры (T2.5b): прохождения, из которых берутся сыщики для проверок; музыка локации; раздатка для показа игрокам и
/// второй экран — права (Хранитель или игрок кампании, где сценарий проходят) и главное: пометка и заметка Хранителя не
/// уходят ни в показ, ни на второй экран.
/// </summary>
public sealed class ScenarioPlayApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
{
    private const string KeeperNote = "Хранителю: выдать после второй ночи";
    private const string HandoutName = "Пометка: письмо тётушки";
    private const string PlayerText = "Дорогой племянник, приезжай скорее";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private ScenariosApiClient Api(User user) => new(app.Http(user));

    private static async Task Fails(HttpStatusCode status, Func<Task> call)
    {
        var error = await Assert.ThrowsAsync<ApiException>(call);
        Assert.Equal(status, error.StatusCode);
    }

    /// <summary>Стол: автор сценария ведёт кампанию с игроком, сценарий в ней проходят; в сценарии одна раздатка.</summary>
    private async Task<Table> TableAsync()
    {
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var player = await app.AddUserAsync();
        var scenario = await Api(keeper).CreateAsync(new ScenarioInput("Письмо из Аркхема", Era: Era.Classic), Cancellation);
        var handout = await Api(keeper).AddHandoutAsync(scenario.Id, new HandoutInput(HandoutName, PlayerText, KeeperNote), Cancellation);
        var campaign = await app.AddCampaignAsync(keeper, player);
        var run = await app.AddRunAsync(campaign.Id, scenarioId: scenario.Id);
        return new Table(keeper, player, scenario, handout, campaign.Id, run.Id);
    }

    private sealed record Table(User Keeper, User Player, ScenarioDto Scenario, HandoutDto Handout, Guid CampaignId, Guid RunId);

    [Fact]
    public async Task Runs_are_those_of_campaigns_the_keeper_leads_unfinished_first()
    {
        TestDatabase.SkipIfMissing();
        var table = await TableAsync();
        var finishedCampaign = await app.AddCampaignAsync(table.Keeper);
        var finished = await app.AddRunAsync(finishedCampaign.Id, scenarioId: table.Scenario.Id, scheduledAt: DateTimeOffset.UtcNow.AddDays(5));
        await using (var db = app.Database.CreateContext())
        {
            await db.ScenarioRuns.Where(r => r.Id == finished.Id)
                .ExecuteUpdateAsync(r => r.SetProperty(x => x.Status, ScenarioRunStatus.Finished), Cancellation);
        }

        var otherKeeper = await app.AddUserAsync(UserRole.Keeper);
        await app.AddRunAsync((await app.AddCampaignAsync(otherKeeper)).Id, scenarioId: table.Scenario.Id);
        var admin = await app.AddUserAsync(UserRole.Admin);

        var runs = await Api(table.Keeper).ListRunsAsync(table.Scenario.Id, Cancellation);
        Assert.Equal([table.RunId, finished.Id], runs.Select(r => r.Id));
        Assert.Equal(table.CampaignId, runs[0].CampaignId);
        Assert.Equal(ScenarioRunStatus.Planned, runs[0].Status);

        // Чужой Хранитель видит сценарий, но сыщиков чужой кампании ему не отдадут — и прохождения в списке нет.
        Assert.Single(await Api(otherKeeper).ListRunsAsync(table.Scenario.Id, Cancellation));
        Assert.Equal(3, (await Api(admin).ListRunsAsync(table.Scenario.Id, Cancellation)).Count);
        await Fails(HttpStatusCode.NotFound, () => Api(table.Player).ListRunsAsync(table.Scenario.Id, Cancellation));
    }

    [Fact]
    public async Task Location_music_is_normalized_and_tracks_come_from_the_library()
    {
        TestDatabase.SkipIfMissing();
        var table = await TableAsync();
        var api = Api(table.Keeper);
        var location = await api.AddLocationAsync(table.Scenario.Id, new LocationInput("Склад Уэйтли"), Cancellation);
        Guid first;
        Guid second;
        await using (var db = app.Database.CreateContext())
        {
            var a = new MusicTrack { Name = $"Трек {Guid.NewGuid():N}", YoutubeId = "aaaaaaaaaaa" };
            var b = new MusicTrack { Name = $"Трек {Guid.NewGuid():N}", YoutubeId = "bbbbbbbbbbb" };
            db.MusicTracks.AddRange(a, b);
            await db.SaveChangesAsync(Cancellation);
            (first, second) = (a.Id, b.Id);
        }

        var saved = await api.SetLocationMusicAsync(table.Scenario.Id, location.Id,
            new LocationMusicInput([" Напряжение ", "напряжение", "Ёлка"], [first, first]), Cancellation);
        Assert.Equal(["елка", "напряжение"], saved.MusicTags);
        Assert.Equal([first], saved.TrackIds);

        await api.SetLocationMusicAsync(table.Scenario.Id, location.Id, new LocationMusicInput(["бой"], [second]), Cancellation);
        var read = (await api.GetAsync(table.Scenario.Id, Cancellation)).Locations.Single(l => l.Id == location.Id);
        Assert.Equal(["бой"], read.MusicTags);
        Assert.Equal([second], read.TrackIds);

        // Правка локации музыку не трогает.
        await api.UpdateLocationAsync(table.Scenario.Id, location.Id, new LocationInput("Склад Уэйтли", "Данвич"), Cancellation);
        Assert.Equal([second], (await api.GetAsync(table.Scenario.Id, Cancellation)).Locations.Single(l => l.Id == location.Id).TrackIds);

        await Fails(HttpStatusCode.BadRequest, () =>
            api.SetLocationMusicAsync(table.Scenario.Id, location.Id, new LocationMusicInput([], [Guid.NewGuid()]), Cancellation));
        await Fails(HttpStatusCode.BadRequest, () =>
            api.SetLocationMusicAsync(table.Scenario.Id, location.Id, new LocationMusicInput([new string('а', 41)], []), Cancellation));
        await Fails(HttpStatusCode.NotFound, () =>
            Api(table.Player).SetLocationMusicAsync(table.Scenario.Id, location.Id, new LocationMusicInput(["бой"], []), Cancellation));

        // Локация чужого сценария по адресу этого — 404.
        var other = await api.CreateAsync(new ScenarioInput("Другой"), Cancellation);
        await Fails(HttpStatusCode.NotFound, () =>
            api.SetLocationMusicAsync(other.Id, location.Id, new LocationMusicInput(["бой"], []), Cancellation));
    }

    [Fact]
    public async Task Handout_screen_is_for_keeper_and_players_of_the_run_and_never_carries_keeper_notes()
    {
        TestDatabase.SkipIfMissing();
        var table = await TableAsync();

        var forKeeper = await Api(table.Keeper).GetHandoutScreenAsync(table.Scenario.Id, table.Handout.Id, Cancellation);
        Assert.True(forKeeper.CanManage);
        Assert.Equal(PlayerText, forKeeper.PlayerText);

        var forPlayer = await Api(table.Player).GetHandoutScreenAsync(table.Scenario.Id, table.Handout.Id, Cancellation);
        Assert.False(forPlayer.CanManage);

        // Ни в одном ответе показа нет ни заметки, ни пометки Хранителя — проверяем сырой JSON, а не только тип.
        foreach (var user in new[] { table.Keeper, table.Player })
        {
            var raw = await app.Http(user).GetStringAsync(ScenariosRoutes.HandoutScreen(table.Scenario.Id, table.Handout.Id), Cancellation);
            Assert.Contains(PlayerText, raw, StringComparison.Ordinal);
            Assert.DoesNotContain(KeeperNote, raw, StringComparison.Ordinal);
            Assert.DoesNotContain(HandoutName, raw, StringComparison.Ordinal);
        }

        var outsider = await app.AddUserAsync();
        await Fails(HttpStatusCode.NotFound, () => Api(outsider).GetHandoutScreenAsync(table.Scenario.Id, table.Handout.Id, Cancellation));

        // Раздатка чужого сценария по адресу этого — 404, даже Хранителю.
        var other = await Api(table.Keeper).CreateAsync(new ScenarioInput("Другой"), Cancellation);
        await Fails(HttpStatusCode.NotFound, () => Api(table.Keeper).GetHandoutScreenAsync(other.Id, table.Handout.Id, Cancellation));
    }

    [Fact]
    public async Task Second_screen_page_is_static_and_shows_only_what_players_see()
    {
        TestDatabase.SkipIfMissing();
        var table = await TableAsync();
        var address = $"/scenarios/{table.Scenario.Id}/handouts/{table.Handout.Id}";

        foreach (var user in new[] { table.Keeper, table.Player })
        {
            using var response = await app.Http(user).GetAsync(address, Cancellation);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync(Cancellation);
            Assert.Contains("data-testid=\"handout-screen\"", html, StringComparison.Ordinal);
            Assert.Contains(PlayerText, html, StringComparison.Ordinal);
            Assert.DoesNotContain(KeeperNote, html, StringComparison.Ordinal);
            Assert.DoesNotContain(HandoutName, html, StringComparison.Ordinal);
            // Статическая страница: маркера интерактивного компонента WebAssembly (как у страниц приложения) нет.
            Assert.DoesNotContain("\"type\":\"webassembly\"", html, StringComparison.Ordinal);
        }

        // «Закрыть»: ведущего — в режим игры сценария, игрока — на главную.
        var keeperHtml = await app.Http(table.Keeper).GetStringAsync(address, Cancellation);
        Assert.Contains($"scenarios/{table.Scenario.Id}?mode=play", keeperHtml, StringComparison.Ordinal);

        var outsider = await app.AddUserAsync();
        using (var denied = await app.Http(outsider).GetAsync(address, Cancellation))
        {
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            var html = await denied.Content.ReadAsStringAsync(Cancellation);
            Assert.Contains("Раздатка недоступна", html, StringComparison.Ordinal);
            Assert.DoesNotContain(PlayerText, html, StringComparison.Ordinal);
        }

        // Аноним — на вход, а не к раздатке.
        using var anonymous = await app.CreateClient(new() { AllowAutoRedirect = false }).GetAsync(address, Cancellation);
        Assert.True(anonymous.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.Unauthorized,
            $"аноним получил {anonymous.StatusCode}");
        Assert.DoesNotContain(PlayerText, await anonymous.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }
}
