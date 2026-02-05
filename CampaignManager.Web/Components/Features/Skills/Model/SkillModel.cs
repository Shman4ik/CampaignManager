using System.ComponentModel.DataAnnotations;

namespace CampaignManager.Web.Components.Features.Skills.Model;

/// <summary>
/// Модель навыка персонажа в Call of Cthulhu
/// </summary>
public class SkillModel
{
    /// <summary>
    /// Уникальный идентификатор навыка
    /// </summary>
    public Guid Id { get; set; }

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
    /// Дата создания
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Дата последнего обновления
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; }
}