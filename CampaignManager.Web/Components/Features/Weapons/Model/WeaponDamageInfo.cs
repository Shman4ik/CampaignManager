using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Weapons.Model;

/// <summary>
/// Запись урона по дальности для оружия с переменным уроном (дробовики).
/// Например: близкая дистанция — 4d6, средняя — 2d6, дальняя — 1d6.
/// </summary>
public sealed class RangeDamageEntry
{
    /// <summary>Метка дальности, например "Близкая", "Средняя", "Дальняя".</summary>
    public string RangeLabel { get; set; } = string.Empty;

    /// <summary>Формула урона для этой дальности.</summary>
    public DamageExpression Damage { get; set; } = new();
}

/// <summary>
/// Структурированная информация об уроне оружия.
/// Заполняется автоматически парсером из текстового поля Damage.
/// Если парсинг не удался, IsParsed = false и для расчёта используется текстовое поле.
/// </summary>
public sealed class WeaponDamageInfo
{
    /// <summary>
    /// Основная формула урона. Null только если оружие имеет исключительно
    /// дальность-зависимый урон (RangeDamages заполнен).
    /// </summary>
    public DamageExpression? Primary { get; set; }

    /// <summary>
    /// Урон по дальностям для дробовиков (например, 4d6 / 2d6 / 1d6).
    /// Null для большинства оружия.
    /// </summary>
    public List<RangeDamageEntry>? RangeDamages { get; set; }

    /// <summary>Радиус взрыва в метрах для взрывчатки и гранат. Null если не применимо.</summary>
    public int? BlastRadiusMeters { get; set; }

    /// <summary>Оригинальная строка урона до парсинга.</summary>
    public string RawText { get; set; } = string.Empty;

    /// <summary>
    /// Был ли оригинальный текст успешно распарсен.
    /// False — в UI отображается предупреждение о необходимости ручного ввода.
    /// </summary>
    public bool IsParsed { get; set; }

    /// <summary>
    /// Возвращает формулу урона для использования в расчётах.
    /// Для дробовиков возвращает формулу ближней дальности (Primary если есть,
    /// иначе первый элемент RangeDamages).
    /// </summary>
    public DamageExpression? GetDefaultDamage() =>
        Primary ?? RangeDamages?.FirstOrDefault()?.Damage;

    /// <summary>
    /// Возвращает строку урона для отображения.
    /// Приоритет: Primary → сводка по всем дальностям → RawText.
    /// </summary>
    public override string ToString() => RawText;
}
