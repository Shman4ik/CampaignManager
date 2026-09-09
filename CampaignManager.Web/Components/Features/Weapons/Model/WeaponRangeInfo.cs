namespace CampaignManager.Web.Components.Features.Weapons.Model;

/// <summary>
///     Способ, которым таблица XVII задаёт дальность оружия.
/// </summary>
public enum WeaponRangeKind
{
    /// <summary>Дальность не указана или неприменима («Нет», пустая строка).</summary>
    None,

    /// <summary>Оружие ближнего боя: «Касание», «контакт».</summary>
    Touch,

    /// <summary>Обычная базовая дальность в метрах: «15 метров».</summary>
    Meters,

    /// <summary>Дробовики: несколько порогов дальности — «10/20/50 метров».</summary>
    RangeBands,

    /// <summary>Метательное: «СИЛ / 5 метров» — дальность считается от СИЛ.</summary>
    StrengthThrow,

    /// <summary>Стационарная закладка: «На месте».</summary>
    Emplaced
}

/// <summary>
///     Структурированная дальность оружия, разобранная из текстового поля
///     <see cref="Weapon.Range" />. Заполняется <c>WeaponStatsParser.ParseRange</c>.
///     Хранится в БД как JSONB (и внутри JSONB листа персонажа), поэтому — простой POCO.
/// </summary>
public sealed class WeaponRangeInfo
{
    /// <summary>Как именно задана дальность.</summary>
    public WeaponRangeKind Kind { get; set; } = WeaponRangeKind.None;

    /// <summary>
    ///     Базовая дальность в метрах. Для <see cref="WeaponRangeKind.RangeBands" /> —
    ///     первый (ближний) порог, он же базовая дальность по правилам.
    /// </summary>
    public int? BaseMeters { get; set; }

    /// <summary>
    ///     Пороги дальности дробовика: «10/20/50 метров» → [10, 20, 50].
    ///     Идут параллельно <see cref="WeaponDamageInfo.RangeDamages" />.
    ///     Null для оружия с единственной дальностью.
    /// </summary>
    public List<int>? Bands { get; set; }

    /// <summary>
    ///     Делитель СИЛ для метательного оружия: «СИЛ / 5 метров» → 5.
    ///     Дальность броска = СИЛ / ThrowDivisor метров.
    /// </summary>
    public int? ThrowDivisor { get; set; }

    /// <summary>Оригинальная строка дальности до разбора.</summary>
    public string RawText { get; set; } = string.Empty;

    /// <summary>Удалось ли разобрать текст. False — потребитель откатывается на строку.</summary>
    public bool IsParsed { get; set; }

    public override string ToString() => RawText;
}
