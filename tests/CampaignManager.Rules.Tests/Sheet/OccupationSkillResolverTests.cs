using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Characters.Services;
using static CampaignManager.Rules.Tests.Sheet.Sheets;

namespace CampaignManager.Rules.Tests.Sheet;

/// <summary>Раскладка профессии на слоты навыков — каждый вид слота отдельно.</summary>
[Trait("page", "38-39")]
public sealed class OccupationSkillResolverTests
{
    /// <summary>Маленький справочник: два навыка с широким спектром и обычные навыки.</summary>
    private static readonly IReadOnlyList<Skill> Catalog =
    [
        Skill("Внимание", 25),
        Skill("Психология", 10),
        Skill("Маскировка", 5),
        Skill("Взлом", 1),
        Skill("Лазание", 20),
        Skill("Плавание", 20),
        Skill("Ближний бой (драка)", 25, "Ближний бой"),
        Skill("Ближний бой (меч)", 20, "Ближний бой"),
        Skill("Стрельба (пистолет)", 20, "Стрельба"),
        Skill("Наука (химия)", 1, "Наука")
    ];

    [Fact]
    public void BuildSlots_NamedSkill_Fixed()
    {
        var slots = Build(new Occupation { Name = "Т", OccupationSkills = ["Внимание"] });

        var slot = slots[0];
        Assert.Equal(OccupationSlotKind.Fixed, slot.Kind);
        Assert.Equal("Внимание", slot.Label);
        Assert.Equal("Внимание", slot.FixedSkillName);
        Assert.Null(slot.ParentSkillName);
        Assert.False(slot.NeedsChoice);
        Assert.False(slot.ChoosesFromAllSkills);
    }

    /// <summary>Имя ищется без учёта регистра, но в слот уходит так, как записано в профессии.</summary>
    [Fact]
    public void BuildSlots_NamedSkill_CaseInsensitive_KeepsOccupationSpelling()
    {
        var slot = Build(new Occupation { Name = "Т", OccupationSkills = ["  внимание "] })[0];

        Assert.Equal(OccupationSlotKind.Fixed, slot.Kind);
        Assert.Equal("внимание", slot.FixedSkillName);
    }

    [Fact]
    [Trait("page", "52")]
    public void BuildSlots_BroadSkill_SpecializationSlotWithSortedOptions()
    {
        var slot = Build(new Occupation { Name = "Т", OccupationSkills = ["Ближний бой"] })[0];

        Assert.Equal(OccupationSlotKind.Specialization, slot.Kind);
        Assert.Equal("Ближний бой (любая специализация)", slot.Label);
        Assert.Equal("Ближний бой", slot.ParentSkillName);
        Assert.Equal(new[] { "Ближний бой (драка)", "Ближний бой (меч)" }, slot.Options);
        Assert.Equal(new[] { "Ближний бой" }, slot.CustomParents);
        Assert.True(slot.NeedsChoice);
        Assert.True(slot.AllowsCustomName);
        Assert.Null(slot.FixedSkillName);
    }

    [Fact]
    public void BuildSlots_SpecializationInCatalog_PlainFixed()
    {
        var slot = Build(new Occupation { Name = "Т", OccupationSkills = ["Ближний бой (драка)"] })[0];

        Assert.Equal(OccupationSlotKind.Fixed, slot.Kind);
        Assert.Null(slot.ParentSkillName);
    }

    /// <summary>Книга называет специализацию прямо, а в справочнике её нет — всё равно Fixed, с родителем.</summary>
    [Fact]
    public void BuildSlots_NamedSpecializationMissingFromCatalog_FixedWithParent()
    {
        var slots = Build(new Occupation
        {
            Name = "Т", OccupationSkills = ["Наука (биология)", "Ближний бой (драка)"]
        });

        Assert.Equal(OccupationSlotKind.Fixed, slots[0].Kind);
        Assert.Equal("Наука", slots[0].ParentSkillName);
        Assert.Equal("Наука (биология)", slots[0].FixedSkillName);

        var fixedSpecializations = OccupationSkillResolver.FixedSpecializations(slots);
        Assert.Equal("Наука", Assert.Single(fixedSpecializations, p => p.Key == "Наука (биология)").Value);
        Assert.Single(fixedSpecializations);
    }

    [Theory]
    [InlineData("Хиромантия")]
    [InlineData("Алхимия (золото)")] // родителя «Алхимия» в справочнике нет
    [InlineData("Наука(химия)")] // без пробела перед скобкой специализацией не считается
    public void BuildSlots_UnknownSkill_Unresolved(string name)
    {
        var slot = Build(new Occupation { Name = "Т", OccupationSkills = [name] })[0];

        Assert.Equal(OccupationSlotKind.Unresolved, slot.Kind);
        Assert.Equal(name, slot.Label);
        Assert.Empty(slot.Options);
        Assert.True(slot.NeedsChoice);
        Assert.True(slot.ChoosesFromAllSkills);
    }

    [Fact]
    public void BuildSlots_CreditRating_AlwaysLastAndOnce()
    {
        var withCredit = Build(new Occupation
        {
            Name = "Т", OccupationSkills = ["Средства", "Внимание", "средства"], FreeSkillSlots = 1
        });
        var withoutCredit = Build(new Occupation { Name = "Т", OccupationSkills = ["Внимание"] });

        foreach (var slots in new[] { withCredit, withoutCredit })
        {
            Assert.Single(slots, s => s.Kind == OccupationSlotKind.CreditRating);
            Assert.Equal(OccupationSlotKind.CreditRating, slots[^1].Kind);
            Assert.Equal("Средства", slots[^1].FixedSkillName);
            Assert.False(slots[^1].NeedsChoice);
        }

        Assert.Equal(3, withCredit.Count);
    }

    [Fact]
    [Trait("page", "34")]
    public void BuildSlots_Mythos_Skipped()
    {
        var slots = Build(new Occupation { Name = "Т", OccupationSkills = ["Мифы Ктулху", "Внимание"] });

        Assert.Equal(new[] { "Внимание", "Средства" }, slots.Select(s => s.Label));
    }

    [Fact]
    public void BuildSlots_BlankEntries_Skipped()
    {
        var slots = Build(new Occupation { Name = "Т", OccupationSkills = ["", "  ", "Внимание"] });

        Assert.Equal(2, slots.Count);
    }

    [Fact]
    public void BuildSlots_ChoiceOfOne_LabelJoinedWithEither()
    {
        var slots = Build(new Occupation
        {
            Name = "Т",
            SkillChoices = [new() { Count = 1, Options = ["Лазание", "Плавание"] }]
        });

        var slot = slots[0];
        Assert.Equal(OccupationSlotKind.Choice, slot.Kind);
        Assert.Equal("Лазание либо Плавание", slot.Label);
        Assert.Equal(new[] { "Лазание", "Плавание" }, slot.Options);
        Assert.Equal(0, slot.ChoiceGroup);
        Assert.Null(slot.Hint);
        Assert.Empty(slot.CustomParents);
        Assert.False(slot.ChoosesFromAllSkills);
    }

    /// <summary>«Четыре специализации следующих навыков»: родители разворачиваются, пул общий.</summary>
    [Fact]
    public void BuildSlots_ChoiceOfFour_ExpandsParents_HintOnFirstOnly()
    {
        var slots = Build(new Occupation
        {
            Name = "Т",
            SkillChoices =
            [
                new() { Count = 1, Options = ["Лазание", "Плавание"] },
                new() { Count = 4, Options = ["Ближний бой", "Взлом", "Ловкость рук", "Стрельба"] }
            ]
        });

        var group = slots.Where(s => s.ChoiceGroup == 1).ToList();
        Assert.Equal(4, group.Count);
        Assert.Equal(
            new[] { "Навык 1 из 4 по выбору", "Навык 2 из 4 по выбору", "Навык 3 из 4 по выбору", "Навык 4 из 4 по выбору" },
            group.Select(s => s.Label));
        Assert.Equal("Выбор из: Ближний бой / Взлом / Ловкость рук / Стрельба", group[0].Hint);
        Assert.All(group.Skip(1), s => Assert.Null(s.Hint));

        // «Ловкости рук» нет в справочнике — вариант просто пропадает.
        Assert.Equal(new[] { "Ближний бой (драка)", "Ближний бой (меч)", "Взлом", "Стрельба (пистолет)" }, group[0].Options);
        Assert.Equal(new[] { "Ближний бой", "Стрельба" }, group[0].CustomParents);
        Assert.Same(group[0].Options, group[3].Options);
    }

    /// <summary>
    ///     В списке выбора, в отличие от названных навыков, нет ни Unresolved, ни разбора
    ///     «Родитель (специализация)»: неизвестный вариант молча выпадает, а группа, где не
    ///     нашлось ни одного, исчезает целиком — профессия теряет слоты без всякой пометки.
    /// </summary>
    [Fact]
    [Trait("finding", "F-S07")]
    public void BuildSlots_ChoiceOptionsMissingFromCatalog_DroppedSilently()
    {
        var occupation = new Occupation
        {
            Name = "Т",
            OccupationSkills = ["Внимание"],
            SkillChoices =
            [
                new() { Count = 1, Options = ["Искусство/ремесло (актёрская игра)", "Маскировка"] },
                new() { Count = 2, Options = ["Хиромантия", "Наука (биология)"] }
            ]
        };

        var slots = Build(occupation);

        Assert.Equal(4, OccupationSkillResolver.ProfessionalSkillCount(occupation));
        Assert.Equal(new[] { "Маскировка" }, slots.Single(s => s.Kind == OccupationSlotKind.Choice).Options);
        Assert.DoesNotContain(slots, s => s.ChoiceGroup == 1);
        Assert.DoesNotContain(slots, s => s.Kind == OccupationSlotKind.Unresolved);
        Assert.Equal(2, slots.Count(s => s.Kind != OccupationSlotKind.CreditRating)); // а профессия обещает 4
    }

    [Fact]
    [Trait("page", "38")]
    public void BuildSlots_SocialSlots_FourSocialSkills()
    {
        var slots = Build(new Occupation { Name = "Т", SocialSkillSlots = 2 });

        Assert.Equal(2, slots.Count(s => s.Kind == OccupationSlotKind.Social));
        Assert.All(slots.Where(s => s.Kind == OccupationSlotKind.Social), s =>
        {
            Assert.Equal("Социальный навык", s.Label);
            Assert.Equal(new[] { "Запугивание", "Красноречие", "Обаяние", "Убеждение" }, s.Options);
            Assert.True(s.NeedsChoice);
            Assert.False(s.ChoosesFromAllSkills);
        });
    }

    [Fact]
    public void BuildSlots_FreeSlots_AnySkill()
    {
        var slot = Build(new Occupation { Name = "Т", FreeSkillSlots = 1 })[0];

        Assert.Equal(OccupationSlotKind.Any, slot.Kind);
        Assert.Equal("Любой навык на выбор", slot.Label);
        Assert.Empty(slot.Options);
        Assert.True(slot.ChoosesFromAllSkills);
    }

    [Fact]
    public void BuildSlots_Order_NamedChoicesSocialAnyCredit()
    {
        var slots = Build(new Occupation
        {
            Name = "Т",
            OccupationSkills = ["Средства", "Внимание", "Хиромантия"],
            SkillChoices = [new() { Count = 1, Options = ["Лазание", "Плавание"] }],
            SocialSkillSlots = 1,
            FreeSkillSlots = 1
        });

        Assert.Equal(
            new[]
            {
                OccupationSlotKind.Fixed, OccupationSlotKind.Unresolved, OccupationSlotKind.Choice,
                OccupationSlotKind.Social, OccupationSlotKind.Any, OccupationSlotKind.CreditRating
            },
            slots.Select(s => s.Kind));
    }

    [Fact]
    [Trait("page", "37")]
    public void ProfessionalSkillCount_NamedWithoutCreditAndMythos_PlusAllSlots()
    {
        var occupation = new Occupation
        {
            Name = "Т",
            OccupationSkills = ["Внимание", "Средства", "Мифы Ктулху", " ", "Хиромантия"],
            SkillChoices = [new() { Count = 2, Options = ["Лазание"] }],
            SocialSkillSlots = 1,
            FreeSkillSlots = 3
        };

        Assert.Equal(8, OccupationSkillResolver.ProfessionalSkillCount(occupation));
        Assert.Equal(8, OccupationSkillResolver.RequiredSkillCount);
    }

    [Fact]
    [Trait("page", "52")]
    public void ParentNames_GroupsSpecializationsCaseInsensitive()
    {
        var parents = OccupationSkillResolver.ParentNames(Catalog);

        Assert.Equal(3, parents.Count);
        Assert.Equal(new[] { "Ближний бой (драка)", "Ближний бой (меч)" }, parents["ближний бой"]);
    }

    [Theory]
    [Trait("page", "52")]
    [InlineData("Наука", "Наука (биология)", 1, "01%")]
    [InlineData("Ближний бой", "Ближний бой (топор)", 25, "25%")] // первая соседняя в справочнике
    [InlineData("Язык, иностранный", "Язык, иностранный (латынь)", 1, "01%")] // соседей нет
    public void CreateSpecialization_BaseFromSiblingOrOne(string parent, string name, int value, string baseText)
    {
        var skill = OccupationSkillResolver.CreateSpecialization(name, parent, Catalog);

        Assert.Equal(name, skill.Name);
        Assert.Equal(parent, skill.ParentSkillName);
        Assert.Equal(value, skill.Value.Regular);
        Assert.Equal(baseText, skill.BaseValue);
    }

    private static List<OccupationSlot> Build(Occupation occupation) =>
        OccupationSkillResolver.BuildSlots(occupation, Catalog);
}
