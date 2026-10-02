using System.Text.RegularExpressions;

namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Профессии книги для «Синхронизировать с правилами» (T2.1): только коды, числа и названия навыков —
/// текстов книги здесь нет (D5). Перенесено из <c>Occupation.GetDefaultOccupations()</c> v1 («Примеры
/// занятий», стр. 37–39). У каждой профессии ровно восемь профессиональных навыков плюс Средства:
/// <see cref="OccupationSeedRow.ProfessionalCount"/> == 8, Средства в слоты не входят (их даёт диапазон).
/// Навыки записаны именами справочника; код даёт <see cref="SkillCodes.FromName"/>, а «Родитель
/// (специализация)», которой нет в таблице навыков («латынь»), — <see cref="ResolveSkill"/>.
/// Археолог, Бухгалтер и Механик — из англоязычных правил: в русских «Примерах занятий» их нет.
/// Современных профессий нет (решение владельца 2026-10-02: современная эпоха не переносится), поэтому
/// «Хакера» v1 в сиде нет — как и его кода в <see cref="OccupationCodes"/>.
/// </summary>
public static partial class OccupationSeed
{
    public static IReadOnlyList<OccupationSeedRow> Rows { get; } =
    [
        new("occupation.antiquarian", SkillPointsFormula.Edu4, 30, 70)
        {
            Skills = ["Внимание", "Язык, иностранный", "Искусство/ремесло", "История", "Оценка", "Работа в библиотеке"],
            Social = 1,
            Free = 1,
            IsLovecraftian = true,
            Tags = ["Academic", "Investigative", "Scholarly"],
        },
        new("occupation.librarian", SkillPointsFormula.Edu4, 9, 35)
        {
            Skills = ["Бухгалтерское дело", "Язык, иностранный", "Работа в библиотеке", "Язык, родной"],
            Free = 4,
            IsLovecraftian = true,
            Tags = ["Academic", "Investigative", "Scholarly"],
        },
        new("occupation.doctor-of-medicine", SkillPointsFormula.Edu4, 30, 80)
        {
            Skills = ["Язык, иностранный (латынь)", "Медицина", "Наука (биология)", "Наука (фармакология)", "Первая помощь", "Психология"],
            Free = 2,
            IsLovecraftian = true,
            Tags = ["Medical", "Academic", "Investigative"],
        },
        new("occupation.police-detective", SkillPointsFormula.Edu2DexOrStr2, 20, 50)
        {
            Skills = ["Внимание", "Психология", "Слух", "Стрельба", "Ориентирование"],
            Choices = [new(1, ["Искусство/ремесло (актёрская игра)", "Маскировка"])],
            Social = 1,
            Free = 1,
            IsLovecraftian = true,
            Tags = ["Investigative", "Social", "Combat"],
        },
        new("occupation.dilettante", SkillPointsFormula.Edu2App2, 50, 99)
        {
            Skills = ["Верховая езда", "Язык, иностранный", "Искусство/ремесло", "Стрельба"],
            Social = 1,
            Free = 3,
            IsLovecraftian = true,
            Tags = ["Social", "Artistic", "Language"],
        },
        new("occupation.journalist", SkillPointsFormula.Edu4, 9, 30)
        {
            Skills = ["Искусство/ремесло (фотография)", "История", "Психология", "Работа в библиотеке", "Язык, родной"],
            Social = 1,
            Free = 2,
            IsLovecraftian = true,
            Tags = ["Investigative", "Social", "Academic"],
        },
        new("occupation.author", SkillPointsFormula.Edu4, 9, 30)
        {
            Skills = ["Язык, иностранный", "Искусство/ремесло (литература)", "История", "Психология", "Работа в библиотеке", "Язык, родной"],
            Choices = [new(1, ["Естествознание", "Оккультизм"])],
            Free = 1,
            IsLovecraftian = true,
            Tags = ["Academic", "Artistic", "Language"],
        },
        new("occupation.professor", SkillPointsFormula.Edu4, 20, 70)
        {
            Skills = ["Язык, иностранный", "Психология", "Работа в библиотеке", "Язык, родной"],
            Free = 4,
            IsLovecraftian = true,
            Tags = ["Academic", "Scholarly", "Language"],
        },
        new("occupation.entertainer", SkillPointsFormula.Edu2App2, 9, 70)
        {
            Skills = ["Искусство/ремесло (актёрская игра)", "Маскировка", "Психология", "Слух"],
            Social = 2,
            Free = 2,
            Tags = ["Artistic", "Social"],
        },
        new("occupation.drifter", SkillPointsFormula.Edu2AppOrDexOrStr2, 0, 5)
        {
            Skills = ["Лазание", "Ориентирование", "Прыжки", "Скрытность", "Слух"],
            Social = 1,
            Free = 2,
            Tags = ["Physical", "Stealth", "Outdoor"],
        },
        new("occupation.military-officer", SkillPointsFormula.Edu2DexOrStr2, 20, 70)
        {
            Skills = ["Бухгалтерское дело", "Выживание", "Ориентирование", "Психология", "Стрельба"],
            Social = 2,
            Free = 1,
            Tags = ["Combat", "Social", "Outdoor"],
        },
        new("occupation.tribe-member", SkillPointsFormula.Edu2DexOrStr2, 0, 15)
        {
            Skills = ["Внимание", "Выживание", "Естествознание", "Лазание", "Оккультизм", "Плавание", "Слух"],
            Choices = [new(1, ["Ближний бой", "Метание"])],
            Tags = ["Physical", "Outdoor", "Occult"],
        },
        new("occupation.engineer", SkillPointsFormula.Edu4, 30, 60)
        {
            Skills = ["Искусство/ремесло (черчение)", "Механика", "Наука (инженерия)", "Наука (физика)", "Работа в библиотеке", "Управление тяжёлыми машинами", "Электрика"],
            Free = 1,
            Tags = ["Technical", "Academic"],
        },
        new("occupation.pilot", SkillPointsFormula.Edu2Dex2, 20, 70)
        {
            Skills = ["Механика", "Наука (астрономия)", "Ориентирование", "Пилотирование (самолёт)", "Управление тяжёлыми машинами", "Электрика"],
            Free = 2,
            Tags = ["Technical", "Physical"],
        },
        new("occupation.missionary", SkillPointsFormula.Edu4, 0, 30)
        {
            Skills = ["Естествознание", "Искусство/ремесло", "Медицина", "Механика", "Первая помощь"],
            Social = 1,
            Free = 2,
            Tags = ["Social", "Medical", "Outdoor"],
        },
        new("occupation.musician", SkillPointsFormula.Edu2DexOrPow2, 9, 30)
        {
            Skills = ["Искусство/ремесло (музыкальный инструмент)", "Психология", "Слух"],
            Social = 1,
            Free = 4,
            Tags = ["Artistic", "Social"],
        },
        new("occupation.parapsychologist", SkillPointsFormula.Edu4, 9, 30)
        {
            Skills = ["Антропология", "Язык, иностранный", "Искусство/ремесло (фотография)", "История", "Оккультизм", "Психология", "Работа в библиотеке"],
            Free = 1,
            Tags = ["Occult", "Academic", "Investigative"],
        },
        new("occupation.police-officer", SkillPointsFormula.Edu2DexOrStr2, 9, 30)
        {
            Skills = ["Ближний бой (драка)", "Внимание", "Первая помощь", "Психология", "Стрельба", "Юриспруденция"],
            Choices = [new(1, ["Верховая езда", "Вождение автомобиля"])],
            Social = 1,
            Tags = ["Combat", "Investigative", "Social"],
        },
        new("occupation.criminal", SkillPointsFormula.Edu2DexOrStr2, 5, 65)
        {
            Skills = ["Внимание", "Психология", "Скрытность"],
            // «Четыре специализации следующих навыков» — четыре разных выбора из одного списка.
            Choices = [new(4, ["Ближний бой", "Взлом", "Ловкость рук", "Маскировка", "Механика", "Оценка", "Стрельба"])],
            Social = 1,
            Tags = ["Criminal", "Stealth", "Social"],
        },
        new("occupation.clergy", SkillPointsFormula.Edu4, 9, 60)
        {
            Skills = ["Бухгалтерское дело", "Язык, иностранный", "История", "Психология", "Работа в библиотеке", "Слух"],
            Social = 1,
            Free = 1,
            Tags = ["Social", "Academic", "Occult"],
        },
        new("occupation.soldier", SkillPointsFormula.Edu2DexOrStr2, 9, 30)
        {
            Skills = ["Ближний бой", "Выживание", "Скрытность", "Стрельба", "Уклонение"],
            Choices = [new(1, ["Лазание", "Плавание"]), new(2, ["Язык, иностранный", "Механика", "Первая помощь"])],
            Tags = ["Combat", "Physical", "Outdoor"],
        },
        new("occupation.athlete", SkillPointsFormula.Edu2DexOrStr2, 9, 70)
        {
            Skills = ["Ближний бой (драка)", "Верховая езда", "Лазание", "Метание", "Плавание", "Прыжки"],
            Social = 1,
            Free = 1,
            Tags = ["Physical", "Combat"],
        },
        new("occupation.zealot", SkillPointsFormula.Edu2AppOrPow2, 0, 30)
        {
            Skills = ["История", "Психология", "Скрытность"],
            Social = 2,
            Free = 3,
            Tags = ["Social", "Occult", "Stealth"],
        },
        new("occupation.farmer", SkillPointsFormula.Edu2DexOrStr2, 9, 30)
        {
            Skills = ["Вождение автомобиля", "Естествознание", "Искусство/ремесло (сельское хозяйство)", "Механика", "Управление тяжёлыми машинами", "Чтение следов"],
            Social = 1,
            Free = 1,
            Tags = ["Outdoor", "Physical", "Technical"],
        },
        new("occupation.artist", SkillPointsFormula.Edu2DexOrPow2, 9, 50)
        {
            Skills = ["Внимание", "Язык, иностранный", "Искусство/ремесло", "Психология"],
            Choices = [new(1, ["Естествознание", "История"])],
            Social = 1,
            Free = 2,
            Tags = ["Artistic", "Investigative"],
        },
        new("occupation.private-investigator", SkillPointsFormula.Edu2DexOrStr2, 9, 30)
        {
            Skills = ["Внимание", "Искусство/ремесло (фотография)", "Маскировка", "Психология", "Работа в библиотеке", "Юриспруденция"],
            Social = 1,
            Free = 1,
            Tags = ["Investigative", "Social", "Stealth"],
        },
        new("occupation.lawyer", SkillPointsFormula.Edu4, 30, 80)
        {
            Skills = ["Бухгалтерское дело", "Психология", "Работа в библиотеке", "Юриспруденция"],
            Social = 2,
            Free = 2,
            Tags = ["Academic", "Social", "Investigative"],
        },
        new("occupation.archaeologist", SkillPointsFormula.Edu4, 10, 40)
        {
            Skills = ["Оценка", "Археология", "История", "Работа в библиотеке", "Внимание", "Механика", "Язык, иностранный"],
            Choices = [new(1, ["Ориентирование", "Наука"])],
            Tags = ["Academic", "Investigative", "Outdoor"],
        },
        new("occupation.accountant", SkillPointsFormula.Edu4, 30, 70)
        {
            Skills = ["Бухгалтерское дело", "Юриспруденция", "Работа в библиотеке", "Слух", "Убеждение", "Внимание"],
            Free = 2,
            Tags = ["Academic", "Investigative"],
        },
        new("occupation.mechanic", SkillPointsFormula.Edu4, 9, 40)
        {
            Skills = ["Искусство/ремесло", "Лазание", "Вождение автомобиля", "Электрика", "Механика", "Управление тяжёлыми машинами"],
            Free = 2,
            Tags = ["Technical", "Physical"],
        },
    ];

    /// <summary>
    /// Навык из записи сида: код справочника и, если такой специализации в таблице нет, её название
    /// при родителе («Язык, иностранный (латынь)» → <c>skill.language-other</c> + «латынь»). null — имя
    /// не распознано (ошибка в сиде; тест это ловит).
    /// </summary>
    public static (string Code, string? Specialization)? ResolveSkill(string name)
    {
        if (SkillCodes.FromName(name) is { } code)
        {
            return (code, null);
        }

        var match = SpecializationName().Match(name);
        return match.Success && SkillCodes.FromName(match.Groups["parent"].Value) is { } parent
            ? (parent, match.Groups["spec"].Value.Trim())
            : null;
    }

    /// <summary>Есть ли у навыка специализации в таблице кодов — тогда слот «Стрельба» значит «любая стрельба».</summary>
    public static bool HasSpecializations(string code) =>
        SkillCodes.BookNames.Keys.Any(other => other.StartsWith(code + ".", StringComparison.Ordinal));

    [GeneratedRegex(@"^(?<parent>.+?)\s*\((?<spec>[^()]+)\)$")]
    private static partial Regex SpecializationName();
}

/// <summary>Профессия сида. <see cref="Code"/> — из <see cref="OccupationCodes"/>, имя — оттуда же.</summary>
public sealed record OccupationSeedRow(string Code, SkillPointsFormula Formula, int CreditRatingMin, int CreditRatingMax)
{
    /// <summary>Названные навыки (без Средств), именами справочника.</summary>
    public IReadOnlyList<string> Skills { get; init; } = [];

    public IReadOnlyList<OccupationSeedChoice> Choices { get; init; } = [];

    /// <summary>Слоты «любой социальный навык».</summary>
    public int Social { get; init; }

    /// <summary>Слоты «любой навык».</summary>
    public int Free { get; init; }

    public bool IsLovecraftian { get; init; }

    /// <summary>Классика: современная эпоха не переносится, как и в переносе T1.3.</summary>
    public IReadOnlyList<Era> Eras { get; init; } = [Era.Classic];

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string Name => OccupationCodes.Table.BookNames[Code];

    /// <summary>Профессиональных навыков без Средств — по книге ровно восемь.</summary>
    public int ProfessionalCount => Skills.Count + Choices.Sum(c => c.Count) + Social + Free;
}

/// <summary>«Выбрать <paramref name="Count"/> из перечисленных».</summary>
public sealed record OccupationSeedChoice(int Count, IReadOnlyList<string> Options);
