using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Checks;
using CampaignManager.Core.Dice;
using CampaignManager.Core.Scenarios;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Checks;

/// <summary>Цели проверки с листа: характеристики, Удача, навыки (строки листа и база справочника).</summary>
public sealed class CheckSubjectsTests
{
    [Fact]
    public void Characteristics_EightInSheetOrder()
    {
        var subjects = CheckSubjects.Characteristics(NewSheet());

        Assert.Equal(["СИЛ", "ВЫН", "ТЕЛ", "ЛВК", "НАР", "ИНТ", "МОЩ", "ОБР"], subjects.Select(s => s.Name));
        Assert.Equal([50, 80, 60, 45, 40, 80, 70, 75], subjects.Select(s => s.Value));
        Assert.All(subjects, s => Assert.Equal(CheckSubjectKind.Characteristic, s.Kind));
    }

    [Fact]
    public void Luck_CurrentValue()
    {
        var sheet = NewSheet();
        sheet.Current.Luck = 55;

        Assert.Equal(new CheckSubject(CheckSubjectKind.Luck, "Удача", 55), CheckSubjects.Luck(sheet));
    }

    [Fact]
    public void CatalogSkill_RowValueOrBase()
    {
        var sheet = NewSheet(50, Skill("Внимание", 65));

        Assert.Equal(65, CheckSubjects.CatalogSkill(sheet, Catalog, Id("Внимание"))!.Value);
        Assert.Equal(20, CheckSubjects.CatalogSkill(sheet, Catalog, Id("Слух"))!.Value);
        Assert.Equal(22, CheckSubjects.CatalogSkill(sheet, Catalog, Id(Dodge))!.Value); // ЛВК 45 / 2 (стр. 57)
        Assert.Null(CheckSubjects.CatalogSkill(sheet, Catalog, Guid.NewGuid()));
    }

    [Fact]
    public void SkillGroups_ByCategoryInCatalogOrder_ParentsExcluded_OwnLast()
    {
        var sheet = NewSheet(50, Skill("Внимание", 65), Specialization(Firearms, "гарпун", 30),
            new Characters.SheetSkill { Name = "Гадание на картах", Value = 15 });

        var groups = CheckSubjects.SkillGroups(sheet, Catalog);
        var all = groups.SelectMany(g => g.Subjects).ToList();

        Assert.DoesNotContain(all, s => s.Name == Firearms); // родитель целью не бывает
        Assert.Contains(all, s => s.Name == "Стрельба (пистолет)" && s.Value == 20);
        Assert.Contains(all, s => s.Name == "Стрельба (гарпун)" && s.Value == 30);
        Assert.Equal(CheckSubjects.OwnSkillsTitle, groups[^1].Title);
        Assert.Equal(["Гадание на картах"], groups[^1].Subjects.Select(s => s.Name));
        Assert.Single(all, s => s.Name == "Внимание");
    }

    [Fact]
    public void FindByKey_RoundTripsEveryKey()
    {
        var sheet = NewSheet(50, Skill("Внимание", 65), new Characters.SheetSkill { Name = "Гадание на картах", Value = 15 });
        sheet.Current.Luck = 40;

        var subjects = CheckSubjects.Characteristics(sheet)
            .Append(CheckSubjects.Luck(sheet))
            .Concat(CheckSubjects.SkillGroups(sheet, Catalog).SelectMany(g => g.Subjects))
            .ToList();

        Assert.All(subjects, subject => Assert.Equal(subject, CheckSubjects.FindByKey(sheet, Catalog, subject.Key)));
        Assert.Null(CheckSubjects.FindByKey(sheet, Catalog, "manual"));
        Assert.Null(CheckSubjects.FindByKey(sheet, Catalog, "skill:не-guid"));
    }

    /// <summary>
    /// Проверка локации в 2.0 — ссылка (вид, навык, характеристика), а не имя: разбор «СИЛ»/«Сила»/«Стрельба (П)»
    /// из v1 (<c>CheckTargets.ResolveByName</c>) не переносится — имена разрешил перенос T1.3.
    /// </summary>
    [Fact]
    public void Resolve_ScenarioCheck()
    {
        var sheet = NewSheet(50, Skill("Внимание", 65));
        sheet.Current.Luck = 33;

        Assert.Equal(65, CheckSubjects.Resolve(sheet, Catalog, CheckTarget.Skill, Id("Внимание"), null)!.Value);
        Assert.Equal(50, CheckSubjects.Resolve(sheet, Catalog, CheckTarget.Characteristic, null, Characteristic.STR)!.Value);
        Assert.Equal(33, CheckSubjects.Resolve(sheet, Catalog, CheckTarget.Luck, null, null)!.Value);
        Assert.Null(CheckSubjects.Resolve(sheet, Catalog, CheckTarget.Skill, null, null));
    }

    /// <summary>Ключ проверки локации совпадает с ключом цели на листе — диалог откроется на ней.</summary>
    [Theory]
    [InlineData(CheckTarget.Skill)]
    [InlineData(CheckTarget.Characteristic)]
    [InlineData(CheckTarget.Luck)]
    public void KeyFor_MatchesResolvedSubjectKey(CheckTarget target)
    {
        var sheet = NewSheet(50, Skill("Внимание", 65));
        var skillId = Id("Внимание");

        var key = CheckSubjects.KeyFor(target, skillId, Characteristic.DEX);

        Assert.Equal(CheckSubjects.Resolve(sheet, Catalog, target, skillId, Characteristic.DEX)!.Key, key);
        Assert.Null(CheckSubjects.KeyFor(CheckTarget.Skill, null, null));
    }

    [Fact]
    public void Manual_DefaultName()
    {
        Assert.Equal("Проверка", CheckSubject.Manual(40).Name);
        Assert.Equal("Внимание", CheckSubject.Manual(40, " Внимание ").Name);
        Assert.Equal("manual", CheckSubject.Manual(40).Key);
    }

    [Fact]
    public void SkillSubject_CarriesCodes()
    {
        var pistol = Skill("Стрельба (пистолет)", 40);
        var subject = CheckSubjects.Skill(Catalog, pistol);

        Assert.Equal(SkillCodes.FromName("Стрельба (пистолет)"), subject.SkillCode);
        Assert.Equal(SkillCodes.Firearms, subject.ParentSkillCode);
        Assert.True(CheckRules.IsCombatSkill(subject));
    }
}

/// <summary>Групповая проверка восприятия (стр. 200–201): лучший результат — правило Core, а не разметки.</summary>
[Trait("page", "200-201")]
public sealed class GroupCheckTests
{
    private static GroupCheckRow Row(string name, int value, int? roll, Difficulty difficulty = Difficulty.Regular) =>
        new(Guid.NewGuid(), name, value, roll is { } r ? CheckRules.Evaluate(r, value, difficulty) : null);

    [Fact]
    public void Best_HighestLevelWins()
    {
        var ann = Row("Энн", 60, 50); // обычный
        var bob = Row("Боб", 40, 8); // чрезвычайный
        var cid = Row("Сид", 70, 90); // провал

        Assert.Equal(bob.Id, Assert.Single(GroupCheck.Best([ann, bob, cid])));
    }

    [Fact]
    public void Best_SameLevel_HigherSkillWins()
    {
        var ann = Row("Энн", 60, 25); // трудный
        var bob = Row("Боб", 50, 20); // трудный

        Assert.Equal(ann.Id, Assert.Single(GroupCheck.Best([ann, bob])));
    }

    [Fact]
    public void Best_FullTie_AllOfThem()
    {
        var ann = Row("Энн", 50, 20);
        var bob = Row("Боб", 50, 25);

        Assert.Equal(new HashSet<Guid> { ann.Id, bob.Id }, GroupCheck.Best([ann, bob]));
    }

    [Fact]
    public void Best_SuccessBelowDifficulty_DoesNotCount()
    {
        var ann = Row("Энн", 60, 40, Difficulty.Hard); // обычный успех, нужен трудный
        var bob = Row("Боб", 30, null);

        Assert.Empty(GroupCheck.Best([ann, bob]));
    }

    [Fact]
    public void PerceptionSkills_AreBookCodes() =>
        Assert.All(GroupCheck.PerceptionSkillCodes, code => Assert.Contains(code, SkillCodes.BookNames.Keys));
}
