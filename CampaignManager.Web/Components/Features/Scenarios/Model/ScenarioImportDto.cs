using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Scenarios.Model;

/// <summary>
///     Обменный формат сценария — то, что читает «Импорт JSON» и пишет «Экспорт JSON».
///     <para>
///         Формат намеренно отличается от <see cref="Scenario" />: в нём нет идентификаторов,
///         а родительская локация указывается по имени (<see cref="ScenarioLocationImportDto.Parent" />).
///         Автор файла — человек или LLM, переносящая сценарий из книги правил, — идентификаторов
///         не знает и знать не должен.
///     </para>
/// </summary>
public sealed class ScenarioImportDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Location { get; set; }

    public string? Era { get; set; }

    /// <summary>
    ///     Заметки Хранителя в Markdown — то же поле, что и «Журнал» на вкладке «Основное».
    /// </summary>
    public string? Journal { get; set; }

    public bool IsTemplate { get; set; } = true;

    public bool IsPublished { get; set; }

    public DateTime? ScheduledDate { get; set; }

    public string? AnnouncementText { get; set; }

    public List<ScenarioKeyFactImportDto> KeyFacts { get; set; } = [];

    public List<ScenarioLocationImportDto> Locations { get; set; } = [];

    public List<ScenarioHandoutImportDto> Handouts { get; set; } = [];

    /// <summary>
    ///     Состав НПС. Лист с таким же именем уже в библиотеке — импорт переиспользует его,
    ///     а не создаёт двойника: НПС общий для всех сценариев.
    /// </summary>
    public List<ScenarioNpcImportDto> Npcs { get; set; } = [];
}

public sealed class ScenarioKeyFactImportDto
{
    public string Title { get; set; } = string.Empty;

    public KeyFactType Type { get; set; } = KeyFactType.Backstory;

    public string? Content { get; set; }
}

public sealed class ScenarioLocationImportDto
{
    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? Description { get; set; }

    /// <summary>
    ///     Имя родительской локации из этого же файла. Несовпадение попадёт в предупреждения
    ///     импорта, а локация останется на верхнем уровне.
    /// </summary>
    public string? Parent { get; set; }

    public List<ScenarioSkillCheckImportDto> SkillChecks { get; set; } = [];
}

public sealed class ScenarioSkillCheckImportDto
{
    public string SkillName { get; set; } = string.Empty;

    /// <summary>
    ///     Пусто — обычная проверка; <c>Hard</c> — сложная; <c>Extreme</c> — чрезвычайная.
    /// </summary>
    public string? Difficulty { get; set; }

    public string? SuccessResult { get; set; }

    public string? FailureResult { get; set; }
}

public sealed class ScenarioHandoutImportDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? FileUrl { get; set; }
}

/// <summary>
///     Лист НПС плюс его роль именно в этом сценарии.
/// </summary>
public sealed class ScenarioNpcImportDto
{
    public string Name { get; set; } = string.Empty;

    public NpcRole Role { get; set; } = NpcRole.Neutral;

    /// <summary>
    ///     Сколько одинаковых НПС выходит на сцену.
    /// </summary>
    public int Count { get; set; } = 1;

    /// <summary>
    ///     Заметка Хранителя об этом появлении НПС.
    /// </summary>
    public string? Notes { get; set; }

    public string? Occupation { get; set; }

    public int Age { get; set; }

    public string? Gender { get; set; }

    public string? Backstory { get; set; }

    public ScenarioNpcCharacteristicsDto Characteristics { get; set; } = new();

    /// <summary>
    ///     Пункты здоровья (ПЗ).
    /// </summary>
    public int HitPoints { get; set; }

    /// <summary>
    ///     Пункты магии.
    /// </summary>
    public int MagicPoints { get; set; }

    public int Sanity { get; set; }

    public int Luck { get; set; }

    /// <summary>
    ///     Бонус к урону, как в статблоке: «+1d4», «0», «−1».
    /// </summary>
    public string? DamageBonus { get; set; }

    /// <summary>
    ///     Комплексия.
    /// </summary>
    public string? Build { get; set; }

    /// <summary>
    ///     Скорость (СКО).
    /// </summary>
    public int MoveSpeed { get; set; }

    public int Dodge { get; set; }

    /// <summary>
    ///     Навыки «имя → значение». Имя, совпавшее со стандартным навыком, попадёт в свою группу
    ///     листа; остальные соберутся в группе «Особые навыки».
    /// </summary>
    public Dictionary<string, int> Skills { get; set; } = [];
}

/// <summary>
///     Восемь характеристик в порядке статблока: СИЛ, ВЫН, ТЕЛ, ЛВК, ИНТ, НАР, МОЩ, ОБР.
/// </summary>
public sealed class ScenarioNpcCharacteristicsDto
{
    public int Str { get; set; }

    public int Con { get; set; }

    public int Siz { get; set; }

    public int Dex { get; set; }

    public int Int { get; set; }

    public int App { get; set; }

    public int Pow { get; set; }

    public int Edu { get; set; }
}

/// <summary>
///     Что получилось из импорта — показывается в модальном окне сразу после запуска.
/// </summary>
public sealed class ScenarioImportResult
{
    public bool Success { get; init; }

    public Guid? ScenarioId { get; init; }

    public string? ScenarioName { get; init; }

    public string? Error { get; init; }

    public int LocationsCreated { get; init; }

    public int KeyFactsCreated { get; init; }

    public int HandoutsCreated { get; init; }

    public int NpcsCreated { get; init; }

    public int NpcsReused { get; init; }

    public List<string> Warnings { get; init; } = [];
}
