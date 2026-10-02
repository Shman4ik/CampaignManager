using System.Text.Json;
using CampaignManager.Core.Admin;
using CampaignManager.Data.Admin;
using CampaignManager.Migrate.V1;

namespace CampaignManager.Migrate.Steps;

/// <summary>
/// История правок → <c>audit_log</c>: только снимок «после» (SCHEMA), почта → <c>actor_id</c>. Записи о
/// выброшенных сущностях остаются — это история; <c>entity_id</c> без FK.
/// </summary>
public static class AuditStep
{
    private static readonly Dictionary<string, string> Tables = new(StringComparer.Ordinal)
    {
        ["SkillModel"] = "skills",
        ["Skill"] = "skills",
        ["Occupation"] = "occupations",
        ["Weapon"] = "weapons",
        ["Spell"] = "spells",
        ["Book"] = "books",
        ["Item"] = "items",
        ["Creature"] = "creatures",
        ["MusicTrack"] = "music_tracks",
    };

    public static void Run(MigrationState s)
    {
        var withoutActor = 0;
        foreach (var row in s.V1.EditHistory)
        {
            var type = row.Text("EntityType") ?? "";
            var actor = s.UserId(row.Str("EditorEmail"));
            withoutActor += actor is null ? 1 : 0;
            s.Db.AuditLog.Add(new AuditLogEntry
            {
                EntityType = Tables.GetValueOrDefault(type, type.ToLowerInvariant()),
                EntityId = row.Guid("EntityId")!.Value,
                Action = row.Enum<AuditAction>("Action") ?? AuditAction.Updated,
                ActorId = actor,
                Snapshot = row["SnapshotJson"] is { } snapshot ? JsonDocument.Parse(snapshot.ToJsonString()) : null,
                CreatedAt = row.Time("CreatedAt") ?? default,
            });
        }

        s.Report.Count("games.EditHistoryEntries", s.V1.EditHistory.Count, "audit_log", s.V1.EditHistory.Count,
            $"без автора (system) — {withoutActor}; снимок «до» не переносится");
        s.Report.Count("games.ChaseSessions", s.V1.ChaseSessions.Count, "encounters", 0, "погони v1 не переносятся (в v1 их нет)");
        if (s.V1.ChaseSessions.Count > 0)
        {
            s.Report.Add(ReportSections.Warnings, $"{s.V1.ChaseSessions.Count} погонь v1 не перенесены: состояние сцены 2.0 делает T2.6");
        }
    }
}
