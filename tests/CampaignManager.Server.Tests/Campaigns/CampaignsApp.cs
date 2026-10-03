using System.Net;
using CampaignManager.ApiClient.Campaigns;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Identity;
using CampaignManager.Data.Campaigns;
using CampaignManager.Data.Characters;
using CampaignManager.Data.Identity;
using CampaignManager.Data.Scenarios;
using CampaignManager.Server.Tests.Schema;
using Xunit;

namespace CampaignManager.Server.Tests.Campaigns;

/// <summary>
/// Сервер со своей базой для тестов модуля кампаний. Каждый тест заводит своих людей и кампании
/// (почты уникальны), поэтому тесты класса друг другу не мешают.
/// </summary>
public sealed class CampaignsApp : CmApp, IAsyncLifetime
{
    public SchemaDatabase Database { get; } = new();

    protected override string ConnectionString =>
        TestDatabase.ConnectionString is null ? TestDatabase.Unreachable : Database.ConnectionString;

    ValueTask IAsyncLifetime.InitializeAsync() => Database.InitializeAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Database.DisposeAsync();
    }

    /// <summary>Клиент API от имени <paramref name="user"/>; <c>null</c> — аноним.</summary>
    public CampaignsApiClient Api(User? user) =>
        new(user is null ? CreateClient() : CreateClient().As(user.Id));

    public HttpClient Http(User user) => CreateClient().As(user.Id);

    /// <param name="displayName">Имя профиля; по умолчанию — почта, как его заводит вход без <c>name</c>.</param>
    public async Task<User> AddUserAsync(UserRole role = UserRole.Player, string? displayName = null)
    {
        await using var db = Database.CreateContext();
        var email = $"{Guid.NewGuid():N}@example.test";
        var user = new User { Email = email, DisplayName = displayName ?? email, Role = role };
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    /// <summary>Кампания прямо в базе: Хранитель и игроки-участники.</summary>
    public async Task<Campaign> AddCampaignAsync(User keeper, params User[] players) =>
        await AddCampaignAsync(keeper, CampaignStatus.Active, CampaignKind.Campaign, players);

    public async Task<Campaign> AddCampaignAsync(User keeper, CampaignStatus status, CampaignKind kind, params User[] players)
    {
        await using var db = Database.CreateContext();
        var campaign = new Campaign { Name = $"Кампания {Guid.NewGuid():N}"[..20], Status = status, Kind = kind };
        campaign.Members.Add(new CampaignMember { UserId = keeper.Id, Role = CampaignRole.Keeper });
        foreach (var player in players)
        {
            campaign.Members.Add(new CampaignMember { UserId = player.Id, Role = CampaignRole.Player });
        }

        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return campaign;
    }

    public async Task<Character> AddCharacterAsync(CharacterKind kind, string name, Guid? owner = null, Guid? campaign = null,
        Guid? scenario = null, CharacterStatus status = CharacterStatus.Active)
    {
        await using var db = Database.CreateContext();
        var character = new Character
        {
            Kind = kind,
            Status = status,
            OwnerId = owner,
            CampaignId = campaign,
            ScenarioId = scenario,
            Sheet = SchemaDatabase.Sheet(name),
            SheetVersion = CharacterSheet.CurrentVersion,
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return character;
    }

    /// <summary>Сценарий и его прохождение в кампании.</summary>
    public async Task<ScenarioRun> AddRunAsync(Guid campaignId, string scenarioName = "Дом с привидениями", bool signupOpen = false,
        Guid? scenarioId = null, DateTimeOffset? scheduledAt = null, ScenarioRunStatus status = ScenarioRunStatus.Planned)
    {
        await using var db = Database.CreateContext();
        if (scenarioId is null)
        {
            var scenario = new Scenario { Name = scenarioName };
            db.Scenarios.Add(scenario);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            scenarioId = scenario.Id;
        }

        var run = new ScenarioRun { ScenarioId = scenarioId.Value, CampaignId = campaignId, SignupOpen = signupOpen, ScheduledAt = scheduledAt, Status = status };
        db.ScenarioRuns.Add(run);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return run;
    }

    public async Task ReserveAsync(Guid runId, Guid pregenId, Guid userId)
    {
        await using var db = Database.CreateContext();
        db.RunReservations.Add(new RunReservation { RunId = runId, PregenId = pregenId, UserId = userId });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}

public static class ApiAssert
{
    /// <summary>Запрос отклонён с кодом <paramref name="status"/>; возвращает текст ошибки.</summary>
    public static async Task<string> FailsWith(HttpStatusCode status, Func<Task> call)
    {
        var error = await Assert.ThrowsAsync<HttpRequestException>(call);
        Assert.Equal(status, error.StatusCode);
        return error.Message;
    }
}
