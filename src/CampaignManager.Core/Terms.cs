using System.Globalization;
using System.Text.RegularExpressions;

namespace CampaignManager.Core;

/// <summary>
/// Словарь интерфейса — <b>одно место</b> для слов, которые были разными на разных страницах (правило 11
/// дизайн-системы, «Словарь замен»). Один термин — одно слово по всему приложению: подпись, заголовок, пустое
/// состояние и тост берут его отсюда, а не пишут своё. Новое слово словаря добавляется здесь, страница
/// ссылается на константу. Сокращения в заголовке столбца допустимы только с расшифровкой видимым текстом (<c>Abbr</c>).
/// </summary>
public static partial class Terms
{
    /// <summary>Значение «нет» — везде тире (не «Нет», не пусто).</summary>
    public const string None = "—";

    // ── Фильтры и списки ──

    public const string ResetFilters = "Сбросить фильтры";
    public const string AnyType = "Любой тип";
    public const string AnyCategory = "Любая категория";
    public const string AnySkillPoints = "Любые очки навыков";
    public const string AnyEra = "Любая эпоха";
    public const string AnyKind = "Любой вид";
    public const string AnyOwner = "Любые владельцы";

    /// <summary>Возраст с правильным склонением: «1 год», «22 года», «42 года», «11 лет».</summary>
    public static string Years(int age) => $"{age} " + (age % 100 is >= 11 and <= 14 ? 0 : age % 10) switch
    {
        1 => "год",
        2 or 3 or 4 => "года",
        _ => "лет",
    };

    /// <summary>Пустой список: «Нет книг.»</summary>
    public static string Empty(string plural) => $"Нет {plural}.";

    /// <summary>Пусто из-за фильтра: «Нет книг по этим условиям.»</summary>
    public static string EmptyFiltered(string plural) => $"Нет {plural} по этим условиям.";

    // ── Разделы ──

    public const string Bestiary = "Бестиарий";
    public const string Library = "Фонотека";
    public const string Player = "Плеер";
    public const string Profession = "Профессия";
    public const string FromLovecraft = "Из Лавкрафта";

    // ── Игровые слова ──

    public const string DamageBonus = "бонус к урону";
    public const string DamageBonusTitle = "Бонус к урону";
    public const string Range = "Дальность";
    public const string AttacksPerRound = "Атаки за раунд";
    public const string SanityLoss = "Потеря Рассудка";
    public const string Power = "Мощь";
    public const string MagicPoints = "Пункты магии";
    public const string Build = "Комплекция";
    public const string Speed = "Скорость";
    public const string Dodge = "Уклонение";
    public const string Hard = "Трудная";
    public const string Extreme = "Чрезвычайная";
    public const string MythosInitial = "Мифы: начальное чтение";
    public const string MythosFull = "Мифы: полное";
    public const string MythosSpells = "Мифы: заклинания";

    // ── Люди и игры ──

    public const string Premade = "готовый сыщик";
    public const string Premades = "Готовые сыщики";
    public const string Booking = "запись на место";
    public const string OneShot = "разовая игра";
    public const string Item = "предмет";
    public const string AddItem = "Добавить предмет";
    public const string OrphanFiles = "файлы без записи";
    public const string OwnCreatureCard = "своя карточка существа";
    public const string CheckResult = "Результат проверки";
    public const string ScenarioImport = "Импорт сценария";
    public const string ImportFile = "файл импорта";
    public const string PinnedTracks = "закреплённые треки";

    // ── Единицы ──

    /// <summary>«100 м».</summary>
    public static string Meters(int meters) => $"{meters} м";

    /// <summary>«$1000» — знак перед числом, без разрядных пробелов.</summary>
    public static string Dollars(decimal amount) => "$" + amount.ToString("0.##", CultureInfo.InvariantCulture);

    // Сокращение бонуса к урону в данных («Б.К.У.», «БкУ»). Только однозначное: падежные формы («преген»,
    // «ваншот») регулярка не перепишет, их правят в источнике.
    [GeneratedRegex(@"Б\.\s?К\.\s?У\.?|Бку", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DamageBonusAbbreviation();

    /// <summary>
    /// Привести к словарю текст из данных: «1d4 + Б.К.У.» → «1d4 + бонус к урону». Броски не форматирует
    /// (это <see cref="Dice.DiceNotation.Format"/>).
    /// </summary>
    public static string Normalize(string? text) =>
        string.IsNullOrEmpty(text)
            ? text ?? ""
            // «1/2БкУ» из данных → «1/2 бонус к урону»: сокращение, прилипшее к числу, получает пробел.
            : DamageBonusAbbreviation().Replace(text, m =>
                m.Index > 0 && char.IsDigit(text[m.Index - 1]) ? " " + DamageBonus : DamageBonus);
}
