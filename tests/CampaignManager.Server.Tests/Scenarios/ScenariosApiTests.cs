using System.Net;
using CampaignManager.ApiClient.Scenarios;
using CampaignManager.Contracts.Platform;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.Core;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.Core.Identity;
using CampaignManager.Core.Scenarios;
using CampaignManager.Data.Catalogs;
using CampaignManager.Data.Identity;
using CampaignManager.Server.Tests.Campaigns;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampaignManager.Server.Tests.Scenarios;

/// <summary>
/// Сценарии (T2.5a): права автора, чужого Хранителя и игрока; правка одной вкладки не затирает другую (каждая
/// часть — своя строка, корень — с версией); дерево локаций; проверки; твари и предметы по справочнику; состав НПС и
/// прегены.
/// </summary>
public sealed class ScenariosApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private ScenariosApiClient Api(User user) => new(app.Http(user));

    private static async Task<ApiException> Fails(HttpStatusCode status, Func<Task> call)
    {
        var error = await Assert.ThrowsAsync<ApiException>(call);
        Assert.Equal(status, error.StatusCode);
        return error;
    }

    private async Task<(User Author, ScenarioDto Scenario)> NewScenarioAsync(string name = "Дом на холме")
    {
        var author = await app.AddUserAsync(UserRole.Keeper, "Автор");
        var scenario = await Api(author).CreateAsync(new ScenarioInput(name, "Кратко", "Аркхем", Era.Classic, "1925"), Cancellation);
        return (author, scenario);
    }

    private async Task<Guid> AddSkillAsync(string name)
    {
        await using var db = app.Database.CreateContext();
        var skill = new Skill { Name = $"{name} {Guid.NewGuid():N}"[..30], BaseValue = 10 };
        db.Skills.Add(skill);
        await db.SaveChangesAsync(Cancellation);
        return skill.Id;
    }

    [Fact]
    public async Task Author_creates_scenario_and_list_shows_rights_per_keeper()
    {
        TestDatabase.SkipIfMissing();
        var (author, created) = await NewScenarioAsync();
        var otherKeeper = await app.AddUserAsync(UserRole.Keeper);
        var admin = await app.AddUserAsync(UserRole.Admin);

        Assert.True(created.CanEdit);
        Assert.True(created.CanDelete);
        Assert.Equal("Автор", created.AuthorName);
        Assert.Equal(Era.Classic, created.Era);
        Assert.True(created.IsAuthor);

        var list = await Api(otherKeeper).ListAsync(Cancellation);
        Assert.True(list.CanCreate);
        var row = Assert.Single(list.Items, s => s.Id == created.Id);
        Assert.True(row.CanEdit);
        Assert.False(row.CanDelete);
        Assert.False(row.IsAuthor);
        Assert.False((await Api(otherKeeper).GetAsync(created.Id, Cancellation)).IsAuthor);
        Assert.True((await Api(author).ListAsync(Cancellation)).Items.Single(s => s.Id == created.Id).IsAuthor);
        Assert.True((await Api(admin).ListAsync(Cancellation)).Items.Single(s => s.Id == created.Id).CanDelete);
    }

    [Fact]
    public async Task Player_sees_nothing_and_writes_get_404()
    {
        TestDatabase.SkipIfMissing();
        var (_, scenario) = await NewScenarioAsync();
        var player = await app.AddUserAsync();
        var api = Api(player);

        await Fails(HttpStatusCode.Forbidden, () => api.ListAsync(Cancellation));
        await Fails(HttpStatusCode.Forbidden, () => api.CreateAsync(new ScenarioInput("Свой"), Cancellation));
        await Fails(HttpStatusCode.NotFound, () => api.GetAsync(scenario.Id, Cancellation));
        await Fails(HttpStatusCode.NotFound, () => api.UpdateAsync(scenario.Id, new ScenarioInput("Взлом"), scenario.Version, Cancellation));
        await Fails(HttpStatusCode.NotFound, () => api.AddLocationAsync(scenario.Id, new LocationInput("Подвал"), Cancellation));
        await Fails(HttpStatusCode.NotFound, () => api.AddFactAsync(scenario.Id, new KeyFactInput(KeyFactType.Truth, "Правда"), Cancellation));
        await Fails(HttpStatusCode.NotFound, () => api.DeleteAsync(scenario.Id, Cancellation));
    }

    [Fact]
    public async Task Other_keeper_edits_parts_but_cannot_delete_scenario()
    {
        TestDatabase.SkipIfMissing();
        var (author, scenario) = await NewScenarioAsync();
        var other = Api(await app.AddUserAsync(UserRole.Keeper));

        var location = await other.AddLocationAsync(scenario.Id, new LocationInput("Библиотека"), Cancellation);
        await other.UpdateAsync(scenario.Id, new ScenarioInput("Дом на холме, редакция"), scenario.Version, Cancellation);
        await Fails(HttpStatusCode.Forbidden, () => other.DeleteAsync(scenario.Id, Cancellation));

        var read = await Api(author).GetAsync(scenario.Id, Cancellation);
        Assert.Equal("Дом на холме, редакция", read.Name);
        Assert.Equal(location.Id, Assert.Single(read.Locations).Id);

        await Api(author).DeleteAsync(scenario.Id, Cancellation);
        await Fails(HttpStatusCode.NotFound, () => Api(author).GetAsync(scenario.Id, Cancellation));
    }

    [Fact]
    public async Task Editing_one_tab_does_not_overwrite_another()
    {
        TestDatabase.SkipIfMissing();
        var (author, scenario) = await NewScenarioAsync();
        var ipad = Api(author);
        var laptop = Api(await app.AddUserAsync(UserRole.Keeper));

        // Оба устройства открыли рабочее место; ноутбук правит локацию и факт, iPad следом — шапку и текст.
        var location = await ipad.AddLocationAsync(scenario.Id, new LocationInput("Особняк", Description: "Старое"), Cancellation);
        var opened = await ipad.GetAsync(scenario.Id, Cancellation);
        await laptop.UpdateLocationAsync(scenario.Id, location.Id, new LocationInput("Особняк", Description: "Новое описание"), Cancellation);
        await laptop.AddFactAsync(scenario.Id, new KeyFactInput(KeyFactType.Truth, "Дом стоит на кладбище"), Cancellation);

        var header = await ipad.UpdateAsync(scenario.Id, new ScenarioInput(opened.Name, "Новое кратко", opened.Setting, opened.Era, opened.SettingDate),
            opened.Version, Cancellation);
        await ipad.SaveTextAsync(scenario.Id, "# Текст Хранителя", header.Version, Cancellation);

        var read = await ipad.GetAsync(scenario.Id, Cancellation);
        Assert.Equal("Новое описание", Assert.Single(read.Locations).Description);
        Assert.Equal("Дом стоит на кладбище", Assert.Single(read.KeyFacts).Title);
        Assert.Equal("Новое кратко", read.Summary);
        Assert.Equal("# Текст Хранителя", read.BodyMd);

        // Правка шапки с устаревшей версией — 409, а не молчаливая перезапись текста.
        var stale = await Fails(HttpStatusCode.Conflict, () =>
            laptop.UpdateAsync(scenario.Id, new ScenarioInput("Чужая правка"), opened.Version, Cancellation));
        Assert.True(stale.IsStale);
        Assert.Equal("Новое кратко", (await ipad.GetAsync(scenario.Id, Cancellation)).Summary);
    }

    [Fact]
    public async Task Location_tree_refuses_cycles_and_deletes_with_children()
    {
        TestDatabase.SkipIfMissing();
        var (author, scenario) = await NewScenarioAsync();
        var api = Api(author);

        var house = await api.AddLocationAsync(scenario.Id, new LocationInput("Дом"), Cancellation);
        var cellar = await api.AddLocationAsync(scenario.Id, new LocationInput("Подвал", ParentId: house.Id), Cancellation);
        var crypt = await api.AddLocationAsync(scenario.Id, new LocationInput("Склеп", ParentId: cellar.Id), Cancellation);
        var garden = await api.AddLocationAsync(scenario.Id, new LocationInput("Сад"), Cancellation);

        await Fails(HttpStatusCode.BadRequest, () => api.UpdateLocationAsync(scenario.Id, house.Id, new LocationInput("Дом", ParentId: crypt.Id), Cancellation));
        await Fails(HttpStatusCode.BadRequest, () => api.UpdateLocationAsync(scenario.Id, house.Id, new LocationInput("Дом", ParentId: house.Id), Cancellation));

        // Дерево — в глубину: Дом, Подвал, Склеп, Сад; порядок соседей меняется.
        var read = await api.GetAsync(scenario.Id, Cancellation);
        Assert.Equal(["Дом", "Подвал", "Склеп", "Сад"], read.Locations.Select(l => l.Name));
        await api.ReorderAsync(scenario.Id, new ReorderRequest(ScenarioPart.Locations, [garden.Id, house.Id]), Cancellation);
        Assert.Equal(["Сад", "Дом", "Подвал", "Склеп"], (await api.GetAsync(scenario.Id, Cancellation)).Locations.Select(l => l.Name));
        await Fails(HttpStatusCode.BadRequest, () =>
            api.ReorderAsync(scenario.Id, new ReorderRequest(ScenarioPart.Locations, [garden.Id, cellar.Id]), Cancellation));

        await api.DeleteLocationAsync(scenario.Id, house.Id, Cancellation);
        Assert.Equal("Сад", Assert.Single((await api.GetAsync(scenario.Id, Cancellation)).Locations).Name);
    }

    [Fact]
    public async Task Checks_target_skill_characteristic_or_luck()
    {
        TestDatabase.SkipIfMissing();
        var (author, scenario) = await NewScenarioAsync();
        var api = Api(author);
        var library = await api.AddLocationAsync(scenario.Id, new LocationInput("Библиотека"), Cancellation);
        var skillId = await AddSkillAsync("Библиотеки");

        var skill = await api.AddCheckAsync(scenario.Id, library.Id,
            new CheckInput(CheckTarget.Skill, skillId, Difficulty: Difficulty.Hard, OnSuccess: "Находит дневник"), Cancellation);
        await api.AddCheckAsync(scenario.Id, library.Id, new CheckInput(CheckTarget.Characteristic, Characteristic: Characteristic.STR), Cancellation);
        var luck = await api.AddCheckAsync(scenario.Id, library.Id, new CheckInput(CheckTarget.Luck), Cancellation);

        await Fails(HttpStatusCode.BadRequest, () => api.AddCheckAsync(scenario.Id, library.Id, new CheckInput(CheckTarget.Skill, Guid.NewGuid()), Cancellation));
        await Fails(HttpStatusCode.BadRequest, () => api.AddCheckAsync(scenario.Id, library.Id, new CheckInput(CheckTarget.Characteristic), Cancellation));

        // Смена вида проверки чистит ненужное: из навыка в Удачу — без навыка.
        var changed = await api.UpdateCheckAsync(scenario.Id, skill.Id, new CheckInput(CheckTarget.Luck, skillId), Cancellation);
        Assert.Null(changed.SkillId);
        await api.DeleteCheckAsync(scenario.Id, luck.Id, Cancellation);

        var checks = Assert.Single((await api.GetAsync(scenario.Id, Cancellation)).Locations).Checks;
        Assert.Equal([CheckTarget.Luck, CheckTarget.Characteristic], checks.Select(c => c.TargetKind));

        // Проверка чужого сценария по своему адресу не находится.
        var (_, foreign) = await NewScenarioAsync("Чужой");
        await Fails(HttpStatusCode.NotFound, () => api.DeleteCheckAsync(foreign.Id, checks[0].Id, Cancellation));
    }

    [Fact]
    public async Task Creatures_follow_bestiary_until_overridden_and_items_can_be_props()
    {
        TestDatabase.SkipIfMissing();
        var (author, scenario) = await NewScenarioAsync();
        var api = Api(author);

        Guid creatureId, itemId, rowId;
        await using (var db = app.Database.CreateContext())
        {
            var creature = new Creature
            {
                Name = $"Гуль {Guid.NewGuid():N}"[..20], Type = CreatureType.Monsters,
                Statblock = CmJson.Write(new Statblock { HitPoints = 13 }), StatblockVersion = Statblock.CurrentVersion,
            };
            var item = new Item { Name = $"Фонарь {Guid.NewGuid():N}"[..20], Description = "Керосиновый" };
            db.AddRange(creature, item);
            await db.SaveChangesAsync(Cancellation);
            (creatureId, itemId) = (creature.Id, item.Id);
        }

        var added = await api.AddCreatureAsync(scenario.Id, new ScenarioCreatureInput(creatureId, Count: 3, LocationNote: "в склепе"), Cancellation);
        Assert.Equal(13, added.Statblock.HitPoints);
        Assert.False(added.HasOwnStatblock);
        rowId = added.Id;

        // Своя версия статблока (как у перенесённых тварей) побеждает бестиарий, «сбросить» возвращает к нему.
        await using (var db = app.Database.CreateContext())
        {
            var row = await db.ScenarioCreatures.SingleAsync(c => c.Id == rowId, Cancellation);
            row.Statblock = CmJson.Write(new Statblock { HitPoints = 20 });
            row.StatblockVersion = Statblock.CurrentVersion;
            await db.SaveChangesAsync(Cancellation);
        }

        var own = Assert.Single((await api.GetAsync(scenario.Id, Cancellation)).Creatures);
        Assert.Equal(20, own.Statblock.HitPoints);
        Assert.True(own.HasOwnStatblock);
        var reset = await api.UpdateCreatureAsync(scenario.Id, rowId, new ScenarioCreatureInput(null, "Вожак", 1, ResetStatblock: true), Cancellation);
        Assert.Equal(13, reset.Statblock.HitPoints);
        Assert.Equal("Вожак", reset.Name);
        await Fails(HttpStatusCode.BadRequest, () => api.AddCreatureAsync(scenario.Id, new ScenarioCreatureInput(null), Cancellation));

        var lantern = await api.AddItemAsync(scenario.Id, new ScenarioItemInput(itemId, LocationNote: "на столе"), Cancellation);
        Assert.Equal("Керосиновый", lantern.Description);
        var prop = await api.AddItemAsync(scenario.Id, new ScenarioItemInput(null, "Резной идол", "Тёплый на ощупь"), Cancellation);
        Assert.Null(prop.ItemId);
        await Fails(HttpStatusCode.BadRequest, () => api.AddItemAsync(scenario.Id, new ScenarioItemInput(null), Cancellation));
        Assert.Equal(2, (await api.GetAsync(scenario.Id, Cancellation)).Items.Count);
    }

    [Fact]
    public async Task Npc_is_cast_by_link_and_removal_keeps_the_sheet()
    {
        TestDatabase.SkipIfMissing();
        var (author, scenario) = await NewScenarioAsync();
        var api = Api(author);
        var npc = await app.AddCharacterAsync(CharacterKind.Npc, "Профессор Армитедж");
        var pregen = await app.AddCharacterAsync(CharacterKind.Pregen, "Сыщица");

        await api.CastNpcAsync(scenario.Id, npc.Id, new NpcCastInput(NpcRole.Ally, 1), Cancellation);
        await api.CastNpcAsync(scenario.Id, npc.Id, new NpcCastInput(NpcRole.Enemy, 2, "Притворяется"), Cancellation);
        var cast = Assert.Single((await api.GetAsync(scenario.Id, Cancellation)).Npcs);
        Assert.Equal((NpcRole.Enemy, 2, "Притворяется"), (cast.Role, cast.Count, cast.Notes));
        Assert.Contains(scenario.Name, cast.Character.CastIn);

        await Fails(HttpStatusCode.BadRequest, () => api.CastNpcAsync(scenario.Id, pregen.Id, new NpcCastInput(), Cancellation));
        await Fails(HttpStatusCode.BadRequest, () => api.CastNpcAsync(scenario.Id, npc.Id, new NpcCastInput(Count: 0), Cancellation));

        // НПС чужой кампании другому Хранителю не виден — и в состав его не занять.
        var otherKeeper = await app.AddUserAsync(UserRole.Keeper);
        var campaign = await app.AddCampaignAsync(otherKeeper);
        var campaignNpc = await app.AddCharacterAsync(CharacterKind.Npc, "Чужой НПС", campaign: campaign.Id);
        await Fails(HttpStatusCode.NotFound, () => api.CastNpcAsync(scenario.Id, campaignNpc.Id, new NpcCastInput(), Cancellation));

        await api.RemoveNpcAsync(scenario.Id, npc.Id, Cancellation);
        Assert.Empty((await api.GetAsync(scenario.Id, Cancellation)).Npcs);
        await using var db = app.Database.CreateContext();
        Assert.True(await db.Characters.AnyAsync(c => c.Id == npc.Id, Cancellation));
    }

    [Fact]
    public async Task Pregen_is_copied_from_library_and_reserved_one_cannot_be_removed()
    {
        TestDatabase.SkipIfMissing();
        var (author, scenario) = await NewScenarioAsync();
        var api = Api(author);
        var template = await app.AddCharacterAsync(CharacterKind.Pregen, "Заготовка");

        var copy = await api.AddPregenAsync(scenario.Id, template.Id, Cancellation);
        Assert.NotEqual(template.Id, copy.Id);
        var pregen = Assert.Single((await api.GetAsync(scenario.Id, Cancellation)).Pregens);
        Assert.Equal("Заготовка", pregen.Character.Name);
        Assert.False(pregen.IsReserved);
        await Fails(HttpStatusCode.BadRequest, () => api.AddPregenAsync(scenario.Id, copy.Id, Cancellation));

        var player = await app.AddUserAsync();
        var campaign = await app.AddCampaignAsync(author, player);
        var run = await app.AddRunAsync(campaign.Id, scenarioId: scenario.Id, signupOpen: true);
        await app.ReserveAsync(run.Id, copy.Id, player.Id);
        Assert.True(Assert.Single((await api.GetAsync(scenario.Id, Cancellation)).Pregens).IsReserved);
        await Fails(HttpStatusCode.Conflict, () => api.RemovePregenAsync(scenario.Id, copy.Id, Cancellation));
        var inUse = await Fails(HttpStatusCode.Conflict, () => api.DeleteAsync(scenario.Id, Cancellation));
        Assert.Equal(ApiProblemCodes.InUse, inUse.Code);

        var second = await api.AddPregenAsync(scenario.Id, template.Id, Cancellation);
        await api.RemovePregenAsync(scenario.Id, second.Id, Cancellation);
        await using var db = app.Database.CreateContext();
        var removed = await db.Characters.SingleAsync(c => c.Id == second.Id, Cancellation);
        Assert.Null(removed.ScenarioId);
        Assert.Equal(CharacterStatus.Archived, removed.Status);
    }

    [Fact]
    public async Task Handout_keeps_keeper_note_apart_and_requires_known_file()
    {
        TestDatabase.SkipIfMissing();
        var (author, scenario) = await NewScenarioAsync();
        var api = Api(author);

        var handout = await api.AddHandoutAsync(scenario.Id, new HandoutInput("Письмо №1", "Дорогой Генри…", "Выдать после второй ночи"), Cancellation);
        Assert.Equal(("Дорогой Генри…", "Выдать после второй ночи"), (handout.PlayerText, handout.KeeperNote));
        await Fails(HttpStatusCode.BadRequest, () =>
            api.UpdateHandoutAsync(scenario.Id, handout.Id, new HandoutInput("Письмо", FileId: Guid.NewGuid()), Cancellation));
        await Fails(HttpStatusCode.BadRequest, () => api.AddHandoutAsync(scenario.Id, new HandoutInput(" "), Cancellation));

        var second = await api.AddHandoutAsync(scenario.Id, new HandoutInput("Карта"), Cancellation);
        await api.ReorderAsync(scenario.Id, new ReorderRequest(ScenarioPart.Handouts, [second.Id, handout.Id]), Cancellation);
        Assert.Equal(["Карта", "Письмо №1"], (await api.GetAsync(scenario.Id, Cancellation)).Handouts.Select(h => h.Name));
    }
}
