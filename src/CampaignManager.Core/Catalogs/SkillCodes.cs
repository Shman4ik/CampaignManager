namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Коды книжных навыков (<c>skills.code</c>) — стабильный ключ, по которому правила, перенос (T1.3) и
/// сиды находят навык. Код — английское название навыка из книги «Call of Cthulhu» 7e в kebab-case
/// (<c>skill.dodge</c>, <c>skill.cthulhu-mythos</c>); специализация — код родителя, точка и английское
/// название специализации (<c>skill.fighting.brawl</c>, <c>skill.firearms.handgun</c>). Из русского имени
/// код не вычисляется: соответствие задаёт явная таблица <see cref="BookNames"/> плюс написания v1
/// (<see cref="FromName"/>). Самодельные навыки кода не имеют (null). В таблице только названия и
/// коды — текстов книги здесь нет (D5).
/// </summary>
public static class SkillCodes
{
    public const string Prefix = "skill.";

    // Навыки, на которые опираются правила. В v1 их искали по строковым именам («Мифы Ктулху» — в 10
    // файлах, у родного языка два написания); в 2.0 — по коду.
    public const string Mythos = "skill.cthulhu-mythos";
    public const string CreditRating = "skill.credit-rating";
    public const string Dodge = "skill.dodge";
    public const string LanguageOwn = "skill.language-own";
    public const string LanguageForeign = "skill.language-other";
    public const string Fighting = "skill.fighting";
    public const string Firearms = "skill.firearms";
    public const string Survival = "skill.survival";
    public const string Intimidate = "skill.intimidate";
    public const string FastTalk = "skill.fast-talk";
    public const string Charm = "skill.charm";
    public const string Persuade = "skill.persuade";

    /// <summary>«Один социальный навык (Запугивание, Красноречие, Обаяние или Убеждение)» (стр. 38).</summary>
    public static IReadOnlyList<string> Social { get; } = [Intimidate, FastTalk, Charm, Persuade];

    /// <summary>
    /// Книжные навыки: код → имя в справочнике (93 навыка справочника v1). Порядок — как в справочнике
    /// по категориям; новый книжный навык — новая строка здесь, код однажды выданный не меняется.
    /// </summary>
    private static readonly (string Code, string Name, string[] V1Names)[] Table =
    [
        // Действия
        ("skill.ride", "Верховая езда", []),
        ("skill.drive-auto", "Вождение автомобиля", ["Вождение"]),
        ("skill.climb", "Лазание", []),
        ("skill.pilot", "Пилотирование", []),
        ("skill.pilot.boat", "Пилотирование (лодка)", []),
        ("skill.pilot.aircraft", "Пилотирование (самолёт)", []),
        ("skill.swim", "Плавание", []),
        ("skill.diving", "Подводное плавание", []),
        ("skill.jump", "Прыжки", []),
        ("skill.operate-heavy-machinery", "Управление тяжёлыми машинами", ["Упр. тяж. машинами"]),

        // Сражение (огнестрельное)
        ("skill.artillery", "Артиллерия", []),
        (Firearms, "Стрельба", []),
        ("skill.firearms.rifle-shotgun", "Стрельба (винтовка/дробовик)", ["Стрельба (винт./дроб.)"]),
        ("skill.firearms.bow", "Стрельба (лук)", []),
        ("skill.firearms.flamethrower", "Стрельба (огнемёт)", []),
        ("skill.firearms.handgun", "Стрельба (пистолет)", []),
        ("skill.firearms.submachine-gun", "Стрельба (пистолет-пулемёт)", ["Автомат"]),
        ("skill.firearms.machine-gun", "Стрельба (пулемёт)", []),
        ("skill.firearms.heavy-weapons", "Стрельба (тяжёлое вооружение)", []),

        // Сражение (общее)
        (Fighting, "Ближний бой", []),
        ("skill.fighting.chainsaw", "Ближний бой (бензопила)", []),
        ("skill.fighting.brawl", "Ближний бой (драка)", []),
        ("skill.fighting.spear", "Ближний бой (копьё)", []),
        ("skill.fighting.sword", "Ближний бой (меч)", []),
        ("skill.fighting.axe", "Ближний бой (топор)", []),
        ("skill.fighting.garrote", "Ближний бой (удавка)", []),
        ("skill.fighting.whip", "Ближний бой (хлыст)", []),
        ("skill.fighting.flail", "Ближний бой (цеп)", []),
        ("skill.throw", "Метание", []),
        (Dodge, "Уклонение", []),

        // Лечение
        ("skill.hypnosis", "Гипноз", []),
        ("skill.medicine", "Медицина", []),
        ("skill.science.pharmacy", "Наука (фармакология)", []),
        ("skill.first-aid", "Первая помощь", []),
        ("skill.psychoanalysis", "Психоанализ", []),

        // Сбор информации
        ("skill.spot-hidden", "Внимание", []),
        ("skill.science.forensics", "Наука (криминалистика)", []),
        ("skill.library-use", "Работа в библиотеке", []),
        ("skill.listen", "Слух", []),

        // Знания
        ("skill.anthropology", "Антропология", []),
        ("skill.archaeology", "Археология", []),
        ("skill.accounting", "Бухгалтерское дело", []),
        ("skill.natural-world", "Естествознание", []),
        ("skill.history", "История", []),
        ("skill.science", "Наука", []),
        ("skill.science.astronomy", "Наука (астрономия)", []),
        ("skill.science.biology", "Наука (биология)", []),
        ("skill.science.botany", "Наука (ботаника)", []),
        ("skill.science.geology", "Наука (геология)", []),
        ("skill.science.zoology", "Наука (зоология)", []),
        ("skill.science.engineering", "Наука (инженерия)", []),
        ("skill.science.cryptography", "Наука (криптография)", []),
        ("skill.science.mathematics", "Наука (математика)", []),
        ("skill.science.meteorology", "Наука (метеорология)", []),
        ("skill.science.physics", "Наука (физика)", []),
        ("skill.science.chemistry", "Наука (химия)", []),
        ("skill.occult", "Оккультизм", []),
        ("skill.appraise", "Оценка", []),
        ("skill.law", "Юриспруденция", []),

        // Решение проблем
        ("skill.locksmith", "Взлом", []),
        ("skill.demolitions", "Взрывчатка", []),
        (Survival, "Выживание", []),
        ("skill.survival.sea", "Выживание (море)", []),
        ("skill.survival.arctic", "Выживание (полярные области)", []),
        ("skill.survival.desert", "Выживание (пустыня)", []),
        ("skill.sleight-of-hand", "Ловкость рук", []),
        ("skill.mechanical-repair", "Механика", []),
        ("skill.navigate", "Ориентирование", []),
        ("skill.computer-use", "Работа с компьютером", []),
        ("skill.stealth", "Скрытность", []),
        ("skill.track", "Чтение следов", []),
        ("skill.electrical-repair", "Электрика", []),
        ("skill.electronics", "Электроника", []),

        // Социальные
        (Intimidate, "Запугивание", []),
        (FastTalk, "Красноречие", []),
        ("skill.disguise", "Маскировка", []),
        (Charm, "Обаяние", []),
        ("skill.psychology", "Психология", []),
        (Persuade, "Убеждение", []),
        ("skill.read-lips", "Чтение по губам", []),
        (LanguageForeign, "Язык, иностранный", ["Языки (иностр.)"]),
        ("skill.language-other.english", "Язык, иностранный (английский)", []),
        (LanguageOwn, "Язык, родной", ["Языки (родной)"]),

        // Специальные
        ("skill.art-craft", "Искусство/ремесло", []),
        ("skill.art-craft.acting", "Искусство/ремесло (актёрская игра)", []),
        ("skill.art-craft.fine-art", "Искусство/ремесло (изобразительное искусство)", []),
        ("skill.art-craft.forgery", "Искусство/ремесло (подделка)", []),
        ("skill.art-craft.photography", "Искусство/ремесло (фотография)", []),
        (Mythos, "Мифы Ктулху", []),
        ("skill.animal-handling", "Обращение с животными", []),
        (CreditRating, "Средства", []),
        ("skill.lore", "Тайные знания", []),
        ("skill.lore.dream-lore", "Тайные знания (сновидения)", []),
    ];

    private static readonly Dictionary<string, string> CodeByName = BuildIndex();

    /// <summary>Книжные навыки: код → имя справочника. Для сидов, переноса и проверки кодов.</summary>
    public static IReadOnlyDictionary<string, string> BookNames { get; } =
        Table.ToDictionary(row => row.Code, row => row.Name, StringComparer.Ordinal);

    /// <summary>
    /// Код книжного навыка по имени — справочника или старому написанию v1 («Языки (родной)», «Вождение»),
    /// без учёта регистра, «ё» и лишних пробелов. Null — навыка нет в таблице (самодельный).
    /// </summary>
    public static string? FromName(string name) => CodeByName.GetValueOrDefault(NormalizeName(name));

    /// <summary>Код родителя специализации (<c>skill.firearms.handgun</c> → <c>skill.firearms</c>); null у навыка без родителя.</summary>
    public static string? ParentOf(string code)
    {
        var dot = code.LastIndexOf('.');
        return dot > Prefix.Length - 1 ? code[..dot] : null;
    }

    /// <summary>Нижний регистр, «ё» → «е», пробелы по краям сняты, подряд идущие схлопнуты.</summary>
    public static string NormalizeName(string name) =>
        string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant()
            .Replace('ё', 'е');

    private static Dictionary<string, string> BuildIndex()
    {
        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (code, name, v1Names) in Table)
        {
            foreach (var spelling in v1Names.Prepend(name))
            {
                if (!index.TryAdd(NormalizeName(spelling), code))
                    throw new InvalidOperationException($"Написание «{spelling}» в таблице навыков дважды.");
            }
        }

        return index;
    }
}
