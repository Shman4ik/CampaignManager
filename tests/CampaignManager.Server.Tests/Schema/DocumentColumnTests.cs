using System.Text.Json;
using System.Text.Json.Nodes;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.Data.Catalogs;
using CampaignManager.Data.Characters;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampaignManager.Server.Tests.Schema;

/// <summary>
/// Документы Core в jsonb-колонках: проходят через Postgres без потерь (jsonb переставляет ключи — сравнение
/// идёт по содержимому), неизвестные поля доживают до следующей записи, generated-колонки листа читают
/// пути <c>personal.name</c>/<c>personal.occupation</c> документа.
/// </summary>
public sealed class DocumentColumnTests(SchemaDatabase db) : IClassFixture<SchemaDatabase>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sheet_round_trips_through_jsonb_and_feeds_generated_columns()
    {
        TestDatabase.SkipIfMissing();
        var sheet = new CharacterSheet
        {
            Personal = new PersonalInfo { Name = "Харви Уолтерс", Occupation = "Журналист", Age = 42 },
            Characteristics = new Characteristics { Str = 50, Con = 60, Siz = 65, Dex = 55, App = 40, Int = 80, Pow = 70, Edu = 85 },
            Current = new CurrentValues { HitPoints = 12, MagicPoints = 14, Sanity = 62, Luck = 45 },
            Skills = [new SheetSkill { SkillId = Guid.CreateVersion7(), Value = 60, Checked = true }],
            Weapons = [new SheetWeapon { Name = "Револьвер .38", Damage = "1d10", Range = "15 метров", Malfunction = "100" }],
            Finances = new Finances { Cash = 1540.5m, PocketMoney = 10, Assets = "дом в Аркхеме" },
            Condition = new SheetCondition { LastBout = new InsanityBout { Mode = InsanityBoutMode.RealTime, Roll = 4 } },
        };
        var row = new Character { Kind = Core.Characters.CharacterKind.Npc, Sheet = CmJson.Write(sheet), SheetVersion = CharacterSheet.CurrentVersion };

        await using (var write = db.CreateContext())
        {
            write.Characters.Add(row);
            await write.SaveChangesAsync(Cancellation);
        }

        await using var read = db.CreateContext();
        var stored = await read.Characters.AsNoTracking().SingleAsync(c => c.Id == row.Id, Cancellation);
        var back = CmJson.ReadSheet(stored.Sheet, stored.SheetVersion);

        Assert.Equal("Харви Уолтерс", stored.Name);
        Assert.Equal("Журналист", stored.Occupation);
        Assert.Equal(CmJson.Write(sheet).RootElement.GetRawText(), CmJson.Write(back).RootElement.GetRawText());
    }

    /// <summary>Поле, которого эта версия не знает, переживает чтение, правку и запись через базу.</summary>
    [Fact]
    public async Task Unknown_sheet_fields_survive_edit_through_the_database()
    {
        TestDatabase.SkipIfMissing();
        using var newer = JsonDocument.Parse("""{ "personal": { "name": "Ада", "pronouns": "она" }, "futureBlock": [1, 2] }""");
        var row = new Character { Kind = Core.Characters.CharacterKind.Npc, Sheet = newer, SheetVersion = CharacterSheet.CurrentVersion };
        await using (var write = db.CreateContext())
        {
            write.Characters.Add(row);
            await write.SaveChangesAsync(Cancellation);
        }

        await using (var edit = db.CreateContext())
        {
            var stored = await edit.Characters.SingleAsync(c => c.Id == row.Id, Cancellation);
            var sheet = CmJson.ReadSheet(stored.Sheet, stored.SheetVersion);
            sheet.Personal.Name = "Ада Лавлейс";
            stored.Sheet = CmJson.Write(sheet);
            await edit.SaveChangesAsync(Cancellation);
        }

        await using var read = db.CreateContext();
        var json = JsonNode.Parse((await read.Characters.AsNoTracking().SingleAsync(c => c.Id == row.Id, Cancellation)).Sheet.RootElement.GetRawText())!;

        Assert.Equal("Ада Лавлейс", (string?)json["personal"]!["name"]);
        Assert.Equal("она", (string?)json["personal"]!["pronouns"]);
        Assert.Equal(2, json["futureBlock"]!.AsArray().Count);
    }

    [Fact]
    public async Task Statblock_round_trips_through_jsonb()
    {
        TestDatabase.SkipIfMissing();
        var statblock = new Statblock
        {
            Str = new StatValue { Value = 80, Dice = "3D6×5" },
            HitPoints = 13,
            SanityLoss = "0/1d6",
            Attacks = [new CreatureAttack { Name = "Когти", Damage = "1d6", DamageBonusMode = CreatureDamageBonusMode.Full }],
        };
        var creature = new Creature { Name = $"Гуль {Guid.NewGuid():N}", Type = CreatureType.Monsters, Statblock = CmJson.Write(statblock), StatblockVersion = Statblock.CurrentVersion };

        await using (var write = db.CreateContext())
        {
            write.Creatures.Add(creature);
            await write.SaveChangesAsync(Cancellation);
        }

        await using var read = db.CreateContext();
        var stored = await read.Creatures.AsNoTracking().SingleAsync(c => c.Id == creature.Id, Cancellation);
        var back = CmJson.ReadStatblock(stored.Statblock, stored.StatblockVersion);

        Assert.Equal(CmJson.Write(statblock).RootElement.GetRawText(), CmJson.Write(back).RootElement.GetRawText());
    }
}
