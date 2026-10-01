using System.Text;

namespace CampaignManager.Core.Catalogs;

/// <summary>
/// Стабильный ключ книжной записи справочника (<c>code</c> в SCHEMA): префикс вида и транслит имени —
/// «Стрельба (пистолет)» → <c>skill.strelba-pistolet</c>. Синхронизация с правилами идёт по нему, а не
/// по имени: переименование становится обычной правкой. Перенос (T1.3) и сиды строят коды этой же
/// функцией, поэтому коды, на которые опираются правила (<see cref="SkillCodes"/>), совпадают с базой.
/// </summary>
public static class CatalogCode
{
    public const string SkillPrefix = "skill";
    public const string OccupationPrefix = "occupation";

    /// <summary>Код записи: <c>{prefix}.{транслит}</c>; транслит — латиница, цифры и дефисы.</summary>
    public static string For(string prefix, string name) => $"{prefix}.{Transliterate(name)}";

    /// <summary>Код навыка по имени из справочника.</summary>
    public static string Skill(string name) => For(SkillPrefix, name);

    /// <summary>
    /// Нижний регистр, кириллица — латиницей (х → kh, щ → shch, ь и ъ пропадают), всё прочее — дефис,
    /// подряд идущие дефисы схлопываются, по краям их нет.
    /// </summary>
    public static string Transliterate(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        foreach (var ch in name.Trim().ToLowerInvariant())
        {
            var latin = ch switch
            {
                'а' => "a", 'б' => "b", 'в' => "v", 'г' => "g", 'д' => "d", 'е' => "e", 'ё' => "e",
                'ж' => "zh", 'з' => "z", 'и' => "i", 'й' => "y", 'к' => "k", 'л' => "l", 'м' => "m",
                'н' => "n", 'о' => "o", 'п' => "p", 'р' => "r", 'с' => "s", 'т' => "t", 'у' => "u",
                'ф' => "f", 'х' => "kh", 'ц' => "ts", 'ч' => "ch", 'ш' => "sh", 'щ' => "shch",
                'ъ' or 'ь' => "", 'ы' => "y", 'э' => "e", 'ю' => "yu", 'я' => "ya",
                _ when ch is >= 'a' and <= 'z' or >= '0' and <= '9' => ch.ToString(),
                _ => "-",
            };

            if (latin == "-" && (builder.Length == 0 || builder[^1] == '-'))
                continue;
            builder.Append(latin);
        }

        while (builder.Length > 0 && builder[^1] == '-')
            builder.Length--;

        return builder.ToString();
    }
}

/// <summary>
/// Навыки, на которые опираются правила книги. В v1 их искали по строковым именам («Мифы Ктулху» — в
/// 10 файлах, у родного языка два написания); в 2.0 — по коду справочника. Значения — <see
/// cref="CatalogCode.Skill"/> от книжного имени, это проверяет тест.
/// </summary>
public static class SkillCodes
{
    public const string Mythos = "skill.mify-ktulkhu";
    public const string CreditRating = "skill.sredstva";
    public const string Dodge = "skill.uklonenie";
    public const string LanguageOwn = "skill.yazyk-rodnoy";
    public const string LanguageForeign = "skill.yazyk-inostrannyy";
    public const string Fighting = "skill.blizhniy-boy";
    public const string Firearms = "skill.strelba";
    public const string Survival = "skill.vyzhivanie";
    public const string Intimidate = "skill.zapugivanie";
    public const string FastTalk = "skill.krasnorechie";
    public const string Charm = "skill.obayanie";
    public const string Persuade = "skill.ubezhdenie";

    /// <summary>Книжные имена этих навыков — для сидов и проверки кодов.</summary>
    public static IReadOnlyDictionary<string, string> BookNames { get; } = new Dictionary<string, string>
    {
        [Mythos] = "Мифы Ктулху",
        [CreditRating] = "Средства",
        [Dodge] = "Уклонение",
        [LanguageOwn] = "Язык, родной",
        [LanguageForeign] = "Язык, иностранный",
        [Fighting] = "Ближний бой",
        [Firearms] = "Стрельба",
        [Survival] = "Выживание",
        [Intimidate] = "Запугивание",
        [FastTalk] = "Красноречие",
        [Charm] = "Обаяние",
        [Persuade] = "Убеждение",
    };

    /// <summary>«Один социальный навык (Запугивание, Красноречие, Обаяние или Убеждение)» (стр. 38).</summary>
    public static IReadOnlyList<string> Social { get; } = [Intimidate, FastTalk, Charm, Persuade];
}
