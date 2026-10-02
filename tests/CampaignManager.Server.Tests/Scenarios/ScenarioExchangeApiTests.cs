using System.Net;
using System.Text;
using System.Text.Json.Nodes;
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
using CampaignManager.Data.Characters;
using CampaignManager.Data.Files;
using CampaignManager.Data.Identity;
using CampaignManager.Data.Music;
using CampaignManager.Data.Scenarios;
using CampaignManager.Server.Tests.Campaigns;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampaignManager.Server.Tests.Scenarios;

/// <summary>
/// Импорт и экспорт сценария файлом (T2.5d): формат v1 читается как есть (родитель и навык по имени, НПС из библиотеки по
/// имени), импорт — одна транзакция с пробным прогоном, экспорт отдаёт и то, что v1 терял (твари, предметы, прегены,
/// музыка), а импорт экспорта даёт тот же экспорт.
/// </summary>
public sealed class ScenarioExchangeApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
{
    private static readonly SemaphoreSlim SkillsSeeded = new(1, 1);
    private static readonly HashSet<string> SeededDatabases = [];

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private ScenarioExchangeApiClient Exchange(User user) => new(app.Http(user));

    private ScenariosApiClient Scenarios(User user) => new(app.Http(user));

    private static MemoryStream Body(string json) => new(Encoding.UTF8.GetBytes(json));

    /// <summary>Навыки книги по <see cref="SkillCodes"/> — один раз на базу класса: на них ссылаются проверки и листы.</summary>
    private async Task<Dictionary<string, Guid>> SkillsAsync()
    {
        await SkillsSeeded.WaitAsync(Cancellation);
        try
        {
            await using var db = app.Database.CreateContext();
            if (SeededDatabases.Add(app.Database.ConnectionString))
            {
                var ids = new Dictionary<string, Guid>();
                foreach (var (code, name) in SkillCodes.BookNames.OrderBy(p => p.Key.Count(c => c == '.')))
                {
                    var skill = new Skill
                    {
                        Code = code,
                        Name = name,
                        ParentId = SkillCodes.ParentOf(code) is { } parent && ids.TryGetValue(parent, out var parentId) ? parentId : null,
                        BaseValue = 5,
                        BaseFormula = code switch { SkillCodes.Dodge => "DEX/2", SkillCodes.LanguageOwn => "EDU", _ => null },
                        Category = SkillCategory.Knowledge,
                    };
                    ids[code] = skill.Id;
                    db.Skills.Add(skill);
                }

                await db.SaveChangesAsync(Cancellation);
            }

            return await db.Skills.Where(s => s.Code != null).ToDictionaryAsync(s => s.Code!, s => s.Id, Cancellation);
        }
        finally
        {
            SkillsSeeded.Release();
        }
    }

    private static string Unique(string name) => $"{name} {Guid.NewGuid():N}"[..(name.Length + 7)];

    /// <summary>Файл v1, как его писал скилл переноса: поля анонса, родитель по имени, навыки строками.</summary>
    private static string V1File(string name, string npcName) => $$"""
        {
          "name": "{{name}}", "location": "Аркхем", "era": "Июнь 1925 года",
          "isTemplate": true, "isPublished": false,
          "description": "Кратко", "journal": "## Текст Хранителя",
          "keyFacts": [ { "title": "Дом стоит на кладбище", "type": "Truth", "content": "…" } ],
          "locations": [
            { "name": "Особняк", "address": "Френч-Хилл", "description": "Старый дом",
              "skillChecks": [
                { "skillName": "Внимание", "difficulty": "Hard", "successResult": "Находит люк" },
                { "skillName": "ИНТ", "successResult": "Догадывается" },
                { "skillName": "Удача", "difficulty": "Extreme" } ] },
            { "name": "Подвал", "parent": "особняк", "skillChecks": [ { "skillName": "Наука (астрономия)" } ] },
            { "name": "Сад", "parent": "Оранжерея" },
          ],
          "handouts": [ { "name": "Письмо", "description": "Дорогой друг…", "fileUrl": "https://old-minio/letter.png", "keeperNote": "после второй ночи" } ],
          "npcs": [ {
            "name": "{{npcName}}", "role": "Enemy", "count": 2, "notes": "в подвале",
            "occupation": "Культист", "age": 40,
            "characteristics": { "str": 70, "con": 60, "siz": 60, "dex": 50, "int": 65, "app": 40, "pow": 75, "edu": 50 },
            "hitPoints": 15, "magicPoints": 15, "sanity": 30, "luck": 40, "damageBonus": "+1d4", "build": "1", "moveSpeed": 8, "dodge": 40,
            "skills": { "Ближний бой (драка)": 55, "внимание": 60, "Хиромантия": 25 }
          } ]
        }
        """;

    [Fact]
    public async Task V1_file_dry_run_writes_nothing_then_import_creates_everything()
    {
        TestDatabase.SkipIfMissing();
        var skills = await SkillsAsync();
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var name = Unique("Дом на холме");
        var npcName = Unique("Сектант");
        var exchange = Exchange(keeper);

        var dry = await exchange.ImportAsync(Body(V1File(name, npcName)), dryRun: true, cancellationToken: Cancellation);
        Assert.True(dry.DryRun);
        Assert.False(dry.Imported);
        Assert.Null(dry.ScenarioId);
        Assert.Equal(0, dry.Failed);
        Assert.Equal((3, 4, 1, 1, 1), (dry.Locations, dry.Checks, dry.KeyFacts, dry.Handouts, dry.NpcsCreated));
        Assert.Contains(dry.Warnings, w => w.Contains("анонс", StringComparison.Ordinal));
        Assert.Contains(dry.Lines, l => l is { Part: ScenarioImportPart.Location, Name: "Сад" } && l.Message!.Contains("Оранжерея", StringComparison.Ordinal));
        Assert.Contains(dry.Lines, l => l.Part == ScenarioImportPart.Handout && l.Message!.Contains("загрузите", StringComparison.Ordinal));
        Assert.Contains(dry.Lines, l => l.Part == ScenarioImportPart.Npc && l.Message!.Contains("Хиромантия", StringComparison.Ordinal));
        Assert.DoesNotContain((await Scenarios(keeper).ListAsync(Cancellation)).Items, s => s.Name == name);
        await using (var db = app.Database.CreateContext())
        {
            Assert.False(await db.Characters.AnyAsync(c => c.Name == npcName, Cancellation));
        }

        var report = await exchange.ImportAsync(Body(V1File(name, npcName)), dryRun: false, cancellationToken: Cancellation);
        Assert.True(report.Imported);
        var scenario = await Scenarios(keeper).GetAsync(report.ScenarioId!.Value, Cancellation);
        Assert.Equal("Хранитель", scenario.AuthorName);
        Assert.Equal(Era.Classic, scenario.Era); // по году в «era»
        Assert.Equal("Июнь 1925 года", scenario.SettingDate);
        Assert.Equal("## Текст Хранителя", scenario.BodyMd);
        Assert.Equal(["Особняк", "Подвал", "Сад"], scenario.Locations.Select(l => l.Name));
        Assert.Equal(scenario.Locations[0].Id, scenario.Locations[1].ParentId);
        Assert.Null(scenario.Locations[2].ParentId);

        var checks = scenario.Locations[0].Checks;
        Assert.Equal((CheckTarget.Skill, skills["skill.spot-hidden"], Difficulty.Hard), (checks[0].TargetKind, checks[0].SkillId!.Value, checks[0].Difficulty));
        Assert.Equal((CheckTarget.Characteristic, Characteristic.INT), (checks[1].TargetKind, checks[1].Characteristic!.Value));
        Assert.Equal((CheckTarget.Luck, Difficulty.Extreme), (checks[2].TargetKind, checks[2].Difficulty));
        Assert.Equal(skills["skill.science.astronomy"], Assert.Single(scenario.Locations[1].Checks).SkillId);

        var handout = Assert.Single(scenario.Handouts);
        Assert.Equal(("Дорогой друг…", "после второй ночи", (Guid?)null), (handout.PlayerText, handout.KeeperNote, handout.FileId));
        var npc = Assert.Single(scenario.Npcs);
        Assert.Equal((NpcRole.Enemy, 2, "в подвале"), (npc.Role, npc.Count, npc.Notes));

        // Лист — SheetBuilder.FromImport: напечатанное, расходящееся с формулой, — поправкой; Уклонение — строкой навыка.
        await using var read = app.Database.CreateContext();
        var row = await read.Characters.SingleAsync(c => c.Id == npc.Character.Id, Cancellation);
        var sheet = CmJson.ReadSheet(row.Sheet, row.SheetVersion);
        Assert.Equal(CharacterKind.Npc, row.Kind);
        Assert.Equal(15, sheet.Overrides.MaxHitPoints); // по формуле 12
        Assert.Equal(40, Assert.Single(sheet.Skills, s => s.SkillId == skills[SkillCodes.Dodge]).Value);
        Assert.Equal(60, Assert.Single(sheet.Skills, s => s.SkillId == skills["skill.spot-hidden"]).Value);
        Assert.Contains(sheet.Skills, s => s is { SkillId: null, Name: "Хиромантия", Value: 25 });
        Assert.Equal(30, sheet.Current.Sanity);
    }

    [Fact]
    public async Task Npc_with_the_same_name_in_library_is_reused_not_duplicated()
    {
        TestDatabase.SkipIfMissing();
        await SkillsAsync();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var npcName = Unique("Пётр Ершов");
        var existing = await app.AddCharacterAsync(CharacterKind.Npc, npcName);

        var report = await Exchange(keeper).ImportAsync(
            Body(V1File(Unique("Ёлка"), npcName.Replace("Пётр", "петр", StringComparison.Ordinal))), dryRun: false, cancellationToken: Cancellation);

        Assert.True(report.Imported);
        Assert.Equal((0, 1), (report.NpcsCreated, report.NpcsReused));
        Assert.Contains(report.Lines, l => l is { Part: ScenarioImportPart.Npc, Outcome: ScenarioImportOutcome.Reused });
        var scenario = await Scenarios(keeper).GetAsync(report.ScenarioId!.Value, Cancellation);
        Assert.Equal(existing.Id, Assert.Single(scenario.Npcs).Character.Id);
        await using var db = app.Database.CreateContext();
        Assert.Equal(1, await db.Characters.CountAsync(c => c.Name == npcName, Cancellation));
    }

    [Fact]
    public async Task Error_in_the_middle_rolls_back_the_whole_import()
    {
        TestDatabase.SkipIfMissing();
        await SkillsAsync();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var name = Unique("Сломанный");
        var first = Unique("Первый");
        // Локации, факт и первый НПС проходят (и уже записаны в транзакции), второй НПС — с количеством 0, проверка — с
        // навыком, которого нет: импорт должен откатить всё, включая лист первого НПС.
        var json = $$"""
            {
              "name": "{{name}}",
              "keyFacts": [ { "title": "Факт" } ],
              "locations": [ { "name": "Холл", "skillChecks": [ { "skillName": "Телепатия" } ] } ],
              "npcs": [
                { "name": "{{first}}", "characteristics": { "str": 50, "con": 50, "siz": 50, "dex": 50, "int": 50, "app": 50, "pow": 50, "edu": 50 } },
                { "name": "Второй", "count": 0 },
              ],
              "pregens": [ { "name": "" } ]
            }
            """;

        var report = await Exchange(keeper).ImportAsync(Body(json), dryRun: false, cancellationToken: Cancellation);

        Assert.False(report.Imported);
        Assert.Null(report.ScenarioId);
        Assert.Equal(3, report.Failed);
        Assert.Contains(report.Lines, l => l is { Part: ScenarioImportPart.Check, Outcome: ScenarioImportOutcome.Failed }
                                           && l.Message!.Contains("Телепатия", StringComparison.Ordinal));
        Assert.Contains(report.Lines, l => l is { Part: ScenarioImportPart.Npc, Name: "Второй", Outcome: ScenarioImportOutcome.Failed });
        Assert.Contains(report.Lines, l => l is { Part: ScenarioImportPart.Pregen, Outcome: ScenarioImportOutcome.Failed });
        Assert.Contains(report.Lines, l => l is { Part: ScenarioImportPart.Npc, Outcome: ScenarioImportOutcome.Created } && l.Name == first);

        await using var db = app.Database.CreateContext();
        Assert.False(await db.Scenarios.AnyAsync(s => s.Name == name, Cancellation));
        Assert.False(await db.Characters.AnyAsync(c => c.Name == first, Cancellation));
        Assert.False(await db.ScenarioKeyFacts.AnyAsync(f => f.Title == "Факт" && !db.Scenarios.Any(s => s.Id == f.ScenarioId), Cancellation));
    }

    [Fact]
    public async Task Broken_file_and_player_are_refused()
    {
        TestDatabase.SkipIfMissing();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var player = await app.AddUserAsync();
        var (scenarioId, _) = await RichScenarioAsync(keeper, await SkillsAsync());

        var broken = await Assert.ThrowsAsync<ApiException>(() => Exchange(keeper).ImportAsync(Body("{ \"name\": "), false, cancellationToken: Cancellation));
        Assert.Equal(HttpStatusCode.BadRequest, broken.StatusCode);
        var nameless = await Assert.ThrowsAsync<ApiException>(() => Exchange(keeper).ImportAsync(Body("{ \"name\": \" \" }"), false, cancellationToken: Cancellation));
        Assert.Equal(HttpStatusCode.BadRequest, nameless.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await Assert.ThrowsAsync<ApiException>(() =>
            Exchange(player).ImportAsync(Body(V1File("Игрок", "Игрок")), false, cancellationToken: Cancellation))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Assert.ThrowsAsync<ApiException>(() =>
            Exchange(player).ExportAsync(scenarioId, Cancellation))).StatusCode);
    }

    /// <summary>
    /// Кругооборот: экспорт сценария, собранного правкой по строке (не импортом), → импорт копией → экспорт копии совпадает
    /// с первым, кроме названия. Сценарий — со всем, что v1 терял: тварь бестиария и своя, предмет справочника и реквизит,
    /// преген с оружием и биографией, музыка локации; плюс одноимённые локации в разных ветках дерева.
    /// </summary>
    [Fact]
    public async Task Export_import_export_round_trip_is_identical_except_name()
    {
        TestDatabase.SkipIfMissing();
        var skills = await SkillsAsync();
        var keeper = await app.AddUserAsync(UserRole.Keeper);
        var (scenarioId, names) = await RichScenarioAsync(keeper, skills);
        var exchange = Exchange(keeper);

        var first = await exchange.ExportAsync(scenarioId, Cancellation);
        Assert.DoesNotContain("\"id\"", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("skillId", first, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Дом на окраине", first, StringComparison.Ordinal); // кириллица — как есть, без \u

        var file = JsonNode.Parse(first)!.AsObject();
        Assert.Equal(2, file["creatures"]!.AsArray().Count);
        Assert.Equal(2, file["items"]!.AsArray().Count);
        Assert.Single(file["pregens"]!.AsArray());
        Assert.Equal("бой", file["locations"]![0]!["musicTags"]![0]!.GetValue<string>());
        Assert.Equal(names.Track, file["locations"]![0]!["tracks"]![0]!.GetValue<string>());
        Assert.Null(file["isTemplate"]); // анонс — у прохождения

        var copyName = Unique("Копия");
        var report = await exchange.ImportAsync(Body(first), dryRun: false, copyName, Cancellation);
        Assert.True(report.Imported, string.Join("\n", report.Lines.Where(l => l.Outcome == ScenarioImportOutcome.Failed).Select(l => $"{l.Name}: {l.Message}")));
        Assert.Equal((1, 1), (report.NpcsReused, report.Pregens));
        Assert.Equal(copyName, report.ScenarioName);

        var second = await exchange.ExportAsync(report.ScenarioId!.Value, Cancellation);
        var expected = JsonNode.Parse(first)!.AsObject();
        expected["name"] = copyName;
        Assert.Equal(expected.ToJsonString(), JsonNode.Parse(second)!.ToJsonString());

        // Преген копии — новый лист сценария-копии, НПС — тот же лист из библиотеки.
        var original = await Scenarios(keeper).GetAsync(scenarioId, Cancellation);
        var copy = await Scenarios(keeper).GetAsync(report.ScenarioId.Value, Cancellation);
        Assert.NotEqual(Assert.Single(original.Pregens).Character.Id, Assert.Single(copy.Pregens).Character.Id);
        Assert.Equal(Assert.Single(original.Npcs).Character.Id, Assert.Single(copy.Npcs).Character.Id);
        Assert.True(Assert.Single(copy.Creatures, c => c.CreatureId is null).HasOwnStatblock);
    }

    private sealed record RichNames(string Track);

    private async Task<(Guid ScenarioId, RichNames Names)> RichScenarioAsync(User keeper, Dictionary<string, Guid> skills)
    {
        var api = Scenarios(keeper);
        var scenario = await api.CreateAsync(new ScenarioInput(Unique("Дом на окраине"), "Кратко", "Аркхем", Era.Classic, "Осень 1925"), Cancellation);
        await api.SaveTextAsync(scenario.Id, "# Текст\n\nМного текста.", scenario.Version, Cancellation);

        var house = await api.AddLocationAsync(scenario.Id, new LocationInput("Дом", "Улица Вязов, 3", "Описание дома"), Cancellation);
        var cellar = await api.AddLocationAsync(scenario.Id, new LocationInput("Подвал", ParentId: house.Id), Cancellation);
        var barn = await api.AddLocationAsync(scenario.Id, new LocationInput("Амбар"), Cancellation);
        await api.AddLocationAsync(scenario.Id, new LocationInput("Подвал", Description: "второй подвал", ParentId: barn.Id), Cancellation);
        await api.AddCheckAsync(scenario.Id, house.Id, new CheckInput(CheckTarget.Skill, skills["skill.spot-hidden"], Difficulty: Difficulty.Hard,
            OnSuccess: "Люк", OnFailure: "Ничего"), Cancellation);
        await api.AddCheckAsync(scenario.Id, house.Id, new CheckInput(CheckTarget.Characteristic, Characteristic: Characteristic.POW), Cancellation);
        await api.AddCheckAsync(scenario.Id, cellar.Id, new CheckInput(CheckTarget.Luck, Difficulty: Difficulty.Extreme), Cancellation);
        await api.AddFactAsync(scenario.Id, new KeyFactInput(KeyFactType.Timeline, "День 1", "Приезд"), Cancellation);
        await api.AddFactAsync(scenario.Id, new KeyFactInput(KeyFactType.Truth, "Правда"), Cancellation);

        var track = Unique("Тревога");
        var fileId = Guid.CreateVersion7();
        Guid creatureId, itemId;
        await using (var db = app.Database.CreateContext())
        {
            db.Files.Add(new StoredFile { Id = fileId, ExternalUrl = "https://example.test/letter.png", ContentType = "image/png" });
            var music = new MusicTrack { Name = track, YoutubeId = "abc", Tags = ["бой"] };
            db.MusicTracks.Add(music);
            var creature = new Creature
            {
                Name = Unique("Гуль"), Type = CreatureType.Monsters,
                Statblock = CmJson.Write(new Statblock { HitPoints = 13 }), StatblockVersion = Statblock.CurrentVersion,
            };
            var item = new Item { Name = Unique("Фонарь"), Description = "Керосиновый" };
            db.Creatures.Add(creature);
            db.Items.Add(item);
            await db.SaveChangesAsync(Cancellation);
            (creatureId, itemId) = (creature.Id, item.Id);

            var location = await db.ScenarioLocations.SingleAsync(l => l.Id == house.Id, Cancellation);
            location.MusicTags = ["бой", "тайна"];
            db.LocationTracks.Add(new LocationTrack { LocationId = house.Id, TrackId = music.Id });
            await db.SaveChangesAsync(Cancellation);
        }

        await api.AddHandoutAsync(scenario.Id, new HandoutInput("Письмо", "Дорогой друг", "после второй ночи", fileId), Cancellation);
        await api.AddHandoutAsync(scenario.Id, new HandoutInput("Газета", "Заметка"), Cancellation);
        await api.AddCreatureAsync(scenario.Id, new ScenarioCreatureInput(creatureId, "Вожак", 2, "в склепе", "Голоден"), Cancellation);
        await api.AddItemAsync(scenario.Id, new ScenarioItemInput(itemId, LocationNote: "на столе"), Cancellation);
        await api.AddItemAsync(scenario.Id, new ScenarioItemInput(null, "Дневник", "Записи хозяина", Notes: "главная улика"), Cancellation);

        await using (var db = app.Database.CreateContext())
        {
            // Тварь только этого сценария — так перенос положил переделанный статблок v1.
            db.ScenarioCreatures.Add(new ScenarioCreature
            {
                ScenarioId = scenario.Id, Ord = 5, Name = "Тень", Count = 1,
                Statblock = CmJson.Write(new Statblock
                {
                    HitPoints = 20, DamageBonus = "+1d4",
                    Skills = [new CreatureSkill { SkillId = skills["skill.spot-hidden"], Name = "Внимание", Value = 60 }],
                }),
                StatblockVersion = Statblock.CurrentVersion,
            });

            var npc = new Character
            {
                Kind = CharacterKind.Npc, Status = CharacterStatus.Active,
                Sheet = CmJson.Write(Sheet(Unique("Смотритель"), skills, weapons: false)), SheetVersion = CharacterSheet.CurrentVersion,
            };
            db.Characters.Add(npc);
            db.ScenarioNpcs.Add(new ScenarioNpc { ScenarioId = scenario.Id, CharacterId = npc.Id, Role = NpcRole.Ally, Count = 1, Notes = "у ворот" });
            db.Characters.Add(new Character
            {
                Kind = CharacterKind.Pregen, Status = CharacterStatus.Active, ScenarioId = scenario.Id, PortraitFileId = fileId,
                Sheet = CmJson.Write(Sheet(Unique("Профессор"), skills, weapons: true)), SheetVersion = CharacterSheet.CurrentVersion,
            });
            await db.SaveChangesAsync(Cancellation);
        }

        return (scenario.Id, new RichNames(track));
    }

    /// <summary>Лист, как его ведут на деле: строка на базе (экспорт её не пишет), специализация вне справочника, свой навык.</summary>
    private static CharacterSheet Sheet(string name, Dictionary<string, Guid> skills, bool weapons) => new()
    {
        Personal = new PersonalInfo { Name = name, Occupation = "Профессор", Age = 52, Gender = "мужской", Birthplace = "Бостон" },
        Characteristics = new Characteristics { Str = 50, Con = 60, Siz = 65, Dex = 55, App = 50, Int = 85, Pow = 70, Edu = 90 },
        Current = new CurrentValues { HitPoints = 9, MagicPoints = 14, Sanity = 64, Luck = 45 },
        Overrides = new SheetOverrides { MaxHitPoints = 13 },
        Skills =
        [
            new SheetSkill { SkillId = skills["skill.spot-hidden"], Value = 65 },
            new SheetSkill { SkillId = skills["skill.science"], Value = 5 }, // на базе
            new SheetSkill { SkillId = skills[SkillCodes.Dodge], Value = 40 },
            new SheetSkill { Name = "геология", ParentSkillId = skills["skill.science"], Value = 30 },
            new SheetSkill { Name = "Хиромантия", Value = 20 },
        ],
        Weapons = weapons
            ? [new SheetWeapon { Name = "Револьвер .38", SkillId = skills["skill.firearms.handgun"], Damage = "1d10", Range = "15 ярдов", Attacks = "1 (3)", Ammo = "6", Malfunction = "100" }]
            : [],
        Spells = weapons ? [new SheetSpell { Name = "Знак Воорта", Cost = "5 ПМ", CastingTime = "1 раунд", Description = "Защитный знак" }] : [],
        Equipment = weapons ? [new EquipmentItem { Name = "Лупа", Description = "латунная" }] : [],
        Finances = weapons ? new Finances { Cash = 120.5m, Assets = "дом в Аркхеме" } : new Finances(),
        Biography = new Biography { Backstory = "Ведёт раскопки", Appearance = weapons ? "Седой, в пенсне" : "" },
    };
}
