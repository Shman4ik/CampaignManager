using CampaignManager.Web.Model;

namespace CampaignManager.Web.Components.Features.Characters.Model;

/// <summary>
/// Хранит фрагменты знаний (правила, справочники), загружаемые в контекст LLM для валидации персонажей.
/// </summary>
public class LlmKnowledgeEntry : BaseDataBaseEntity
{
    /// <summary>Уникальный ключ записи, например «character-creation» или «skills-reference».</summary>
    public string Key { get; set; } = "";

    /// <summary>Человекочитаемое название записи.</summary>
    public string Title { get; set; } = "";

    /// <summary>Содержимое в формате Markdown, передаваемое в промпт LLM.</summary>
    public string Content { get; set; } = "";

    /// <summary>Определяет порядок объединения фрагментов при построении промпта.</summary>
    public int SortOrder { get; set; }

    /// <summary>Отключённые записи игнорируются при загрузке.</summary>
    public bool IsActive { get; set; } = true;
}
