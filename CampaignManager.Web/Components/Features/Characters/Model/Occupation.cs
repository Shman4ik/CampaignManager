using System.ComponentModel.DataAnnotations;
using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Characters.Model;

/// <summary>
/// Формула расчёта очков навыков профессии (CoC 7e)
/// </summary>
public enum OccupationSkillPointFormula
{
    /// <summary>ОБР × 4</summary>
    Edu4,

    /// <summary>ОБР × 2 + ЛВК × 2</summary>
    Edu2Dex2,

    /// <summary>ОБР × 2 + ВНШ × 2</summary>
    Edu2App2,

    /// <summary>ОБР × 2 + СИЛ × 2</summary>
    Edu2Str2,

    /// <summary>ОБР × 2 + МОЩ × 2</summary>
    Edu2Pow2,

    /// <summary>ОБР × 2 + (ЛВК или СИЛ) × 2</summary>
    Edu2DexOrStr2
}

/// <summary>
/// Профессия персонажа Call of Cthulhu 7e — хранится в БД
/// </summary>
public sealed class Occupation : BaseDataBaseEntity, INamedEntity
{
    [Required]
    [StringLength(100)]
    public required string Name { get; set; }

    public OccupationSkillPointFormula SkillPointFormula { get; set; }

    public int CreditRatingMin { get; set; }

    public int CreditRatingMax { get; set; }

    /// <summary>
    /// Названия навыков профессии (JSONB). null означает «любой навык на выбор».
    /// </summary>
    public List<string> OccupationSkills { get; set; } = [];

    /// <summary>
    /// Количество свободных слотов навыков (выбираемых из любого навыка)
    /// </summary>
    public int FreeSkillSlots { get; set; }

    public int CalculateSkillPoints(Characteristics c)
    {
        return SkillPointFormula switch
        {
            OccupationSkillPointFormula.Edu4 => c.Education.Regular * 4,
            OccupationSkillPointFormula.Edu2Dex2 => c.Education.Regular * 2 + c.Dexterity.Regular * 2,
            OccupationSkillPointFormula.Edu2App2 => c.Education.Regular * 2 + c.Appearance.Regular * 2,
            OccupationSkillPointFormula.Edu2Str2 => c.Education.Regular * 2 + c.Strength.Regular * 2,
            OccupationSkillPointFormula.Edu2Pow2 => c.Education.Regular * 2 + c.Power.Regular * 2,
            OccupationSkillPointFormula.Edu2DexOrStr2 => c.Education.Regular * 2 + Math.Max(c.Dexterity.Regular, c.Strength.Regular) * 2,
            _ => c.Education.Regular * 4
        };
    }

    /// <summary>
    /// Начальные данные для заполнения БД
    /// </summary>
    public static List<Occupation> GetDefaultOccupations() =>
    [
        new() { Name = "Антиквар", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 30, CreditRatingMax = 70, OccupationSkills = ["Оценка", "Искусство/ремесло", "История", "Работа в библиотеке", "Языки (иностр.)", "Внимание", "Средства"], FreeSkillSlots = 1 },
        new() { Name = "Писатель", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 9, CreditRatingMax = 30, OccupationSkills = ["Искусство/ремесло", "История", "Работа в библиотеке", "Языки (родной)", "Языки (иностр.)", "Оккультизм", "Психология", "Средства"], FreeSkillSlots = 0 },
        new() { Name = "Бухгалтер", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 30, CreditRatingMax = 70, OccupationSkills = ["Бухгалтерское дело", "Юриспруденция", "Работа в библиотеке", "Слух", "Убеждение", "Внимание", "Средства"], FreeSkillSlots = 1 },
        new() { Name = "Актёр", SkillPointFormula = OccupationSkillPointFormula.Edu2App2, CreditRatingMin = 9, CreditRatingMax = 40, OccupationSkills = ["Искусство/ремесло", "Маскировка", "Красноречие", "Слух", "Языки (иностр.)", "Психология", "Средства"], FreeSkillSlots = 1 },
        new() { Name = "Археолог", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 10, CreditRatingMax = 40, OccupationSkills = ["Оценка", "Археология", "История", "Языки (иностр.)", "Работа в библиотеке", "Внимание", "Наука", "Средства"], FreeSkillSlots = 0 },
        new() { Name = "Художник", SkillPointFormula = OccupationSkillPointFormula.Edu2Pow2, CreditRatingMin = 9, CreditRatingMax = 50, OccupationSkills = ["Искусство/ремесло", "История", "Языки (иностр.)", "Языки (родной)", "Психология", "Внимание", "Средства"], FreeSkillSlots = 1 },
        new() { Name = "Спортсмен", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2, CreditRatingMin = 9, CreditRatingMax = 70, OccupationSkills = ["Лазание", "Прыжки", "Ближний бой (драка)", "Верховая езда", "Плавание", "Метание", "Средства"], FreeSkillSlots = 1 },
        new() { Name = "Священник", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 9, CreditRatingMax = 60, OccupationSkills = ["Бухгалтерское дело", "История", "Работа в библиотеке", "Слух", "Языки (иностр.)", "Убеждение", "Психология", "Средства"], FreeSkillSlots = 0 },
        new() { Name = "Преступник", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2, CreditRatingMin = 5, CreditRatingMax = 65, OccupationSkills = ["Запугивание", "Взлом", "Ловкость рук", "Скрытность", "Внимание", "Психология", "Средства"], FreeSkillSlots = 1 },
        new() { Name = "Доктор", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 30, CreditRatingMax = 80, OccupationSkills = ["Первая помощь", "Медицина", "Языки (иностр.)", "Психология", "Наука", "Работа в библиотеке", "Средства"], FreeSkillSlots = 1 },
        new() { Name = "Инженер", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 30, CreditRatingMax = 60, OccupationSkills = ["Искусство/ремесло", "Электрика", "Работа в библиотеке", "Механика", "Наука", "Внимание", "Средства"], FreeSkillSlots = 1 },
        new() { Name = "Детектив", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2, CreditRatingMin = 20, CreditRatingMax = 50, OccupationSkills = ["Искусство/ремесло", "Маскировка", "Юриспруденция", "Работа в библиотеке", "Внимание", "Психология", "Скрытность", "Средства"], FreeSkillSlots = 0 },
        new() { Name = "Журналист", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 9, CreditRatingMax = 30, OccupationSkills = ["Искусство/ремесло", "История", "Работа в библиотеке", "Языки (родной)", "Психология", "Убеждение", "Средства"], FreeSkillSlots = 1 },
        new() { Name = "Юрист", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 30, CreditRatingMax = 80, OccupationSkills = ["Бухгалтерское дело", "Юриспруденция", "Работа в библиотеке", "Убеждение", "Психология", "Красноречие", "Средства"], FreeSkillSlots = 1 },
        new() { Name = "Библиотекарь", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 9, CreditRatingMax = 35, OccupationSkills = ["Бухгалтерское дело", "Языки (иностр.)", "Работа в библиотеке", "Языки (родной)", "Средства"], FreeSkillSlots = 3 },
        new() { Name = "Военный", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2, CreditRatingMin = 9, CreditRatingMax = 30, OccupationSkills = ["Ближний бой (драка)", "Первая помощь", "Запугивание", "Ориентирование", "Стрельба (винт./дроб.)", "Стрельба (пистолет)", "Скрытность", "Средства"], FreeSkillSlots = 0 },
        new() { Name = "Профессор", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 20, CreditRatingMax = 70, OccupationSkills = ["Работа в библиотеке", "Языки (иностр.)", "Языки (родной)", "Психология", "Наука", "Средства"], FreeSkillSlots = 2 },
        new() { Name = "Полицейский", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2, CreditRatingMin = 9, CreditRatingMax = 30, OccupationSkills = ["Ближний бой (драка)", "Стрельба (пистолет)", "Первая помощь", "Запугивание", "Юриспруденция", "Психология", "Внимание", "Средства"], FreeSkillSlots = 0 },
        new() { Name = "Пилот", SkillPointFormula = OccupationSkillPointFormula.Edu2Dex2, CreditRatingMin = 20, CreditRatingMax = 60, OccupationSkills = ["Электрика", "Механика", "Ориентирование", "Пилотирование", "Наука", "Внимание", "Средства"], FreeSkillSlots = 1 },
        new() { Name = "Механик", SkillPointFormula = OccupationSkillPointFormula.Edu2Dex2, CreditRatingMin = 9, CreditRatingMax = 40, OccupationSkills = ["Вождение", "Электрика", "Механика", "Упр. тяж. машинами", "Внимание", "Средства"], FreeSkillSlots = 2 }
    ];
}
