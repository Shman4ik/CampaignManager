using System.Text.Json.Nodes;
using CampaignManager.Core;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Migrate.Catalogs;
using CampaignManager.Migrate.Steps;
using Xunit;

namespace CampaignManager.Migrate.Tests;

/// <summary>Правила переноса справочников и сценариев на данных в форме v1 (синтетических: тексты книги в репозиторий не кладём, D5).</summary>
public sealed class ConversionRulesTests
{
    [Theory]
    [InlineData("Внимание", "skill.spot-hidden")]
    [InlineData("Языки (иностр.)", "skill.language-other")]
    [InlineData("Наука судмедэксперт ", "skill.science.forensics")]
    [InlineData("Язык, иностранный (английский)", "skill.language-other.english")]
    [InlineData("Язык (английский)", "skill.language-other.english")] // язык без «иностранный», но в справочнике есть
    public void Resolver_finds_catalog_skill(string name, string code) =>
        Assert.Equal(TestCatalog.Id(code), Assert.IsType<SkillMatch.Catalog>(TestCatalog.Resolver.Resolve(name)).Skill.Id);

    [Theory]
    [InlineData("Искусство/ремесло (черчение)", "skill.art-craft", "черчение")]
    [InlineData("Язык, иностранный (латынь)", "skill.language-other", "латынь")]
    [InlineData("Язык (французский)", "skill.language-other", "французский")]
    [InlineData("Латынь", "skill.language-other", "латынь")]
    [InlineData("Ближний бой (бензопила)", "skill.fighting", "бензопила")] // навык убран вместе с современной эпохой
    public void Resolver_specialization_missing_from_catalog(string name, string parent, string specialization)
    {
        var match = Assert.IsType<SkillMatch.Specialization>(TestCatalog.Resolver.Resolve(name));
        Assert.Equal(TestCatalog.Id(parent), match.Parent.Id);
        Assert.Equal(specialization, match.Name);
    }

    [Theory]
    [InlineData("Электроника")]
    [InlineData("Гадание на кофейной гуще")]
    [InlineData("")]
    public void Resolver_homebrew_is_null(string name) => Assert.Null(TestCatalog.Resolver.Resolve(name));

    [Fact]
    public void Occupation_slots_from_v1_fields()
    {
        var occupation = JsonNode.Parse("""
            {
              "OccupationSkills": ["Стрельба", "Язык, иностранный (латынь)", "Внимание", "Средства", "Хиромантия"],
              "SkillChoices": [{"Count": 2, "Options": ["Наука", "Ближний бой (драка)", "Наука"]}],
              "SocialSkillSlots": 1,
              "FreeSkillSlots": 2
            }
            """)!;
        List<string> problems = [];

        var slots = OccupationSlots.Build(occupation, TestCatalog.Skills, TestCatalog.Resolver, problems.Add);

        Assert.Equal(
            [OccupationSlotKind.AnySpecialization, OccupationSlotKind.Specialization, OccupationSlotKind.Skill, OccupationSlotKind.Skill,
             OccupationSlotKind.Choice, OccupationSlotKind.Social, OccupationSlotKind.Free, OccupationSlotKind.Free],
            slots.Select(s => s.Kind));
        Assert.Equal(Enumerable.Range(0, slots.Count), slots.Select(s => s.Ord));
        Assert.Equal(TestCatalog.Id(SkillCodes.Firearms), slots[0].SkillId);
        Assert.Equal((TestCatalog.Id(SkillCodes.LanguageForeign), "латынь"), (slots[1].SkillId!.Value, slots[1].Specialization));
        Assert.Equal(TestCatalog.Id(SkillCodes.CreditRating), slots[3].SkillId);
        Assert.Equal(2, slots[4].ChooseCount);
        Assert.Equal([TestCatalog.Id("skill.science"), TestCatalog.Id("skill.fighting.brawl")], slots[4].Options.Select(o => o.SkillId));
        Assert.Contains(problems, p => p.Contains("Хиромантия", StringComparison.Ordinal));
    }

    [Fact]
    public void Occupation_tags_from_bitmask()
    {
        Assert.Equal(["Academic", "Investigative", "Scholarly"], OccupationSlots.Tags(1 | 128 | 16384));
        Assert.Equal(["Научная", "Расследовательская", "Книжная"], OccupationTags.Translate(OccupationSlots.Tags(1 | 128 | 16384)));
    }

    [Fact]
    public void Statblock_from_v1_columns_keeps_attack_text_and_drops_legacy()
    {
        var creature = JsonNode.Parse("""
            {
              "CreatureCharacteristics": {
                "Strength": {"Value": 80, "DiceRoll": "3d6×5"}, "Constitution": {"Value": 60, "DiceRoll": null},
                "Size": {"Value": 90}, "Dexterity": {"Value": 50}, "Intelligence": {"Value": 40}, "Power": {"Value": 55},
                "HealPoint": 15, "ManaPoint": 11, "AverageDamageBonus": "+1d4", "AverageComplexity": 1,
                "Speed": 8, "SwimSpeed": 10, "FlySpeed": null, "SpeedNote": "в воде",
                "AttacksPerRound": 2, "AttacksPerRoundNote": null, "Armor": 1, "ArmorNote": "чешуя",
                "DodgeSkill": 25, "SanityLoss": "0/1d6", "Initiative": 0, "Luck": 50, "Appearance": 10
              },
              "Attacks": [{"Name": "Когти", "SkillValue": 45, "DamageFormula": "1d6", "Kind": "Melee", "DamageBonus": "Full", "Description": "Рвёт когтями."}],
              "Skills": [{"Name": "Скрытность", "Value": 60, "Note": "в воде"}, {"Name": "Пение", "Value": 30}],
              "SpecialAbilities": {"Дыхание под водой": "Не тонет."},
              "CombatDescriptions": {"Когти": "1d6"}
            }
            """)!;

        var statblock = StatblockConverter.Convert(creature, TestCatalog.Resolver.CatalogId);

        Assert.Equal((80, "3d6×5"), (statblock.Str.Value, statblock.Str.Dice));
        Assert.Equal((15, 11, "+1d4", 1), (statblock.HitPoints, statblock.MagicPoints, statblock.DamageBonus, statblock.Build));
        Assert.Equal((8, 10, "в воде"), (statblock.Speed.Move, statblock.Speed.Swim!.Value, statblock.Speed.Note));
        var attack = Assert.Single(statblock.Attacks);
        Assert.Equal(("1d6", CreatureDamageBonusMode.Full, "Рвёт когтями."), (attack.Damage, attack.DamageBonusMode, attack.Description));
        Assert.Equal(TestCatalog.Id("skill.stealth"), statblock.Skills[0].SkillId);
        Assert.Equal("в воде", statblock.Skills[0].Note);
        Assert.Null(statblock.Skills[1].SkillId);
        Assert.Equal("Не тонет.", Assert.Single(statblock.SpecialAbilities).Text);

        var json = StatblockConverter.Canonical(statblock);
        Assert.DoesNotContain("luck", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CombatDescriptions", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Июнь 1925 года", Era.Classic)]
    [InlineData("1930-е", Era.Classic)]
    [InlineData("1931", Era.Classic)]
    [InlineData("наши дни", null)]
    [InlineData("2015", null)]
    public void Scenario_era_from_free_text(string text, Era? expected) => Assert.Equal(expected, ScenarioStep.EraOf(text));

    /// <summary>Пояс Хранителя по умолчанию — Прага, смещение берётся на саму дату (летнее время).</summary>
    [Theory]
    [InlineData("2026-04-13T19:00:00", 17)] // летнее время, +2
    [InlineData("2026-01-10T19:00:00", 18)] // зимнее, +1
    [InlineData("2026-10-25T19:00:00", 18)] // день перевода назад: вечером уже +1
    public void Scheduled_date_without_zone_is_keeper_local_time(string text, int utcHour)
    {
        var at = ScenarioStep.ScheduledAt(text, new MigrationOptions().KeeperTimeZone)!.Value;

        Assert.Equal("Europe/Prague", new MigrationOptions().KeeperTimeZone);
        Assert.Equal(TimeSpan.Zero, at.Offset);
        Assert.Equal(utcHour, at.Hour);
        Assert.Equal(DateTime.Parse(text, System.Globalization.CultureInfo.InvariantCulture).Date, at.Date);
    }

    [Fact]
    public void Scheduled_date_in_spring_gap_moves_forward() =>
        // 29 марта 2026 в Праге 02:00–03:00 не существует — 02:30 читается как 03:30 летнего (+2)
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero), ScenarioStep.ScheduledAt("2026-03-29T02:30:00", "Europe/Prague"));

    [Fact]
    public void Scheduled_date_other_zone_still_honoured() =>
        Assert.Equal(new DateTimeOffset(2026, 4, 13, 16, 0, 0, TimeSpan.Zero), ScenarioStep.ScheduledAt("2026-04-13T19:00:00", "Europe/Moscow"));

    [Theory]
    [InlineData("1920-е, наши дни", "")]
    [InlineData("1920-е", "")]
    [InlineData("(пронз.)", "(пронз.)")]
    [InlineData(null, "")]
    public void Weapon_era_caption_is_not_a_note(string? notes, string expected) => Assert.Equal(expected, CatalogStep.WeaponNotes(notes));

    [Fact]
    public void Shotgun_damage_by_range_uses_bands()
    {
        var range = WeaponStatsParser.ParseRange("10/20/50 метров");

        var byRange = CatalogStep.DamageByRange("2d6/1d6/1d3", range);

        Assert.Equal(["10 м:2d6", "20 м:1d6", "50 м:1d3"], byRange!.Select(r => $"{r.Range}:{r.Damage}"));
        Assert.Null(CatalogStep.DamageByRange("1d10+2", WeaponStatsParser.ParseRange("15 метров")));
    }

    [Theory]
    [InlineData("9000 доллара", 9000, null)]
    [InlineData("4000,00 долларов", 4000, null)]
    [InlineData("1,00 доллар", 1, null)]
    [InlineData("0,10 доллара", 0.10, null)]
    [InlineData("от 20 000,00 доллара", 20000, "Цена — от указанной суммы.")]
    [InlineData("20 000 долларов", 20000, null)]
    [InlineData("0,05–0,20 доллара", 0.05, "Верхняя граница цены — $0,20.")]
    [InlineData("15 долларов за 50 штук", 15, "Цена за 50 штук.")]
    [InlineData("12,50 доллара в неделю", 12.5, "Цена в неделю.")]
    [InlineData("0,75 доллара за ночь", 0.75, "Цена за ночь.")]
    [InlineData("от 90 долларов", 90, "Цена — от указанной суммы.")]
    public void Item_price_is_cut_out_of_description(string text, double price, string? rest)
    {
        var result = ItemPriceParser.TryParse(text);

        Assert.NotNull(result);
        Assert.Equal((decimal)price, result.Price);
        Assert.Equal(rest, result.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("машина")]
    [InlineData("Стоит 9000 долларов, не продаётся")]
    [InlineData("около 40 долларов")]
    [InlineData("9000 рублей")]
    public void Item_description_that_is_not_a_price_is_left(string? text) =>
        Assert.Null(ItemPriceParser.TryParse(text));

    [Theory]
    [InlineData("20 метров", "20 м")]
    [InlineData("3 метра", "3 м")]
    [InlineData("10/20/50 метров", "10/20/50 м")]
    [InlineData("СИЛ / 5 метров", "СИЛ / 5 м")]
    [InlineData("100 м", "100 м")]
    [InlineData("СИЛ/5м", "СИЛ/5м")]
    [InlineData("Касание", "Касание")]
    public void Weapon_range_meters_are_written_short_and_still_parse_the_same(string raw, string expected)
    {
        var text = TextRules.Meters(raw);

        Assert.Equal(expected, text);
        var before = WeaponStatsParser.ParseRange(raw);
        var after = WeaponStatsParser.ParseRange(text);
        Assert.Equal((before.Kind, before.BaseMeters, before.ThrowDivisor, before.IsParsed), (after.Kind, after.BaseMeters, after.ThrowDivisor, after.IsParsed));
        Assert.Equal(before.Bands ?? [], after.Bands ?? []);
    }

    [Fact]
    public void Greater_or_equal_sign_is_typographic_and_cost_still_parses()
    {
        Assert.Equal("≥1 магии с человека, 1d10 рассудка", TextRules.GreaterOrEqual(">=1 магии с человека, 1d10 рассудка"));
        var before = SpellStatsReader.ParseCost(">=1 магии с человека, 1d10 рассудка");
        var after = SpellStatsReader.ParseCost("≥1 магии с человека, 1d10 рассудка");
        Assert.Equal(before, after);
    }

    [Fact]
    public void Owner_decisions_are_consistent_with_code_tables()
    {
        // Выброшенное и перенесённое в сценарий — не книжные записи таблицы; их имена кода не дают
        Assert.Equal(53, OwnerDecisions.ModernItems.Count);
        Assert.All(OwnerDecisions.ModernItems.Concat(OwnerDecisions.ScenarioProps).Concat(OwnerDecisions.HotelDuplicates.Keys),
            name => Assert.Null(ItemCodes.FromName(name)));
        // Повторы указывают на книжные записи, которые остаются
        Assert.All(OwnerDecisions.HotelDuplicates.Values, name => Assert.NotNull(ItemCodes.FromName(name)));
    }

    [Theory]
    [InlineData("Атака/Проклятие", "Атака", "Проклятие")]
    [InlineData("Создание предмета/Путешествие", "Создание предмета", "Путешествие")]
    [InlineData("Ментальное/Контроль/Иллюзия", "Ментальное", "Контроль, Иллюзия")]
    [InlineData("Связь с божеством", "Связь", "с божеством")]
    [InlineData("Защита", "Защита", null)]
    [InlineData("", "", null)]
    public void Spell_type_is_the_first_of_the_compound_rest_goes_to_also(string type, string expected, string? also) =>
        Assert.Equal((expected, also), SpellRules.SplitType(type));

    [Theory]
    [InlineData("10 магии, 1d4 рассудка", "10 ПМ, 1d4 рассудка")]
    [InlineData("1 магия за камень", "1 ПМ за камень")]
    [InlineData("Магия и рассудок варьируют", "ПМ и рассудок варьируют")]
    [InlineData("3 магии за 10 мин. речи, 1d6 рассудка", "3 ПМ за 10 мин. речи, 1d6 рассудка")]
    [InlineData("5 МОЩ, 1d6 рассудка", "5 МОЩ, 1d6 рассудка")]
    [InlineData("l рассудка, 4 ПМ", "1 рассудка, 4 ПМ")]
    [InlineData("1 ПМ", "1 ПМ")]
    public void Spell_cost_uses_one_unit(string cost, string expected) => Assert.Equal(expected, SpellRules.Cost(cost));

    [Fact]
    public void Spell_also_line_goes_to_the_end_of_the_description()
    {
        Assert.Equal("текст\n\nТакже: Проклятие.", SpellRules.AppendAlso("текст\n", "Проклятие"));
        Assert.Equal("Также: Проклятие.", SpellRules.AppendAlso("", "Проклятие"));
        Assert.Equal("текст", SpellRules.AppendAlso("текст", null));
    }
}
