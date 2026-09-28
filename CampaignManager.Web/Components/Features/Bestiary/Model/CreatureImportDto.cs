namespace CampaignManager.Web.Components.Features.Bestiary.Model;

/// <summary>
///     Обменный файл бестиария: <c>{ "creatures": [ ... ] }</c>. Тот же вид отдаёт экспорт.
/// </summary>
public sealed class BestiaryImportDto
{
    public List<CreatureImportDto> Creatures { get; set; } = [];
}

/// <summary>
///     Одно существо в обменном файле. Вложенные части — те же классы, что лежат в JSONB
///     (<see cref="CreatureCharacteristics" />, <see cref="CreatureAttack" />, <see cref="CreatureSkill" />),
///     поэтому формат не расходится с моделью. Служебных полей (Id, даты) и legacy-словаря
///     <see cref="Creature.CombatDescriptions" /> в нём нет.
/// </summary>
public sealed class CreatureImportDto
{
    /// <summary>Ключ сопоставления с базой — без учёта регистра.</summary>
    public string? Name { get; set; }

    /// <summary>
    ///     Прежнее название, если существо переименовывается: опечатка распознавания
    ///     («Шатгая» → «Шаггая») или разделение на формы («Шоггот-владыка» → «… (форма человека)»).
    ///     Если под <see cref="Name" /> ничего нет, обновляется запись с этим именем — вместе с её
    ///     картинками и историей правок, — а не заводится двойник.
    /// </summary>
    public string? FormerName { get; set; }

    /// <summary>Имя члена <see cref="CreatureType" />: «MythicMonsters», «Beast»…</summary>
    public string? Type { get; set; }

    public string? Description { get; set; }

    public CreatureCharacteristics? Characteristics { get; set; }

    public List<CreatureAttack>? Attacks { get; set; }

    public List<CreatureSkill>? Skills { get; set; }

    public Dictionary<string, string>? SpecialAbilities { get; set; }

    /// <summary>
    ///     <c>null</c> — картинки существа не трогать; пустой массив — снять все.
    ///     Статблок и иллюстрации обычно готовят раздельно, поэтому по умолчанию они не затираются.
    /// </summary>
    public List<CreatureImage>? Images { get; set; }
}

/// <summary>Итог импорта бестиария.</summary>
public sealed class CreatureImportResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Renamed { get; set; }

    /// <summary>Прогон без записи: счётчики показывают, что было бы сделано.</summary>
    public bool DryRun { get; set; }

    /// <summary>
    ///     Замечания по данным: то, что сохранилось, но в бою сработает не так, как ждёшь
    ///     (формула урона, которую не бросить; существо без атак; пустые ПЗ).
    /// </summary>
    public List<string> Warnings { get; set; } = [];
}
