using System.Text.Json;
using System.Text.Json.Nodes;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.Core.Encounters;

namespace CampaignManager.Core.Tests.Documents;

/// <summary>Документы 2.0: формат JSON, неизвестные поля, версии (SCHEMA, правило 6 и «Документы»).</summary>
public sealed class DocumentTests
{
    [Fact]
    public void Sheet_CamelCase_EnumsAsNames_GeneratedColumnPaths()
    {
        var sheet = new CharacterSheet
        {
            Personal = new PersonalInfo { Name = "Харви Уолтерс", Occupation = "Журналист" },
            Condition = new SheetCondition { LastBout = new InsanityBout { Mode = InsanityBoutMode.Summary, Roll = 3 } },
            MythosBooks = [new MythosBookRecord { Name = "Книга", Stage = MythosBookStage.FullStudy }],
        };

        var json = JsonNode.Parse(CmJson.Write(sheet).RootElement.GetRawText())!;

        // characters.name / occupation — generated-колонки по этим путям
        Assert.Equal("Харви Уолтерс", (string?)json["personal"]!["name"]);
        Assert.Equal("Журналист", (string?)json["personal"]!["occupation"]);
        Assert.Equal("Summary", (string?)json["condition"]!["lastBout"]!["mode"]);
        Assert.Equal("FullStudy", (string?)json["mythosBooks"]![0]!["stage"]);
    }

    [Fact]
    public void Sheet_ComputedPropertiesAreNotStored()
    {
        var sheet = new CharacterSheet();
        sheet.Condition.Habituations.Add(new MythosHabituation { CreatureName = "Гуль", MaxLoss = 6, LostSanity = 6 });

        var habituation = CmJson.Write(sheet).RootElement.GetProperty("condition").GetProperty("habituations")[0];

        Assert.False(habituation.TryGetProperty("isHabituated", out _));
        Assert.False(habituation.TryGetProperty("remaining", out _));
        Assert.False(CmJson.Write(sheet).RootElement.TryGetProperty("extra", out _));
    }

    /// <summary>Поля новой версии приложения переживают чтение и запись старой — на корне и внутри.</summary>
    [Fact]
    public void Sheet_UnknownFieldsSurviveRoundTrip()
    {
        const string json = """
            {
              "personal": { "name": "Ада", "pronouns": "она" },
              "skills": [ { "skillId": "0199f2a0-0000-7000-8000-000000000001", "value": 40, "checked": true, "tags": ["любимый"] } ],
              "futureBlock": { "a": 1 }
            }
            """;

        using var document = JsonDocument.Parse(json);
        var sheet = CmJson.ReadSheet(document, CharacterSheet.CurrentVersion);
        sheet.Skills[0].Value = 45;

        var written = JsonNode.Parse(CmJson.Write(sheet).RootElement.GetRawText())!;

        Assert.Equal("она", (string?)written["personal"]!["pronouns"]);
        Assert.Equal("любимый", (string?)written["skills"]![0]!["tags"]![0]);
        Assert.Equal(45, (int?)written["skills"]![0]!["value"]);
        Assert.Equal(1, (int?)written["futureBlock"]!["a"]);
    }

    [Fact]
    public void Sheet_FullRoundTrip_KeepsEverything()
    {
        var sheet = new CharacterSheet
        {
            Personal = new PersonalInfo { Name = "Ада", Age = 31, OccupationId = Guid.CreateVersion7() },
            Characteristics = new Characteristics { Str = 50, Edu = 80 },
            Current = new CurrentValues { HitPoints = 11, Sanity = 60, Luck = 45 },
            Overrides = new SheetOverrides { MaxHitPoints = 14, DamageBonus = "+1D4" },
            Skills = [new SheetSkill { ParentSkillId = Guid.CreateVersion7(), Name = "латынь", Value = 20 }],
            Weapons = [new SheetWeapon { Name = "Револьвер .38", Damage = "1d10", Malfunction = "100" }],
            Spells = [new SheetSpell { Name = "Призыв", AlternativeNames = ["Вызов"] }],
            Equipment = [new EquipmentItem { Name = "Фонарь" }],
            Finances = new Finances { Cash = 1540.5m, PocketMoney = 10, Assets = "дом в Аркхеме" },
            Biography = new Biography { KeyConnection = "Сестра" },
            InsanityConditions = [new InsanityCondition { Kind = InsanityConditionKind.Mania, Name = "Клептомания" }],
            FellowInvestigators = [new FellowInvestigator { CharacterId = Guid.CreateVersion7(), Note = "должен денег" }],
        };

        using var written = CmJson.Write(sheet);
        var read = CmJson.ReadSheet(written, CharacterSheet.CurrentVersion);

        Assert.Equal(written.RootElement.GetRawText(), CmJson.Write(read).RootElement.GetRawText());
        Assert.Equal(1540.5m, read.Finances.Cash);
        Assert.Equal(InsanityConditionKind.Mania, read.InsanityConditions[0].Kind);
    }

    [Fact]
    public void Statblock_AndEncounterState_RoundTrip()
    {
        var statblock = new Statblock
        {
            Str = new StatValue { Value = 80, Dice = "3D6×5" },
            SanityLoss = "0/1d6",
            Attacks = [new CreatureAttack { Name = "Когти", Damage = "1d6", Kind = CreatureAttackKind.Melee, DamageBonusMode = CreatureDamageBonusMode.Full }],
            SpecialAbilities = [new SpecialAbility { Name = "Регенерация", Text = "…" }],
        };
        var state = new EncounterState
        {
            Participants = [new EncounterParticipant { Name = "Гуль", Side = EncounterSide.Enemies }],
        };

        using var statblockJson = CmJson.Write(statblock);
        using var stateJson = CmJson.Write(state);

        Assert.Equal("Full", statblockJson.RootElement.GetProperty("attacks")[0].GetProperty("damageBonusMode").GetString());
        Assert.Equal(80, CmJson.ReadStatblock(statblockJson, Statblock.CurrentVersion).Str.Value);
        Assert.Equal("Enemies", stateJson.RootElement.GetProperty("participants")[0].GetProperty("side").GetString());
        Assert.Equal("Гуль", CmJson.ReadEncounterState(stateJson, EncounterState.CurrentVersion).Participants[0].Name);
    }

    [Theory]
    [InlineData(DocumentKind.CharacterSheet)]
    [InlineData(DocumentKind.Statblock)]
    [InlineData(DocumentKind.EncounterState)]
    public void Upgrader_HasStepForEveryVersionBelowCurrent(DocumentKind kind)
    {
        for (var version = DocumentUpgrader.FirstVersion; version < DocumentUpgrader.CurrentVersion(kind); version++)
            Assert.True(DocumentUpgrader.Steps.ContainsKey((kind, version)), $"{kind}: нет шага {version} → {version + 1}");
    }

    /// <summary>Листы v1 переводит перенос (T1.3), а не апкастер: версия 0 — ошибка, а не тихое чтение.</summary>
    [Fact]
    public void Upgrader_RejectsV1Documents()
    {
        using var document = JsonDocument.Parse("""{ "PersonalInfo": { "Name": "v1" } }""");

        Assert.Throws<NotSupportedException>(() => CmJson.ReadSheet(document, 0));
    }

    /// <summary>Документ новее приложения читается как есть: незнакомое уходит в Extra и возвращается при записи.</summary>
    [Fact]
    public void Upgrader_NewerVersion_ReadAsIs()
    {
        using var document = JsonDocument.Parse("""{ "personal": { "name": "Ада" }, "v2only": true }""");

        var sheet = CmJson.ReadSheet(document, CharacterSheet.CurrentVersion + 1);

        Assert.Equal("Ада", sheet.Personal.Name);
        Assert.True(CmJson.Write(sheet).RootElement.GetProperty("v2only").GetBoolean());
    }

    [Fact]
    public void Draft_RoundTrip_EnumKeysAsNames()
    {
        var draft = new InvestigatorDraft
        {
            Era = Era.Modern,
            Rolled = { [Characteristic.STR] = 60 },
            EducationChecks = [new EducationCheck(71, 70, 4)],
            FormulaChoice = Characteristic.DEX,
            OccupationPoints = { ["Внимание"] = 10 },
        };

        var json = CmJson.Serialize(draft);
        var read = CmJson.DeserializeDraft(json)!;

        Assert.Contains("\"STR\":60", json, StringComparison.Ordinal);
        Assert.Contains("\"era\":\"Modern\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"text\"", json, StringComparison.Ordinal); // вычисляемое не хранится
        Assert.Equal(60, read.Rolled[Characteristic.STR]);
        Assert.Equal(4, read.EducationChecks[0].Gain);
        Assert.Equal(Characteristic.DEX, read.FormulaChoice);
    }

    /// <summary>Коды, на которые опираются правила, — транслит книжных имён той же функцией, что у переноса.</summary>
    [Fact]
    public void SkillCodes_AreTransliteratedBookNames()
    {
        foreach (var (code, name) in SkillCodes.BookNames)
            Assert.Equal(code, CatalogCode.Skill(name));

        Assert.Equal("skill.strelba-pistolet", CatalogCode.Skill("Стрельба (пистолет)")); // пример из SCHEMA
        Assert.Equal("skill.iskusstvo-remeslo-akterskaya-igra", CatalogCode.Skill("Искусство/ремесло (актёрская игра)"));
        Assert.Equal("skill.shchit-1920", CatalogCode.Skill("  Щит, 1920 "));
    }
}
