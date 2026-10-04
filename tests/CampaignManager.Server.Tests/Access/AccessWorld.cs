using System.Security.Claims;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Identity;
using CampaignManager.Data;
using CampaignManager.Data.Campaigns;
using CampaignManager.Data.Characters;
using CampaignManager.Data.Encounters;
using CampaignManager.Data.Identity;
using CampaignManager.Data.Scenarios;
using CampaignManager.Server.Access;
using CampaignManager.Server.Identity;
using CampaignManager.Server.Tests.Schema;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CampaignManager.Server.Tests.Access;

/// <summary>
/// Один стол на все правила карты прав: кто есть кто — по имени (<see cref="User"/>).
/// <list type="bullet">
/// <item><c>keeper</c> ведёт кампанию «Маски» и автор сценария; <c>player</c> — игрок «Масок»;</item>
/// <item><c>otherKeeper</c> — Хранитель, но не этой кампании (ведёт завершённую «Старую»);</item>
/// <item><c>outsider</c> — игрок без кампании; <c>admin</c> — администратор.</item>
/// </list>
/// </summary>
public sealed class AccessWorld : IAsyncLifetime
{
    public SchemaDatabase Database { get; } = new();

    private readonly Dictionary<string, User> _users = [];

    public Guid Campaign { get; private set; }
    public Guid CompletedCampaign { get; private set; }
    public Guid Scenario { get; private set; }
    public Guid OrphanScenario { get; private set; }
    public Guid Run { get; private set; }
    public Guid Handout { get; private set; }
    public Guid UnplayedHandout { get; private set; }
    public Guid PlayerSheet { get; private set; }
    public Guid OutsiderSheet { get; private set; }
    public Guid Pregen { get; private set; }
    public Guid LibraryNpc { get; private set; }
    public Guid CampaignNpc { get; private set; }
    public Guid Encounter { get; private set; }

    public User User(string name) => _users[name];

    public async ValueTask InitializeAsync()
    {
        await Database.InitializeAsync();
        if (TestDatabase.ConnectionString is null)
        {
            return;
        }

        await using var db = Database.CreateContext();
        foreach (var (name, role) in new[]
                 {
                     ("admin", UserRole.Admin), ("keeper", UserRole.Keeper), ("otherKeeper", UserRole.Keeper),
                     ("player", UserRole.Player), ("outsider", UserRole.Player),
                 })
        {
            var user = new User { Email = $"{name}@example.test", DisplayName = name, Role = role };
            _users[name] = user;
            db.Users.Add(user);
        }

        var campaign = new Campaign { Name = "Маски Ньярлатхотепа" };
        campaign.Members.Add(new CampaignMember { UserId = User("keeper").Id, Role = CampaignRole.Keeper });
        campaign.Members.Add(new CampaignMember { UserId = User("player").Id, Role = CampaignRole.Player });
        var completed = new Campaign { Name = "Старая", Status = CampaignStatus.Completed };
        completed.Members.Add(new CampaignMember { UserId = User("otherKeeper").Id, Role = CampaignRole.Keeper });
        db.Campaigns.AddRange(campaign, completed);

        var scenario = new Scenario { Name = "Дом с привидениями", AuthorId = User("keeper").Id };
        var handout = new ScenarioHandout { Name = "Письмо", Ord = 1 };
        scenario.Handouts.Add(handout);
        var orphan = new Scenario { Name = "Без автора" };
        var unplayedHandout = new ScenarioHandout { Name = "Газета", Ord = 1 };
        orphan.Handouts.Add(unplayedHandout);
        db.Scenarios.AddRange(scenario, orphan);
        await db.SaveChangesAsync();

        var run = new ScenarioRun { ScenarioId = scenario.Id, CampaignId = campaign.Id, SignupOpen = true };
        db.ScenarioRuns.Add(run);

        var playerSheet = Sheet(CharacterKind.Player, "Харви Уолтерс", owner: User("player").Id, campaign: campaign.Id);
        var outsiderSheet = Sheet(CharacterKind.Player, "Без кампании", owner: User("outsider").Id);
        var pregen = Sheet(CharacterKind.Pregen, "Преген", scenario: scenario.Id);
        var libraryNpc = Sheet(CharacterKind.Npc, "Библиотекарь");
        var campaignNpc = Sheet(CharacterKind.Npc, "Джексон Элиас", campaign: campaign.Id);
        db.Characters.AddRange(playerSheet, outsiderSheet, pregen, libraryNpc, campaignNpc);
        await db.SaveChangesAsync();

        db.RunReservations.Add(new RunReservation { RunId = run.Id, PregenId = pregen.Id, UserId = User("player").Id });
        var encounter = new Encounter
        {
            KeeperId = User("keeper").Id,
            CampaignId = campaign.Id,
            Kind = EncounterKind.Combat,
            State = CmJson.Write(new EncounterState()),
            StateVersion = EncounterState.CurrentVersion,
        };
        db.Encounters.Add(encounter);
        db.CharacterDrafts.Add(new CharacterDraft
        {
            CampaignId = campaign.Id,
            OwnerId = User("player").Id,
            Draft = CmJson.Write(new InvestigatorDraft { StepIndex = 3 }),
            DraftVersion = InvestigatorDraft.CurrentVersion,
            Step = 3,
        });
        await db.SaveChangesAsync();

        (Campaign, CompletedCampaign, Scenario, OrphanScenario, Run) = (campaign.Id, completed.Id, scenario.Id, orphan.Id, run.Id);
        (Handout, UnplayedHandout) = (handout.Id, unplayedHandout.Id);
        (PlayerSheet, OutsiderSheet, Pregen, LibraryNpc, CampaignNpc) =
            (playerSheet.Id, outsiderSheet.Id, pregen.Id, libraryNpc.Id, campaignNpc.Id);
        Encounter = encounter.Id;
    }

    public ValueTask DisposeAsync() => Database.DisposeAsync();

    /// <summary>Политика глазами пользователя <paramref name="name"/> — как её собирает DI сервера на запрос.</summary>
    public AccessPolicy PolicyFor(string name) => Policy(new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim(CmClaims.UserId, User(name).Id.ToString())], "Test")));

    public AccessPolicy AnonymousPolicy() => Policy(new ClaimsPrincipal(new ClaimsIdentity()));

    private AccessPolicy Policy(ClaimsPrincipal principal)
    {
        var db = Database.CreateContext();
        var directory = new UserDirectory(db, Options.Create(new AccessListOptions()), TimeProvider.System,
            NullLogger<UserDirectory>.Instance);
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        return new AccessPolicy(db, new CurrentUser(accessor, directory));
    }

    private static Character Sheet(CharacterKind kind, string name, Guid? owner = null, Guid? campaign = null, Guid? scenario = null) =>
        new()
        {
            Kind = kind,
            OwnerId = owner,
            CampaignId = campaign,
            ScenarioId = scenario,
            Sheet = SchemaDatabase.Sheet(name),
            SheetVersion = CharacterSheet.CurrentVersion,
        };
}
