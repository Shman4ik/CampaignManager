using System.Net;
using CampaignManager.ApiClient.Characters;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Platform;
using CampaignManager.Core;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Identity;
using CampaignManager.Core.Scenarios;
using CampaignManager.Data.Identity;
using CampaignManager.Data.Scenarios;
using CampaignManager.Server.Tests.Campaigns;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampaignManager.Server.Tests.Characters;

/// <summary>
/// Создание листов и библиотека НПС и прегенов (T2.4): кто и куда заводит лист, один активный сыщик в кампании,
/// быстрый НПС из сценария — лист и связь одной записью, библиотека только Хранителю и без чужих НПС кампаний.
/// </summary>
public sealed class CharacterLibraryApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private CharactersApiClient Api(User user) => new(app.Http(user));

    private static async Task<ApiException> Fails(HttpStatusCode status, Func<Task> call)
    {
        var error = await Assert.ThrowsAsync<ApiException>(call);
        Assert.Equal(status, error.StatusCode);
        return error;
    }

    private static CharacterSheet Sheet(string name, params SheetSkill[] skills) => new()
    {
        Personal = new PersonalInfo { Name = name, Age = 30 },
        Characteristics = new Characteristics { Str = 50, Con = 50, Siz = 50, Dex = 50, App = 50, Int = 50, Pow = 50, Edu = 50 },
        Skills = [.. skills],
    };

    private async Task<Scenario> AddScenarioAsync(Era? era = null)
    {
        await using var db = app.Database.CreateContext();
        var scenario = new Scenario { Name = $"Сценарий {Guid.NewGuid():N}"[..20], Era = era };
        db.Scenarios.Add(scenario);
        await db.SaveChangesAsync(Cancellation);
        return scenario;
    }

    [Fact]
    public async Task Player_creates_own_sheet_in_campaign_and_second_active_is_refused()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync(UserRole.Player, "Игрок");
        var campaign = await app.AddCampaignAsync(keeper, player);
        var api = Api(player);

        var context = await api.GetCreationContextAsync(CharacterKind.Player, campaign.Id, null, Cancellation);
        Assert.True(context.CanCreate);
        Assert.Equal(campaign.Name, context.CampaignName);

        var created = await api.CreateAsync(new CreateCharacterRequest
        {
            Kind = CharacterKind.Player, CampaignId = campaign.Id, Sheet = Sheet("Харви Уолтерс"),
        }, Cancellation);

        var read = await api.GetAsync(created.Id, Cancellation);
        Assert.Equal("Харви Уолтерс", read.Sheet.Personal.Name);
        Assert.Equal(campaign.Id, read.CampaignId);
        Assert.Equal(created.Version, read.Version);
        Assert.True((await Api(keeper).GetAsync(created.Id, Cancellation)).CanEdit);

        var again = await api.GetCreationContextAsync(CharacterKind.Player, campaign.Id, null, Cancellation);
        Assert.False(again.CanCreate);
        Assert.Equal(created.Id, again.ActiveCharacterId);
        await Fails(HttpStatusCode.Conflict, () => api.CreateAsync(new CreateCharacterRequest
        {
            Kind = CharacterKind.Player, CampaignId = campaign.Id, Sheet = Sheet("Второй"),
        }, Cancellation));
    }

    [Fact]
    public async Task Stranger_cannot_put_a_sheet_into_someone_elses_campaign()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var stranger = await app.AddUserAsync(UserRole.Player);
        var campaign = await app.AddCampaignAsync(keeper);

        var context = await Api(stranger).GetCreationContextAsync(CharacterKind.Player, campaign.Id, null, Cancellation);
        Assert.False(context.CanCreate);
        Assert.Null(context.CampaignName); // название чужой кампании не утекает
        await Fails(HttpStatusCode.Forbidden, () => Api(stranger).CreateAsync(new CreateCharacterRequest
        {
            Kind = CharacterKind.Player, CampaignId = campaign.Id, Sheet = Sheet("Чужак"),
        }, Cancellation));
        await Fails(HttpStatusCode.Forbidden, () => Api(stranger).CreateAsync(new CreateCharacterRequest
        {
            Kind = CharacterKind.Npc, Sheet = Sheet("НПС игрока"),
        }, Cancellation));
    }

    [Fact]
    public async Task Unknown_skill_in_new_sheet_is_400()
    {
        TestDatabase.SkipIfMissing();
        var player = await app.AddUserAsync(UserRole.Player);

        await Fails(HttpStatusCode.BadRequest, () => Api(player).CreateAsync(new CreateCharacterRequest
        {
            Kind = CharacterKind.Player, Sheet = Sheet("Без кампании", new SheetSkill { SkillId = Guid.NewGuid(), Value = 40 }),
        }, Cancellation));
    }

    [Fact]
    public async Task Quick_npc_from_scenario_writes_sheet_and_cast_together()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var scenario = await AddScenarioAsync(Era.Modern);

        var context = await Api(keeper).GetCreationContextAsync(CharacterKind.Npc, null, scenario.Id, Cancellation);
        Assert.True(context.CanCreate);
        Assert.Equal(Era.Modern, context.Era);

        var created = await Api(keeper).CreateAsync(new CreateCharacterRequest
        {
            Kind = CharacterKind.Npc,
            Sheet = Sheet("Громила"),
            Cast = new NpcCastRequest(scenario.Id, NpcRole.Enemy, 3),
        }, Cancellation);

        await using var db = app.Database.CreateContext();
        var cast = await db.ScenarioNpcs.SingleAsync(n => n.CharacterId == created.Id, Cancellation);
        Assert.Equal((scenario.Id, NpcRole.Enemy, 3), (cast.ScenarioId, cast.Role, cast.Count));
        Assert.Null((await db.Characters.SingleAsync(c => c.Id == created.Id, Cancellation)).CampaignId);

        // Связь в несуществующий сценарий — отказ целиком: листа без связи не остаётся.
        var before = await db.Characters.CountAsync(Cancellation);
        await Fails(HttpStatusCode.NotFound, () => Api(keeper).CreateAsync(new CreateCharacterRequest
        {
            Kind = CharacterKind.Npc, Sheet = Sheet("Двойник"), Cast = new NpcCastRequest(Guid.NewGuid()),
        }, Cancellation));
        Assert.Equal(before, await db.Characters.CountAsync(Cancellation));
    }

    [Fact]
    public async Task Npc_of_campaign_only_by_its_keeper_and_library_hides_foreign_campaign_npcs()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var otherKeeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync(UserRole.Player);
        var campaign = await app.AddCampaignAsync(keeper, player);

        await Fails(HttpStatusCode.Forbidden, () => Api(otherKeeper).CreateAsync(new CreateCharacterRequest
        {
            Kind = CharacterKind.Npc, CampaignId = campaign.Id, Sheet = Sheet("Чужой НПС"),
        }, Cancellation));
        var ownNpc = await Api(keeper).CreateAsync(new CreateCharacterRequest
        {
            Kind = CharacterKind.Npc, CampaignId = campaign.Id, Sheet = Sheet($"Свой НПС {Guid.NewGuid():N}"),
        }, Cancellation);
        var libraryNpc = await Api(otherKeeper).CreateAsync(new CreateCharacterRequest
        {
            Kind = CharacterKind.Npc, Sheet = Sheet($"Библиотечный {Guid.NewGuid():N}"),
        }, Cancellation);

        var mine = await Api(keeper).ListAsync(CharacterKind.Npc, archived: false, Cancellation);
        Assert.Contains(mine, n => n.Id == ownNpc.Id && n.CampaignName == campaign.Name && n.CanEdit);
        Assert.Contains(mine, n => n.Id == libraryNpc.Id);

        var theirs = await Api(otherKeeper).ListAsync(CharacterKind.Npc, archived: false, Cancellation);
        Assert.DoesNotContain(theirs, n => n.Id == ownNpc.Id);

        await Fails(HttpStatusCode.Forbidden, () => Api(player).ListAsync(CharacterKind.Npc, archived: false, Cancellation));
    }

    [Fact]
    public async Task Archive_moves_npc_between_lists()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var created = await Api(keeper).CreateAsync(new CreateCharacterRequest
        {
            Kind = CharacterKind.Pregen, Sheet = Sheet($"Преген {Guid.NewGuid():N}") with
            {
                Biography = new Biography { Backstory = new string('а', 500) },
            },
        }, Cancellation);

        var active = await Api(keeper).ListAsync(CharacterKind.Pregen, archived: false, Cancellation);
        var row = Assert.Single(active, p => p.Id == created.Id);
        Assert.Equal(CharacterLimits.SummaryBackstoryLength + 1, row.Backstory!.Length); // начало и «…»

        await Api(keeper).SetStatusAsync(created.Id, CharacterStatus.Archived, row.Version, Cancellation);

        Assert.DoesNotContain(await Api(keeper).ListAsync(CharacterKind.Pregen, archived: false, Cancellation), p => p.Id == created.Id);
        Assert.Contains(await Api(keeper).ListAsync(CharacterKind.Pregen, archived: true, Cancellation), p => p.Id == created.Id);
    }

    [Fact]
    public async Task Pregen_of_scenario_needs_a_keeper_and_lands_in_the_scenario()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync(UserRole.Player);
        var scenario = await AddScenarioAsync();

        await Fails(HttpStatusCode.Forbidden, () => Api(player).CreateAsync(new CreateCharacterRequest
        {
            Kind = CharacterKind.Pregen, ScenarioId = scenario.Id, Sheet = Sheet("Преген игрока"),
        }, Cancellation));

        var created = await Api(keeper).CreateAsync(new CreateCharacterRequest
        {
            Kind = CharacterKind.Pregen, ScenarioId = scenario.Id, Sheet = Sheet("Доктор Морган"),
        }, Cancellation);

        var read = await Api(player).GetAsync(created.Id, Cancellation); // преген читает любой вошедший
        Assert.Equal(scenario.Id, read.ScenarioId);
        Assert.False(read.CanEdit);
    }
}
