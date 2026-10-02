using System.Text.Json;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.Migrate.Sheets;
using Xunit;

namespace CampaignManager.Migrate.Tests;

/// <summary>
/// Перевод листа v1 → <see cref="CharacterSheet"/> на настоящих листах v1 (Fixtures/Sheets, обезличены: без имени
/// игрока и текстов биографии). Каталог оружия и заклинаний пуст — ссылки на них проверяет прогон переноса.
/// </summary>
public sealed class SheetConverterTests
{
    private static readonly Guid Handgun = Guid.CreateVersion7();

    private static SheetConverter Converter(bool withRevolver = false) => new(
        TestCatalog.Skills,
        TestCatalog.Resolver,
        withRevolver
            ? new Dictionary<Guid, (string, Guid)> { [Guid.Parse("c63e5443-f9e7-4a0a-a1d1-4492b55e4643")] = ("Револьвер 38-го калибра (9 мм)", Handgun) }
            : [],
        new Dictionary<Guid, string>(),
        new Dictionary<string, Guid>(StringComparer.Ordinal) { ["журналист"] = Guid.Parse("00000000-0000-0000-0000-00000000000a") });

    private static (CharacterSheet Sheet, SheetNotes Notes) Convert(string fixture, bool withRevolver = false)
    {
        var row = TestCatalog.Sheet(fixture);
        return Converter(withRevolver).Convert(row["Character"]!, row["Kind"]!.GetValue<string>() == "Npc");
    }

    private static SheetSkill Skill(CharacterSheet sheet, string code) =>
        sheet.Skills.Single(s => s.SkillId == TestCatalog.Id(code));

    [Theory]
    [InlineData("player-milie-mare")]
    [InlineData("npc-rene-pierce")]
    [InlineData("pregen-helen-wright")]
    [InlineData("npc-henry-pierce")]
    public void Document_has_only_what_a_person_enters(string fixture)
    {
        var (sheet, _) = Convert(fixture);

        var json = CmJson.Write(sheet).RootElement.GetRawText();
        foreach (var legacy in new[] { "\"Id\"", "\"half\"", "\"fifth\"", "CharacterType", "NewSkillName", "baseValue", "maxValue", "playerName\":\"А" })
        {
            Assert.DoesNotContain(legacy, json, StringComparison.OrdinalIgnoreCase);
        }

        // Документ читается типами Core той же версии, без «лишних» полей в Extra
        var read = CmJson.ReadSheet(CmJson.Write(sheet), CharacterSheet.CurrentVersion);
        Assert.Null(read.Extra);
        Assert.Equal(sheet.Skills.Count, read.Skills.Count);
        Assert.All(read.Skills, s => Assert.True(s.SkillId is not null || s.Name is not null));
    }

    [Fact]
    public void Player_sheet_skills_by_old_spellings_and_language_specialization()
    {
        var (sheet, notes) = Convert("player-milie-mare");

        Assert.Equal("Врач-патологоанатом", sheet.Personal.Occupation);
        Assert.Equal(75, Skill(sheet, SkillCodes.LanguageOwn).Value); // «Языки (родной)»
        Assert.Equal(20, Skill(sheet, "skill.science.pharmacy").Value); // «Наука фармакология »
        Assert.Equal(32, Skill(sheet, SkillCodes.Dodge).Value);

        var latin = sheet.Skills.Single(s => s.Name == "латынь");
        Assert.Equal(TestCatalog.Id(SkillCodes.LanguageForeign), latin.ParentSkillId);
        Assert.Null(latin.SkillId);
        Assert.Equal(50, latin.Value);
        Assert.Contains(notes.UnmatchedSkills, line => line.Contains("Латынь", StringComparison.Ordinal));

        // Состояние и текущие значения переезжают, максимумы — нет: у сыщика побеждает формула
        Assert.True(sheet.Condition.TemporaryInsanity);
        Assert.NotNull(sheet.Condition.TemporaryInsanityStartedAt);
        Assert.Equal(new CurrentValues { HitPoints = 10, MagicPoints = 10, Sanity = 44, Luck = 1 }, sheet.Current with { Extra = null });
        Assert.Equal(new SheetOverrides(), sheet.Overrides);
    }

    /// <summary>
    /// Точечная правка тестового листа с нулями (решение владельца): текущие = максимумы v1, но не выше формулы;
    /// незаполненный максимум v1 — формула.
    /// </summary>
    [Fact]
    public void Test_sheet_with_zeros_gets_current_values_from_maxima()
    {
        var row = TestCatalog.Sheet("player-milie-mare");
        var document = row["Character"]!;
        var derivedV1 = document["DerivedAttributes"]!;
        derivedV1["HitPoints"] = new System.Text.Json.Nodes.JsonObject { ["Value"] = 0, ["MaxValue"] = 999 };
        derivedV1["MagicPoints"] = new System.Text.Json.Nodes.JsonObject { ["Value"] = 0, ["MaxValue"] = 0 };
        derivedV1["Sanity"] = new System.Text.Json.Nodes.JsonObject { ["Value"] = 0, ["MaxValue"] = 40 };
        derivedV1["Luck"] = new System.Text.Json.Nodes.JsonObject { ["Value"] = 0, ["MaxValue"] = 70 };
        var (sheet, _) = Converter().Convert(document, isNpc: false);
        var formula = DerivedAttributeRules.Compute(sheet, TestCatalog.Skills);

        var current = Steps.CharacterStep.FillCurrentToMax(document, sheet, TestCatalog.Skills);

        Assert.Equal(formula.MaxHitPoints, current.HitPoints);   // 999 в v1 — не выше формулы
        Assert.Equal(formula.MaxMagicPoints, current.MagicPoints); // не заполнен в v1 — формула
        Assert.Equal(40, current.Sanity);                          // стартовый Рассудок v1, а не потолок 99 − Мифы
        Assert.Equal(70, current.Luck);
        Assert.Same(sheet.Current, current);
    }

    [Fact]
    public void Npc_from_book_keeps_printed_values_in_overrides_and_loses_import_checkmarks()
    {
        var (sheet, notes) = Convert("npc-rene-pierce");

        // ТЕЛ 15 + ВЫН 25 → формула даёт 4 ПЗ, БкУ −2 и Комплекцию −2; в книге 7, −1, −1
        Assert.Equal(7, sheet.Overrides.MaxHitPoints);
        Assert.Equal("-1", sheet.Overrides.DamageBonus);
        Assert.Equal(-1, sheet.Overrides.Build);
        Assert.Equal(8, sheet.Overrides.Move);
        // 85/85 — это текущий Рассудок из книги, а не максимум
        Assert.Null(sheet.Overrides.MaxSanity);
        Assert.Equal(7, DerivedAttributeRules.Compute(sheet, TestCatalog.Skills).MaxHitPoints);

        Assert.All(sheet.Skills, s => Assert.False(s.Checked));
        Assert.Equal(6, notes.CheckedDropped);
        Assert.Equal(TestCatalog.Id("skill.fighting.brawl"), Assert.Single(sheet.Weapons).SkillId); // «Ближний бой» у «Драки»
    }

    [Fact]
    public void Npc_languages_without_parent_become_specializations()
    {
        var (sheet, _) = Convert("npc-henry-pierce", withRevolver: true);

        var foreign = TestCatalog.Id(SkillCodes.LanguageForeign);
        Assert.Equal(40, sheet.Skills.Single(s => s.ParentSkillId == foreign && s.Name == "латынь").Value);
        Assert.Equal(60, sheet.Skills.Single(s => s.ParentSkillId == foreign && s.Name == "древнескандинавский").Value);
        Assert.Equal(80, Skill(sheet, "skill.science.pharmacy").Value); // «Наука (фармацевтика)»

        // Сыщик книги: 35/35 — текущий Рассудок; Скорость 7 при формуле 4 (68 лет) — значение книги
        Assert.Null(sheet.Overrides.MaxSanity);
        Assert.Equal(7, sheet.Overrides.Move);

        var revolver = sheet.Weapons.Single(w => w.Name == "Револьвер .38 кал.");
        Assert.Equal(Guid.Parse("c63e5443-f9e7-4a0a-a1d1-4492b55e4643"), revolver.CatalogWeaponId);
        Assert.Equal(TestCatalog.Id("skill.firearms.handgun"), revolver.SkillId);
        Assert.Equal("Вытягивание жизни", Assert.Single(sheet.Spells).Name);
    }

    [Fact]
    public void Pregen_modern_skills_dropped_at_base_and_kept_as_specialization_above_it()
    {
        var (sheet, notes) = Convert("pregen-helen-wright");

        Assert.Contains("Работа с компьютером", notes.DroppedSkills);
        Assert.Contains("Электроника", notes.DroppedSkills);
        Assert.DoesNotContain(sheet.Skills, s => s.Name is "Работа с компьютером" or "Электроника");

        // «Ближний бой (бензопила)» 14 при базе 10 — вложены пункты: остаётся своей специализацией ближнего боя
        var chainsaw = sheet.Skills.Single(s => s.Name == "бензопила");
        Assert.Equal(TestCatalog.Id(SkillCodes.Fighting), chainsaw.ParentSkillId);
        Assert.Equal(14, chainsaw.Value);

        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-00000000000a"), sheet.Personal.OccupationId);
        Assert.Equal(30m, sheet.Finances.Cash);
        Assert.Equal(5m, sheet.Finances.PocketMoney);
        Assert.Equal("$750", sheet.Finances.Assets);

        var derringer = Assert.Single(sheet.Weapons);
        Assert.Equal(TestCatalog.Id("skill.firearms.handgun"), derringer.SkillId); // «Стрельба (П)»
        Assert.Null(derringer.CatalogWeaponId); // в каталоге его нет — текст книги остаётся
        Assert.Equal("1d6", derringer.Damage);
        Assert.True(derringer.Impaling);
        Assert.Equal(Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"), derringer.RowId);
    }

    [Theory]
    [InlineData("$120", "120")]
    [InlineData("80 долларов", "80")]
    [InlineData("110", "110")]
    [InlineData("$0,50", "0.50")]
    [InlineData("1.234.56", null)]
    [InlineData("", null)]
    [InlineData("нет", null)]
    public void Money_from_v1_text(string text, string? expected) =>
        Assert.Equal(expected is null ? null : decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), SheetConverter.TryParseMoney(text));

    [Fact]
    public void Unparsed_money_goes_to_note_and_assets_join()
    {
        var finances = SheetConverter.Finances(JsonDocument.Parse("""
            {"Cash": "много", "PocketMoney": "$10", "Assets": ["500 долларов на счету", "дом"]}
            """).RootElement.Deserialize<System.Text.Json.Nodes.JsonObject>());

        Assert.Null(finances.Cash);
        Assert.Equal(10m, finances.PocketMoney);
        Assert.Equal("500 долларов на счету; дом", finances.Assets);
        Assert.Equal("Наличные: много", finances.Note);
    }
}
