using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Characters.Services;
using CampaignManager.Web.Components.Features.Scenarios.Model;

namespace CampaignManager.Web.Components.Features.Scenarios.Services;

/// <summary>
///     Перенос сценария целиком одним JSON-файлом: заполнять полтора десятка локаций и десяток
///     листов НПС по одной форме — часы кликов, а сам текст всё равно готовится снаружи.
///     <para>
///         Пишет только через <see cref="ScenarioService" /> и <see cref="CharacterService" />,
///         поэтому валидация, права и <c>Init()</c> отрабатывают ровно так же, как при ручном вводе.
///     </para>
/// </summary>
public sealed class ScenarioImportService(
    ScenarioService scenarioService,
    CharacterService characterService,
    ILogger<ScenarioImportService> logger)
{
    private const string ExtraSkillsGroupName = "Особые навыки";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        // Импорт читает имена без учёта регистра, но экспорт — ещё и образец формата,
        // поэтому пишем camelCase: ровно так, как задокументирован обменный файл.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = null,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    ///     Создаёт новый сценарий из JSON. Импорт всегда создаёт новый сценарий, а не дописывает
    ///     существующий: повторный запуск того же файла даёт второй экземпляр, который видно в списке.
    /// </summary>
    public async Task<ScenarioImportResult> ImportAsync(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ScenarioImportResult { Success = false, Error = "Пустой JSON." };

        ScenarioImportDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ScenarioImportDto>(json, ReadOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Scenario import: malformed JSON");
            return new ScenarioImportResult { Success = false, Error = $"JSON не разобран: {ex.Message}" };
        }

        if (dto is null)
            return new ScenarioImportResult { Success = false, Error = "JSON не содержит объекта сценария." };

        if (string.IsNullOrWhiteSpace(dto.Name))
            return new ScenarioImportResult { Success = false, Error = "У сценария не заполнено поле name." };

        List<string> warnings = [];

        var locations = BuildLocations(dto.Locations, warnings);
        var keyFacts = BuildKeyFacts(dto.KeyFacts);
        var handouts = BuildHandouts(dto.Handouts);

        Scenario scenario = new()
        {
            Name = dto.Name.Trim(),
            Description = dto.Description,
            Location = dto.Location,
            Era = dto.Era,
            Journal = dto.Journal,
            IsTemplate = dto.IsTemplate,
            IsPublished = dto.IsPublished,
            ScheduledDate = dto.ScheduledDate,
            AnnouncementText = dto.AnnouncementText,
            Locations = locations,
            KeyFacts = keyFacts,
            Handouts = handouts
        };

        var created = await scenarioService.CreateScenarioAsync(scenario);
        if (created is null)
            return new ScenarioImportResult { Success = false, Error = "Не удалось создать сценарий." };

        var (npcsCreated, npcsReused) = await ImportCastAsync(created.Id, dto.Npcs, warnings);

        logger.LogInformation(
            "Imported scenario {ScenarioId} ({ScenarioName}): {Locations} locations, {KeyFacts} key facts, {Handouts} handouts, {NpcsCreated} new NPCs, {NpcsReused} reused NPCs",
            created.Id, created.Name, locations.Count, keyFacts.Count, handouts.Count, npcsCreated, npcsReused);

        return new ScenarioImportResult
        {
            Success = true,
            ScenarioId = created.Id,
            ScenarioName = created.Name,
            LocationsCreated = locations.Count,
            KeyFactsCreated = keyFacts.Count,
            HandoutsCreated = handouts.Count,
            NpcsCreated = npcsCreated,
            NpcsReused = npcsReused,
            Warnings = warnings
        };
    }

    /// <summary>
    ///     Отдаёт сценарий в том же формате, который принимает <see cref="ImportAsync" />.
    /// </summary>
    public async Task<string?> ExportAsync(Guid scenarioId)
    {
        var scenario = await scenarioService.GetScenarioByIdAsync(scenarioId);
        if (scenario is null) return null;

        var locationsById = scenario.Locations.ToDictionary(l => l.Id);
        var cast = await scenarioService.GetScenarioCastAsync(scenarioId);

        ScenarioImportDto dto = new()
        {
            Name = scenario.Name,
            Description = scenario.Description,
            Location = scenario.Location,
            Era = scenario.Era,
            Journal = scenario.Journal,
            IsTemplate = scenario.IsTemplate,
            IsPublished = scenario.IsPublished,
            ScheduledDate = scenario.ScheduledDate,
            AnnouncementText = scenario.AnnouncementText,
            KeyFacts = scenario.KeyFacts
                .OrderBy(f => f.Order)
                .Select(f => new ScenarioKeyFactImportDto { Title = f.Title, Type = f.Type, Content = f.Content })
                .ToList(),
            Locations = scenario.Locations
                .OrderBy(l => l.Order)
                .Select(l => new ScenarioLocationImportDto
                {
                    Name = l.Name,
                    Address = l.Address,
                    Description = l.Description,
                    Parent = l.ParentLocationId is { } parentId && locationsById.TryGetValue(parentId, out var parent)
                        ? parent.Name
                        : null,
                    SkillChecks = l.SkillChecks
                        .Select(c => new ScenarioSkillCheckImportDto
                        {
                            SkillName = c.SkillName,
                            Difficulty = c.Difficulty,
                            SuccessResult = c.SuccessResult,
                            FailureResult = c.FailureResult
                        })
                        .ToList()
                })
                .ToList(),
            Handouts = scenario.Handouts
                .OrderBy(h => h.Order)
                .Select(h => new ScenarioHandoutImportDto
                {
                    Name = h.Name,
                    Description = h.Description,
                    FileUrl = h.FileUrl
                })
                .ToList(),
            Npcs = cast.Where(c => c.Character is not null).Select(ToNpcDto).ToList()
        };

        return JsonSerializer.Serialize(dto, WriteOptions);
    }

    // ── Сборка сценария ────────────────────────────────────────────

    private static List<ScenarioLocation> BuildLocations(
        List<ScenarioLocationImportDto> source, List<string> warnings)
    {
        List<ScenarioLocation> locations = [];

        for (var i = 0; i < source.Count; i++)
        {
            var item = source[i];
            locations.Add(new ScenarioLocation
            {
                Name = string.IsNullOrWhiteSpace(item.Name) ? $"Локация {i + 1}" : item.Name.Trim(),
                Address = item.Address,
                Description = item.Description,
                Order = i,
                SkillChecks = item.SkillChecks
                    .Select(c => new ScenarioSkillCheck
                    {
                        SkillName = c.SkillName,
                        Difficulty = string.IsNullOrWhiteSpace(c.Difficulty) ? null : c.Difficulty,
                        SuccessResult = c.SuccessResult,
                        FailureResult = c.FailureResult
                    })
                    .ToList()
            });
        }

        // Родитель указан по имени: автор файла идентификаторов не знает и знать не должен.
        for (var i = 0; i < source.Count; i++)
        {
            var parentName = source[i].Parent;
            if (string.IsNullOrWhiteSpace(parentName)) continue;

            var parent = locations.FirstOrDefault(
                l => string.Equals(l.Name, parentName.Trim(), StringComparison.OrdinalIgnoreCase));

            if (parent is null)
                warnings.Add(
                    $"Локация «{locations[i].Name}»: родитель «{parentName}» не найден, оставлена на верхнем уровне.");
            else if (parent.Id == locations[i].Id)
                warnings.Add($"Локация «{locations[i].Name}» указана родителем самой себе — связь пропущена.");
            else
                locations[i].ParentLocationId = parent.Id;
        }

        return locations;
    }

    private static List<ScenarioKeyFact> BuildKeyFacts(List<ScenarioKeyFactImportDto> source)
    {
        return source
            .Where(f => !string.IsNullOrWhiteSpace(f.Title))
            .Select((f, index) => new ScenarioKeyFact
            {
                Title = f.Title.Trim(),
                Content = f.Content,
                Type = f.Type,
                Order = index
            })
            .ToList();
    }

    private static List<ScenarioHandout> BuildHandouts(List<ScenarioHandoutImportDto> source)
    {
        return source
            .Where(h => !string.IsNullOrWhiteSpace(h.Name))
            .Select((h, index) => new ScenarioHandout
            {
                Name = h.Name.Trim(),
                Description = h.Description,
                FileUrl = h.FileUrl,
                Order = index
            })
            .ToList();
    }

    // ── Состав НПС ─────────────────────────────────────────────────

    private async Task<(int Created, int Reused)> ImportCastAsync(
        Guid scenarioId, List<ScenarioNpcImportDto> npcs, List<string> warnings)
    {
        if (npcs.Count == 0) return (0, 0);

        var library = await characterService.GetNpcsAsync();
        Dictionary<string, Guid> byName = new(StringComparer.OrdinalIgnoreCase);
        foreach (var npc in library) byName.TryAdd(npc.CharacterName, npc.Id);

        var created = 0;
        var reused = 0;

        foreach (var item in npcs)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                warnings.Add("Пропущен НПС без имени.");
                continue;
            }

            var name = item.Name.Trim();

            if (byName.TryGetValue(name, out var existingId))
            {
                await scenarioService.AddNpcToScenarioAsync(scenarioId, existingId, item.Role, item.Count, item.Notes);
                warnings.Add(
                    $"НПС «{name}» уже был в библиотеке — занят существующий лист, параметры из файла не применялись.");
                reused++;
                continue;
            }

            try
            {
                var sheet = await characterService.CreateCharacterAsync(BuildCharacter(item), CharacterKind.Npc);
                byName[name] = sheet.Id;
                await scenarioService.AddNpcToScenarioAsync(scenarioId, sheet.Id, item.Role, item.Count, item.Notes);
                created++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scenario import: failed to create NPC sheet {NpcName}", name);
                warnings.Add($"Не удалось создать лист НПС «{name}»: {ex.Message}");
            }
        }

        return (created, reused);
    }

    private static Character BuildCharacter(ScenarioNpcImportDto item)
    {
        var characteristics = item.Characteristics;

        return new Character
        {
            PersonalInfo = new PersonalInfo
            {
                Name = item.Name.Trim(),
                Occupation = item.Occupation ?? string.Empty,
                Age = item.Age,
                Gender = item.Gender ?? string.Empty,
                DamageBonus = item.DamageBonus ?? "0",
                Build = item.Build ?? "0",
                MoveSpeed = item.MoveSpeed,
                Dodge = item.Dodge
            },
            Characteristics = new Characteristics
            {
                Strength = new AttributeValue(characteristics.Str),
                Constitution = new AttributeValue(characteristics.Con),
                Size = new AttributeValue(characteristics.Siz),
                Dexterity = new AttributeValue(characteristics.Dex),
                Intelligence = new AttributeValue(characteristics.Int),
                Appearance = new AttributeValue(characteristics.App),
                Power = new AttributeValue(characteristics.Pow),
                Education = new AttributeValue(characteristics.Edu)
            },
            DerivedAttributes = new DerivedAttributes
            {
                HitPoints = new AttributeWithMaxValue(item.HitPoints, item.HitPoints),
                MagicPoints = new AttributeWithMaxValue(item.MagicPoints, item.MagicPoints),
                Sanity = new AttributeWithMaxValue(item.Sanity, 99),
                Luck = new AttributeWithMaxValue(item.Luck, 99)
            },
            Skills = BuildSkills(item.Skills),
            Backstory = item.Backstory ?? string.Empty
        };
    }

    private static SkillsModel BuildSkills(Dictionary<string, int> source)
    {
        var skills = SkillsModel.DefaultSkillsModel();
        if (source.Count == 0) return skills;

        foreach (var (rawName, value) in source)
        {
            if (string.IsNullOrWhiteSpace(rawName)) continue;

            var name = rawName.Trim();
            var match = skills.SkillGroups
                .SelectMany(g => g.Skills)
                .FirstOrDefault(s => IsSameSkillName(s.Name, name));

            if (match is not null)
            {
                match.Value = new AttributeValue(value);
                match.IsUsed = true;
                continue;
            }

            var extras = skills.SkillGroups.FirstOrDefault(g => g.Name == ExtraSkillsGroupName);
            if (extras is null)
            {
                extras = new SkillGroup { Name = ExtraSkillsGroupName };
                skills.SkillGroups.Add(extras);
            }

            extras.AddSkill(new Skill
            {
                Name = name,
                Value = new AttributeValue(value),
                BaseValue = "—",
                IsUsed = true
            });
        }

        return skills;
    }

    /// <summary>
    ///     Имена навыков в книге и в листе расходятся по «ё» и регистру — сверяем без них.
    /// </summary>
    private static bool IsSameSkillName(string left, string right)
    {
        return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

        static string Normalize(string value)
        {
            return value.Trim().Replace('ё', 'е').Replace('Ё', 'Е');
        }
    }

    private static ScenarioNpcImportDto ToNpcDto(ScenarioNpc cast)
    {
        var character = cast.Character!.Character;
        var personal = character.PersonalInfo;
        var characteristics = character.Characteristics;

        return new ScenarioNpcImportDto
        {
            Name = cast.Character.CharacterName,
            Role = cast.Role,
            Count = cast.Count,
            Notes = cast.Notes,
            Occupation = string.IsNullOrWhiteSpace(personal.Occupation) ? null : personal.Occupation,
            Age = personal.Age,
            Gender = string.IsNullOrWhiteSpace(personal.Gender) ? null : personal.Gender,
            Backstory = string.IsNullOrWhiteSpace(character.Backstory) ? null : character.Backstory,
            Characteristics = new ScenarioNpcCharacteristicsDto
            {
                Str = characteristics.Strength.Regular,
                Con = characteristics.Constitution.Regular,
                Siz = characteristics.Size.Regular,
                Dex = characteristics.Dexterity.Regular,
                Int = characteristics.Intelligence.Regular,
                App = characteristics.Appearance.Regular,
                Pow = characteristics.Power.Regular,
                Edu = characteristics.Education.Regular
            },
            HitPoints = character.DerivedAttributes.HitPoints.MaxValue,
            MagicPoints = character.DerivedAttributes.MagicPoints.MaxValue,
            Sanity = character.DerivedAttributes.Sanity.Value,
            Luck = character.DerivedAttributes.Luck.Value,
            DamageBonus = personal.DamageBonus,
            Build = personal.Build,
            MoveSpeed = personal.MoveSpeed,
            Dodge = personal.Dodge,
            Skills = character.Skills.SkillGroups
                .SelectMany(g => g.Skills)
                .Where(s => s.IsUsed)
                .GroupBy(s => s.Name)
                .ToDictionary(g => g.Key, g => g.First().Value.Regular)
        };
    }
}
