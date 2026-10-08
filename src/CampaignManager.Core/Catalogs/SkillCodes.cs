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
    /// Книжные навыки: код → имя в справочнике (93 навыка справочника v1 без трёх только современной эпохи —
    /// «Ближний бой (бензопила)», «Работа с компьютером», «Электроника»: современную эпоху владелец убрал
    /// 2026-10-02). Порядок — как в справочнике по категориям; новый книжный навык — новая строка здесь, код
    /// однажды выданный не меняется. Варианты написания — со справочника и с листов v1 (перенос T1.3).
    /// </summary>
    public static CatalogCodeTable Table { get; } = new(Prefix,
    [
        // Действия
        new("skill.ride", "Верховая езда"),
        new("skill.drive-auto", "Вождение автомобиля", ["Вождение"]),
        new("skill.climb", "Лазание"),
        new("skill.pilot", "Пилотирование"),
        new("skill.pilot.boat", "Пилотирование (лодка)"),
        new("skill.pilot.aircraft", "Пилотирование (самолёт)"),
        new("skill.swim", "Плавание"),
        new("skill.diving", "Подводное плавание"),
        new("skill.jump", "Прыжки"),
        new("skill.operate-heavy-machinery", "Управление тяжёлыми машинами", ["Упр. тяж. машинами"]),

        // Сражение (огнестрельное)
        new("skill.artillery", "Артиллерия"),
        new(Firearms, "Стрельба"),
        new("skill.firearms.rifle-shotgun", "Стрельба (винтовка/дробовик)", ["Стрельба (винт./дроб.)"]),
        new("skill.firearms.bow", "Стрельба (лук)"),
        new("skill.firearms.flamethrower", "Стрельба (огнемёт)"),
        new("skill.firearms.handgun", "Стрельба (пистолет)"),
        new("skill.firearms.submachine-gun", "Стрельба (пистолет-пулемёт)", ["Автомат"]),
        new("skill.firearms.machine-gun", "Стрельба (пулемёт)"),
        new("skill.firearms.heavy-weapons", "Стрельба (тяжёлое вооружение)"),
        new("skill.firearms.lightning-gun", "Стрельба (молниемёт)"), // гл. 13, стр. 269: оружие йитиан, база 10%
        new("skill.firearms.electric-gun", "Стрельба (электропушка)"), // гл. 13, стр. 273: оружие ми-го, база 10%

        // Сражение (общее)
        new(Fighting, "Ближний бой"),
        new("skill.fighting.brawl", "Ближний бой (драка)"),
        new("skill.fighting.spear", "Ближний бой (копьё)"),
        new("skill.fighting.sword", "Ближний бой (меч)"),
        new("skill.fighting.axe", "Ближний бой (топор)"),
        new("skill.fighting.garrote", "Ближний бой (удавка)"),
        new("skill.fighting.whip", "Ближний бой (хлыст)"),
        new("skill.fighting.flail", "Ближний бой (цеп)"),
        new("skill.throw", "Метание"),
        new(Dodge, "Уклонение"),

        // Лечение
        new("skill.hypnosis", "Гипноз"),
        new("skill.medicine", "Медицина"),
        new("skill.science.pharmacy", "Наука (фармакология)", ["Наука (фармацевтика)", "Наука фармакология"]),
        new("skill.first-aid", "Первая помощь"),
        new("skill.psychoanalysis", "Психоанализ"),

        // Сбор информации
        new("skill.spot-hidden", "Внимание"),
        new("skill.science.forensics", "Наука (криминалистика)", ["Наука судмедэксперт"]),
        new("skill.library-use", "Работа в библиотеке"),
        new("skill.listen", "Слух"),

        // Знания
        new("skill.anthropology", "Антропология"),
        new("skill.archaeology", "Археология"),
        new("skill.accounting", "Бухгалтерское дело"),
        new("skill.natural-world", "Естествознание"),
        new("skill.history", "История"),
        new("skill.science", "Наука"),
        new("skill.science.astronomy", "Наука (астрономия)"),
        new("skill.science.biology", "Наука (биология)"),
        new("skill.science.botany", "Наука (ботаника)"),
        new("skill.science.geology", "Наука (геология)"),
        new("skill.science.zoology", "Наука (зоология)"),
        new("skill.science.engineering", "Наука (инженерия)"),
        new("skill.science.cryptography", "Наука (криптография)"),
        new("skill.science.mathematics", "Наука (математика)"),
        new("skill.science.meteorology", "Наука (метеорология)"),
        new("skill.science.physics", "Наука (физика)"),
        new("skill.science.chemistry", "Наука (химия)"),
        new("skill.occult", "Оккультизм"),
        new("skill.appraise", "Оценка"),
        new("skill.law", "Юриспруденция"),

        // Решение проблем
        new("skill.locksmith", "Взлом"),
        new("skill.demolitions", "Взрывчатка"),
        new(Survival, "Выживание"),
        new("skill.survival.sea", "Выживание (море)"),
        new("skill.survival.arctic", "Выживание (полярные области)"),
        new("skill.survival.desert", "Выживание (пустыня)"),
        new("skill.sleight-of-hand", "Ловкость рук"),
        new("skill.mechanical-repair", "Механика"),
        new("skill.navigate", "Ориентирование"),
        new("skill.stealth", "Скрытность"),
        new("skill.track", "Чтение следов"),
        new("skill.electrical-repair", "Электрика"),

        // Социальные
        new(Intimidate, "Запугивание"),
        new(FastTalk, "Красноречие"),
        new("skill.disguise", "Маскировка"),
        new(Charm, "Обаяние"),
        new("skill.psychology", "Психология"),
        new(Persuade, "Убеждение"),
        new("skill.read-lips", "Чтение по губам"),
        new(LanguageForeign, "Язык, иностранный", ["Языки (иностр.)"]),
        new("skill.language-other.english", "Язык, иностранный (английский)"),
        new(LanguageOwn, "Язык, родной", ["Языки (родной)"]),

        // Специальные
        new("skill.art-craft", "Искусство/ремесло"),
        new("skill.art-craft.acting", "Искусство/ремесло (актёрская игра)"),
        new("skill.art-craft.fine-art", "Искусство/ремесло (изобразительное искусство)", ["Рисование"]), // спорно: «Рисование» с листа v1
        new("skill.art-craft.forgery", "Искусство/ремесло (подделка)"),
        new("skill.art-craft.photography", "Искусство/ремесло (фотография)", ["Фотография", "Фотографирование"]),
        new(Mythos, "Мифы Ктулху"),
        new("skill.animal-handling", "Обращение с животными"),
        new(CreditRating, "Средства"),
        new("skill.lore", "Тайные знания"),
        new("skill.lore.dream-lore", "Тайные знания (сновидения)"),
    ]);

    /// <summary>Книжные навыки: код → имя справочника. Для сидов, переноса и проверки кодов.</summary>
    public static IReadOnlyDictionary<string, string> BookNames => Table.BookNames;

    /// <summary>
    /// Код книжного навыка по имени — справочника или старому написанию v1 («Языки (родной)», «Вождение»),
    /// без учёта регистра, «ё» и лишних пробелов. Null — навыка нет в таблице (самодельный).
    /// </summary>
    public static string? FromName(string name) => Table.FromName(name);

    /// <summary>Код родителя специализации (<c>skill.firearms.handgun</c> → <c>skill.firearms</c>); null у навыка без родителя.</summary>
    public static string? ParentOf(string code)
    {
        var dot = code.LastIndexOf('.');
        return dot > Prefix.Length - 1 ? code[..dot] : null;
    }

    /// <summary>Нижний регистр, «ё» → «е», пробелы по краям сняты, подряд идущие схлопнуты.</summary>
    public static string NormalizeName(string name) => CatalogCodeTable.NormalizeName(name);
}
