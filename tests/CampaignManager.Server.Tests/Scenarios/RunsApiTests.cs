using System.Net;
using CampaignManager.ApiClient.Campaigns;
using CampaignManager.ApiClient.Scenarios;
using CampaignManager.Contracts.Platform;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Identity;
using CampaignManager.Data.Identity;
using CampaignManager.Server.Tests.Campaigns;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampaignManager.Server.Tests.Scenarios;

/// <summary>
/// Прохождения и ваншоты (T2.5c): «играть в кампании» без копии содержимого, ваншот одной транзакцией, бронь — копия листа
/// прегена, повтор и чужая бронь отсекаются, снять бронь — игрок или ведущий прохождения, а не любой Хранитель (v1).
/// </summary>
public sealed class RunsApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private RunsApiClient Runs(User user) => new(app.Http(user));

    private ScenariosApiClient Scenarios(User user) => new(app.Http(user));

    private static async Task<ApiException> Fails(HttpStatusCode status, Func<Task> call)
    {
        var error = await Assert.ThrowsAsync<ApiException>(call);
        Assert.Equal(status, error.StatusCode);
        return error;
    }

    /// <summary>Сценарий Хранителя с двумя прегенами и объявленный по нему ваншот.</summary>
    private async Task<OneShot> OneShotAsync()
    {
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var scenario = await Scenarios(keeper).CreateAsync(new ScenarioInput("Эликсир жизни", Era: Era.Classic), Cancellation);
        var doctor = await app.AddCharacterAsync(CharacterKind.Pregen, "Доктор", scenario: scenario.Id);
        var librarian = await app.AddCharacterAsync(CharacterKind.Pregen, "Букинист", scenario: scenario.Id);
        var run = await Runs(keeper).AnnounceOneShotAsync(scenario.Id,
            new AnnounceOneShotRequest(null, DateTimeOffset.UtcNow.AddDays(7), "Приходите в субботу"), Cancellation);
        return new OneShot(keeper, scenario.Id, run, doctor.Id, librarian.Id);
    }

    private sealed record OneShot(User Keeper, Guid ScenarioId, ScenarioRunDto Run, Guid Doctor, Guid Librarian);

    [Fact]
    public async Task One_shot_is_campaign_keeper_and_open_run_in_one_transaction()
    {
        TestDatabase.SkipIfMissing();
        var table = await OneShotAsync();

        Assert.Equal(CampaignKind.OneShot, table.Run.CampaignKind);
        Assert.Equal("Эликсир жизни (ваншот)", table.Run.CampaignName);
        Assert.Equal(ScenarioRunStatus.Announced, table.Run.Status);
        Assert.True(table.Run.SignupOpen);
        Assert.Equal("Приходите в субботу", table.Run.Announcement);
        await using (var db = app.Database.CreateContext())
        {
            var campaign = await db.Campaigns.Include(c => c.Members).SingleAsync(c => c.Id == table.Run.CampaignId, Cancellation);
            Assert.Equal(CampaignKind.OneShot, campaign.Kind);
            Assert.Equal(Era.Classic, campaign.Era);
            var keeper = Assert.Single(campaign.Members);
            Assert.Equal((table.Keeper.Id, CampaignRole.Keeper), (keeper.UserId, keeper.Role));
        }

        // Любой вошедший видит ваншот на главной и может бронировать.
        var stranger = await app.AddUserAsync();
        var home = Assert.Single((await app.Api(stranger).GetHomeAsync(Cancellation)).OneShots, o => o.RunId == table.Run.Id);
        Assert.True(home.CanReserve);
        Assert.Equal(2, home.Pregens.Count);
        Assert.False(Assert.Single((await app.Api(table.Keeper).GetHomeAsync(Cancellation)).OneShots, o => o.RunId == table.Run.Id).CanReserve);

        // Отказ — ни кампании, ни участника, ни прохождения: запись одна.
        int Count(Data.CmDbContext db) => db.Campaigns.Count(c => c.CreatedById == table.Keeper.Id);
        int before;
        await using (var db = app.Database.CreateContext())
        {
            before = Count(db);
        }

        await Fails(HttpStatusCode.BadRequest, () => Runs(table.Keeper).AnnounceOneShotAsync(table.ScenarioId,
            new AnnounceOneShotRequest(null, null, new string('а', RunLimits.AnnouncementLength + 1)), Cancellation));
        await Fails(HttpStatusCode.BadRequest, () => Runs(table.Keeper).AnnounceOneShotAsync(table.ScenarioId,
            new AnnounceOneShotRequest(new string('а', 101), null, null), Cancellation));
        await using (var db = app.Database.CreateContext())
        {
            Assert.Equal(before, Count(db));
        }

        // Игроку сценарий не виден, объявить нельзя.
        await Fails(HttpStatusCode.NotFound, () =>
            Runs(stranger).AnnounceOneShotAsync(table.ScenarioId, new AnnounceOneShotRequest(null, null, null), Cancellation));
    }

    [Fact]
    public async Task Play_in_campaign_makes_a_run_without_copying_the_scenario()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var player = await app.AddUserAsync(UserRole.Player, "Анна");
        var campaign = await app.AddCampaignAsync(keeper, player);
        var scenario = await Scenarios(keeper).CreateAsync(new ScenarioInput("Дом Корбитта"), Cancellation);
        await Scenarios(keeper).AddLocationAsync(scenario.Id, new LocationInput("Подвал"), Cancellation);
        int scenarios;
        await using (var db = app.Database.CreateContext())
        {
            scenarios = await db.Scenarios.CountAsync(Cancellation);
        }

        var at = new DateTimeOffset(2026, 10, 10, 19, 0, 0, TimeSpan.FromHours(2));
        var run = await Runs(keeper).PlayInCampaignAsync(scenario.Id, new PlayInCampaignRequest(campaign.Id, at), Cancellation);
        Assert.Equal((campaign.Id, ScenarioRunStatus.Planned, false), (run.CampaignId, run.Status, run.SignupOpen));
        Assert.Equal(at, run.ScheduledAt);
        Assert.Equal(TimeSpan.Zero, run.ScheduledAt!.Value.Offset);
        await using (var db = app.Database.CreateContext())
        {
            Assert.Equal(scenarios, await db.Scenarios.CountAsync(Cancellation));
            Assert.Equal(scenario.Id, (await db.ScenarioRuns.SingleAsync(r => r.Id == run.Id, Cancellation)).ScenarioId);
        }

        // Режим игры и журнал кампании видят прохождение; второе незавершённое того же сценария — отказ.
        Assert.Equal([run.Id], (await Scenarios(keeper).ListRunsAsync(scenario.Id, Cancellation)).Select(r => r.Id));
        Assert.Contains((await app.Api(keeper).GetJournalAsync(campaign.Id, Cancellation)).Runs, r => r.RunId == run.Id);
        await Fails(HttpStatusCode.Conflict, () =>
            Runs(keeper).PlayInCampaignAsync(scenario.Id, new PlayInCampaignRequest(campaign.Id), Cancellation));

        // Чужая кампания не видна, свою кампанию игрок не правит, сценарий игроку не виден.
        var otherKeeper = await app.AddUserAsync(UserRole.Keeper);
        await Fails(HttpStatusCode.NotFound, () =>
            Runs(otherKeeper).PlayInCampaignAsync(scenario.Id, new PlayInCampaignRequest(campaign.Id), Cancellation));
        await Fails(HttpStatusCode.NotFound, () =>
            Runs(player).PlayInCampaignAsync(scenario.Id, new PlayInCampaignRequest(campaign.Id), Cancellation));
    }

    [Fact]
    public async Task Reservation_copies_the_pregen_sheet_to_the_player()
    {
        TestDatabase.SkipIfMissing();
        var table = await OneShotAsync();
        var player = await app.AddUserAsync(UserRole.Player, "Анна");

        var reservation = await Runs(player).ReserveAsync(table.Run.Id, table.Doctor, Cancellation);
        Assert.Equal(table.Run.CampaignId, reservation.CampaignId);
        Assert.NotEqual(table.Doctor, reservation.CharacterId);

        await using (var db = app.Database.CreateContext())
        {
            var copy = await db.Characters.SingleAsync(c => c.Id == reservation.CharacterId, Cancellation);
            Assert.Equal((CharacterKind.Player, CharacterStatus.Active), (copy.Kind, copy.Status));
            Assert.Equal((player.Id, table.Run.CampaignId, table.Doctor), (copy.OwnerId!.Value, copy.CampaignId!.Value, copy.OriginCharacterId!.Value));
            Assert.Null(copy.ScenarioId);
            Assert.Equal("Доктор", copy.Name);

            // Преген остаётся в сценарии нетронутым — для следующего прохождения.
            var pregen = await db.Characters.SingleAsync(c => c.Id == table.Doctor, Cancellation);
            Assert.Equal((CharacterKind.Pregen, table.ScenarioId, CharacterStatus.Active), (pregen.Kind, pregen.ScenarioId!.Value, pregen.Status));

            var member = await db.CampaignMembers.SingleAsync(m => m.CampaignId == table.Run.CampaignId && m.UserId == player.Id, Cancellation);
            Assert.Equal(CampaignRole.Player, member.Role);
            var row = await db.RunReservations.SingleAsync(r => r.RunId == table.Run.Id, Cancellation);
            Assert.Equal((table.Doctor, player.Id, reservation.CharacterId), (row.PregenId, row.UserId, row.CharacterId!.Value));
        }

        // Главная игрока: преген его, ссылка на копию, второй брони нет; ваншот — среди его кампаний с листом.
        var home = await app.Api(player).GetHomeAsync(Cancellation);
        var oneShot = Assert.Single(home.OneShots, o => o.RunId == table.Run.Id);
        Assert.False(oneShot.CanReserve);
        var mine = Assert.Single(oneShot.Pregens, p => p.IsMine);
        Assert.Equal((table.Doctor, reservation.CharacterId, true), (mine.Id, mine.MyCharacterId!.Value, mine.CanRelease));
        Assert.Equal(reservation.CharacterId, Assert.Single(home.Mine, c => c.Id == table.Run.CampaignId).MyCharacter!.Id);

        // Хранитель видит бронь — на главной и в прохождениях сценария.
        var seenByKeeper = Assert.Single((await app.Api(table.Keeper).GetHomeAsync(Cancellation)).OneShots, o => o.RunId == table.Run.Id);
        var reserved = Assert.Single(seenByKeeper.Pregens, p => p.IsReserved);
        Assert.Equal(("Анна", true, (Guid?)null), (reserved.ReservedBy, reserved.CanRelease, reserved.MyCharacterId));
        var run = Assert.Single(await Scenarios(table.Keeper).ListRunsAsync(table.ScenarioId, Cancellation));
        var listed = Assert.Single(run.Reservations);
        Assert.Equal(("Доктор", "Анна", reservation.CharacterId), (listed.PregenName, listed.PlayerName, listed.CharacterId!.Value));
    }

    [Fact]
    public async Task Second_and_foreign_reservations_are_cut_off()
    {
        TestDatabase.SkipIfMissing();
        var table = await OneShotAsync();
        var anna = await app.AddUserAsync(UserRole.Player, "Анна");
        var boris = await app.AddUserAsync(UserRole.Player, "Борис");
        await Runs(anna).ReserveAsync(table.Run.Id, table.Doctor, Cancellation);

        Assert.Equal(ApiProblemCodes.Duplicate,
            (await Fails(HttpStatusCode.Conflict, () => Runs(anna).ReserveAsync(table.Run.Id, table.Librarian, Cancellation))).Code);
        Assert.Equal(ApiProblemCodes.Duplicate,
            (await Fails(HttpStatusCode.Conflict, () => Runs(anna).ReserveAsync(table.Run.Id, table.Doctor, Cancellation))).Code);
        Assert.Equal(ApiProblemCodes.Conflict,
            (await Fails(HttpStatusCode.Conflict, () => Runs(boris).ReserveAsync(table.Run.Id, table.Doctor, Cancellation))).Code);

        // Ведущий не бронирует; чужой преген и несуществующее прохождение — 404.
        await Fails(HttpStatusCode.BadRequest, () => Runs(table.Keeper).ReserveAsync(table.Run.Id, table.Librarian, Cancellation));
        var otherPregen = await app.AddCharacterAsync(CharacterKind.Pregen, "Чужой");
        await Fails(HttpStatusCode.NotFound, () => Runs(boris).ReserveAsync(table.Run.Id, otherPregen.Id, Cancellation));
        await Fails(HttpStatusCode.NotFound, () => Runs(boris).ReserveAsync(Guid.NewGuid(), table.Librarian, Cancellation));

        // У Бориса уже свой сыщик в этой кампании — второго активного не будет.
        await app.Api(boris).JoinAsync(table.Run.CampaignId, new Contracts.Campaigns.JoinCampaignRequest(null), Cancellation);
        await app.AddCharacterAsync(CharacterKind.Player, "Свой сыщик", owner: boris.Id, campaign: table.Run.CampaignId);
        await Fails(HttpStatusCode.Conflict, () => Runs(boris).ReserveAsync(table.Run.Id, table.Librarian, Cancellation));

        // Запись закрыта — 403.
        var run = table.Run;
        await Runs(table.Keeper).UpdateAsync(run.Id, new RunInput(run.Status, run.ScheduledAt, run.Announcement, SignupOpen: false), Cancellation);
        var carl = await app.AddUserAsync();
        await Fails(HttpStatusCode.Forbidden, () => Runs(carl).ReserveAsync(table.Run.Id, table.Librarian, Cancellation));

        await using var db = app.Database.CreateContext();
        Assert.Equal(1, await db.RunReservations.CountAsync(r => r.RunId == table.Run.Id, Cancellation));
        Assert.Equal(1, await db.Characters.CountAsync(c => c.OriginCharacterId == table.Doctor, Cancellation));
    }

    [Fact]
    public async Task Signup_closes_itself_when_the_scheduled_time_has_passed()
    {
        TestDatabase.SkipIfMissing();
        var table = await OneShotAsync();
        var anna = await app.AddUserAsync(UserRole.Player, "Анна");
        var boris = await app.AddUserAsync(UserRole.Player, "Борис");
        var annas = await Runs(anna).ReserveAsync(table.Run.Id, table.Doctor, Cancellation);
        Assert.True(Assert.Single((await app.Api(boris).GetHomeAsync(Cancellation)).OneShots, o => o.RunId == table.Run.Id).CanReserve);

        // Игра была вчера: флаг в базе остаётся, но запись закрыта — бронь 409 с текстом, флаги ложны везде.
        var run = table.Run;
        await Runs(table.Keeper).UpdateAsync(run.Id, new RunInput(run.Status, DateTimeOffset.UtcNow.AddDays(-1), run.Announcement, SignupOpen: true), Cancellation);
        var error = await Fails(HttpStatusCode.Conflict, () => Runs(boris).ReserveAsync(run.Id, table.Librarian, Cancellation));
        Assert.Contains("запись закрыта", error.Message);
        Assert.False(Assert.Single((await app.Api(boris).GetHomeAsync(Cancellation)).OneShots, o => o.RunId == run.Id).CanReserve);
        Assert.False(Assert.Single(await Scenarios(table.Keeper).ListRunsAsync(table.ScenarioId, Cancellation), r => r.Id == run.Id).SignupOpen);
        await using (var db = app.Database.CreateContext())
        {
            Assert.True(await db.ScenarioRuns.Where(r => r.Id == run.Id).Select(r => r.SignupOpen).SingleAsync(Cancellation));
            Assert.Equal(1, await db.RunReservations.CountAsync(r => r.RunId == run.Id, Cancellation));
        }

        // Свою бронь игрок после даты снять может (сыгранную игру она не меняет).
        await Runs(anna).ReleaseAsync(run.Id, table.Doctor, Cancellation);

        // Без даты и с датой в будущем запись открыта.
        await Runs(table.Keeper).UpdateAsync(run.Id, new RunInput(run.Status, null, run.Announcement, SignupOpen: true), Cancellation);
        Assert.True(Assert.Single((await app.Api(boris).GetHomeAsync(Cancellation)).OneShots, o => o.RunId == run.Id).CanReserve);
        await Runs(boris).ReserveAsync(run.Id, table.Librarian, Cancellation);
    }

    [Fact]
    public async Task Reservation_is_released_by_its_player_or_the_runs_keeper_only()
    {
        TestDatabase.SkipIfMissing();
        var table = await OneShotAsync();
        var anna = await app.AddUserAsync(UserRole.Player, "Анна");
        var boris = await app.AddUserAsync(UserRole.Player, "Борис");
        var annas = await Runs(anna).ReserveAsync(table.Run.Id, table.Doctor, Cancellation);
        await Runs(boris).ReserveAsync(table.Run.Id, table.Librarian, Cancellation);

        // Чужой Хранитель кампанию не видит — 404 (в v1 снять бронь мог любой Хранитель); игрок той же кампании — 403.
        var otherKeeper = await app.AddUserAsync(UserRole.Keeper);
        await Fails(HttpStatusCode.NotFound, () => Runs(otherKeeper).ReleaseAsync(table.Run.Id, table.Doctor, Cancellation));
        await Fails(HttpStatusCode.Forbidden, () => Runs(boris).ReleaseAsync(table.Run.Id, table.Doctor, Cancellation));
        await Fails(HttpStatusCode.NotFound, () => Runs(anna).ReleaseAsync(table.Run.Id, Guid.NewGuid(), Cancellation));

        // Сам игрок: бронь снята, копия — в архив (не удалена), преген свободен для другого.
        await Runs(anna).ReleaseAsync(table.Run.Id, table.Doctor, Cancellation);
        await using (var db = app.Database.CreateContext())
        {
            Assert.Equal(CharacterStatus.Archived, (await db.Characters.SingleAsync(c => c.Id == annas.CharacterId, Cancellation)).Status);
            Assert.False(await db.RunReservations.AnyAsync(r => r.RunId == table.Run.Id && r.PregenId == table.Doctor, Cancellation));
        }

        var again = await Runs(anna).ReserveAsync(table.Run.Id, table.Doctor, Cancellation);
        Assert.NotEqual(annas.CharacterId, again.CharacterId);

        // Ведущий прохождения снимает чужую бронь.
        await Runs(table.Keeper).ReleaseAsync(table.Run.Id, table.Librarian, Cancellation);

        // Сыгранная игра брони не отдаёт.
        var run = table.Run;
        await Runs(table.Keeper).UpdateAsync(run.Id, new RunInput(ScenarioRunStatus.Finished, run.ScheduledAt, run.Announcement, true), Cancellation);
        await Fails(HttpStatusCode.Conflict, () => Runs(anna).ReleaseAsync(table.Run.Id, table.Doctor, Cancellation));
    }

    [Fact]
    public async Task Run_form_edits_announcement_and_finishing_closes_signup()
    {
        TestDatabase.SkipIfMissing();
        var table = await OneShotAsync();
        var anna = await app.AddUserAsync(UserRole.Player, "Анна");
        var annas = await Runs(anna).ReserveAsync(table.Run.Id, table.Doctor, Cancellation);
        var stranger = await app.AddUserAsync();
        var input = new RunInput(ScenarioRunStatus.Running, table.Run.ScheduledAt, "Начинаем в 19:00", true);

        // Участник кампании видит, но не правит; посторонний не видит.
        await Fails(HttpStatusCode.Forbidden, () => Runs(anna).UpdateAsync(table.Run.Id, input, Cancellation));
        await Fails(HttpStatusCode.NotFound, () => Runs(stranger).UpdateAsync(table.Run.Id, input, Cancellation));

        var running = await Runs(table.Keeper).UpdateAsync(table.Run.Id, input, Cancellation);
        Assert.Equal((ScenarioRunStatus.Running, "Начинаем в 19:00", true), (running.Status, running.Announcement, running.SignupOpen));
        Assert.Single(running.Reservations);

        var finished = await Runs(table.Keeper).UpdateAsync(table.Run.Id, input with { Status = ScenarioRunStatus.Finished }, Cancellation);
        Assert.False(finished.SignupOpen);
        Assert.DoesNotContain((await app.Api(stranger).GetHomeAsync(Cancellation)).OneShots, o => o.RunId == table.Run.Id);

        // Удалить — ведущий; брони уходят, копия листа остаётся у игрока.
        await Fails(HttpStatusCode.Forbidden, () => Runs(anna).DeleteAsync(table.Run.Id, Cancellation));
        await Runs(table.Keeper).DeleteAsync(table.Run.Id, Cancellation);
        await using var db = app.Database.CreateContext();
        Assert.False(await db.RunReservations.AnyAsync(r => r.RunId == table.Run.Id, Cancellation));
        Assert.Equal(CharacterStatus.Active, (await db.Characters.SingleAsync(c => c.Id == annas.CharacterId, Cancellation)).Status);
    }

    [Fact]
    public async Task Campaign_runs_list_unfinished_runs_with_scenario_for_the_campaign_keeper_only()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var otherKeeper = await app.AddUserAsync(UserRole.Keeper, "Чужой");
        var player = await app.AddUserAsync(UserRole.Player, "Аня");
        var campaign = await app.AddCampaignAsync(keeper, player);
        var elsewhere = await app.AddCampaignAsync(otherKeeper);
        var fog = await Scenarios(keeper).CreateAsync(new ScenarioInput("Туман", Era: Era.Classic), Cancellation);
        var masks = await Scenarios(keeper).CreateAsync(new ScenarioInput("Маски", Era: Era.Classic), Cancellation);
        var played = await Scenarios(keeper).CreateAsync(new ScenarioInput("Сыгранный", Era: Era.Classic), Cancellation);

        var fogRun = await Runs(keeper).PlayInCampaignAsync(fog.Id, new PlayInCampaignRequest(campaign.Id), Cancellation);
        var masksRun = await Runs(keeper).PlayInCampaignAsync(masks.Id, new PlayInCampaignRequest(campaign.Id), Cancellation);
        var playedRun = await Runs(keeper).PlayInCampaignAsync(played.Id, new PlayInCampaignRequest(campaign.Id), Cancellation);
        await Runs(keeper).UpdateAsync(playedRun.Id, new RunInput(ScenarioRunStatus.Finished, null, null, false), Cancellation);
        await Runs(otherKeeper).PlayInCampaignAsync(fog.Id, new PlayInCampaignRequest(elsewhere.Id), Cancellation);

        var runs = await Runs(keeper).ListForCampaignAsync(campaign.Id, Cancellation);

        // Только этой кампании и только незавершённые; у каждого — сценарий.
        Assert.Equal([fogRun.Id, masksRun.Id], runs.Select(r => r.Id).Order());
        Assert.Equal(new[] { "Маски", "Туман" }, runs.Select(r => r.ScenarioName).Order());
        Assert.All(runs, r => Assert.Equal(campaign.Id, r.CampaignId));
        Assert.Equal(fog.Id, runs.Single(r => r.Id == fogRun.Id).ScenarioId);

        // Игрок кампании видит её, но не правит; посторонний Хранитель кампании не видит.
        await Fails(HttpStatusCode.Forbidden, () => Runs(player).ListForCampaignAsync(campaign.Id, Cancellation));
        await Fails(HttpStatusCode.NotFound, () => Runs(otherKeeper).ListForCampaignAsync(campaign.Id, Cancellation));
        await Fails(HttpStatusCode.NotFound, () => Runs(keeper).ListForCampaignAsync(Guid.NewGuid(), Cancellation));
    }
}
