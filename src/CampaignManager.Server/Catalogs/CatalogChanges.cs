using System.Reflection;
using System.Text.Json;
using CampaignManager.Contracts.Catalogs;

namespace CampaignManager.Server.Catalogs;

/// <summary>
/// «Что изменилось» в отчёте «С правилами» и перезаписывающего импорта: снимок полей записи до и после,
/// разница — подписями колонок. Без этого Хранитель видит «обновлена» у всех записей и не знает, что
/// именно сид перезаписал из его правок.
/// </summary>
internal static class CatalogChanges
{
    // Служебные и производные поля: меняются сами или показывают то, что уже есть в другом поле.
    private static readonly HashSet<string> Ignored =
    [
        nameof(CatalogItemDto.Id), nameof(CatalogItemDto.Version), "ImageUrl", "SkillName", "OptionNames",
        "ProfessionalSkillCount", "BaseRangeM", "ShotsPerRound", "MaxShotsPerRound", "AmmoCapacity",
        "AmmoCapacityOptions", "CostClassic", "CostModern", "DamageByRange",
    ];

    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["Code"] = "код", ["Name"] = "название", ["Source"] = "источник", ["ParentId"] = "родитель",
        ["BaseValue"] = "база", ["BaseFormula"] = "формула базы", ["Category"] = "категория",
        ["IsUncommon"] = "редкий", ["Eras"] = "эпохи", ["Description"] = "описание",
        ["UsageExamples"] = "примеры применения", ["FailureConsequences"] = "последствия провала",
        ["OpposingSkills"] = "противоположные навыки", ["TimeRequired"] = "время", ["CanRetry"] = "повтор",
        ["SkillPointsFormula"] = "очки навыков", ["CreditRatingMin"] = "Средства: от", ["CreditRatingMax"] = "Средства: до",
        ["IsLovecraftian"] = "из Лавкрафта", ["Tags"] = "теги", ["Slots"] = "слоты навыков",
        ["Type"] = "тип", ["SkillId"] = "навык", ["IsRare"] = "редкое", ["IsImpaling"] = "колющее",
        ["Damage"] = "урон", ["Range"] = "дальность", ["Attacks"] = "атаки", ["Ammo"] = "боезапас",
        ["Malfunction"] = "осечка", ["Cost"] = "стоимость", ["Notes"] = "заметки", ["SingleUse"] = "одноразовое",
        ["AltNames"] = "другие названия", ["SpellType"] = "тип заклинания", ["CastingTime"] = "время сотворения",
        ["BookType"] = "вид книги", ["Language"] = "язык", ["Year"] = "год", ["Author"] = "автор",
        ["SanityLoss"] = "потеря Рассудка", ["MythosInitial"] = "Мифы (начальное)", ["MythosFull"] = "Мифы (полное)",
        ["MythosRating"] = "значение Мифов", ["StudyWeeks"] = "недель изучения", ["OccultismBonus"] = "бонус к Оккультизму",
        ["ImageFileId"] = "картинка", ["Spells"] = "заклинания", ["Price"] = "цена",
        ["Statblock"] = "статблок", ["Images"] = "картинки",
    };

    private static readonly Dictionary<Type, PropertyInfo[]> Properties = [];

    public static Dictionary<string, string> Snapshot<TDto>(TDto dto)
        where TDto : CatalogItemDto
    {
        var type = dto.GetType();
        PropertyInfo[] properties;
        lock (Properties)
        {
            if (!Properties.TryGetValue(type, out properties!))
            {
                properties = [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && !Ignored.Contains(p.Name))];
                Properties[type] = properties;
            }
        }

        return properties.ToDictionary(p => p.Name, p => JsonSerializer.Serialize(p.GetValue(dto), p.PropertyType));
    }

    /// <summary>Подписи изменившихся полей; пусто — запись совпала с сидом.</summary>
    public static IReadOnlyList<string> Diff(Dictionary<string, string> before, Dictionary<string, string> after) =>
        [.. after.Where(kv => !before.TryGetValue(kv.Key, out var old) || old != kv.Value)
            .Select(kv => Labels.GetValueOrDefault(kv.Key, kv.Key))];
}
