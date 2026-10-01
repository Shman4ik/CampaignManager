using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Rules.Tests.Sheet;

/// <summary>Короткая сборка листа и навыков для тестов правил листа.</summary>
internal static class Sheets
{
    public const string Mythos = "Мифы Ктулху";
    public const string CreditRating = "Средства";
    public const string Dodge = "Уклонение";

    /// <summary>Характеристики; не названное — значения по умолчанию из модели.</summary>
    public static Characteristics Chars(
        int str = 50, int con = 80, int siz = 60, int dex = 45,
        int app = 40, int @int = 80, int pow = 70, int edu = 75) => new()
    {
        Strength = new AttributeValue(str),
        Constitution = new AttributeValue(con),
        Size = new AttributeValue(siz),
        Dexterity = new AttributeValue(dex),
        Appearance = new AttributeValue(app),
        Intelligence = new AttributeValue(@int),
        Power = new AttributeValue(pow),
        Education = new AttributeValue(edu)
    };

    public static Skill Skill(string name, int value, string? parent = null) => new()
    {
        Name = name,
        Value = new AttributeValue(value),
        BaseValue = $"{value:00}%",
        ParentSkillName = parent
    };

    /// <summary>Лист с одной группой навыков и заданным Рассудком.</summary>
    public static Character Character(int sanity = 50, params Skill[] skills) => new()
    {
        Characteristics = Chars(),
        DerivedAttributes = new DerivedAttributes
        {
            Sanity = new AttributeWithMaxValue(sanity, 99)
        },
        Skills = new SkillsModel
        {
            SkillGroups = [new SkillGroup { Name = "Навыки", Skills = [.. skills] }]
        }
    };

    /// <summary>Лист с навыком Мифов заданного значения.</summary>
    public static Character WithMythos(int mythos, int sanity = 50, params Skill[] other) =>
        Character(sanity, [Skill(Mythos, mythos), .. other]);

    public static Skill Find(Character character, string name) =>
        character.Skills.SkillGroups.SelectMany(g => g.Skills).Single(s => s.Name == name);
}
