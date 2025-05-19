using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Spells.Model;

/// <summary>
///     Represents a spell entity for database storage or general use.
/// </summary>
public class Spell : BaseDataBaseEntity
{
    /// <summary>
    ///     Название заклинания.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Стоимость сотворения заклинания (пункты магии, рассудка, МОЩ и т.д.).
    /// </summary>
    public string? Cost { get; set; }

    /// <summary>
    ///     Время, необходимое для сотворения заклинания.
    /// </summary>
    public string? CastingTime { get; set; }

    /// <summary>
    ///     Подробное описание эффектов и требований заклинания.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Список альтернативных названий заклинания.
    /// </summary>
    public List<string> AlternativeNames { get; set; } = [];

    /// <summary>
    ///     Тип или категория заклинания (например, Призыв, Зачарование, Связь).
    /// </summary>
    public required string SpellType { get; set; }
}