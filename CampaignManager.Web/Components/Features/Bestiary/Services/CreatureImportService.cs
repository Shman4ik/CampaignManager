using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CampaignManager.Web.Components.Features.Bestiary.Model;

namespace CampaignManager.Web.Components.Features.Bestiary.Services;

/// <summary>
///     Перенос бестиария одним JSON-файлом. Статблок твари — это полсотни полей, и сверять
///     их с книгой удобнее снаружи, пачкой, чем правкой формы по одному существу.
///     <para>
///         В отличие от фонотеки, существо с уже занятым именем по умолчанию <b>обновляется</b>:
///         основной сценарий — привести заведённых тварей к книге. Пишет только через
///         <see cref="CreatureService" />, поэтому кеш и <c>Init()</c> отрабатывают как при ручном вводе.
///         Устроен по образцу <c>MusicImportService</c>.
///     </para>
/// </summary>
public sealed partial class CreatureImportService(
    CreatureService creatureService,
    ILogger<CreatureImportService> logger)
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        // Экспорт — ещё и образец формата, поэтому camelCase и отступы.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    ///     Заводит и обновляет существ из JSON. Имя сравнивается без учёта регистра.
    /// </summary>
    /// <param name="json">Объект <c>{ "creatures": [...] }</c>, голый массив или одно существо.</param>
    /// <param name="overwriteExisting">
    ///     <c>true</c> — существо с тем же именем переписывается целиком, кроме картинок, если их нет в файле,
    ///     и исходного текста книги; <c>false</c> — пропускается.
    /// </param>
    /// <param name="dryRun">
    ///     Только проверить: посчитать, что заведётся и обновится, и собрать замечания, ничего не сохраняя.
    ///     Большой файл сверки с книгой разумно сначала прогнать так.
    /// </param>
    public async Task<CreatureImportResult> ImportAsync(string json, bool overwriteExisting, bool dryRun = false)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new CreatureImportResult { Success = false, Error = "Пустой JSON." };

        BestiaryImportDto? dto;
        try
        {
            dto = ParseBestiary(json);
        }
        catch (JsonException ex)
        {
            return new CreatureImportResult { Success = false, Error = $"JSON не разобран: {ex.Message}" };
        }

        if (dto is null || dto.Creatures.Count == 0)
            return new CreatureImportResult { Success = false, Error = "В файле нет ни одного существа." };

        var result = new CreatureImportResult { Success = true, DryRun = dryRun };
        var existing = (await creatureService.GetAllCreaturesAsync(pageSize: int.MaxValue))
            .ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in dto.Creatures)
        {
            var name = item.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                result.Warnings.Add("Существо без поля name пропущено.");
                result.Skipped++;
                continue;
            }

            if (!seenInFile.Add(name))
            {
                result.Warnings.Add($"«{name}» встречается в файле дважды — второе пропущено.");
                result.Skipped++;
                continue;
            }

            var creature = Build(name, item, result.Warnings);

            var formerName = item.FormerName?.Trim();
            var renaming = !existing.ContainsKey(name)
                           && !string.IsNullOrEmpty(formerName)
                           && existing.ContainsKey(formerName);

            if (existing.TryGetValue(renaming ? formerName! : name, out var current))
            {
                if (!overwriteExisting)
                {
                    result.Warnings.Add($"«{name}» уже есть в бестиарии, пропущено.");
                    result.Skipped++;
                    continue;
                }

                creature.Id = current.Id;
                creature.CreatedAt = current.CreatedAt;
                creature.LastUpdated = DateTimeOffset.UtcNow;
                // Исходный текст книги — источник для повторного разбора, импорт его не знает и не трогает.
                creature.CombatDescriptions = current.CombatDescriptions;
                creature.Images = item.Images is null ? current.Images : creature.Images;

                if (dryRun || await creatureService.UpdateCreatureAsync(creature))
                {
                    result.Updated++;
                    if (renaming)
                    {
                        result.Renamed++;
                        result.Warnings.Add($"«{formerName}» переименовано в «{name}».");
                        // Прежнее имя освободилось: следующее существо файла может завестись под ним.
                        existing.Remove(formerName!);
                        existing[name] = creature;
                    }
                }
                else
                {
                    result.Warnings.Add($"«{name}» — не удалось сохранить.");
                    result.Skipped++;
                }

                continue;
            }

            if (!dryRun && await creatureService.CreateCreatureAsync(creature) is null)
            {
                result.Warnings.Add($"«{name}» — не удалось сохранить.");
                result.Skipped++;
                continue;
            }

            result.Created++;
        }

        if (!dryRun)
            logger.LogInformation("Импорт бестиария: заведено {Created}, обновлено {Updated}, пропущено {Skipped}",
                result.Created, result.Updated, result.Skipped);

        return result;
    }

    /// <summary>
    ///     Принимает объект <c>{ "creatures": [...] }</c>, голый массив и одно существо —
    ///     поправить одну тварь проще, не заворачивая её в список.
    /// </summary>
    private static BestiaryImportDto? ParseBestiary(string json)
    {
        var trimmed = json.TrimStart();
        if (trimmed.StartsWith('['))
            return new BestiaryImportDto
            {
                Creatures = JsonSerializer.Deserialize<List<CreatureImportDto>>(json, ReadOptions) ?? []
            };

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        var isSingle = document.RootElement.ValueKind == JsonValueKind.Object
                       && !document.RootElement.EnumerateObject()
                           .Any(p => p.Name.Equals("creatures", StringComparison.OrdinalIgnoreCase));

        if (isSingle)
        {
            var single = document.RootElement.Deserialize<CreatureImportDto>(ReadOptions);
            return single is null ? null : new BestiaryImportDto { Creatures = [single] };
        }

        return document.RootElement.Deserialize<BestiaryImportDto>(ReadOptions);
    }

    private static Creature Build(string name, CreatureImportDto dto, List<string> warnings)
    {
        var type = CreatureType.Other;
        if (!string.IsNullOrWhiteSpace(dto.Type) && !Enum.TryParse(dto.Type.Trim(), true, out type))
        {
            warnings.Add($"«{name}» — неизвестный тип «{dto.Type}», записан как «Прочее».");
            type = CreatureType.Other;
        }

        var characteristics = dto.Characteristics ?? new CreatureCharacteristics();
        characteristics.Strength ??= new CreatureCharacteristicModel();
        characteristics.Dexterity ??= new CreatureCharacteristicModel();
        characteristics.Intelligence ??= new CreatureCharacteristicModel();
        characteristics.Constitution ??= new CreatureCharacteristicModel();
        characteristics.Power ??= new CreatureCharacteristicModel();
        characteristics.Size ??= new CreatureCharacteristicModel();
        characteristics.AverageDamageBonus = NormalizeDice(characteristics.AverageDamageBonus ?? string.Empty);
        characteristics.SanityLoss = NormalizeDice(characteristics.SanityLoss ?? string.Empty);
        characteristics.AttacksPerRound = Math.Max(1, characteristics.AttacksPerRound);

        if (dto.Characteristics is null)
            warnings.Add($"«{name}» — нет блока characteristics, характеристики пустые.");
        if (characteristics.HealPoint <= 0)
            warnings.Add($"«{name}» — ПЗ не заданы: в бою существо упадёт от первого попадания.");
        if (!string.IsNullOrEmpty(characteristics.AverageDamageBonus) && !IsRollable(characteristics.AverageDamageBonus))
            warnings.Add($"«{name}» — бонус к урону «{characteristics.AverageDamageBonus}» бой не сможет бросить.");
        if (!string.IsNullOrEmpty(characteristics.SanityLoss) && !characteristics.SanityLoss.Contains('/'))
            warnings.Add($"«{name}» — потеря рассудка «{characteristics.SanityLoss}» не в виде «успех/провал».");

        var attacks = new List<CreatureAttack>();
        foreach (var attack in dto.Attacks ?? [])
        {
            attack.Name = attack.Name?.Trim() ?? string.Empty;
            if (attack.Name.Length == 0)
            {
                warnings.Add($"«{name}» — атака без названия пропущена.");
                continue;
            }

            attack.DamageFormula = NormalizeDice(attack.DamageFormula ?? string.Empty);
            if (attack.DamageFormula.Length == 0)
                attack.DamageFormula = "0";

            if (!IsRollable(attack.DamageFormula))
                warnings.Add($"«{name}»: «{attack.Name}» — урон «{attack.DamageFormula}» бой не сможет бросить.");
            // У особой атаки процента может не быть вовсе — её решает встречная проверка или
            // автоматический эффект; бой такую атаку в бросок не берёт (AttackSetupPanel).
            if (attack.SkillValue <= 0 && attack.Kind is not CreatureAttackKind.Special)
                warnings.Add($"«{name}»: «{attack.Name}» — навык атаки не задан.");

            attack.Description = NullIfBlank(attack.Description);
            attacks.Add(attack);
        }

        if (attacks.Count == 0)
            warnings.Add($"«{name}» — ни одной атаки: в бою существо сможет только уклоняться.");

        return new Creature
        {
            Name = name,
            Type = type,
            Description = NullIfBlank(dto.Description),
            CreatureCharacteristics = characteristics,
            Attacks = attacks,
            Skills =
            [
                .. (dto.Skills ?? [])
                    .Where(s => !string.IsNullOrWhiteSpace(s.Name))
                    .Select(s => new CreatureSkill { Name = s.Name.Trim(), Value = s.Value, Note = NullIfBlank(s.Note) })
            ],
            // Ключи, различные только пробелами по краям, после обрезки совпали бы — побеждает последний.
            SpecialAbilities = (dto.SpecialAbilities ?? [])
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                .GroupBy(kv => kv.Key.Trim())
                .ToDictionary(g => g.Key, g => g.Last().Value?.Trim() ?? string.Empty),
            Images =
            [
                .. (dto.Images ?? [])
                    .Where(i => !string.IsNullOrWhiteSpace(i.Url))
                    .Select(i => new CreatureImage { Url = i.Url.Trim(), Caption = NullIfBlank(i.Caption) })
            ]
        };
    }

    /// <summary>
    ///     Приводит формулу к виду, который понимает <c>CombatService.RollDiceFormula</c>:
    ///     русская «д» из книги и типографский минус превращаются в «d» и «-», пробелы уходят.
    /// </summary>
    public static string NormalizeDice(string formula) =>
        formula.Trim()
            .Replace('д', 'd').Replace('Д', 'd').Replace('D', 'd')
            .Replace('−', '-').Replace('–', '-')
            .Replace(" ", string.Empty);

    /// <summary>
    ///     Формула урона, которую бой бросит целиком. Нераспознанные куски
    ///     <c>RollDiceFormula</c> молча пропускает — «2d6+БкУ» дала бы только 2d6.
    /// </summary>
    public static bool IsRollable(string formula) => RollableFormula().IsMatch(formula);

    [GeneratedRegex(@"^[+-]?(\d*d\d+|\d+)([+-](\d*d\d+|\d+))*$", RegexOptions.IgnoreCase)]
    private static partial Regex RollableFormula();

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    ///     Отдаёт существ в том же виде, который принимает импорт. Без аргумента — весь бестиарий.
    /// </summary>
    public async Task<string> ExportAsync(IReadOnlyCollection<Guid>? ids = null)
    {
        var creatures = await creatureService.GetAllCreaturesAsync(pageSize: int.MaxValue);
        if (ids is not null)
            creatures = [.. creatures.Where(c => ids.Contains(c.Id))];

        var dto = new BestiaryImportDto
        {
            Creatures =
            [
                .. creatures
                    .OrderBy(c => c.Type)
                    .ThenBy(c => c.Name, StringComparer.CurrentCulture)
                    .Select(c => new CreatureImportDto
                    {
                        Name = c.Name,
                        Type = c.Type.ToString(),
                        Description = c.Description,
                        Characteristics = c.CreatureCharacteristics,
                        Attacks = c.Attacks,
                        Skills = c.Skills,
                        SpecialAbilities = c.SpecialAbilities,
                        Images = c.Images
                    })
            ]
        };

        return JsonSerializer.Serialize(dto, WriteOptions);
    }
}
