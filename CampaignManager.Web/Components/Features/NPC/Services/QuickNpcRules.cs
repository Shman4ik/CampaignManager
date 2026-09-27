using System.Text.RegularExpressions;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Characters.Services;
using CampaignManager.Web.Components.Features.Combat.Services;
using CampaignManager.Web.Components.Features.NPC.Model;
using CampaignManager.Web.Components.Features.Weapons.Model;
using CampaignManager.Web.Components.Features.Weapons.Services;
using CampaignManager.Web.Components.Shared.Model;

namespace CampaignManager.Web.Components.Features.NPC.Services;

/// <summary>
///     «Быстрый НПС»: лист персонажа Хранителя, придуманного по ходу игры (гл. 10, стр. 187–189).
///     Книга прямо говорит, что такому персонажу полный лист не нужен: навык, связанный с его
///     занятием, — 50% и выше, не связанный — ниже 50%, боевой — 25/40/70% по опытности, имя —
///     из заранее подготовленного списка под эпоху.
///     <para>
///         Лист при этом собирается настоящий: навыки ложатся на <see cref="SkillsModel.DefaultSkillsModel" />,
///         вторичные атрибуты считает <see cref="DerivedAttributeRules" />, оружие копируется из каталога
///         через <see cref="WeaponFactory.CopyForCharacter" />. Своих формул здесь нет.
///     </para>
/// </summary>
public static partial class QuickNpcRules
{
    /// <summary>Характеристика «среднего человека»: Комплекция 0, БкУ 0, 10 ПЗ.</summary>
    public const int TypicalCharacteristic = 50;

    public const int TypicalLuck = 50;

    /// <summary>Сколько строк ключевых навыков помещается в форму.</summary>
    public const int MaxSkillRows = 8;

    private const string ExtraSkillsGroupName = "Особые навыки";
    private const string BrawlSkill = "Ближний бой (драка)";
    private const string HandgunSkill = "Стрельба (пистолет)";
    private const string OwnLanguageSkill = "Языки (родной)";

    public sealed record CombatLevelOption(QuickNpcCombatLevel Level, string Label);

    /// <summary>Уровни боевого навыка из книги (стр. 187).</summary>
    public static readonly IReadOnlyList<CombatLevelOption> CombatLevels =
    [
        new(QuickNpcCombatLevel.Novice, "Неопытный"),
        new(QuickNpcCombatLevel.Brawler, "Забияка"),
        new(QuickNpcCombatLevel.Professional, "Профи")
    ];

    /// <summary>
    ///     Типаж: род занятий, ключевые навыки и опытность в бою. Навыки, связанные с занятием, —
    ///     от 50%, как велит книга; боевой навык добавляется отдельно по <see cref="Combat" />.
    /// </summary>
    public sealed record Archetype(
        string Key,
        string Label,
        string Icon,
        string Occupation,
        QuickNpcCombatLevel Combat,
        IReadOnlyList<(string Skill, int Value)> Skills,
        bool CarriesHandgun = false);

    public static readonly IReadOnlyList<Archetype> Archetypes =
    [
        new("police", "Полицейский", "fa-user-shield", "Полицейский", QuickNpcCombatLevel.Brawler,
            [("Внимание", 50), ("Запугивание", 50), ("Юриспруденция", 30)], CarriesHandgun: true),
        new("thug", "Громила", "fa-hand-fist", "Громила", QuickNpcCombatLevel.Brawler,
            [("Запугивание", 50), ("Скрытность", 30), ("Внимание", 30)]),
        new("barkeep", "Бармен", "fa-martini-glass", "Бармен", QuickNpcCombatLevel.Novice,
            [("Слух", 50), ("Психология", 50), ("Обаяние", 40)]),
        new("servant", "Прислуга", "fa-bell-concierge", "Прислуга", QuickNpcCombatLevel.Novice,
            [("Внимание", 40), ("Слух", 40), ("Скрытность", 35)]),
        new("reporter", "Журналист", "fa-newspaper", "Журналист", QuickNpcCombatLevel.Novice,
            [("Красноречие", 50), ("Работа в библиотеке", 50), ("Психология", 40), ("Внимание", 40)]),
        new("doctor", "Врач", "fa-user-doctor", "Врач", QuickNpcCombatLevel.Novice,
            [("Медицина", 60), ("Первая помощь", 60), ("Психология", 40)]),
        new("scholar", "Учёный", "fa-book", "Библиотекарь", QuickNpcCombatLevel.Novice,
            [("Работа в библиотеке", 60), ("История", 50), ("Оккультизм", 30)]),
        new("cultist", "Культист", "fa-skull", "Культист", QuickNpcCombatLevel.Brawler,
            [("Оккультизм", 40), ("Скрытность", 40), ("Убеждение", 35), ("Мифы Ктулху", 10)]),
        new("killer", "Убийца", "fa-user-ninja", "Наёмный убийца", QuickNpcCombatLevel.Professional,
            [("Скрытность", 60), ("Внимание", 50)], CarriesHandgun: true),
        new("bystander", "Обыватель", "fa-user", "Обыватель", QuickNpcCombatLevel.Novice,
            [("Внимание", 30), ("Слух", 30)])
    ];

    /// <summary>Все навыки чистого листа — подсказки для поля имени навыка.</summary>
    public static readonly IReadOnlyList<string> SheetSkillNames = SkillsModel.DefaultSkillsModel().SkillGroups
        .SelectMany(g => g.Skills)
        .Select(s => s.Name)
        .ToList();

    // ── Имена ──────────────────────────────────────────────────────
    // Книга советует держать под рукой список имён, подходящих времени и месту (стр. 189):
    // когда Хранитель выдумывает имя на ходу, игроки сразу понимают, что персонаж проходной.

    private static readonly string[] ClassicMaleNames =
    [
        "Джон", "Уильям", "Джордж", "Фрэнк", "Эдвард", "Генри", "Уолтер", "Артур", "Гарольд", "Альберт",
        "Кларенс", "Эрнест", "Честер", "Хорас", "Элмер", "Хайрам", "Эзра", "Сайлас", "Мартин", "Лерой"
    ];

    private static readonly string[] ClassicFemaleNames =
    [
        "Мэри", "Хелен", "Дороти", "Маргарет", "Рут", "Милдред", "Этель", "Глэдис", "Эдна", "Флоренс",
        "Бернис", "Хейзел", "Мейбл", "Ирма", "Люсиль", "Вайолет", "Агнес", "Элси", "Перл", "Корделия"
    ];

    private static readonly string[] ClassicSurnames =
    [
        "Смит", "Джонсон", "Браун", "Миллер", "Уилсон", "Мур", "Тейлор", "Андерсон", "Джексон", "Уайт",
        "Харрис", "Томпсон", "Кларк", "Льюис", "Уокер", "Холл", "Аллен", "Кинг", "Райт", "Хилл",
        "Грин", "Бейкер", "Адамс", "Картер", "Митчелл", "О'Брайен", "Мёрфи", "Келли", "Салливан",
        "Ковальски", "Шмидт", "Росси", "Коэн", "Уэйтли", "Марш", "Пикман"
    ];

    private static readonly string[] ModernMaleNames =
    [
        "Майкл", "Кристофер", "Джейсон", "Дэвид", "Брайан", "Кевин", "Мэттью", "Джошуа", "Эндрю", "Райан",
        "Тайлер", "Джастин", "Брэндон", "Эрик", "Скотт", "Дэниел", "Нейтан", "Итан", "Логан", "Карлос"
    ];

    private static readonly string[] ModernFemaleNames =
    [
        "Дженнифер", "Джессика", "Эшли", "Аманда", "Сара", "Стефани", "Николь", "Мелисса", "Лорен", "Эмили",
        "Ханна", "Меган", "Рэйчел", "Кайла", "Саманта", "Эмма", "Оливия", "Хлоя", "Мэдисон", "Мария"
    ];

    private static readonly string[] ModernSurnames =
    [
        "Смит", "Джонсон", "Уильямс", "Браун", "Джонс", "Гарсия", "Миллер", "Дэвис", "Родригес", "Мартинес",
        "Эрнандес", "Лопес", "Гонсалес", "Уилсон", "Андерсон", "Томас", "Тейлор", "Мур", "Ли", "Нгуен",
        "Пател", "Ким", "Чен", "Томпсон", "Уайт", "Харрис", "Кларк", "Льюис", "Робинсон", "Уокер"
    ];

    /// <summary>Случайное имя под эпоху: 1920-е — одни имена, современность — другие.</summary>
    public static string RandomName(Eras era, bool female)
    {
        var modern = IsModern(era);
        var first = female
            ? modern ? ModernFemaleNames : ClassicFemaleNames
            : modern ? ModernMaleNames : ClassicMaleNames;
        var last = modern ? ModernSurnames : ClassicSurnames;

        return $"{Pick(first)} {Pick(last)}";
    }

    /// <summary>Современность — только когда эпоха названа одна и она не классическая (как у генератора).</summary>
    public static bool IsModern(Eras era) => era.HasFlag(Eras.Modern) && !era.HasFlag(Eras.Classic);

    /// <summary>
    ///     Эпоха сценария: у него она свободной строкой («1920-е», «Современность», «2010-е»).
    ///     Не распознали — 1920-е, классика «Зова Ктулху».
    /// </summary>
    public static Eras EraFromText(string? era)
    {
        if (string.IsNullOrWhiteSpace(era)) return Eras.Classic;

        var text = era.Trim().ToLowerInvariant();
        if (text.Contains("соврем") || text.Contains("modern") || text.Contains("наши дни"))
            return Eras.Modern;

        // Год целиком: «1920-е» и «1890-е» — классика, «2010-е» и «2023» — современность.
        // Короткое «20-е» года не содержит и остаётся классикой.
        var year = YearPattern().Match(text);
        return year.Success && year.Value.StartsWith("20", StringComparison.Ordinal) ? Eras.Modern : Eras.Classic;
    }

    public static string EraLabel(Eras era) => IsModern(era) ? "современность" : "1920-е";

    // ── Характеристики ─────────────────────────────────────────────

    public static Dictionary<CharacteristicKey, int> TypicalCharacteristics() =>
        InvestigatorCreationRules.Characteristics.ToDictionary(c => c.Key, _ => TypicalCharacteristic);

    /// <summary>Бросок по правилам главы 3 (стр. 28–29): 3d6 × 5 или (2d6 + 6) × 5.</summary>
    public static Dictionary<CharacteristicKey, int> RollCharacteristics() =>
        InvestigatorCreationRules.Characteristics.ToDictionary(
            c => c.Key,
            c => InvestigatorCreationRules.Roll(c.Key).Value);

    public static int RollLuck() => InvestigatorCreationRules.RollLuck().Value;

    public static Characteristics ToCharacteristics(IReadOnlyDictionary<CharacteristicKey, int> values)
    {
        return new Characteristics
        {
            Strength = Attribute(CharacteristicKey.Strength),
            Constitution = Attribute(CharacteristicKey.Constitution),
            Size = Attribute(CharacteristicKey.Size),
            Dexterity = Attribute(CharacteristicKey.Dexterity),
            Appearance = Attribute(CharacteristicKey.Appearance),
            Intelligence = Attribute(CharacteristicKey.Intelligence),
            Power = Attribute(CharacteristicKey.Power),
            Education = Attribute(CharacteristicKey.Education)
        };

        AttributeValue Attribute(CharacteristicKey key) =>
            new(ClampCharacteristic(values.GetValueOrDefault(key, TypicalCharacteristic)));
    }

    public static int ClampCharacteristic(int value) =>
        Math.Clamp(value, 1, InvestigatorCreationRules.MaxCharacteristic);

    public static int ClampSkill(int value) => Math.Clamp(value, 0, 99);

    // ── Навыки ─────────────────────────────────────────────────────

    /// <summary>
    ///     Заполняет форму типажом: род занятий, ключевые навыки и опытность в бою.
    ///     Имя, характеристики и оружие не трогает — их Хранитель мог уже выбрать.
    /// </summary>
    public static void ApplyArchetype(QuickNpcDraft draft, Archetype archetype)
    {
        draft.ArchetypeKey = archetype.Key;
        draft.Occupation = archetype.Occupation;
        draft.Skills = archetype.Skills
            .Select(s => new QuickNpcSkill { Name = s.Skill, Value = s.Value })
            .ToList();

        // Строка оружия переживает смену типажа: оружие выбрано отдельно от него.
        var weaponSkill = draft.WeaponSkillName;
        draft.WeaponSkillName = null;

        ApplyCombatLevel(draft, archetype.Combat);

        if (archetype.CarriesHandgun)
            UpsertSkill(draft, HandgunSkill, (int)draft.CombatLevel);

        if (weaponSkill is not null)
        {
            UpsertSkill(draft, weaponSkill, (int)draft.CombatLevel);
            draft.WeaponSkillName = weaponSkill;
        }
    }

    /// <summary>
    ///     Опытность в бою: все боевые строки формы получают её значение, а драка добавляется,
    ///     если её ещё нет, — без неё НПС нечем ответить на удар.
    /// </summary>
    public static void ApplyCombatLevel(QuickNpcDraft draft, QuickNpcCombatLevel level)
    {
        draft.CombatLevel = level;

        foreach (var row in draft.Skills.Where(r => IsCombatSkill(r.Name)))
            row.Value = (int)level;

        if (!draft.Skills.Any(r => string.Equals(r.Name.Trim(), BrawlSkill, StringComparison.OrdinalIgnoreCase)))
            draft.Skills.Add(new QuickNpcSkill { Name = BrawlSkill, Value = (int)level });
    }

    /// <summary>
    ///     Выбранное оружие: навык, которым им бьют, получает боевое значение. Навык из прошлого
    ///     выбора оружия убирается, если его добавила форма, а не Хранитель или типаж.
    /// </summary>
    public static void ApplyWeapon(QuickNpcDraft draft, Weapon? weapon)
    {
        if (draft.WeaponSkillName is { } previous
            && !string.Equals(previous, BrawlSkill, StringComparison.OrdinalIgnoreCase))
        {
            draft.Skills.RemoveAll(r => string.Equals(r.Name.Trim(), previous, StringComparison.OrdinalIgnoreCase));
        }

        draft.WeaponId = weapon?.Id;
        draft.WeaponSkillName = null;

        if (weapon is null || string.IsNullOrWhiteSpace(weapon.Skill)) return;

        var skillName = ResolveWeaponSkill(weapon.Skill) ?? weapon.Skill.Trim();
        var alreadyThere = draft.Skills.Any(r => string.Equals(r.Name.Trim(), skillName, StringComparison.OrdinalIgnoreCase));

        UpsertSkill(draft, skillName, (int)draft.CombatLevel);
        if (!alreadyThere)
            draft.WeaponSkillName = skillName;
    }

    /// <summary>Боевые навыки — те, что поднимает уровень опытности в бою.</summary>
    public static bool IsCombatSkill(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        var trimmed = name.Trim();
        return trimmed.StartsWith("Ближний бой", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("Стрельба", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("Метание", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("Автомат", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Строка чистого листа, которую бой возьмёт для этого оружия. Ищет её тот же
    ///     <see cref="SkillNameMatcher.FindBest{T}" />, что и
    ///     <see cref="CombatService.FindSkillValue(Character, string)" />: полное имя, затем база и
    ///     специализация по отдельности («Стрельба (П)» — это «Стрельба (пистолет)»).
    ///     <c>null</c> — такой строки на листе нет, навык нужно завести под именем из каталога.
    /// </summary>
    public static string? ResolveWeaponSkill(string weaponSkill) =>
        SkillNameMatcher.FindBest(SheetSkillNames, n => n, weaponSkill);

    private static void UpsertSkill(QuickNpcDraft draft, string name, int value)
    {
        var row = draft.Skills.FirstOrDefault(r => string.Equals(r.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
        if (row is null)
            draft.Skills.Add(new QuickNpcSkill { Name = name, Value = value });
        else
            row.Value = Math.Max(row.Value, value);
    }

    // ── Лист ───────────────────────────────────────────────────────

    /// <summary>
    ///     Собирает валидный лист НПС. Навыки из формы ложатся на чистый лист по имени (без учёта
    ///     регистра и «ё»); не нашедшиеся уходят в группу «Особые навыки», как при импорте сценария.
    /// </summary>
    public static Character BuildCharacter(QuickNpcDraft draft, Weapon? catalogWeapon)
    {
        var character = new Character
        {
            PersonalInfo = new PersonalInfo
            {
                Name = draft.Name.Trim(),
                Occupation = draft.Occupation.Trim(),
                Age = Math.Clamp(draft.Age, InvestigatorCreationRules.MinAge, InvestigatorCreationRules.MaxAge),
                Gender = draft.IsFemale ? "Женский" : "Мужской"
            },
            Characteristics = ToCharacteristics(draft.Characteristics),
            Skills = SkillsModel.DefaultSkillsModel(),
            Backstory = draft.Note?.Trim() ?? string.Empty
        };

        // Родной язык по правилам равен ОБР (стр. 77): на чистом листе он стоит нулём.
        var ownLanguage = FindSkill(character.Skills, OwnLanguageSkill);
        if (ownLanguage is not null)
            ownLanguage.Value = new AttributeValue(character.Characteristics.Education.Regular);

        foreach (var row in draft.Skills.Where(r => !string.IsNullOrWhiteSpace(r.Name)))
            SetSkill(character.Skills, row.Name.Trim(), ClampSkill(row.Value));

        // После навыков: Уклонение не опускается ниже половины ЛВК, но вписанное выше — остаётся.
        DerivedAttributeRules.InitializeNewSheet(character);
        character.DerivedAttributes.Luck = new AttributeWithMaxValue(
            Math.Clamp(draft.Luck, 0, DerivedAttributeRules.MaxLuck), DerivedAttributeRules.MaxLuck);

        if (catalogWeapon is not null)
            character.Weapons.Add(WeaponFactory.CopyForCharacter(catalogWeapon));

        return character;
    }

    private static void SetSkill(SkillsModel skills, string name, int value)
    {
        var skill = FindSkill(skills, name);
        if (skill is not null)
        {
            skill.Value = new AttributeValue(value);
            return;
        }

        var extras = skills.SkillGroups.FirstOrDefault(g => g.Name == ExtraSkillsGroupName);
        if (extras is null)
        {
            extras = new SkillGroup { Name = ExtraSkillsGroupName };
            skills.SkillGroups.Add(extras);
        }

        extras.AddSkill(new Skill { Name = name, Value = new AttributeValue(value), BaseValue = "—" });
    }

    private static Skill? FindSkill(SkillsModel skills, string name) =>
        skills.SkillGroups
            .SelectMany(g => g.Skills)
            .FirstOrDefault(s => SameName(s.Name, name));

    private static bool SameName(string left, string right) =>
        string.Equals(
            left.Trim().Replace('ё', 'е').Replace('Ё', 'Е'),
            right.Trim().Replace('ё', 'е').Replace('Ё', 'Е'),
            StringComparison.OrdinalIgnoreCase);

    private static string Pick(string[] values) => values[Random.Shared.Next(values.Length)];

    [GeneratedRegex(@"(?<!\d)(18|19|20)\d{2}(?!\d)")]
    private static partial Regex YearPattern();
}
