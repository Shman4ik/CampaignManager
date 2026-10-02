using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.Data.Characters;
using CampaignManager.Migrate.Sheets;
using CampaignManager.Migrate.V1;

namespace CampaignManager.Migrate.Steps;

/// <summary>
/// Листы (SCHEMA, «Персонажи»): вид и владелец — колонки, документ переводит <see cref="SheetConverter"/>.
/// Забронированный преген v1 (место игрока + сценарий) становится листом игрока в кампании ваншота; брони
/// в <c>run_reservations</c> не восстанавливаются — нетронутого прегена в v1 не осталось.
/// </summary>
public static class CharacterStep
{
    public static void Run(MigrationState s)
    {
        var converter = new SheetConverter(
            s.SkillCatalog,
            s.Resolver,
            s.Weapons.ToDictionary(pair => pair.Key, pair => (pair.Value.Name, pair.Value.SkillId)),
            s.Spells.ToDictionary(pair => pair.Key, pair => pair.Value.Name),
            s.Occupations.Values.ToDictionary(o => CatalogCodeTable.NormalizeName(o.Name), o => o.Id));

        var byKind = new Dictionary<CharacterKind, int>();
        var mappedLines = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        var droppedLines = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        var (skills, mapped, unmatched, overrides, checkedDropped, weapons, linked) = (0, 0, 0, 0, 0, 0, 0);
        foreach (var row in s.V1.Characters)
        {
            var id = row.Guid("Id")!.Value;
            var v1Kind = row.Str("Kind");
            var cpId = row.Guid("CampaignPlayerId");
            var scenarioId = row.Guid("ScenarioId");
            var character = new Character
            {
                Id = id,
                Status = row.Enum<CharacterStatus>("Status") ?? CharacterStatus.Active,
                CreatedAt = row.Time("CreatedAt") ?? default,
                UpdatedAt = row.Time("LastUpdated") ?? default,
                Sheet = null!,
            };

            if (cpId is { } placeId)
            {
                // Сыщик игрока или забронированный преген: владелец — игрок места, кампания — его
                var (campaignId, userId) = s.CampaignPlayers.TryGetValue(placeId, out var place)
                    ? place
                    : throw new InvalidOperationException($"Лист {id}: места игрока {placeId} нет.");
                character.Kind = CharacterKind.Player;
                character.OwnerId = userId;
                character.CampaignId = campaignId;
                character.CreatedById = userId;
                if (v1Kind == "Pregen")
                {
                    s.Report.Add(ReportSections.Fixed, $"преген «{row.Text("CharacterName")}», забронированный в v1, → лист игрока в кампании ваншота");
                }
            }
            else if (v1Kind == "Pregen")
            {
                character.Kind = CharacterKind.Pregen;
                character.ScenarioId = scenarioId is { } sid && s.Scenarios.Contains(sid) ? sid : null;
            }
            else if (v1Kind == "Npc")
            {
                character.Kind = CharacterKind.Npc;
                character.CampaignId = row.Guid("CampaignId") is { } campaign && s.Campaigns.ContainsKey(campaign) ? campaign : null;
            }
            else
            {
                throw new InvalidOperationException($"Лист {id}: вид {v1Kind} без места игрока — CK_Characters_Owner v1 такого не пропускает.");
            }

            var document = row.Obj("Character") ?? throw new InvalidOperationException($"Лист {id} пуст.");
            var (sheet, notes) = converter.Convert(document, character.Kind == CharacterKind.Npc);
            character.Sheet = CmJson.Write(sheet);
            character.SheetVersion = CharacterSheet.CurrentVersion;

            // Документ обязан читаться типами Core — иначе перенос упадёт здесь, а не у пользователя
            _ = CmJson.ReadSheet(character.Sheet, character.SheetVersion);

            var label = $"«{sheet.Personal.Name}» ({character.Kind})";
            if (document.Guid("Id") is { } innerId && innerId != id)
            {
                s.Report.Add(ReportSections.DroppedJunk, $"лист {label}: Id внутри JSON не совпадал со строкой — взят id строки");
            }

            foreach (var line in notes.UnmatchedSkills)
            {
                s.Report.Add(ReportSections.SheetSkills, $"{label}: {line}");
            }

            foreach (var line in notes.MappedSkills)
            {
                Collect(mappedLines, line, sheet.Personal.Name);
            }

            foreach (var line in notes.DroppedSkills)
            {
                Collect(droppedLines, $"навык «{line}» на базовом значении", sheet.Personal.Name);
            }

            if (notes.Overrides.Count > 0)
            {
                s.Report.Add(ReportSections.Overrides, $"{label}: {string.Join("; ", notes.Overrides)}");
            }

            if (character.Kind != CharacterKind.Npc && character.Status == CharacterStatus.Active
                && sheet.Current is { HitPoints: 0, MagicPoints: 0, Sanity: 0, Luck: 0 })
            {
                s.Report.Add(ReportSections.Warnings, $"лист {label}: текущие ПЗ, ПМ, Рассудок и Удача — нули (в v1 так и было; перенесено как есть)");
            }

            if (notes.Discrepancies.Count > 0)
            {
                s.Report.Add(ReportSections.Warnings, $"лист {label}: вычисленное расходится с v1 (побеждает формула): {string.Join("; ", notes.Discrepancies)}");
            }

            byKind[character.Kind] = byKind.GetValueOrDefault(character.Kind) + 1;
            skills += sheet.Skills.Count;
            mapped += notes.MappedSkills.Count;
            unmatched += notes.UnmatchedSkills.Count;
            overrides += notes.Overrides.Count > 0 ? 1 : 0;
            checkedDropped += notes.CheckedDropped;
            weapons += sheet.Weapons.Count;
            linked += notes.WeaponsLinked;

            s.Characters.Add(id);
            s.Db.Characters.Add(character);
        }

        foreach (var (line, names) in mappedLines)
        {
            s.Report.Add(ReportSections.SheetSkillsMapped, $"{line} — {Sheets(names)}");
        }

        foreach (var (line, names) in droppedLines)
        {
            s.Report.Add(ReportSections.DroppedModern, $"листы: {line} — {Sheets(names)}");
        }

        if (checkedDropped > 0)
        {
            s.Report.Add(ReportSections.DroppedJunk, $"листы НПС: {checkedDropped} отметок развития (ставил импорт сценария, у НПС смысла не имеют)");
        }

        foreach (var group in s.V1.Characters
                     .GroupBy(row => row.Text("CharacterName") ?? "", StringComparer.Ordinal)
                     .Where(g => g.Count() > 1))
        {
            s.Report.Add(ReportSections.SameNames, $"«{group.Key}» ×{group.Count()} ({string.Join(", ", group.Select(r => $"{r.Str("Kind")}/{r.Str("Status")}"))})");
        }

        var v1Skills = s.V1.Characters.Sum(row => row.Obj("Character").Obj("Skills").Arr("SkillGroups").OfType<System.Text.Json.Nodes.JsonObject>().Sum(g => g.Arr("Skills").Count));
        s.Report.Count("games.Characters", s.V1.Characters.Count, "characters", s.Characters.Count,
            string.Join(", ", byKind.OrderBy(p => p.Key).Select(p => $"{p.Key} — {p.Value}")) + $"; с overrides — {overrides}");
        s.Report.Count("листы: навыки", v1Skills, "sheet.skills", skills,
            $"по старому написанию — {mapped}, без справочника — {unmatched}");
        static void Collect(SortedDictionary<string, List<string>> lines, string line, string sheetName)
        {
            if (!lines.TryGetValue(line, out var names))
            {
                lines[line] = names = [];
            }

            names.Add(sheetName);
        }

        static string Sheets(List<string> names) => names.Count > 6
            ? $"{names.Count} листов"
            : $"{names.Count} ({string.Join(", ", names.Order(StringComparer.Ordinal))})";

        s.Report.Count("листы: оружие", s.V1.Characters.Sum(row => row.Obj("Character").Arr("Weapons").Count), "sheet.weapons", weapons,
            $"со ссылкой на каталог — {linked}");
    }
}
