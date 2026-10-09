using CampaignManager.Core;
using CampaignManager.Core.Catalogs;

namespace CampaignManager.Contracts.Catalogs;

/// <summary>
/// Общее у записей справочников. Те же классы — и ответ, и тело записи, и строка файла обмена: форма
/// правит копию записи, импорт читает экспорт. Изменяемые свойства — ради форм; клиент получает
/// неизменяемый по смыслу список и правит только копию (<c>CatalogPage</c>).
/// </summary>
/// <remarks>
/// Эпоха новой записи по умолчанию — классика: современная эпоха не переносится (решение владельца
/// 2026-10-02), и перенос оставил в справочниках только её.
/// </remarks>
public abstract class CatalogItemDto
{
    /// <summary>При создании и импорте не читается — id выдаёт сервер.</summary>
    public Guid Id { get; set; }

    /// <summary>Версия строки (<c>xmin</c>): уходит в <c>If-Match</c> при правке, иначе 409.</summary>
    public uint Version { get; set; }

    /// <summary>
    /// Код книжной записи (<c>weapon.thompson</c>); null — самодельная. При правке не меняется: выданный
    /// код постоянен, синхронизация с правилами и перенос ищут по нему.
    /// </summary>
    public string? Code { get; set; }

    public string Name { get; set; } = "";

    /// <summary>Страница книги; null — самодельное.</summary>
    public string? Source { get; set; }
}

public sealed class SkillDto : CatalogItemDto
{
    /// <summary>Специализация → родитель («Стрельба (пистолет)» → «Стрельба»).</summary>
    public Guid? ParentId { get; set; }

    public int BaseValue { get; set; }

    /// <summary>Когда база не число: <c>DEX/2</c> у Уклонения, <c>EDU</c> у родного языка.</summary>
    public string? BaseFormula { get; set; }

    public SkillCategory Category { get; set; }

    public bool IsUncommon { get; set; }

    public List<Era> Eras { get; set; } = [Era.Classic];

    public string Description { get; set; } = "";

    public List<string> UsageExamples { get; set; } = [];

    public List<string> FailureConsequences { get; set; } = [];

    public List<string> OpposingSkills { get; set; } = [];

    public string? TimeRequired { get; set; }

    public bool CanRetry { get; set; }

    /// <summary>Иллюстрация — одна, как у предмета: сыщик за делом. У специализации без своей показывается родительская.</summary>
    public Guid? ImageFileId { get; set; }

    /// <summary>Адрес иллюстрации (только чтение).</summary>
    public string? ImageUrl { get; set; }
}

public sealed class OccupationDto : CatalogItemDto
{
    public SkillPointsFormula SkillPointsFormula { get; set; }

    public int CreditRatingMin { get; set; }

    public int CreditRatingMax { get; set; }

    public List<Era> Eras { get; set; } = [Era.Classic];

    public bool IsLovecraftian { get; set; }

    public List<string> Tags { get; set; } = [];

    /// <summary>Слоты в порядке книги. Слот «Средства» (он есть у перенесённых из v1) навыком профессии не считается.</summary>
    public List<OccupationSlotDto> Slots { get; set; } = [];

    /// <summary>Иллюстрации, как у твари бестиария: первая — обложка карточки.</summary>
    public List<CatalogImageDto> Images { get; set; } = [];

    /// <summary>
    /// Сколько профессиональных навыков дают слоты (только чтение; считает сервер правилом Core
    /// <c>OccupationRules.ProfessionalSkillCount</c>: Средства и Мифы не в счёт). По книге — ровно 8.
    /// </summary>
    public int ProfessionalSkillCount { get; set; }
}

/// <summary>
/// Слот профессии (<c>cm.occupation_slots</c>). Какие поля заполнены, решает <see cref="Kind"/>:
/// Skill и AnySpecialization — <see cref="SkillId"/>, Specialization — ещё и <see cref="Specialization"/>,
/// Choice — <see cref="Options"/> и <see cref="ChooseCount"/>, Social и Free — ничего.
/// </summary>
public sealed class OccupationSlotDto
{
    public OccupationSlotKind Kind { get; set; }

    public Guid? SkillId { get; set; }

    /// <summary>Имя навыка — для файла обмена: в другой базе id другой, импорт ищет по имени.</summary>
    public string? SkillName { get; set; }

    public string? Specialization { get; set; }

    public int ChooseCount { get; set; } = 1;

    public List<Guid> Options { get; set; } = [];

    /// <summary>Имена вариантов — для файла обмена, как <see cref="SkillName"/>.</summary>
    public List<string> OptionNames { get; set; } = [];
}

/// <summary>
/// Оружие. Строки книги (урон, дальность, атаки, боезапас, стоимость) вводит человек; числа для боя
/// сервер разбирает из них при записи (<c>Core.WeaponStatsParser</c>) — присланные клиентом числа
/// не читаются.
/// </summary>
public sealed class WeaponDto : CatalogItemDto
{
    public WeaponType Type { get; set; }

    public Guid SkillId { get; set; }

    /// <summary>Имя навыка — для показа и файла обмена (в другой базе id другой).</summary>
    public string? SkillName { get; set; }

    public List<Era> Eras { get; set; } = [Era.Classic];

    /// <summary>«Редко» в колонке «Встречается»; ортогонально эпохе.</summary>
    public bool IsRare { get; set; }

    /// <summary>Пронзающее (стр. 113).</summary>
    public bool IsImpaling { get; set; }

    /// <summary>«1d6 + БкУ», как в книге.</summary>
    public string Damage { get; set; } = "";

    public string Range { get; set; } = "";

    public string Attacks { get; set; } = "";

    public string Ammo { get; set; } = "";

    /// <summary>Осечка; «00» книги = 100.</summary>
    public int? Malfunction { get; set; }

    public string Cost { get; set; } = "";

    public string Notes { get; set; } = "";

    public bool SingleUse { get; set; }

    /// <summary>Иллюстрации, как у твари бестиария: первая — обложка (миниатюра в строке).</summary>
    public List<CatalogImageDto> Images { get; set; } = [];

    // ── Разобрано сервером из строк ──

    public int? BaseRangeM { get; set; }

    public int? ShotsPerRound { get; set; }

    public int? MaxShotsPerRound { get; set; }

    public int? AmmoCapacity { get; set; }

    public List<int>? AmmoCapacityOptions { get; set; }

    public decimal? CostClassic { get; set; }

    public decimal? CostModern { get; set; }

    /// <summary>Дробовики: урон по дистанциям («4d6/2d6/1d6»).</summary>
    public List<RangeDamageDto>? DamageByRange { get; set; }
}

public sealed record RangeDamageDto(string Range, string Damage);

public sealed class SpellDto : CatalogItemDto
{
    public List<string> AltNames { get; set; } = [];

    public string SpellType { get; set; } = "";

    /// <summary>Текст книги; подсказку для боя разбирает <c>Core.SpellStatsReader</c>.</summary>
    public string? Cost { get; set; }

    public string? CastingTime { get; set; }

    public string Description { get; set; } = "";

    /// <summary>Иллюстрации, как у оружия: первая — обложка в строке справочника.</summary>
    public List<CatalogImageDto> Images { get; set; } = [];
}

public sealed class BookDto : CatalogItemDto
{
    public BookType BookType { get; set; }

    public List<string> AltNames { get; set; } = [];

    public string? Language { get; set; }

    public string? Year { get; set; }

    public string? Author { get; set; }

    public string? SanityLoss { get; set; }

    /// <summary>МКН — прибавка Мифов за начальное чтение.</summary>
    public int? MythosInitial { get; set; }

    /// <summary>МКП — за полное изучение.</summary>
    public int? MythosFull { get; set; }

    /// <summary>ЗМ — значение Мифов книги.</summary>
    public int? MythosRating { get; set; }

    public int? StudyWeeks { get; set; }

    public int? OccultismBonus { get; set; }

    public string Description { get; set; } = "";

    public Guid? ImageFileId { get; set; }

    /// <summary>Адрес обложки (только чтение).</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Возможные заклинания книги в её порядке.</summary>
    public List<BookSpellDto> Spells { get; set; } = [];
}

/// <summary>
/// Заклинание книги: как записано в книге и, если сопоставилось, — заклинание справочника. Сервер
/// сопоставляет несвязанные при записи (только точное совпадение имени или другого названия).
/// </summary>
public sealed record BookSpellDto(string RawName, Guid? SpellId);

public sealed class ItemDto : CatalogItemDto
{
    public string? Type { get; set; }

    public List<Era> Eras { get; set; } = [Era.Classic];

    public string? Description { get; set; }

    /// <summary>Цена в долларах 1920-х; null — не указана.</summary>
    /// <summary>Цена без хвоста нулей: 9000.00 в поле формы — «9000» (число то же, сравнение черновика не видит разницы).</summary>
    public decimal? Price
    {
        get;
        set => field = value / 1.0000000000000000000000000000m;
    }

    public Guid? ImageFileId { get; set; }

    /// <summary>Адрес картинки (только чтение).</summary>
    public string? ImageUrl { get; set; }
}

public sealed class CreatureDto : CatalogItemDto
{
    public CreatureType Type { get; set; }

    public string? Description { get; set; }

    public Statblock Statblock { get; set; } = new();

    /// <summary>Первая — обложка.</summary>
    public List<CatalogImageDto> Images { get; set; } = [];
}

/// <summary>
/// Артефакт главы 13. Оружие-артефакт стреляет в бою записью справочника оружия с тем же названием (молниемёт, электропушка):
/// связи по id нет, её находит страница.
/// </summary>
public sealed class ArtifactDto : CatalogItemDto
{
    public ArtifactKind Kind { get; set; } = ArtifactKind.Device;

    /// <summary>Кто им пользуется: «ми-го», «йитиане», «кто угодно».</summary>
    public List<string> UsedBy { get; set; } = [];

    /// <summary>Главное правило одной строкой («В игре»): проверка, урон, цена; null — не сказано.</summary>
    public string? Rule { get; set; }

    public string Description { get; set; } = "";

    /// <summary>Иллюстрации, как у заклинаний: первая — обложка в строке справочника.</summary>
    public List<CatalogImageDto> Images { get; set; } = [];
}

/// <summary>Картинка записи справочника (тварь, оружие, профессия, заклинание, артефакт) — файл <c>cm.files</c>; порядок — порядок в списке.</summary>
/// <param name="Url">Адрес картинки (только чтение; при записи сервер берёт <paramref name="FileId"/>).</param>
public sealed record CatalogImageDto(Guid FileId, string? Url, string? Caption);

/// <summary>Обложка записи (<c>PUT …/{id}/cover</c>): файл и можно ли заменить ту, что уже стоит.</summary>
public sealed record CatalogCoverRequest(Guid FileId, bool Replace);

/// <summary>
/// Кто держит запись справочника: <see cref="Count"/> держателей, имена первых — <see cref="Examples"/> («профессия
/// Врач», «лист сыщика Артур Нельсон»). <see cref="Blocks"/> — держатель не даёт удалить запись (навык у оружия и
/// профессий); иначе удаление возможно, а <see cref="Note"/> говорит, что останется («в листах останется название»).
/// </summary>
public sealed record CatalogUsage(int Count, IReadOnlyList<string> Examples, bool Blocks, string? Note = null)
{
    public static CatalogUsage None { get; } = new(0, [], false);
}

/// <summary>Справочник целиком и права текущего пользователя на него (одни на все записи).</summary>
public sealed record CatalogList<T>(IReadOnlyList<T> Items, bool CanEdit)
    where T : CatalogItemDto;

/// <summary>
/// Файл обмена справочником (<c>{ "catalog": "weapons", "items": [...] }</c>): его отдаёт экспорт и
/// читает импорт. Ключ записи — <c>code</c>, без него — имя без учёта регистра.
/// </summary>
public sealed record CatalogFile<T>(string Catalog, IReadOnlyList<T> Items)
    where T : CatalogItemDto;

public enum CatalogImportOutcome
{
    Created,
    Updated,
    Skipped,
    Failed,
}

/// <param name="Message">Почему пропущено или не записано; предупреждение по правилам.</param>
public sealed record CatalogImportLine(string Name, CatalogImportOutcome Outcome, string? Message);

/// <summary>Итог импорта или синхронизации с правилами. <see cref="DryRun"/> — ничего не записано.</summary>
public sealed record CatalogImportReport(
    bool DryRun,
    int Created,
    int Updated,
    int Skipped,
    int Failed,
    IReadOnlyList<CatalogImportLine> Lines,
    IReadOnlyList<string> Warnings);
