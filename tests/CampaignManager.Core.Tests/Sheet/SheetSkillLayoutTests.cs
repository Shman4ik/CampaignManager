using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>
/// Раскладка навыков листа (T2.3): что видно на бланке, что свёрнуто, и правки строк. Ошибка v1 «навык из
/// справочника получал 0» (AUDIT, «Персонажи и НПС → Ошибки», 4) — здесь.
/// </summary>
public sealed class SheetSkillLayoutTests
{
    private static IEnumerable<SkillLine> AllLines(CharacterSheet sheet) =>
        SheetSkillLayout.Groups(sheet, Catalog).SelectMany(g => g.Lines.Concat(g.Folds.SelectMany(f => f.Lines)));

    private static SkillLine Line(CharacterSheet sheet, string name) => AllLines(sheet).Single(l => l.Name == name);

    [Fact]
    public void Base_skills_are_on_the_sheet_even_without_a_row_and_parents_are_not()
    {
        var sheet = NewSheet(50, Skill("Внимание", 60));

        var visible = SheetSkillLayout.Groups(sheet, Catalog).SelectMany(g => g.Lines).ToList();

        Assert.Contains(visible, l => l.Name == "Внимание" && l.Value == 60 && l.Entry is not null);
        // Слух на листе не заведён — стоит на базе, как пустая графа бланка
        Assert.Contains(visible, l => l.Name == "Слух" && l.Value == 20 && l.Entry is null);
        // Уклонение — база формулой (½ ЛВК 45 → 22)
        Assert.Contains(visible, l => l.Name == Dodge && l.Value == 22);
        Assert.DoesNotContain(AllLines(sheet), l => l.Name is Firearms or Fighting or Science);
    }

    [Fact]
    public void Specializations_at_base_are_folded_under_parent_and_raised_ones_are_visible()
    {
        var sheet = NewSheet(50, Skill("Стрельба (пистолет)", 45), Skill("Стрельба (винтовка)", 25));

        var groups = SheetSkillLayout.Groups(sheet, Catalog);
        var visible = groups.SelectMany(g => g.Lines).Select(l => l.Name).ToList();
        var folds = groups.SelectMany(g => g.Folds).ToList();

        Assert.Contains("Стрельба (пистолет)", visible);
        Assert.DoesNotContain("Стрельба (винтовка)", visible); // на базе — свёрнута
        var firearms = folds.Single(f => f.ParentName == Firearms);
        Assert.Equal(["Стрельба (винтовка)", "Стрельба (дробовик)"], firearms.Lines.Select(l => l.Name));
    }

    [Fact]
    public void Own_skills_and_own_specializations_are_lines_too()
    {
        var sheet = NewSheet(50, Specialization(ForeignLanguage, "латынь", 50), new SheetSkill { Name = "Гадание на картах", Value = 30 });

        var lines = SheetSkillLayout.Groups(sheet, Catalog).SelectMany(g => g.Lines).ToList();

        var latin = lines.Single(l => l.Name == "Язык, иностранный (латынь)" && l.IsOwn);
        Assert.Equal(50, latin.Value);
        var own = Assert.Single(SheetSkillLayout.Groups(sheet, Catalog), g => g.Title == SheetSkillLayout.OwnSkillsTitle).Lines.Single();
        Assert.Equal("Гадание на картах", own.Name);
    }

    [Fact]
    public void Setting_value_of_a_base_line_creates_the_row()
    {
        var sheet = NewSheet();
        var hearing = Line(sheet, "Слух");

        SheetSkillLayout.SetValue(sheet, Catalog, hearing, 35);

        Assert.Equal(35, sheet.Entry(Id("Слух"))!.Value);
        Assert.Equal(35, Line(sheet, "Слух").Value);
    }

    [Fact]
    public void Adding_catalog_skill_takes_base_value_not_zero()
    {
        var sheet = NewSheet();

        var added = SheetSkillLayout.AddFromCatalog(sheet, Catalog, Id("Лазание"));
        var language = SheetSkillLayout.AddFromCatalog(sheet, Catalog, Id(OwnLanguage));

        Assert.Equal(20, added!.Value);
        Assert.Equal(75, language!.Value); // база = ОБР (стр. 77)
        Assert.Null(SheetSkillLayout.AddFromCatalog(sheet, Catalog, Id(Firearms))); // родителя на лист не кладут
        Assert.Same(added, SheetSkillLayout.AddFromCatalog(sheet, Catalog, Id("Лазание")));
    }

    [Fact]
    public void Own_specialization_gets_sibling_base_and_catalog_name_is_reused()
    {
        var sheet = NewSheet();

        var japanese = SheetSkillLayout.AddSpecialization(sheet, Catalog, Id(ForeignLanguage), " японский ");
        var latin = SheetSkillLayout.AddSpecialization(sheet, Catalog, Id(ForeignLanguage), "латынь");

        Assert.Equal("японский", japanese!.Name);
        Assert.Equal(1, japanese.Value);
        Assert.Equal(Id(Latin), latin!.SkillId); // такая есть в справочнике — строка справочника
        Assert.Same(japanese, SheetSkillLayout.AddSpecialization(sheet, Catalog, Id(ForeignLanguage), "Японский"));
    }

    [Fact]
    public void Mythos_and_credit_rating_cannot_be_checked()
    {
        var sheet = NewSheet();

        var mythos = Line(sheet, Mythos);
        SheetSkillLayout.SetChecked(sheet, mythos, true);
        SheetSkillLayout.SetChecked(sheet, Line(sheet, "Внимание"), true);

        Assert.False(mythos.CanBeChecked);
        Assert.Null(sheet.Entry(Id(Mythos)));
        Assert.True(sheet.Entry(Id("Внимание"))!.Checked);
    }

    [Fact]
    public void Removing_catalog_line_returns_it_to_base_and_own_line_disappears()
    {
        var sheet = NewSheet(50, Skill("Внимание", 60), new SheetSkill { Name = "Гадание", Value = 30 });

        SheetSkillLayout.Remove(sheet, Line(sheet, "Внимание"));
        SheetSkillLayout.Remove(sheet, Line(sheet, "Гадание"));

        Assert.Empty(sheet.Skills);
        Assert.Equal(25, Line(sheet, "Внимание").Value);
        Assert.DoesNotContain(AllLines(sheet), l => l.Name == "Гадание");
    }

    [Fact]
    public void Specialization_bonus_is_shown_on_the_line()
    {
        var sheet = NewSheet(50, Skill("Стрельба (пистолет)", 50), Skill("Стрельба (винтовка)", 30));

        Assert.Equal(10, Line(sheet, "Стрельба (винтовка)").SpecializationBonus);
        Assert.Equal(0, Line(sheet, "Стрельба (пистолет)").SpecializationBonus);
    }

    [Fact]
    public void Weapon_copy_reads_edited_text_and_spell_copy_is_independent()
    {
        // Ошибки v1 1 и 2: правка листа не трогает справочник, а бой читает правленый урон.
        var catalogSpell = new SpellData(Guid.NewGuid(), "Призыв") { AlternativeNames = ["Зов"], Cost = "5 ПМ" };
        var spell = SheetCopies.Spell(catalogSpell);
        spell.Cost = "10 ПМ";
        spell.AlternativeNames.Add("Другое");

        Assert.Equal("5 ПМ", catalogSpell.Cost);
        Assert.Equal(["Зов"], catalogSpell.AlternativeNames);

        var weapon = SheetCopies.Weapon(new WeaponData(Guid.NewGuid(), "Револьвер") { Damage = "1d8", Ammo = "6" });
        weapon.Damage = "1d10+2";

        var damage = WeaponStatsReader.Damage(weapon).GetDefaultDamage()!;
        Assert.Equal(10, damage.Dice.Single().Sides);
        Assert.Equal(2, damage.FlatModifier);
    }

    [Fact]
    public void Frequent_block_follows_the_owner_order_by_code_and_missing_skills_come_from_the_catalog()
    {
        var sheet = NewSheet(50, Skill("Внимание", 60));

        var frequent = SheetSkillLayout.Sections(sheet, Catalog).Frequent;

        // В тестовом справочнике есть не все тринадцать — порядок тот, что в FrequentCodes
        Assert.Equal(
            ["Внимание", "Слух", "Психология", "Уклонение", "Ближний бой (драка)", "Стрельба (пистолет)", "Убеждение", "Обаяние", "Красноречие", "Запугивание"],
            frequent.Select(l => l.Name));
        Assert.Equal(60, frequent[0].Value);
        // Слуха на листе нет — база справочника, строки документа тоже нет
        Assert.Null(frequent[1].Entry);
        Assert.Equal(20, frequent[1].Value);
        Assert.Equal(13, SheetSkillLayout.FrequentCodes.Count);
    }

    [Fact]
    public void Frequent_specializations_are_not_folded_and_do_not_repeat_in_the_rest()
    {
        var sheet = NewSheet(50);

        var sections = SheetSkillLayout.Sections(sheet, Catalog);
        var everything = sections.Frequent.Concat(sections.Others).Concat(sections.Folds.SelectMany(f => f.Lines)).Select(l => l.Key).ToList();

        Assert.Contains(sections.Frequent, l => l.Name == "Стрельба (пистолет)");
        Assert.DoesNotContain(sections.Folds.SelectMany(f => f.Lines), l => l.Name == "Стрельба (пистолет)");
        Assert.Equal(everything.Count, everything.Distinct().Count());
        Assert.DoesNotContain(sections.Others, l => sections.Frequent.Any(f => f.Key == l.Key));
    }

    [Fact]
    public void The_rest_is_alphabetical_in_one_list_with_yo_equal_to_ye()
    {
        var sheet = NewSheet(50,
            new SheetSkill { Name = "Ёлки", Value = 10 },
            new SheetSkill { Name = "Ежи", Value = 10 },
            new SheetSkill { Name = "Ербуль", Value = 10 },
            new SheetSkill { Name = "Яма", Value = 10 });

        var names = SheetSkillLayout.Sections(sheet, Catalog).Others.Select(l => l.Name).ToList();

        // «Ёлки» = «Елки»: между «Ежи» и «Ербуль», а не после «Ямы»
        Assert.True(names.IndexOf("Ежи") < names.IndexOf("Ёлки"));
        Assert.True(names.IndexOf("Ёлки") < names.IndexOf("Ербуль"));
        Assert.True(names.IndexOf("Ербуль") < names.IndexOf("Яма"));
        Assert.Equal(names.OrderBy(SheetSkillLayout.AlphabeticKey, StringComparer.Ordinal), names);
        Assert.Equal(-1, string.CompareOrdinal(SheetSkillLayout.AlphabeticKey("Ёж"), SheetSkillLayout.AlphabeticKey("Жук")));
    }

    [Fact]
    public void Play_mode_shows_every_unfolded_skill_alphabetically_and_search_also_sees_folded_ones()
    {
        var sheet = NewSheet(50,
            Skill("Слух", 45),
            Skill("Стрельба (пистолет)", 60),
            new SheetSkill { Name = "Гадание на картах", Value = 30 });

        var visible = SheetSkillLayout.Visible(sheet, Catalog);

        // Навык на базе без строки листа — виден (база справочника), развитая специализация — тоже
        Assert.Contains(visible, l => l.Name == "Психология" && l.Value == 10 && l.Entry is null);
        Assert.Contains(visible, l => l.Name == "Стрельба (пистолет)" && l.Value == 60);
        Assert.Contains(visible, l => l.Name == "Гадание на картах");
        // Специализация на базе свёрнута, как на листе, а родитель строкой не бывает
        Assert.DoesNotContain(visible, l => l.Name is "Стрельба (винтовка)" or Firearms);
        Assert.Equal(visible.Select(l => l.Name).OrderBy(SheetSkillLayout.AlphabeticKey, StringComparer.Ordinal), visible.Select(l => l.Name));

        var all = SheetSkillLayout.All(sheet, Catalog);
        Assert.Contains(all, l => l.Name == "Стрельба (винтовка)"); // свёрнутую специализацию находит поиск
        Assert.Equal(all.Count, all.Select(l => l.Key).Distinct().Count());
        Assert.Equal(all.Select(l => l.Name).OrderBy(SheetSkillLayout.AlphabeticKey, StringComparer.Ordinal), all.Select(l => l.Name));
    }

    [Theory]
    [InlineData(0, new int[0])]
    [InlineData(2, new[] { 1, 1 })]
    [InlineData(7, new[] { 3, 2, 2 })]
    [InlineData(8, new[] { 3, 3, 2 })]
    [InlineData(9, new[] { 3, 3, 3 })]
    public void Split_into_three_columns_keeps_order_and_puts_extra_items_first(int count, int[] sizes)
    {
        var columns = SheetSkillLayout.SplitInto(Enumerable.Range(0, count).ToList(), 3);

        Assert.Equal(sizes, columns.Select(c => c.Count));
        Assert.Equal(Enumerable.Range(0, count), columns.SelectMany(c => c));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 0)]
    [InlineData(4, 2, 2)]
    [InlineData(7, 4, 3)]
    public void Split_in_half_puts_the_extra_item_to_the_left(int count, int left, int right)
    {
        var (l, r) = SheetSkillLayout.SplitInHalf(Enumerable.Range(0, count).ToList());

        Assert.Equal(left, l.Count);
        Assert.Equal(right, r.Count);
        Assert.Equal(Enumerable.Range(0, count), l.Concat(r));
    }
}
