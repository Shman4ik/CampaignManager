using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using static CampaignManager.Core.Tests.Sheet.Sheets;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>
/// Слоты профессии в помощнике — каждый вид отдельно. Перенесено из T0.2 (<c>OccupationSkillResolverTests</c>)
/// на слоты схемы 2.0: разбор строковых имён профессий v1 («Наука (биология)», «Хиромантия» → Unresolved)
/// делает перенос T1.3, и его тесты переезжают туда. Ожидания, которые не зависят от строк, — те же.
/// </summary>
[Trait("page", "38-39")]
public sealed class OccupationRulesTests
{
    [Fact]
    public void BuildSlots_NamedSkill_Fixed()
    {
        var slot = Build(SkillSlot("Внимание"))[0];

        Assert.Equal(OccupationSlotViewKind.Fixed, slot.Kind);
        Assert.Equal("Внимание", slot.Label);
        Assert.Equal(Id("Внимание"), slot.SkillId);
        Assert.Null(slot.ParentSkillId);
        Assert.False(slot.NeedsChoice);
        Assert.False(slot.ChoosesFromAllSkills);
    }

    [Theory]
    [Trait("page", "52")]
    [InlineData(OccupationSlotKind.AnySpecialization)]
    [InlineData(OccupationSlotKind.Skill)] // родитель, записанный навыком, — тоже «любая специализация», как в v1
    public void BuildSlots_BroadSkill_SpecializationSlotWithSortedOptions(OccupationSlotKind kind)
    {
        var slot = Build(new OccupationSlotDefinition(kind) { SkillId = Id(Fighting) })[0];

        Assert.Equal(OccupationSlotViewKind.Specialization, slot.Kind);
        Assert.Equal("Ближний бой (любая специализация)", slot.Label);
        Assert.Equal(Id(Fighting), slot.ParentSkillId);
        Assert.Equal([Id("Ближний бой (драка)"), Id("Ближний бой (меч)")], slot.Options);
        Assert.Equal([Id(Fighting)], slot.CustomParents);
        Assert.True(slot.NeedsChoice);
        Assert.True(slot.AllowsCustomName);
    }

    [Fact]
    public void BuildSlots_SpecializationInCatalog_PlainFixed()
    {
        var slot = Build(new OccupationSlotDefinition(OccupationSlotKind.Specialization)
        {
            SkillId = Id(Fighting), Specialization = "драка",
        })[0];

        Assert.Equal(OccupationSlotViewKind.Fixed, slot.Kind);
        Assert.Equal(Id("Ближний бой (драка)"), slot.SkillId);
        Assert.Null(slot.ParentSkillId);
    }

    /// <summary>Книга называет специализацию прямо, а в справочнике её нет — Fixed с родителем.</summary>
    [Fact]
    public void BuildSlots_NamedSpecializationMissingFromCatalog_FixedWithParent()
    {
        var slot = Build(new OccupationSlotDefinition(OccupationSlotKind.Specialization)
        {
            SkillId = Id(Science), Specialization = "биология",
        })[0];

        Assert.Equal(OccupationSlotViewKind.Fixed, slot.Kind);
        Assert.Equal("Наука (биология)", slot.Label);
        Assert.Null(slot.SkillId);
        Assert.Equal(Id(Science), slot.ParentSkillId);
        Assert.Equal("биология", slot.Specialization);
    }

    [Fact]
    public void BuildSlots_CreditRating_AlwaysLastAndOnce()
    {
        var withCredit = Build(SkillSlot(CreditRating), SkillSlot("Внимание"), SkillSlot(CreditRating),
            new OccupationSlotDefinition(OccupationSlotKind.Free));
        var withoutCredit = Build(SkillSlot("Внимание"));

        foreach (var slots in new[] { withCredit, withoutCredit })
        {
            Assert.Single(slots, s => s.Kind == OccupationSlotViewKind.CreditRating);
            Assert.Equal(OccupationSlotViewKind.CreditRating, slots[^1].Kind);
            Assert.Equal(Id(CreditRating), slots[^1].SkillId);
            Assert.Equal("Средства", slots[^1].Label);
            Assert.False(slots[^1].NeedsChoice);
        }

        Assert.Equal(3, withCredit.Count);
    }

    [Fact]
    [Trait("page", "34")]
    public void BuildSlots_Mythos_Skipped() =>
        Assert.Equal(["Внимание", "Средства"], Build(SkillSlot(Mythos), SkillSlot("Внимание")).Select(s => s.Label));

    [Fact]
    public void BuildSlots_ChoiceOfOne_LabelJoinedWithEither()
    {
        var slot = Build(Choice(1, "Лазание", "Плавание"))[0];

        Assert.Equal(OccupationSlotViewKind.Choice, slot.Kind);
        Assert.Equal("Лазание либо Плавание", slot.Label);
        Assert.Equal([Id("Лазание"), Id("Плавание")], slot.Options);
        Assert.Equal(0, slot.ChoiceGroup);
        Assert.Null(slot.Hint);
        Assert.Empty(slot.CustomParents);
        Assert.False(slot.ChoosesFromAllSkills);
    }

    /// <summary>«Четыре специализации следующих навыков»: родители разворачиваются, пул общий.</summary>
    [Fact]
    public void BuildSlots_ChoiceOfFour_ExpandsParents_HintOnFirstOnly()
    {
        var slots = Build(Choice(1, "Лазание", "Плавание"), Choice(4, Fighting, "Взлом", Firearms));

        var group = slots.Where(s => s.ChoiceGroup == 1).ToList();
        Assert.Equal(4, group.Count);
        Assert.Equal(
            ["Навык 1 из 4 по выбору", "Навык 2 из 4 по выбору", "Навык 3 из 4 по выбору", "Навык 4 из 4 по выбору"],
            group.Select(s => s.Label));
        Assert.Equal("Выбор из: Ближний бой / Взлом / Стрельба", group[0].Hint);
        Assert.All(group.Skip(1), s => Assert.Null(s.Hint));

        Assert.Equal(
            [Id("Ближний бой (драка)"), Id("Ближний бой (меч)"), Id("Взлом"),
             Id("Стрельба (винтовка)"), Id("Стрельба (дробовик)"), Id("Стрельба (пистолет)")],
            group[0].Options);
        Assert.Equal([Id(Fighting), Id(Firearms)], group[0].CustomParents);
        Assert.Same(group[0].Options, group[3].Options);
    }

    [Fact]
    [Trait("page", "38")]
    public void BuildSlots_SocialSlots_FourSocialSkills()
    {
        var slots = Build(Social(), Social());

        Assert.Equal(2, slots.Count(s => s.Kind == OccupationSlotViewKind.Social));
        Assert.All(slots.Where(s => s.Kind == OccupationSlotViewKind.Social), s =>
        {
            Assert.Equal("Социальный навык", s.Label);
            Assert.Equal([Id("Запугивание"), Id("Красноречие"), Id("Обаяние"), Id("Убеждение")], s.Options);
            Assert.True(s.NeedsChoice);
            Assert.False(s.ChoosesFromAllSkills);
        });
    }

    [Fact]
    public void BuildSlots_FreeSlots_AnySkill()
    {
        var slot = Build(new OccupationSlotDefinition(OccupationSlotKind.Free))[0];

        Assert.Equal(OccupationSlotViewKind.Any, slot.Kind);
        Assert.Equal("Любой навык на выбор", slot.Label);
        Assert.Empty(slot.Options);
        Assert.True(slot.ChoosesFromAllSkills);
    }

    [Fact]
    public void BuildSlots_Order_NamedChoicesSocialAnyCredit()
    {
        var slots = Build(
            new OccupationSlotDefinition(OccupationSlotKind.Free),
            Social(),
            SkillSlot(CreditRating),
            Choice(1, "Лазание", "Плавание"),
            SkillSlot("Внимание"),
            new OccupationSlotDefinition(OccupationSlotKind.AnySpecialization) { SkillId = Id(Science) });

        Assert.Equal(
        [
            OccupationSlotViewKind.Fixed, OccupationSlotViewKind.Specialization, OccupationSlotViewKind.Choice,
            OccupationSlotViewKind.Social, OccupationSlotViewKind.Any, OccupationSlotViewKind.CreditRating,
        ], slots.Select(s => s.Kind));
    }

    [Fact]
    [Trait("page", "37")]
    public void ProfessionalSkillCount_NamedWithoutCreditAndMythos_PlusAllSlots()
    {
        var occupation = Occupation(
            SkillSlot("Внимание"), SkillSlot(CreditRating), SkillSlot(Mythos),
            new OccupationSlotDefinition(OccupationSlotKind.Specialization) { SkillId = Id(Science), Specialization = "биология" },
            Choice(2, "Лазание"), Social(),
            new OccupationSlotDefinition(OccupationSlotKind.Free),
            new OccupationSlotDefinition(OccupationSlotKind.Free),
            new OccupationSlotDefinition(OccupationSlotKind.Free));

        Assert.Equal(8, OccupationRules.ProfessionalSkillCount(occupation, Catalog));
        Assert.Equal(8, OccupationRules.RequiredSkillCount);
        Assert.Equal(9, OccupationRules.BuildSlots(occupation, Catalog).Count); // восемь и Средства
    }

    [Theory]
    [Trait("page", "52")]
    [InlineData(Science, "биология", 1)]
    [InlineData(Fighting, "топор", 25)] // первая соседняя в справочнике
    public void NewSpecialization_BaseFromSiblingOrOne(string parent, string name, int value)
    {
        var skill = OccupationRules.NewSpecialization(Id(parent), $" {name} ", Catalog);

        Assert.Equal(name, skill.Name);
        Assert.Equal(Id(parent), skill.ParentSkillId);
        Assert.Equal(value, skill.Value);
        Assert.Equal($"{parent} ({name})", skill.DisplayName(Catalog));
    }

    [Fact]
    [Trait("page", "52")]
    public void NewSpecialization_NoSiblings_One()
    {
        var parent = new SkillDefinition(Guid.NewGuid(), "Пилотирование");
        var catalog = new SkillCatalog([parent]);

        Assert.Equal(1, OccupationRules.NewSpecialization(parent.Id, "дирижабль", catalog).Value);
    }

    /// <summary>
    /// База своей специализации — общая у большинства соседних (Наука — 1%, хотя у математики 10%); у Ближнего боя базы разные
    /// (стр. 56) — число первой соседней остаётся заготовкой, но лист его не показывает.
    /// </summary>
    [Fact]
    [Trait("page", "56")]
    public void SpecializationBase_CommonOrUnknown()
    {
        var science = new SkillDefinition(Guid.NewGuid(), "Наука");
        var fighting = new SkillDefinition(Guid.NewGuid(), "Ближний бой");
        var catalog = new SkillCatalog(
        [
            science,
            new SkillDefinition(Guid.NewGuid(), "Наука (математика)") { ParentId = science.Id, BaseValue = 10 },
            new SkillDefinition(Guid.NewGuid(), "Наука (химия)") { ParentId = science.Id, BaseValue = 1 },
            new SkillDefinition(Guid.NewGuid(), "Наука (физика)") { ParentId = science.Id, BaseValue = 1 },
            fighting,
            new SkillDefinition(Guid.NewGuid(), "Ближний бой (драка)") { ParentId = fighting.Id, BaseValue = 25 },
            new SkillDefinition(Guid.NewGuid(), "Ближний бой (хлыст)") { ParentId = fighting.Id, BaseValue = 5 },
        ]);

        Assert.Equal((1, true), catalog.SpecializationBase(science.Id));
        Assert.Equal((25, false), catalog.SpecializationBase(fighting.Id));

        var chainsaw = new SheetSkill { ParentSkillId = fighting.Id, Name = "бензопила", Value = 14 };
        var line = SheetSkillLayout.Line(new CharacterSheet(), catalog, chainsaw);
        Assert.False(line.BaseKnown);
        Assert.True(SheetSkillLayout.Line(new CharacterSheet(), catalog, new SheetSkill { ParentSkillId = science.Id, Name = "астрономия", Value = 1 }).BaseKnown);
    }

    private static OccupationSlotDefinition SkillSlot(string name) => new(OccupationSlotKind.Skill) { SkillId = Id(name) };

    private static OccupationSlotDefinition Social() => new(OccupationSlotKind.Social);

    private static OccupationSlotDefinition Choice(int count, params string[] options) =>
        new(OccupationSlotKind.Choice) { ChooseCount = count, Options = [.. options.Select(Id)] };

    private static OccupationDefinition Occupation(params OccupationSlotDefinition[] slots) =>
        new(Guid.NewGuid(), "Т", SkillPointsFormula.Edu4) { Slots = slots };

    private static List<OccupationSlotView> Build(params OccupationSlotDefinition[] slots) =>
        OccupationRules.BuildSlots(Occupation(slots), Catalog);
}
