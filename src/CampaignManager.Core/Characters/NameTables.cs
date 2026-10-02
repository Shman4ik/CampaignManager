using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Characters;

/// <summary>Пол для подбора имени и графы «Пол» на бланке.</summary>
public enum NameGender
{
    Male,
    Female,
}

/// <summary>
/// Одна таблица имён по эпохам — для быстрого НПС и случайного сыщика. Книга советует держать под рукой
/// список имён, подходящих времени и месту (гл. 10, стр. 189): выдуманное на ходу имя выдаёт проходного
/// персонажа. В v1 списков было два — у генератора без эпохи и у быстрого НПС с эпохой (AUDIT, «Дубли»).
/// Эпоха — поле кампании или сценария (<see cref="Era"/>), а не разбор текста «1920-е».
/// </summary>
public static class NameTables
{
    private static readonly string[] ClassicMale =
    [
        "Джон", "Уильям", "Джордж", "Фрэнк", "Эдвард", "Генри", "Уолтер", "Артур", "Гарольд", "Альберт",
        "Кларенс", "Эрнест", "Честер", "Хорас", "Элмер", "Хайрам", "Эзра", "Сайлас", "Мартин", "Лерой",
    ];

    private static readonly string[] ClassicFemale =
    [
        "Мэри", "Хелен", "Дороти", "Маргарет", "Рут", "Милдред", "Этель", "Глэдис", "Эдна", "Флоренс",
        "Бернис", "Хейзел", "Мейбл", "Ирма", "Люсиль", "Вайолет", "Агнес", "Элси", "Перл", "Корделия",
    ];

    private static readonly string[] ClassicSurnames =
    [
        "Смит", "Джонсон", "Браун", "Миллер", "Уилсон", "Мур", "Тейлор", "Андерсон", "Джексон", "Уайт",
        "Харрис", "Томпсон", "Кларк", "Льюис", "Уокер", "Холл", "Аллен", "Кинг", "Райт", "Хилл",
        "Грин", "Бейкер", "Адамс", "Картер", "Митчелл", "О'Брайен", "Мёрфи", "Келли", "Салливан",
        "Ковальски", "Шмидт", "Росси", "Коэн", "Уэйтли", "Марш", "Пикман",
    ];

    private static readonly string[] ModernMale =
    [
        "Майкл", "Кристофер", "Джейсон", "Дэвид", "Брайан", "Кевин", "Мэттью", "Джошуа", "Эндрю", "Райан",
        "Тайлер", "Джастин", "Брэндон", "Эрик", "Скотт", "Дэниел", "Нейтан", "Итан", "Логан", "Карлос",
    ];

    private static readonly string[] ModernFemale =
    [
        "Дженнифер", "Джессика", "Эшли", "Аманда", "Сара", "Стефани", "Николь", "Мелисса", "Лорен", "Эмили",
        "Ханна", "Меган", "Рэйчел", "Кайла", "Саманта", "Эмма", "Оливия", "Хлоя", "Мэдисон", "Мария",
    ];

    private static readonly string[] ModernSurnames =
    [
        "Смит", "Джонсон", "Уильямс", "Браун", "Джонс", "Гарсия", "Миллер", "Дэвис", "Родригес", "Мартинес",
        "Эрнандес", "Лопес", "Гонсалес", "Уилсон", "Андерсон", "Томас", "Тейлор", "Мур", "Ли", "Нгуен",
        "Пател", "Ким", "Чен", "Томпсон", "Уайт", "Харрис", "Кларк", "Льюис", "Робинсон", "Уокер",
    ];

    public static IReadOnlyList<string> FirstNames(Era era, NameGender gender) => (era, gender) switch
    {
        (Era.Modern, NameGender.Female) => ModernFemale,
        (Era.Modern, _) => ModernMale,
        (_, NameGender.Female) => ClassicFemale,
        _ => ClassicMale,
    };

    public static IReadOnlyList<string> Surnames(Era era) => era is Era.Modern ? ModernSurnames : ClassicSurnames;

    /// <summary>Случайное «Имя Фамилия» эпохи.</summary>
    public static string RandomName(Era era, NameGender gender, IDiceRoller dice) =>
        $"{Pick(FirstNames(era, gender), dice)} {Pick(Surnames(era), dice)}";

    /// <summary>Пол словами — как на бланке (графа «Пол»).</summary>
    public static string GenderText(NameGender gender) => gender is NameGender.Female ? "Женский" : "Мужской";

    private static string Pick(IReadOnlyList<string> values, IDiceRoller dice) => values[dice.Next(0, values.Count)];
}
