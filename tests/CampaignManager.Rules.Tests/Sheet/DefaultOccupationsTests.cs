using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Characters.Services;
using static CampaignManager.Rules.Tests.Sheet.Sheets;

namespace CampaignManager.Rules.Tests.Sheet;

/// <summary>
///     Проверка данных «Примеров занятий»: у каждой профессии ровно восемь профессиональных
///     навыков плюс Средства (стр. 37). Справочник навыков живёт в базе, поэтому здесь он
///     собирается из самих профессий — так проверяется согласованность данных, а не содержимое базы.
/// </summary>
[Trait("page", "37-39")]
public sealed class DefaultOccupationsTests
{
    /// <summary>Навыки с широким спектром — родители специализаций в справочнике (Characters/CLAUDE.md).</summary>
    private static readonly string[] BroadSkills =
        ["Искусство/ремесло", "Наука", "Ближний бой", "Стрельба", "Выживание", "Язык, иностранный"];

    private static readonly List<Occupation> Occupations = Occupation.GetDefaultOccupations();

    private static readonly IReadOnlyList<Skill> Catalog = BuildCatalog();

    public static TheoryData<string> OccupationNames => new(Occupations.Select(o => o.Name));

    [Fact]
    public void GetDefaultOccupations_31Professions_NamesUnique()
    {
        Assert.Equal(31, Occupations.Count);
        Assert.Equal(Occupations.Count, Occupations.Select(o => o.Name).Distinct().Count());
        Assert.Single(Occupations, o => o.IsModern);
    }

    [Theory]
    [MemberData(nameof(OccupationNames))]
    public void Occupation_EightProfessionalSkillsPlusCreditRating(string name)
    {
        var occupation = Get(name);

        Assert.Equal(OccupationSkillResolver.RequiredSkillCount, OccupationSkillResolver.ProfessionalSkillCount(occupation));
        Assert.Single(occupation.OccupationSkills, s => s == OccupationSkillResolver.CreditRatingSkill);
    }

    [Theory]
    [MemberData(nameof(OccupationNames))]
    public void BuildSlots_FullCatalog_EightSlotsPlusCreditRating_NoneUnresolved(string name)
    {
        var slots = OccupationSkillResolver.BuildSlots(Get(name), Catalog);

        Assert.Equal(8, slots.Count(s => s.Kind != OccupationSlotKind.CreditRating));
        Assert.Equal(OccupationSlotKind.CreditRating, slots[^1].Kind);
        Assert.DoesNotContain(slots, s => s.Kind == OccupationSlotKind.Unresolved);
        Assert.All(slots.Where(s => s.Kind == OccupationSlotKind.Choice), s => Assert.NotEmpty(s.Options));
    }

    [Theory]
    [MemberData(nameof(OccupationNames))]
    public void Occupation_NamedSkillsDistinct_AndNotRepeatedInChoices(string name)
    {
        var occupation = Get(name);
        var named = occupation.OccupationSkills.Where(s => s != OccupationSkillResolver.CreditRatingSkill).ToList();

        Assert.Equal(named.Count, named.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain(occupation.SkillChoices.SelectMany(c => c.Options), named.Contains);
        Assert.All(occupation.SkillChoices, c => Assert.True(c.Count >= 1 && c.Count <= c.Options.Count));
    }

    [Theory]
    [MemberData(nameof(OccupationNames))]
    public void Occupation_CreditRatingRangeWithin0To99(string name)
    {
        var occupation = Get(name);

        Assert.InRange(occupation.CreditRatingMin, 0, 99);
        Assert.InRange(occupation.CreditRatingMax, occupation.CreditRatingMin, 99);
    }

    /// <summary>Названные книгой специализации, которых может не быть в справочнике, отдаются листу.</summary>
    [Theory]
    [InlineData("Врач", "Язык, иностранный (латынь)", "Язык, иностранный")]
    [InlineData("Инженер", "Искусство/ремесло (черчение)", "Искусство/ремесло")]
    public void BuildSlots_CatalogWithoutNamedSpecialization_StillFixedWithParent(string occupation, string skill, string parent)
    {
        var catalog = Catalog.Where(s => s.Name != skill).ToList();

        var slots = OccupationSkillResolver.BuildSlots(Get(occupation), catalog);

        Assert.Equal(parent, OccupationSkillResolver.FixedSpecializations(slots)[skill]);
        Assert.Equal(8, slots.Count(s => s.Kind != OccupationSlotKind.CreditRating));
    }

    private static Occupation Get(string name) => Occupations.Single(o => o.Name == name);

    private static List<Skill> BuildCatalog()
    {
        var names = Occupations
            .SelectMany(o => o.OccupationSkills.Concat(o.SkillChoices.SelectMany(c => c.Options)))
            .Concat(OccupationSkillResolver.SocialSkills)
            .Where(n => n != OccupationSkillResolver.CreditRatingSkill)
            .Distinct()
            .ToList();

        List<Skill> catalog = [Skill(OccupationSkillResolver.CreditRatingSkill, 0)];
        foreach (var parent in BroadSkills)
            catalog.Add(Skill($"{parent} (пример)", 1, parent));

        foreach (var name in names.Where(n => !BroadSkills.Contains(n)))
        {
            var parent = BroadSkills.FirstOrDefault(p => name.StartsWith(p + " (", StringComparison.Ordinal));
            catalog.Add(Skill(name, 1, parent));
        }

        return catalog;
    }
}
