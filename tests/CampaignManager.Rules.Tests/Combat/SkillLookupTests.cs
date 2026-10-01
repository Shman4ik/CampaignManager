using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Combat.Services;
using CampaignManager.Web.Components.Features.Weapons.Model;

namespace CampaignManager.Rules.Tests.Combat;

/// <summary>
///     Поиск навыка под оружие: <see cref="SkillNameMatcher" /> и
///     <see cref="CombatService.FindSkillValue(Character, Weapon)" />. Страницы у сопоставления нет.
/// </summary>
[Trait("page", "?")]
public sealed class SkillLookupTests
{
    private static readonly Guid PistolSkillId = Guid.Parse("0199a000-0000-7000-8000-000000000001");

    private static Character Sheet()
    {
        var sheet = new Character();
        sheet.Skills.SkillGroups =
        [
            new SkillGroup
            {
                Name = "Сражение",
                Skills =
                [
                    Skill("Ближний бой (драка)", 55),
                    Skill("Ближний бой (топор)", 35),
                    Skill("Стрельба (винт./дроб.)", 40),
                    Skill("Стрельба (пистолет)", 45, PistolSkillId),
                    Skill("Стрельба (пистолет-пулемёт)", 30),
                    Skill("Вождение автомобиля", 25),
                    Skill("Управление тяжёлыми машинами", 15),
                    Skill("Метание", 20)
                ]
            }
        ];
        return sheet;
    }

    private static Skill Skill(string name, int value, Guid? id = null) =>
        new() { Name = name, Value = new AttributeValue(value), BaseValue = "0", SkillModelId = id };

    [Theory]
    [InlineData("Стрельба (П)", "стрельба", "пистолет")]
    [InlineData("Стрельба (В/Д)", "стрельба", "винтовка/дробовик")]
    [InlineData("Стрельба (ПП)", "стрельба", "пистолет-пулемет")]
    [InlineData("Стрельба (ППМ)", "стрельба", "пистолет-пулемет")]
    [InlineData("Стрельба (ПМ)", "стрельба", "пулемет")]
    [InlineData("Стрельба (ТВ)", "стрельба", "тяжелое вооружение")]
    [InlineData("Ближний бой (гаррота)", "ближний бой", "удавка")]
    [InlineData("  Ближний   бой  ", "ближний бой", null)]
    [InlineData("Ближний бой ()", "ближний бой", null)]
    [InlineData("Стрельба (пулемёт)", "стрельба", "пулемет")]
    [InlineData("", "", null)]
    [InlineData(null, "", null)]
    public void Parse_SplitsBaseAndSpecialization_ExpandsAliases(string? name, string expectedBase, string? expectedSpec)
    {
        var parsed = SkillNameMatcher.Parse(name);

        Assert.Equal(expectedBase, parsed.Base);
        Assert.Equal(expectedSpec, parsed.Specialization);
    }

    [Theory]
    [InlineData("Стрельба (П)", 45)]
    [InlineData("Стрельба (В/Д)", 40)] // «винт./дроб.» на листе — та же строка
    [InlineData("Стрельба (ПП)", 30)] // «пулемёт» с «ё» на листе
    [InlineData("стрельба (пистолет)", 45)] // точное имя без учёта регистра
    [InlineData("Ближний бой", 55)] // без уточнения — драка
    [InlineData("Ближний бой (топор)", 35)]
    [InlineData("Вождение", 25)] // база по словам
    [InlineData("Упр. тяж. машинами", 15)]
    [InlineData("Стрельба (ПМ)", 0)] // пулемёта на листе нет, пистолет не подходит
    [InlineData("Стрельба", 0)] // строки без уточнения на листе нет
    [InlineData("Плавание", 0)]
    [InlineData("", 0)]
    public void FindSkillValue_ByName(string skillName, int expected)
    {
        Assert.Equal(expected, CombatService.FindSkillValue(Sheet(), skillName));
    }

    [Fact]
    public void FindSkillValue_WeaponSkillIdWinsOverName()
    {
        var weapon = new Weapon { Name = "Кольт", Skill = "Стрельба (В/Д)", SkillId = PistolSkillId };

        Assert.Equal(45, CombatService.FindSkillValue(Sheet(), weapon));
    }

    [Fact]
    public void FindSkillValue_UnknownSkillId_FallsBackToName()
    {
        var weapon = new Weapon { Name = "Винтовка", Skill = "Стрельба (В/Д)", SkillId = Guid.Empty };

        Assert.Equal(40, CombatService.FindSkillValue(Sheet(), weapon));
    }

    [Fact]
    public void FindSkillValue_NoWeapon_Zero()
    {
        Assert.Equal(0, CombatService.FindSkillValue(Sheet(), (Weapon?)null));
    }

    [Fact]
    public void FindBest_UnspecializedRowUsedWhenSpecializationMissing()
    {
        string[] rows = ["Языки (латынь)", "Языки"];

        Assert.Equal("Языки", SkillNameMatcher.FindBest(rows, r => r, "Языки (греческий)"));
        Assert.Equal("Языки (латынь)", SkillNameMatcher.FindBest(rows, r => r, "Языки (лат.)"));
        Assert.Null(SkillNameMatcher.FindBest(rows, r => r, " "));
    }

    [Fact]
    public void FindBest_FirstCandidateInListOrderWins()
    {
        string[] rows = ["Вождение автомобиля", "Вождение повозки"];

        Assert.Equal("Вождение автомобиля", SkillNameMatcher.FindBest(rows, r => r, "Вождение"));
    }

    [Theory]
    [InlineData("винт./дроб.", "винтовка/дробовик", true)]
    [InlineData("пистолет", "пистолет-пулемет", false)]
    [InlineData("п", "пистолет", false)] // префикс короче трёх букв не считается
    [InlineData("пис", "пистолет", true)]
    [InlineData(null, "пистолет", false)]
    public void SpecializationMatches_TokensByPrefixOfThree(string? left, string right, bool expected)
    {
        Assert.Equal(expected, SkillNameMatcher.SpecializationMatches(left, right));
    }

    [Theory]
    [InlineData("вождение", "вождение автомобиля", true)]
    [InlineData("упр. тяж. машинами", "управление тяжелыми машинами", true)]
    [InlineData("стрельба", "метание", false)]
    [InlineData("", "метание", false)]
    public void BaseMatches_WordsInOrder_TailAllowed(string left, string right, bool expected)
    {
        Assert.Equal(expected, SkillNameMatcher.BaseMatches(left, right));
    }

    [Theory]
    [InlineData("Стрельба (пистолет)", "стрельба  (ПИСТОЛЕТ)", true)]
    [InlineData("Ёж", "еж", true)]
    [InlineData("", "", false)]
    public void FullNameEquals_IgnoresCaseYoAndSpaces(string left, string right, bool expected)
    {
        Assert.Equal(expected, SkillNameMatcher.FullNameEquals(left, right));
    }
}
