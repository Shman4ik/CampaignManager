namespace CampaignManager.Web.Components.Features.Bestiary.Model;

/// <summary>
///     Статблок существа в том виде, в каком его печатает книга (гл. 14, стр. 277–280).
///     Наружности, Образования и Удачи у чудовищ книга не указывает, поэтому их здесь нет.
/// </summary>
public class CreatureCharacteristics
{
    /// <summary>
    ///     Сила
    /// </summary>
    public CreatureCharacteristicModel Strength { get; set; } = new();

    /// <summary>
    ///     Ловкость
    /// </summary>
    public CreatureCharacteristicModel Dexterity { get; set; } = new();

    /// <summary>
    ///     Интеллект
    /// </summary>
    public CreatureCharacteristicModel Intelligence { get; set; } = new();

    /// <summary>
    ///     Выносливость
    /// </summary>
    public CreatureCharacteristicModel Constitution { get; set; } = new();

    /// <summary>
    ///     Мощь
    /// </summary>
    public CreatureCharacteristicModel Power { get; set; } = new();

    /// <summary>
    ///     Телосложение
    /// </summary>
    public CreatureCharacteristicModel Size { get; set; } = new();

    /// <summary>
    ///     Очерёдность хода. Ходы идут по убыванию ЛВК (стр. 110), поэтому по умолчанию
    ///     здесь та же ЛВК; поле остаётся, чтобы Хранитель мог задать существу свой
    ///     порядок. Ноль означает «взять ЛВК».
    /// </summary>
    public int Initiative { get; set; }

    /// <summary>
    ///     Средний бонус к урону, как в книге: «+2d6», «+1d4», «-2», «0» (стр. 280).
    ///     Это бонус именно к урону, а не к попаданию.
    /// </summary>
    public string AverageDamageBonus { get; set; } = string.Empty;

    /// <summary>
    ///     Средняя Комплекция (стр. 277).
    /// </summary>
    public int AverageComplexity { get; set; }

    /// <summary>
    ///     Скорость на своём основном способе передвижения (стр. 280).
    /// </summary>
    public int Speed { get; set; }

    /// <summary>
    ///     Скорость плавания, если книга указывает её отдельно («8 / плавание 10»).
    ///     Пусто — существо плывёт с половиной обычной скорости (стр. 141).
    /// </summary>
    public int? SwimSpeed { get; set; }

    /// <summary>
    ///     Скорость полёта, если книга указывает её отдельно («6 / полёт 20»).
    ///     Пусто — полёт существу недоступен.
    /// </summary>
    public int? FlySpeed { get; set; }

    /// <summary>
    ///     Уточнение к скорости, если книга пишет её словами: «перекатывание 10»,
    ///     «0 (парит в воздухе)», «15; может исчезать и появляться, когда пожелает».
    /// </summary>
    public string? SpeedNote { get; set; }

    /// <summary>
    ///     ПЗ пункты здоровья
    /// </summary>
    public int HealPoint { get; set; }

    /// <summary>
    ///     ПМ пункты магии
    /// </summary>
    public int ManaPoint { get; set; }

    /// <summary>
    ///     Броня в пунктах: вычитается из физического урона (стр. 106).
    /// </summary>
    public int Armor { get; set; }

    /// <summary>
    ///     Броня словами — то, что не сводится к числу: «огнестрел наносит минимальный
    ///     урон», «регенерирует 2 ПЗ за раунд», «неуязвим к проникающему оружию».
    ///     У доброй половины тварей книга печатает «Броня: нет» и следом такую оговорку,
    ///     поэтому пустая <see cref="Armor" /> ещё не значит, что существо уязвимо.
    /// </summary>
    public string? ArmorNote { get; set; }

    /// <summary>
    ///     Атак за раунд (стр. 279). Столько же раз существо может уклониться или
    ///     контратаковать, прежде чем противники получат бонусную кость за численное
    ///     превосходство.
    /// </summary>
    public int AttacksPerRound { get; set; } = 1;

    /// <summary>
    ///     Оговорка к числу атак: «максимум 1 укус за раунд», «1d8» у тех тварей,
    ///     у которых книга задаёт число атак броском.
    /// </summary>
    public string? AttacksPerRoundNote { get; set; }

    /// <summary>
    ///     Навык уклонения (если 0, используется ЛВК/2 как fallback)
    /// </summary>
    public int DodgeSkill { get; set; }

    /// <summary>
    ///     Потеря рассудка при встрече, как в бестиарии: «успех/провал», например «0/1d6».
    ///     Из провальной части выводится предел привыкания к ужасному (стр. 167):
    ///     больше этого сыщик за данный вид тварей не потеряет.
    /// </summary>
    public string SanityLoss { get; set; } = string.Empty;
}
