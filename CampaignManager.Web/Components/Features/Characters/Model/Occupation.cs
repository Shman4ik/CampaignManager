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
///     Слот «выбрать несколько навыков из перечисленных» («Зов Ктулху» 7e, стр. 38–39):
///     «Лазание либо Плавание» (<see cref="Count" /> = 1), «любые два из: Иностранный язык,
///     Механика, Первая помощь» (= 2), «четыре специализации следующих навыков…» (= 4).
///     Отдельная колонка, а не строка внутри <see cref="Occupation.OccupationSkills" />: тот jsonb —
///     плоский <c>List&lt;string&gt;</c>, и уже сохранённые справочники обязаны читаться дальше.
///     Если вариант — навык с широким спектром (Ближний бой, Стрельба, Иностранный язык), игрок
///     выбирает его специализацию: книга и просит «специализации следующих навыков».
/// </summary>
public sealed class OccupationSkillChoice
{
    /// <summary>Сколько навыков из <see cref="Options" /> берёт игрок.</summary>
    public int Count { get; set; } = 1;

    /// <summary>Варианты — имена ровно как в справочнике навыков games."Skills".</summary>
    public List<string> Options { get; set; } = [];
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
    ///     Слоты «выбрать N навыков из перечисленных» (JSONB). Пустой список — у профессии
    ///     таких слотов нет; ради старых строк колонка допускает и отсутствие значения.
    /// </summary>
    public List<OccupationSkillChoice> SkillChoices { get; set; } = [];

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
    ///     Список «Примеры занятий» из книги правил («Зов Ктулху» 7e, стр. 37–39) — тот же,
    ///     что лежит в справочнике <c>games."Occupations"</c>. Служит и фолбэком генератора,
    ///     и источником для кнопки «Синхронизировать с правилами» на <c>/occupations</c>.
    ///     Имена навыков пишутся ровно как в справочнике навыков <c>games."Skills"</c>, иначе
    ///     слот профессии не найдёт навык и превратится в <c>Unresolved</c>.
    ///     У каждой профессии профессиональных навыков ровно восемь плюс Средства:
    ///     <c>OccupationSkills</c> без «Средств» + сумма <c>SkillChoices[].Count</c> +
    ///     <c>SocialSkillSlots</c> + <c>FreeSkillSlots</c> == 8 (стр. 37).
    ///     Три последние профессии (Археолог, Бухгалтер, Механик) в русских «Примерах занятий»
    ///     отсутствуют — они взяты из англоязычных правил, см. комментарий у их блока.
    /// </summary>
    public static List<Occupation> GetDefaultOccupations() =>
    [
        // === Лавкрафтовские профессии ===
        new()
        {
            Name = "Антиквар", SkillPointFormula = OccupationSkillPointFormula.Edu4,
            CreditRatingMin = 30, CreditRatingMax = 70,
            OccupationSkills =
            [
                "Внимание", "Язык, иностранный", "Искусство/ремесло", "История", "Оценка",
                "Работа в библиотеке", "Средства"
            ],
            SocialSkillSlots = 1, FreeSkillSlots = 1, IsLovecraftian = true,
            Tags = OccupationTag.Academic | OccupationTag.Investigative | OccupationTag.Scholarly
        },
        new()
        {
            Name = "Библиотекарь", SkillPointFormula = OccupationSkillPointFormula.Edu4,
            CreditRatingMin = 9, CreditRatingMax = 35,
            OccupationSkills =
                ["Бухгалтерское дело", "Язык, иностранный", "Работа в библиотеке", "Язык, родной", "Средства"],
            FreeSkillSlots = 4, IsLovecraftian = true,
            Tags = OccupationTag.Academic | OccupationTag.Investigative | OccupationTag.Scholarly
        },
        new()
        {
            // «Иностранный язык (латынь)» — книга называет язык прямо, это не выбор игрока.
            Name = "Врач", SkillPointFormula = OccupationSkillPointFormula.Edu4,
            CreditRatingMin = 30, CreditRatingMax = 80,
            OccupationSkills =
            [
                "Язык, иностранный (латынь)", "Медицина", "Наука (биология)", "Наука (фармакология)",
                "Первая помощь", "Психология", "Средства"
            ],
            FreeSkillSlots = 2, IsLovecraftian = true,
            Tags = OccupationTag.Medical | OccupationTag.Academic | OccupationTag.Investigative
        },
        new()
        {
            Name = "Детектив полиции", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2,
            CreditRatingMin = 20, CreditRatingMax = 50,
            OccupationSkills = ["Внимание", "Психология", "Слух", "Стрельба", "Ориентирование", "Средства"],
            SkillChoices = [new() { Count = 1, Options = ["Искусство/ремесло (актёрская игра)", "Маскировка"] }],
            SocialSkillSlots = 1, FreeSkillSlots = 1, IsLovecraftian = true,
            Tags = OccupationTag.Investigative | OccupationTag.Social | OccupationTag.Combat
        },
        new()
        {
            Name = "Дилетант", SkillPointFormula = OccupationSkillPointFormula.Edu2App2,
            CreditRatingMin = 50, CreditRatingMax = 99,
            OccupationSkills = ["Верховая езда", "Язык, иностранный", "Искусство/ремесло", "Стрельба", "Средства"],
            SocialSkillSlots = 1, FreeSkillSlots = 3, IsLovecraftian = true,
            Tags = OccupationTag.Social | OccupationTag.Artistic | OccupationTag.Language
        },
        new()
        {
            Name = "Журналист", SkillPointFormula = OccupationSkillPointFormula.Edu4,
            CreditRatingMin = 9, CreditRatingMax = 30,
            OccupationSkills =
            [
                "Искусство/ремесло (фотография)", "История", "Психология", "Работа в библиотеке",
                "Язык, родной", "Средства"
            ],
            SocialSkillSlots = 1, FreeSkillSlots = 2, IsLovecraftian = true,
            Tags = OccupationTag.Investigative | OccupationTag.Social | OccupationTag.Academic
        },
        new()
        {
            Name = "Писатель", SkillPointFormula = OccupationSkillPointFormula.Edu4,
            CreditRatingMin = 9, CreditRatingMax = 30,
            OccupationSkills =
            [
                "Язык, иностранный", "Искусство/ремесло (литература)", "История", "Психология",
                "Работа в библиотеке", "Язык, родной", "Средства"
            ],
            SkillChoices = [new() { Count = 1, Options = ["Естествознание", "Оккультизм"] }],
            FreeSkillSlots = 1, IsLovecraftian = true,
            Tags = OccupationTag.Academic | OccupationTag.Artistic | OccupationTag.Language
        },
        new()
        {
            Name = "Профессор", SkillPointFormula = OccupationSkillPointFormula.Edu4,
            CreditRatingMin = 20, CreditRatingMax = 70,
            OccupationSkills = ["Язык, иностранный", "Психология", "Работа в библиотеке", "Язык, родной", "Средства"],
            FreeSkillSlots = 4, IsLovecraftian = true,
            Tags = OccupationTag.Academic | OccupationTag.Scholarly | OccupationTag.Language
        },

        // === Общие профессии ===
        new()
        {
            Name = "Артист", SkillPointFormula = OccupationSkillPointFormula.Edu2App2,
            CreditRatingMin = 9, CreditRatingMax = 70,
            OccupationSkills = ["Искусство/ремесло (актёрская игра)", "Маскировка", "Психология", "Слух", "Средства"],
            SocialSkillSlots = 2, FreeSkillSlots = 2,
            Tags = OccupationTag.Artistic | OccupationTag.Social
        },
        new()
        {
            Name = "Бродяга", SkillPointFormula = OccupationSkillPointFormula.Edu2AppOrDexOrStr2,
            CreditRatingMin = 0, CreditRatingMax = 5,
            OccupationSkills = ["Лазание", "Ориентирование", "Прыжки", "Скрытность", "Слух", "Средства"],
            SocialSkillSlots = 1, FreeSkillSlots = 2,
            Tags = OccupationTag.Physical | OccupationTag.Stealth | OccupationTag.Outdoor
        },
        new()
        {
            Name = "Военный (офицер)", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2,
            CreditRatingMin = 20, CreditRatingMax = 70,
            OccupationSkills =
                ["Бухгалтерское дело", "Выживание", "Ориентирование", "Психология", "Стрельба", "Средства"],
            SocialSkillSlots = 2, FreeSkillSlots = 1,
            Tags = OccupationTag.Combat | OccupationTag.Social | OccupationTag.Outdoor
        },
        new()
        {
            Name = "Дикарь", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2,
            CreditRatingMin = 0, CreditRatingMax = 15,
            OccupationSkills =
            [
                "Внимание", "Выживание", "Естествознание", "Лазание", "Оккультизм", "Плавание", "Слух",
                "Средства"
            ],
            SkillChoices = [new() { Count = 1, Options = ["Ближний бой", "Метание"] }],
            Tags = OccupationTag.Physical | OccupationTag.Outdoor | OccupationTag.Occult
        },
        new()
        {
            Name = "Инженер", SkillPointFormula = OccupationSkillPointFormula.Edu4,
            CreditRatingMin = 30, CreditRatingMax = 60,
            OccupationSkills =
            [
                "Искусство/ремесло (черчение)", "Механика", "Наука (инженерия)", "Наука (физика)",
                "Работа в библиотеке", "Управление тяжёлыми машинами", "Электрика", "Средства"
            ],
            FreeSkillSlots = 1,
            Tags = OccupationTag.Technical | OccupationTag.Academic
        },
        new()
        {
            Name = "Лётчик", SkillPointFormula = OccupationSkillPointFormula.Edu2Dex2,
            CreditRatingMin = 20, CreditRatingMax = 70,
            OccupationSkills =
            [
                "Механика", "Наука (астрономия)", "Ориентирование", "Пилотирование (самолёт)",
                "Управление тяжёлыми машинами", "Электрика", "Средства"
            ],
            FreeSkillSlots = 2,
            Tags = OccupationTag.Technical | OccupationTag.Physical
        },
        new()
        {
            Name = "Миссионер", SkillPointFormula = OccupationSkillPointFormula.Edu4,
            CreditRatingMin = 0, CreditRatingMax = 30,
            OccupationSkills =
                ["Естествознание", "Искусство/ремесло", "Медицина", "Механика", "Первая помощь", "Средства"],
            SocialSkillSlots = 1, FreeSkillSlots = 2,
            Tags = OccupationTag.Social | OccupationTag.Medical | OccupationTag.Outdoor
        },
        new()
        {
            Name = "Музыкант", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrPow2,
            CreditRatingMin = 9, CreditRatingMax = 30,
            OccupationSkills = ["Искусство/ремесло (музыкальный инструмент)", "Психология", "Слух", "Средства"],
            SocialSkillSlots = 1, FreeSkillSlots = 4,
            Tags = OccupationTag.Artistic | OccupationTag.Social
        },
        new()
        {
            Name = "Парапсихолог", SkillPointFormula = OccupationSkillPointFormula.Edu4,
            CreditRatingMin = 9, CreditRatingMax = 30,
            OccupationSkills =
            [
                "Антропология", "Язык, иностранный", "Искусство/ремесло (фотография)", "История",
                "Оккультизм", "Психология", "Работа в библиотеке", "Средства"
            ],
            FreeSkillSlots = 1,
            Tags = OccupationTag.Occult | OccupationTag.Academic | OccupationTag.Investigative
        },
        new()
        {
            Name = "Полицейский", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2,
            CreditRatingMin = 9, CreditRatingMax = 30,
            OccupationSkills =
            [
                "Ближний бой (драка)", "Внимание", "Первая помощь", "Психология", "Стрельба",
                "Юриспруденция", "Средства"
            ],
            SkillChoices = [new() { Count = 1, Options = ["Верховая езда", "Вождение автомобиля"] }],
            SocialSkillSlots = 1,
            Tags = OccupationTag.Combat | OccupationTag.Investigative | OccupationTag.Social
        },
        new()
        {
            // «Четыре специализации следующих навыков» — четыре разных выбора из одного списка,
            // а не четыре произвольных навыка: свободными слотами это не выражается.
            Name = "Преступник", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2,
            CreditRatingMin = 5, CreditRatingMax = 65,
            OccupationSkills = ["Внимание", "Психология", "Скрытность", "Средства"],
            SkillChoices =
            [
                new()
                {
                    Count = 4,
                    Options =
                        ["Ближний бой", "Взлом", "Ловкость рук", "Маскировка", "Механика", "Оценка", "Стрельба"]
                }
            ],
            SocialSkillSlots = 1,
            Tags = OccupationTag.Criminal | OccupationTag.Stealth | OccupationTag.Social
        },
        new()
        {
            Name = "Священник", SkillPointFormula = OccupationSkillPointFormula.Edu4,
            CreditRatingMin = 9, CreditRatingMax = 60,
            OccupationSkills =
            [
                "Бухгалтерское дело", "Язык, иностранный", "История", "Психология", "Работа в библиотеке",
                "Слух", "Средства"
            ],
            SocialSkillSlots = 1, FreeSkillSlots = 1,
            Tags = OccupationTag.Social | OccupationTag.Academic | OccupationTag.Occult
        },
        new()
        {
            Name = "Солдат", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2,
            CreditRatingMin = 9, CreditRatingMax = 30,
            OccupationSkills = ["Ближний бой", "Выживание", "Скрытность", "Стрельба", "Уклонение", "Средства"],
            SkillChoices =
            [
                new() { Count = 1, Options = ["Лазание", "Плавание"] },
                new() { Count = 2, Options = ["Язык, иностранный", "Механика", "Первая помощь"] }
            ],
            Tags = OccupationTag.Combat | OccupationTag.Physical | OccupationTag.Outdoor
        },
        new()
        {
            Name = "Спортсмен", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2,
            CreditRatingMin = 9, CreditRatingMax = 70,
            OccupationSkills =
            [
                "Ближний бой (драка)", "Верховая езда", "Лазание", "Метание", "Плавание", "Прыжки", "Средства"
            ],
            SocialSkillSlots = 1, FreeSkillSlots = 1,
            Tags = OccupationTag.Physical | OccupationTag.Combat
        },
        new()
        {
            Name = "Фанатик", SkillPointFormula = OccupationSkillPointFormula.Edu2AppOrPow2,
            CreditRatingMin = 0, CreditRatingMax = 30,
            OccupationSkills = ["История", "Психология", "Скрытность", "Средства"],
            SocialSkillSlots = 2, FreeSkillSlots = 3,
            Tags = OccupationTag.Social | OccupationTag.Occult | OccupationTag.Stealth
        },
        new()
        {
            Name = "Фермер", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2,
            CreditRatingMin = 9, CreditRatingMax = 30,
            OccupationSkills =
            [
                "Вождение автомобиля", "Естествознание", "Искусство/ремесло (сельское хозяйство)", "Механика",
                "Управление тяжёлыми машинами", "Чтение следов", "Средства"
            ],
            SocialSkillSlots = 1, FreeSkillSlots = 1,
            Tags = OccupationTag.Outdoor | OccupationTag.Physical | OccupationTag.Technical
        },
        new()
        {
            Name = "Художник", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrPow2,
            CreditRatingMin = 9, CreditRatingMax = 50,
            OccupationSkills = ["Внимание", "Язык, иностранный", "Искусство/ремесло", "Психология", "Средства"],
            SkillChoices = [new() { Count = 1, Options = ["Естествознание", "История"] }],
            SocialSkillSlots = 1, FreeSkillSlots = 2,
            Tags = OccupationTag.Artistic | OccupationTag.Investigative
        },
        new()
        {
            Name = "Частный сыщик", SkillPointFormula = OccupationSkillPointFormula.Edu2DexOrStr2,
            CreditRatingMin = 9, CreditRatingMax = 30,
            OccupationSkills =
            [
                "Внимание", "Искусство/ремесло (фотография)", "Маскировка", "Психология",
                "Работа в библиотеке", "Юриспруденция", "Средства"
            ],
            SocialSkillSlots = 1, FreeSkillSlots = 1,
            Tags = OccupationTag.Investigative | OccupationTag.Social | OccupationTag.Stealth
        },
        new()
        {
            Name = "Юрист", SkillPointFormula = OccupationSkillPointFormula.Edu4,
            CreditRatingMin = 30, CreditRatingMax = 80,
            OccupationSkills = ["Бухгалтерское дело", "Психология", "Работа в библиотеке", "Юриспруденция", "Средства"],
            SocialSkillSlots = 2, FreeSkillSlots = 2,
            Tags = OccupationTag.Academic | OccupationTag.Social | OccupationTag.Investigative
        },

        // === Не из «Примеров занятий»: этих трёх в русском тексте главы 3 нет ===
        // Достались приложению от самой ранней самодельной заготовки генератора и потому долго
        // жили с семью навыками вместо восьми. Состав выправлен по англоязычным правилам, где они
        // есть: Archaeologist — Roll20-компендиум CoC 7e, Accountant и Mechanic (and Skilled
        // Trades) — Investigator Handbook. Правило «ровно восемь плюс Средства» соблюдено.
        new()
        {
            Name = "Археолог", SkillPointFormula = OccupationSkillPointFormula.Edu4,
            CreditRatingMin = 10, CreditRatingMax = 40,
            OccupationSkills =
            [
                "Оценка", "Археология", "История", "Работа в библиотеке", "Внимание", "Механика",
                "Язык, иностранный", "Средства"
            ],
            SkillChoices = [new() { Count = 1, Options = ["Ориентирование", "Наука"] }],
            Tags = OccupationTag.Academic | OccupationTag.Investigative | OccupationTag.Outdoor
        },
        new()
        {
            Name = "Бухгалтер", SkillPointFormula = OccupationSkillPointFormula.Edu4,
            CreditRatingMin = 30, CreditRatingMax = 70,
            OccupationSkills =
            [
                "Бухгалтерское дело", "Юриспруденция", "Работа в библиотеке", "Слух", "Убеждение",
                "Внимание", "Средства"
            ],
            FreeSkillSlots = 2,
            Tags = OccupationTag.Academic | OccupationTag.Investigative
        },
        new()
        {
            // В оригинале это «Mechanic (and Skilled Trades)» — плотники, сварщики, электрики;
            // отсюда и слот «Искусство/ремесло», выбор специализации в котором и задаёт ремесло.
            Name = "Механик", SkillPointFormula = OccupationSkillPointFormula.Edu4,
            CreditRatingMin = 9, CreditRatingMax = 40,
            OccupationSkills =
            [
                "Искусство/ремесло", "Лазание", "Вождение автомобиля", "Электрика", "Механика",
                "Управление тяжёлыми машинами", "Средства"
            ],
            FreeSkillSlots = 2,
            Tags = OccupationTag.Technical | OccupationTag.Physical
        },

        // === Современные профессии ===
        new()
        {
            Name = "Хакер", SkillPointFormula = OccupationSkillPointFormula.Edu4,
            CreditRatingMin = 10, CreditRatingMax = 70,
            OccupationSkills =
                ["Внимание", "Работа в библиотеке", "Работа с компьютером", "Электрика", "Электроника", "Средства"],
            SocialSkillSlots = 1, FreeSkillSlots = 2, IsModern = true,
            Tags = OccupationTag.Technical | OccupationTag.Criminal | OccupationTag.Investigative
        }
    ];
}
