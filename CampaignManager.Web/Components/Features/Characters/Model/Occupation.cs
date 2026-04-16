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

    /// <summary>ОБР × 2 + НАР × 2</summary>
    Edu2App2,

    /// <summary>ОБР × 2 + СИЛ × 2</summary>
    Edu2Str2,

    /// <summary>ОБР × 2 + МОЩ × 2</summary>
    Edu2Pow2,

    /// <summary>ОБР × 2 + (ЛВК или СИЛ) × 2</summary>
    Edu2DexOrStr2,

    /// <summary>ОБР × 2 + НАР × 2 или ОБР × 2 + МОЩ × 2</summary>
    Edu2AppOrPow2,

    /// <summary>ОБР × 2 + ЛВК × 2 или ОБР × 2 + МОЩ × 2</summary>
    Edu2DexOrPow2,

    /// <summary>ОБР × 2 + НАР × 2 / ЛВК × 2 / СИЛ × 2</summary>
    Edu2AppOrDexOrStr2
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

    /// <summary>
    /// Количество слотов социальных навыков (Запугивание, Красноречие, Обаяние или Убеждение)
    /// </summary>
    public int SocialSkillSlots { get; set; }

    /// <summary>
    /// Признак современной профессии (true = только для современных сеттингов)
    /// </summary>
    public bool IsModern { get; set; }

    /// <summary>
    /// Признак лавкрафтовской профессии (true = упоминается в произведениях Лавкрафта)
    /// </summary>
    public bool IsLovecraftian { get; set; }

    /// <summary>
    /// Теги профессии для взвешенного выбора навыков при генерации персонажа
    /// </summary>
    public OccupationTag Tags { get; set; } = OccupationTag.None;

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
            OccupationSkillPointFormula.Edu2AppOrPow2 => c.Education.Regular * 2 + Math.Max(c.Appearance.Regular, c.Power.Regular) * 2,
            OccupationSkillPointFormula.Edu2DexOrPow2 => c.Education.Regular * 2 + Math.Max(c.Dexterity.Regular, c.Power.Regular) * 2,
            OccupationSkillPointFormula.Edu2AppOrDexOrStr2 => c.Education.Regular * 2 + Math.Max(c.Appearance.Regular, Math.Max(c.Dexterity.Regular, c.Strength.Regular)) * 2,
            _ => c.Education.Regular * 4
        };
    }

    /// <summary>
    /// Начальные данные для заполнения БД
    /// </summary>
    public static List<Occupation> GetDefaultOccupations() =>
    [
        // === Лавкрафтовские профессии ===
        new() { Name = "Антиквар", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 30, CreditRatingMax = 70, OccupationSkills = ["Оценка", "Искусство/ремесло", "История", "Работа в библиотеке", "Языки (иностр.)", "Внимание", "Средства"], FreeSkillSlots = 1, SocialSkillSlots = 1, IsLovecraftian = true, Tags = OccupationTag.Academic | OccupationTag.Investigative | OccupationTag.Scholarly },
        new() { Name = "Писатель", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 9, CreditRatingMax = 30, OccupationSkills = ["Искусство/ремесло", "История", "Работа в библиотеке", "Языки (родной)", "Языки (иностр.)", "Оккультизм", "Психология", "Средства"], FreeSkillSlots = 1, IsLovecraftian = true, Tags = OccupationTag.Academic | OccupationTag.Artistic | OccupationTag.Language },
        new() { Name = "Библиотекарь", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 9, CreditRatingMax = 35, OccupationSkills = ["Бухгалтерское дело", "Языки (иностр.)", "Работа в библиотеке", "Языки (родной)", "Средства"], FreeSkillSlots = 4, IsLovecraftian = true, Tags = OccupationTag.Academic | OccupationTag.Investigative | OccupationTag.Scholarly },
        new() { Name = "Врач", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 30, CreditRatingMax = 80, OccupationSkills = ["Первая помощь", "Медицина", "Языки (иностр.)", "Психология", "Наука", "Средства"], FreeSkillSlots = 2, IsLovecraftian = true, Tags = OccupationTag.Medical | OccupationTag.Academic | OccupationTag.Investigative },
        new() { Name = "Детектив", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2, CreditRatingMin = 20, CreditRatingMax = 50, OccupationSkills = ["Искусство/ремесло", "Маскировка", "Психология", "Слух", "Стрельба (пистолет)", "Юриспруденция", "Средства"], FreeSkillSlots = 1, SocialSkillSlots = 1, IsLovecraftian = true, Tags = OccupationTag.Investigative | OccupationTag.Social | OccupationTag.Combat },
        new() { Name = "Дилетант", SkillPointFormula = OccupationSkillPointFormula.Edu2App2, CreditRatingMin = 50, CreditRatingMax = 99, OccupationSkills = ["Верховая езда", "Языки (иностр.)", "Искусство/ремесло", "Средства"], FreeSkillSlots = 3, SocialSkillSlots = 1, IsLovecraftian = true, Tags = OccupationTag.Social | OccupationTag.Artistic | OccupationTag.Language },
        new() { Name = "Журналист", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 9, CreditRatingMax = 30, OccupationSkills = ["Искусство/ремесло", "История", "Психология", "Работа в библиотеке", "Языки (родной)", "Средства"], FreeSkillSlots = 2, SocialSkillSlots = 1, IsLovecraftian = true, Tags = OccupationTag.Investigative | OccupationTag.Social | OccupationTag.Academic },
        new() { Name = "Профессор", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 20, CreditRatingMax = 70, OccupationSkills = ["Языки (иностр.)", "Психология", "Работа в библиотеке", "Языки (родной)", "Наука", "Средства"], FreeSkillSlots = 4, IsLovecraftian = true, Tags = OccupationTag.Academic | OccupationTag.Scholarly | OccupationTag.Language },

        // === Общие профессии ===
        new() { Name = "Артист", SkillPointFormula = OccupationSkillPointFormula.Edu2App2, CreditRatingMin = 9, CreditRatingMax = 70, OccupationSkills = ["Искусство/ремесло", "Маскировка", "Психология", "Средства"], FreeSkillSlots = 2, SocialSkillSlots = 2, Tags = OccupationTag.Artistic | OccupationTag.Social },
        new() { Name = "Археолог", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 10, CreditRatingMax = 40, OccupationSkills = ["Оценка", "Археология", "История", "Языки (иностр.)", "Работа в библиотеке", "Внимание", "Наука", "Средства"], Tags = OccupationTag.Academic | OccupationTag.Investigative | OccupationTag.Outdoor },
        new() { Name = "Бродяга", SkillPointFormula = OccupationSkillPointFormula.Edu2AppOrDexOrStr2, CreditRatingMin = 0, CreditRatingMax = 5, OccupationSkills = ["Лазание", "Ориентирование", "Прыжки", "Скрытность", "Слух", "Средства"], FreeSkillSlots = 2, SocialSkillSlots = 1, Tags = OccupationTag.Physical | OccupationTag.Stealth | OccupationTag.Outdoor },
        new() { Name = "Бухгалтер", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 30, CreditRatingMax = 70, OccupationSkills = ["Бухгалтерское дело", "Юриспруденция", "Работа в библиотеке", "Слух", "Убеждение", "Внимание", "Средства"], FreeSkillSlots = 1, Tags = OccupationTag.Academic | OccupationTag.Investigative },
        new() { Name = "Военный (офицер)", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2, CreditRatingMin = 20, CreditRatingMax = 70, OccupationSkills = ["Бухгалтерское дело", "Выживание", "Ориентирование", "Психология", "Стрельба (пистолет)", "Средства"], FreeSkillSlots = 1, SocialSkillSlots = 2, Tags = OccupationTag.Combat | OccupationTag.Social | OccupationTag.Outdoor },
        new() { Name = "Дикарь", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2, CreditRatingMin = 0, CreditRatingMax = 15, OccupationSkills = ["Ближний бой (драка)", "Внимание", "Выживание", "Естествознание", "Лазание", "Оккультизм", "Плавание", "Слух", "Средства"], Tags = OccupationTag.Physical | OccupationTag.Outdoor | OccupationTag.Occult },
        new() { Name = "Инженер", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 30, CreditRatingMax = 60, OccupationSkills = ["Искусство/ремесло", "Механика", "Наука", "Работа в библиотеке", "Упр. тяж. машинами", "Электрика", "Средства"], FreeSkillSlots = 1, Tags = OccupationTag.Technical | OccupationTag.Academic },
        new() { Name = "Лётчик", SkillPointFormula = OccupationSkillPointFormula.Edu2Dex2, CreditRatingMin = 20, CreditRatingMax = 70, OccupationSkills = ["Механика", "Наука", "Ориентирование", "Пилотирование", "Упр. тяж. машинами", "Электрика", "Средства"], FreeSkillSlots = 2, Tags = OccupationTag.Technical | OccupationTag.Physical },
        new() { Name = "Механик", SkillPointFormula = OccupationSkillPointFormula.Edu2Dex2, CreditRatingMin = 9, CreditRatingMax = 40, OccupationSkills = ["Вождение", "Электрика", "Механика", "Упр. тяж. машинами", "Внимание", "Средства"], FreeSkillSlots = 2, Tags = OccupationTag.Technical },
        new() { Name = "Миссионер", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 0, CreditRatingMax = 30, OccupationSkills = ["Естествознание", "Искусство/ремесло", "Медицина", "Механика", "Первая помощь", "Средства"], FreeSkillSlots = 2, SocialSkillSlots = 1, Tags = OccupationTag.Social | OccupationTag.Medical | OccupationTag.Outdoor },
        new() { Name = "Музыкант", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrPow2, CreditRatingMin = 9, CreditRatingMax = 30, OccupationSkills = ["Искусство/ремесло", "Психология", "Слух", "Средства"], FreeSkillSlots = 4, SocialSkillSlots = 1, Tags = OccupationTag.Artistic | OccupationTag.Social },
        new() { Name = "Парапсихолог", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 9, CreditRatingMax = 30, OccupationSkills = ["Антропология", "Языки (иностр.)", "Искусство/ремесло", "История", "Оккультизм", "Психология", "Работа в библиотеке", "Средства"], FreeSkillSlots = 1, Tags = OccupationTag.Occult | OccupationTag.Academic | OccupationTag.Investigative },
        new() { Name = "Полицейский", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2, CreditRatingMin = 9, CreditRatingMax = 30, OccupationSkills = ["Ближний бой (драка)", "Внимание", "Первая помощь", "Психология", "Стрельба (пистолет)", "Юриспруденция", "Средства"], SocialSkillSlots = 1, Tags = OccupationTag.Combat | OccupationTag.Investigative | OccupationTag.Social },
        new() { Name = "Преступник", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2, CreditRatingMin = 5, CreditRatingMax = 65, OccupationSkills = ["Внимание", "Психология", "Скрытность", "Средства"], FreeSkillSlots = 4, SocialSkillSlots = 1, Tags = OccupationTag.Criminal | OccupationTag.Stealth | OccupationTag.Social },
        new() { Name = "Священник", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 9, CreditRatingMax = 60, OccupationSkills = ["Бухгалтерское дело", "История", "Работа в библиотеке", "Слух", "Языки (иностр.)", "Психология", "Средства"], FreeSkillSlots = 1, SocialSkillSlots = 1, Tags = OccupationTag.Social | OccupationTag.Academic | OccupationTag.Occult },
        new() { Name = "Солдат", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2, CreditRatingMin = 9, CreditRatingMax = 30, OccupationSkills = ["Ближний бой (драка)", "Выживание", "Скрытность", "Стрельба (винт./дроб.)", "Уклонение", "Средства"], FreeSkillSlots = 2, Tags = OccupationTag.Combat | OccupationTag.Physical | OccupationTag.Outdoor },
        new() { Name = "Спортсмен", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2, CreditRatingMin = 9, CreditRatingMax = 70, OccupationSkills = ["Ближний бой (драка)", "Верховая езда", "Лазание", "Метание", "Плавание", "Прыжки", "Средства"], FreeSkillSlots = 1, SocialSkillSlots = 1, Tags = OccupationTag.Physical | OccupationTag.Combat },
        new() { Name = "Фанатик", SkillPointFormula = OccupationSkillPointFormula.Edu2AppOrPow2, CreditRatingMin = 0, CreditRatingMax = 30, OccupationSkills = ["История", "Психология", "Скрытность", "Средства"], FreeSkillSlots = 3, SocialSkillSlots = 2, Tags = OccupationTag.Social | OccupationTag.Occult | OccupationTag.Stealth },
        new() { Name = "Фермер", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2, CreditRatingMin = 9, CreditRatingMax = 30, OccupationSkills = ["Вождение", "Естествознание", "Искусство/ремесло", "Механика", "Упр. тяж. машинами", "Чтение следов", "Средства"], FreeSkillSlots = 1, SocialSkillSlots = 1, Tags = OccupationTag.Outdoor | OccupationTag.Physical | OccupationTag.Technical },
        new() { Name = "Художник", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrPow2, CreditRatingMin = 9, CreditRatingMax = 50, OccupationSkills = ["Внимание", "Естествознание", "Языки (иностр.)", "История", "Искусство/ремесло", "Психология", "Средства"], FreeSkillSlots = 2, SocialSkillSlots = 1, Tags = OccupationTag.Artistic | OccupationTag.Investigative },
        new() { Name = "Частный сыщик", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2, CreditRatingMin = 9, CreditRatingMax = 30, OccupationSkills = ["Внимание", "Искусство/ремесло", "Маскировка", "Психология", "Работа в библиотеке", "Юриспруденция", "Средства"], FreeSkillSlots = 1, SocialSkillSlots = 1, Tags = OccupationTag.Investigative | OccupationTag.Social | OccupationTag.Stealth },
        new() { Name = "Юрист", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 30, CreditRatingMax = 80, OccupationSkills = ["Бухгалтерское дело", "Психология", "Работа в библиотеке", "Юриспруденция", "Средства"], FreeSkillSlots = 2, SocialSkillSlots = 2, Tags = OccupationTag.Academic | OccupationTag.Social | OccupationTag.Investigative },

        // === Современные профессии ===
        new() { Name = "Хакер", SkillPointFormula = OccupationSkillPointFormula.Edu4, CreditRatingMin = 10, CreditRatingMax = 70, OccupationSkills = ["Внимание", "Работа в библиотеке", "Электрика", "Средства"], FreeSkillSlots = 2, SocialSkillSlots = 1, IsModern = true, Tags = OccupationTag.Technical | OccupationTag.Criminal | OccupationTag.Investigative }
    ];
}
