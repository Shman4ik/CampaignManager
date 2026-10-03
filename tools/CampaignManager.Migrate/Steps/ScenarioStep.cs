using System.Globalization;
using System.Text.Json.Nodes;
using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Documents;
using CampaignManager.Core.Scenarios;
using CampaignManager.Data.Campaigns;
using CampaignManager.Data.Scenarios;
using CampaignManager.Migrate.Catalogs;
using CampaignManager.Migrate.V1;

namespace CampaignManager.Migrate.Steps;

/// <summary>
/// Сценарии: содержимое — библиотека, игра в кампании — прохождение (<c>scenario_runs</c>). Копия шаблона в
/// кампании — форк с <c>source_scenario_id</c>: тексты разошлись в обе стороны, поэтому не сливаем.
/// </summary>
public static partial class ScenarioStep
{
    private static readonly Dictionary<string, Characteristic> Characteristics = new(StringComparer.OrdinalIgnoreCase)
    {
        ["СИЛ"] = Characteristic.STR,
        ["ВЫН"] = Characteristic.CON,
        ["ТЕЛ"] = Characteristic.SIZ,
        ["ЛВК"] = Characteristic.DEX,
        ["НАР"] = Characteristic.APP,
        ["ИНТ"] = Characteristic.INT,
        ["МОЩ"] = Characteristic.POW,
        ["ОБР"] = Characteristic.EDU,
    };

    public static async Task RunAsync(MigrationState s, CancellationToken cancellationToken)
    {
        var templates = s.V1.Scenarios.Where(row => row.Bool("IsTemplate"))
            .GroupBy(row => row.Text("Name")!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Guid("Id")!.Value, StringComparer.Ordinal);
        var counts = new Counts();
        var runsByScenario = new Dictionary<(Guid Campaign, Guid Scenario), ScenarioRun>();

        foreach (var row in s.V1.Scenarios)
        {
            var name = row.Text("Name")!;
            var id = row.Guid("Id")!.Value;
            var scenario = new Scenario
            {
                Id = id,
                Name = name,
                Summary = row.Text("Description"),
                BodyMd = row.Text("Journal"),
                Setting = row.Text("Location"),
                SettingDate = row.Text("Era"),
                Era = EraOf(row.Text("Era")),
                AuthorId = s.UserId(row.Str("CreatorEmail")),
                CreatedAt = row.Time("CreatedAt") ?? default,
                UpdatedAt = row.Time("LastUpdated") ?? default,
            };
            if (!row.Bool("IsTemplate") && templates.TryGetValue(name, out var templateId) && templateId != id)
            {
                scenario.SourceScenarioId = templateId;
                s.Report.Add(ReportSections.Fixed, $"сценарий «{name}»: копия шаблона в кампании → форк шаблона (source_scenario_id)");
            }

            if (scenario.Era is null && scenario.SettingDate is not null)
            {
                s.Report.Add(ReportSections.Warnings, $"сценарий «{name}»: эпоха «{scenario.SettingDate}» не разобрана — era пуста");
            }

            Locations(s, row, scenario, counts);
            KeyFacts(row, scenario, counts);
            await HandoutsAsync(s, row, scenario, counts, cancellationToken);
            Creatures(s, row, scenario, counts);
            Items(s, row, scenario, counts);

            s.Scenarios.Add(id);
            s.Db.Scenarios.Add(scenario);

            if (row.Guid("CampaignId") is { } campaignId && s.Campaigns.TryGetValue(campaignId, out var campaign))
            {
                var run = new ScenarioRun
                {
                    ScenarioId = id,
                    CampaignId = campaignId,
                    Status = campaign.Status == CampaignStatus.Completed ? ScenarioRunStatus.Finished
                        : row.Bool("IsPublished") ? ScenarioRunStatus.Announced
                        : ScenarioRunStatus.Planned,
                    ScheduledAt = ScheduledAt(row.Str("ScheduledDate"), s.Options.KeeperTimeZone),
                    Announcement = row.Text("AnnouncementText"),
                    // У завершённого прохождения запись закрыта (решение владельца 2026-10-03, #192), как после «Завершить»
                    SignupOpen = row.Bool("IsPublished") && campaign.Status != CampaignStatus.Completed,
                    CreatedAt = scenario.CreatedAt,
                    UpdatedAt = scenario.UpdatedAt,
                };
                if (row.Bool("IsPublished") && run.Status == ScenarioRunStatus.Finished)
                {
                    s.Report.Add(ReportSections.Fixed, $"прохождение «{name}»: завершено, а в v1 опубликовано → запись закрыта (signup_open = false)");
                }

                if (run.ScheduledAt is { } at)
                {
                    s.Report.Add(ReportSections.Fixed,
                        $"сценарий «{name}»: дата игры «{row.Str("ScheduledDate")}» без пояса → {at:yyyy-MM-dd HH:mm} UTC (пояс Хранителя {s.Options.KeeperTimeZone})");
                }

                runsByScenario[(campaignId, id)] = run;
                s.Db.ScenarioRuns.Add(run);
            }
        }

        var sessions = 0;
        foreach (var row in s.V1.CampaignSessions)
        {
            var campaignId = row.Guid("CampaignId")!.Value;
            s.Db.CampaignSessions.Add(new CampaignSession
            {
                Id = row.Guid("Id")!.Value,
                CampaignId = campaignId,
                RunId = row.Guid("ScenarioId") is { } scenarioId && runsByScenario.TryGetValue((campaignId, scenarioId), out var run) ? run.Id : null,
                Number = row.Int("Number") ?? 0,
                SessionDate = DateOnly.TryParse(row.Str("SessionDate"), CultureInfo.InvariantCulture, out var date) ? date : default,
                Title = row.Text("Title"),
                Summary = row.Text("Summary"),
                KeeperNotes = row.Text("KeeperNotes"),
                ScenarioCompleted = row.Bool("ScenarioCompleted"),
                CreatedAt = row.Time("CreatedAt") ?? default,
                UpdatedAt = row.Time("LastUpdated") ?? default,
            });
            sessions++;
        }

        foreach (var (name, prop) in s.ScenarioProps)
        {
            s.Report.Add(ReportSections.Warnings,
                $"реквизит «{name}» ({prop.Text("Type")}) не нашёлся ни в одном сценарии — не перенесён (вопрос владельцу)");
        }

        s.Report.Count("games.Scenarios", s.V1.Scenarios.Count, "scenarios", s.Scenarios.Count, $"прохождений — {runsByScenario.Count}");
        s.Report.Count("Scenarios.Locations", counts.V1Locations, "scenario_locations", counts.Locations);
        s.Report.Count("Locations.SkillChecks", counts.V1Checks, "scenario_checks", counts.Checks,
            $"характеристика — {counts.CharacteristicChecks}, Удача — {counts.LuckChecks}");
        s.Report.Count("Scenarios.KeyFacts", counts.KeyFacts, "scenario_key_facts", counts.KeyFacts);
        s.Report.Count("Scenarios.Handouts", counts.Handouts, "scenario_handouts", counts.Handouts, $"с файлом — {counts.HandoutFiles}");
        s.Report.Count("Scenarios.ScenarioCreatures", counts.V1Creatures, "scenario_creatures", counts.Creatures,
            $"со своим статблоком — {counts.StatblockOverrides}");
        s.Report.Count("Scenarios.ScenarioItems", counts.V1Items, "scenario_items", counts.Items, $"реквизит без справочника — {counts.Props}");
        s.Report.Count("games.CampaignSessions", s.V1.CampaignSessions.Count, "campaign_sessions", sessions);
    }

    /// <summary>Состав НПС — после листов: ссылается на <c>characters</c>.</summary>
    public static void Npcs(MigrationState s)
    {
        var count = 0;
        foreach (var row in s.V1.ScenarioNpcs)
        {
            var (scenarioId, characterId) = (row.Guid("ScenarioId")!.Value, row.Guid("CharacterId")!.Value);
            if (!s.Scenarios.Contains(scenarioId) || !s.Characters.Contains(characterId))
            {
                s.Report.Add(ReportSections.DroppedJunk, $"связь НПС {characterId} со сценарием {scenarioId}: одной из сторон нет");
                continue;
            }

            s.Db.ScenarioNpcs.Add(new ScenarioNpc
            {
                ScenarioId = scenarioId,
                CharacterId = characterId,
                Role = row.Enum<NpcRole>("Role") ?? NpcRole.Neutral,
                Count = Math.Max(1, row.Int("Count") ?? 1),
                Notes = row.Text("Notes"),
            });
            count++;
        }

        s.Report.Count("games.ScenarioNpcs", s.V1.ScenarioNpcs.Count, "scenario_npcs", count);
    }

    /// <summary>Эпоха из свободного текста v1 («Июнь 1925 года», «1930-е»): год 1890–1949 — классика.</summary>
    public static Era? EraOf(string? text) => ScenarioEra.FromSettingDate(text);

    /// <summary>
    /// Время игры v1 «как ввели» (без пояса) — в UTC из пояса Хранителя; смещение — на саму дату (летом в Праге
    /// +2, зимой +1). Время, которого в поясе нет (час перевода стрелок вперёд), сдвигается на час позже.
    /// </summary>
    public static DateTimeOffset? ScheduledAt(string? text, string timeZone)
    {
        if (string.IsNullOrWhiteSpace(text)
            || !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return null;
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        var utc = TimeZoneInfo.ConvertTimeToUtc(local, zone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    private static void Locations(MigrationState s, JsonObject row, Scenario scenario, Counts counts)
    {
        var locations = row.Arr("Locations").OfType<JsonObject>().ToList();
        var ids = locations.Select(l => l.Guid("Id")).OfType<Guid>().ToHashSet();
        foreach (var location in locations)
        {
            counts.V1Locations++;
            var entity = new ScenarioLocation
            {
                Id = location.Guid("Id") ?? Guid.CreateVersion7(),
                ScenarioId = scenario.Id,
                ParentId = location.Guid("ParentLocationId") is { } parent && ids.Contains(parent) ? parent : null,
                Ord = location.Int("Order") ?? counts.Locations,
                Name = location.Text("Name") ?? "Без названия",
                Address = location.Text("Address"),
                Description = location.Text("Description"),
                MusicTags = location.Strings("MusicTags"),
            };
            if (location.Guid("ParentLocationId") is { } missing && !ids.Contains(missing))
            {
                s.Report.Add(ReportSections.Warnings, $"сценарий «{scenario.Name}», локация «{entity.Name}»: родителя нет — стала корневой");
            }

            var ord = 0;
            foreach (var check in location.Arr("SkillChecks").OfType<JsonObject>())
            {
                counts.V1Checks++;
                var target = check.Text("SkillName") ?? "";
                var entityCheck = new ScenarioCheck
                {
                    Id = check.Guid("Id") ?? Guid.CreateVersion7(),
                    Ord = ord,
                    Difficulty = check.Enum<Difficulty>("Difficulty") ?? Difficulty.Regular,
                    OnSuccess = check.Text("SuccessResult"),
                    OnFailure = check.Text("FailureResult"),
                };
                if (Characteristics.TryGetValue(target, out var characteristic))
                {
                    entityCheck.TargetKind = CheckTarget.Characteristic;
                    entityCheck.Characteristic = characteristic;
                    counts.CharacteristicChecks++;
                }
                else if (string.Equals(target, "Удача", StringComparison.OrdinalIgnoreCase))
                {
                    entityCheck.TargetKind = CheckTarget.Luck;
                    counts.LuckChecks++;
                }
                else if (s.Resolver.CatalogId(target) is { } skillId)
                {
                    entityCheck.TargetKind = CheckTarget.Skill;
                    entityCheck.SkillId = skillId;
                }
                else
                {
                    s.Report.Add(ReportSections.Warnings,
                        $"сценарий «{scenario.Name}», локация «{entity.Name}»: проверка «{target}» — навыка нет в справочнике, не перенесена");
                    continue;
                }

                entity.Checks.Add(entityCheck);
                ord++;
                counts.Checks++;
            }

            foreach (var trackId in location.Arr("MusicTrackIds").Select(t => Guid.TryParse(t?.ToString(), out var g) ? g : (Guid?)null).OfType<Guid>())
            {
                if (s.Tracks.Contains(trackId))
                {
                    s.Db.LocationTracks.Add(new LocationTrack { LocationId = entity.Id, TrackId = trackId });
                }
            }

            scenario.Locations.Add(entity);
            counts.Locations++;
        }
    }

    private static void KeyFacts(JsonObject row, Scenario scenario, Counts counts)
    {
        foreach (var fact in row.Arr("KeyFacts").OfType<JsonObject>())
        {
            scenario.KeyFacts.Add(new ScenarioKeyFact
            {
                Id = fact.Guid("Id") ?? Guid.CreateVersion7(),
                Ord = fact.Int("Order") ?? scenario.KeyFacts.Count,
                Type = fact.Enum<KeyFactType>("Type") ?? KeyFactType.Backstory,
                Title = fact.Text("Title") ?? "",
                Content = fact.Text("Content"),
            });
            counts.KeyFacts++;
        }
    }

    private static async Task HandoutsAsync(MigrationState s, JsonObject row, Scenario scenario, Counts counts, CancellationToken cancellationToken)
    {
        foreach (var handout in row.Arr("Handouts").OfType<JsonObject>())
        {
            var entity = new ScenarioHandout
            {
                Id = handout.Guid("Id") ?? Guid.CreateVersion7(),
                Ord = handout.Int("Order") ?? scenario.Handouts.Count,
                Name = handout.Text("Name") ?? "Раздатка",
                PlayerText = handout.Text("Description"),
                KeeperNote = handout.Text("KeeperNote"),
            };
            if (handout.Text("FileUrl") is { } url)
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.EndsWith("example.com", StringComparison.OrdinalIgnoreCase))
                {
                    // Заглушка из примера импорта: такого файла нет и не было
                    s.Report.Add(ReportSections.DroppedJunk, $"сценарий «{scenario.Name}», раздатка «{entity.Name}»: адрес-заглушка `{url}`");
                }
                else if (uri is not null && uri.Scheme == Uri.UriSchemeHttps)
                {
                    entity.FileId = s.ExternalFile(url).Id;
                    counts.HandoutFiles++;
                }
                else if (await s.StoredFileAsync(url, $"раздатка «{entity.Name}»", cancellationToken) is { } file)
                {
                    entity.FileId = file.Id;
                    counts.HandoutFiles++;
                }
            }

            scenario.Handouts.Add(entity);
            counts.Handouts++;
        }
    }

    private static void Creatures(MigrationState s, JsonObject row, Scenario scenario, Counts counts)
    {
        var ord = 0;
        foreach (var creature in row.Arr("ScenarioCreatures").OfType<JsonObject>())
        {
            counts.V1Creatures++;
            var name = creature.Text("Name") ?? "";
            var statblock = StatblockConverter.Convert(creature, s.Resolver.CatalogId);
            var entity = new ScenarioCreature
            {
                Id = creature.Guid("Id") ?? Guid.CreateVersion7(),
                ScenarioId = scenario.Id,
                Ord = ord++,
                LocationNote = creature.Text("Location"),
                Notes = creature.Text("Notes"),
            };

            if (s.CreaturesByV1Name.TryGetValue(name, out var catalog))
            {
                entity.CreatureId = catalog.Creature.Id;
                if (StatblockConverter.Canonical(statblock) != catalog.StatblockJson)
                {
                    entity.Statblock = CmJson.Write(statblock);
                    entity.StatblockVersion = Statblock.CurrentVersion;
                    counts.StatblockOverrides++;
                }

                // Своё описание у твари сценария — столбца нет, а терять текст нельзя: в заметку
                if (creature.Text("Description") is { } description && description != catalog.Creature.Description)
                {
                    entity.Notes = entity.Notes is null ? $"Описание в сценарии:\n\n{description}" : $"{entity.Notes}\n\nОписание в сценарии:\n\n{description}";
                    s.Report.Add(ReportSections.Fixed, $"сценарий «{scenario.Name}», тварь «{name}»: своё описание отличается от бестиария — перенесено в заметку");
                }
            }
            else
            {
                entity.Name = name;
                entity.Statblock = CmJson.Write(statblock);
                entity.StatblockVersion = Statblock.CurrentVersion;
                s.Report.Add(ReportSections.Warnings, $"сценарий «{scenario.Name}»: тварь «{name}» не найдена в бестиарии — своя, со статблоком");
            }

            scenario.Creatures.Add(entity);
            counts.Creatures++;
        }
    }

    private static void Items(MigrationState s, JsonObject row, Scenario scenario, Counts counts)
    {
        var ord = 0;
        foreach (var item in row.Arr("ScenarioItems").OfType<JsonObject>())
        {
            counts.V1Items++;
            var name = item.Text("Name") ?? "";
            var entity = new ScenarioItem
            {
                Id = item.Guid("Id") ?? Guid.CreateVersion7(),
                ScenarioId = scenario.Id,
                Ord = ord++,
                LocationNote = item.Text("Location"),
                Notes = item.Text("Notes"),
            };

            if (s.ScenarioProps.TryGetValue(name, out var prop))
            {
                // Реквизит — предмет сценария, а не справочника (решение владельца 2026-10-02)
                entity.Name = name;
                entity.Description = item.Text("Description") ?? prop.Text("Description");
                s.ScenarioProps.Remove(name);
                s.Report.Add(ReportSections.MovedToScenario, $"«{name}» ({prop.Text("Type")}) → предмет сценария «{scenario.Name}»");
                counts.Props++;
            }
            else if (s.ItemsByV1Name.TryGetValue(name, out var catalog))
            {
                entity.ItemId = catalog.Id;
                if (item.Text("Description") is { } description && description != catalog.Description)
                {
                    entity.Description = description;
                }
            }
            else
            {
                entity.Name = name;
                entity.Description = item.Text("Description");
                s.Report.Add(ReportSections.Warnings, $"сценарий «{scenario.Name}»: предмет «{name}» не найден в справочнике — свой");
            }

            scenario.Items.Add(entity);
            counts.Items++;
        }
    }

    private sealed class Counts
    {
        public int V1Locations;
        public int Locations;
        public int V1Checks;
        public int Checks;
        public int CharacteristicChecks;
        public int LuckChecks;
        public int KeyFacts;
        public int Handouts;
        public int HandoutFiles;
        public int V1Creatures;
        public int Creatures;
        public int StatblockOverrides;
        public int V1Items;
        public int Items;
        public int Props;
    }
}
