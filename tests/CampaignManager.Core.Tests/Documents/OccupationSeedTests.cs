using CampaignManager.Core.Catalogs;

namespace CampaignManager.Core.Tests.Documents;

/// <summary>
/// Сид профессий для «Синхронизировать с правилами» (T2.1): каждая запись книжная (код из
/// <see cref="OccupationCodes"/>), каждый навык распознаётся таблицей навыков, и навыков ровно восемь
/// плюс Средства (стр. 37).
/// </summary>
public sealed class OccupationSeedTests
{
    [Fact]
    public void Every_book_occupation_is_in_seed_once()
    {
        var codes = OccupationSeed.Rows.Select(r => r.Code).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.Equal(OccupationCodes.Table.BookNames.Keys.Order(), codes.Order());
    }

    [Fact]
    [Trait("page", "37")]
    public void Every_occupation_has_eight_professional_skills()
    {
        Assert.All(OccupationSeed.Rows, row => Assert.True(row.ProfessionalCount == 8, $"{row.Name}: {row.ProfessionalCount}"));
    }

    [Fact]
    public void Every_skill_name_resolves_to_a_code()
    {
        var names = OccupationSeed.Rows.SelectMany(r => r.Skills.Concat(r.Choices.SelectMany(c => c.Options)));

        Assert.All(names, name => Assert.True(OccupationSeed.ResolveSkill(name) is not null, name));
    }

    [Fact]
    public void Credit_rating_is_never_a_slot()
    {
        Assert.All(OccupationSeed.Rows, row => Assert.DoesNotContain("Средства", row.Skills));
    }

    [Theory]
    [InlineData("Язык, иностранный (латынь)", SkillCodes.LanguageForeign, "латынь")]
    [InlineData("Наука (биология)", "skill.science.biology", null)]
    [InlineData("Внимание", "skill.spot-hidden", null)]
    public void Named_specialization_missing_from_table_keeps_its_parent(string name, string code, string? specialization)
    {
        var resolved = OccupationSeed.ResolveSkill(name);

        Assert.Equal((code, specialization), resolved);
    }

    [Fact]
    public void Broad_skill_means_any_specialization()
    {
        Assert.True(OccupationSeed.HasSpecializations(SkillCodes.Firearms));
        Assert.False(OccupationSeed.HasSpecializations(SkillCodes.Dodge));
    }
}
