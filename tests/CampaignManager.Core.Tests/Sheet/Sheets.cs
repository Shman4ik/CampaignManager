using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;

namespace CampaignManager.Core.Tests.Sheet;

/// <summary>
/// Короткая сборка листа для тестов правил: маленький справочник навыков с книжными кодами и лист,
/// навыки которого ссылаются на него. Значения характеристик по умолчанию — те же, что в T0.2.
/// </summary>
internal static class Sheets
{
    public const string Mythos = "Мифы Ктулху";
    public const string CreditRating = "Средства";
    public const string Dodge = "Уклонение";
    public const string OwnLanguage = "Язык, родной";
    public const string ForeignLanguage = "Язык, иностранный";
    public const string Latin = "Язык, иностранный (латынь)";
    public const string Fighting = "Ближний бой";
    public const string Firearms = "Стрельба";
    public const string Science = "Наука";

    /// <summary>Справочник: книжные навыки с кодами, родители со специализациями, обычные навыки.</summary>
    public static SkillCatalog Catalog { get; } = BuildCatalog();

    /// <summary>Тот же справочник без Мифов и Средств — «записать некуда».</summary>
    public static SkillCatalog CatalogWithoutMythosAndCredit { get; } =
        new(Catalog.Skills.Where(s => s.Code is not (SkillCodes.Mythos or SkillCodes.CreditRating)));

    public static SkillDefinition Def(string name) =>
        Catalog.FindByName(name) ?? throw new ArgumentException($"Нет навыка «{name}» в тестовом справочнике.");

    public static Guid Id(string name) => Def(name).Id;

    /// <summary>Характеристики; не названное — значения по умолчанию.</summary>
    public static Characteristics Chars(
        int str = 50, int con = 80, int siz = 60, int dex = 45,
        int app = 40, int @int = 80, int pow = 70, int edu = 75) => new()
    {
        Str = str, Con = con, Siz = siz, Dex = dex, App = app, Int = @int, Pow = pow, Edu = edu,
    };

    /// <summary>Строка навыка справочника.</summary>
    public static SheetSkill Skill(string name, int value, bool isChecked = false) =>
        new() { SkillId = Id(name), Value = value, Checked = isChecked };

    /// <summary>Своя специализация, которой нет в справочнике.</summary>
    public static SheetSkill Specialization(string parent, string name, int value) =>
        new() { ParentSkillId = Id(parent), Name = name, Value = value };

    /// <summary>Лист с заданным Рассудком и навыками.</summary>
    public static CharacterSheet NewSheet(int sanity = 50, params SheetSkill[] skills) => new()
    {
        Characteristics = Chars(),
        Current = new CurrentValues { Sanity = sanity },
        Skills = [.. skills],
    };

    /// <summary>Лист с навыком Мифов заданного значения.</summary>
    public static CharacterSheet WithMythos(int mythos, int sanity = 50, params SheetSkill[] other) =>
        NewSheet(sanity, [Skill(Mythos, mythos), .. other]);

    public static SheetSkill Find(CharacterSheet sheet, string name) =>
        sheet.Skills.Single(s => s.DisplayName(Catalog) == name);

    private static SkillCatalog BuildCatalog()
    {
        List<SkillDefinition> skills = [];

        SkillDefinition Add(string name, int baseValue = 1, string? code = null, Guid? parent = null, string? formula = null)
        {
            var skill = new SkillDefinition(Guid.NewGuid(), name)
            {
                Code = code ?? SkillCodes.FromName(name),
                BaseValue = baseValue,
                ParentId = parent,
                BaseFormula = formula,
            };
            skills.Add(skill);
            return skill;
        }

        Add(Mythos, 0, SkillCodes.Mythos);
        Add(CreditRating, 0, SkillCodes.CreditRating);
        Add(Dodge, 0, SkillCodes.Dodge, formula: "DEX/2");
        Add(OwnLanguage, 0, SkillCodes.LanguageOwn, formula: "EDU");
        var foreign = Add(ForeignLanguage, 1, SkillCodes.LanguageForeign);
        Add(Latin, 1, parent: foreign.Id);
        Add("Язык, иностранный (греческий)", 1, parent: foreign.Id);
        var fighting = Add(Fighting, 0, SkillCodes.Fighting);
        Add("Ближний бой (драка)", 25, parent: fighting.Id);
        Add("Ближний бой (меч)", 20, parent: fighting.Id);
        var firearms = Add(Firearms, 0, SkillCodes.Firearms);
        Add("Стрельба (пистолет)", 20, parent: firearms.Id);
        Add("Стрельба (винтовка)", 25, parent: firearms.Id);
        Add("Стрельба (дробовик)", 25, parent: firearms.Id);
        var survival = Add("Выживание", 10, SkillCodes.Survival);
        Add("Выживание (горы)", 10, parent: survival.Id);
        var science = Add(Science, 1);
        Add("Наука (химия)", 1, parent: science.Id);
        Add("Наука (физика)", 1, parent: science.Id);
        Add("Запугивание", 15, SkillCodes.Intimidate);
        Add("Красноречие", 5, SkillCodes.FastTalk);
        Add("Обаяние", 15, SkillCodes.Charm);
        Add("Убеждение", 10, SkillCodes.Persuade);
        Add("Внимание", 25);
        Add("Слух", 20);
        Add("Психология", 10);
        Add("Маскировка", 5);
        Add("Взлом", 1);
        Add("Лазание", 20);
        Add("Плавание", 20);

        return new SkillCatalog(skills);
    }
}
