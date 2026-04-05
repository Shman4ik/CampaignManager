using System.ComponentModel.DataAnnotations;
using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Skills.Model;

/// <summary>
/// Модель навыка персонажа в Call of Cthulhu
/// </summary>
public class SkillModel : BaseDataBaseEntity, INamedEntity
{
    /// <summary>
    /// Название навыка
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Базовое значение навыка (в процентах)
    /// </summary>
    [Range(0, 100)]
    public int BaseValue { get; set; }

    /// <summary>
    /// Описание навыка
    /// </summary>
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Категория навыка
    /// </summary>
    public SkillCategory Category { get; set; }

    /// <summary>
    /// Является ли навык необычным (требует особых условий)
    /// </summary>
    public bool IsUncommon { get; set; }

    /// <summary>
    /// Примеры использования навыка
    /// </summary>
    public List<string> UsageExamples { get; set; } = [];

    /// <summary>
    /// Последствия провала проверки
    /// </summary>
    public List<string> FailureConsequences { get; set; } = [];

    /// <summary>
    /// Время выполнения проверки
    /// </summary>
    public string TimeRequired { get; set; } = string.Empty;

    /// <summary>
    /// Можно ли повторить проверку
    /// </summary>
    public bool CanRetry { get; set; }

    /// <summary>
    /// Противоположные навыки (если применимо)
    /// </summary>
    public List<string> OpposingSkills { get; set; } = [];

    /// <summary>
    /// Ссылка на родительский навык для специализаций (например, "Ближний бой" для "Ближний бой (драка)").
    /// Null — навык не является специализацией.
    /// </summary>
    public Guid? ParentSkillId { get; set; }
}