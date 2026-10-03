using System.Text.Json;
using CampaignManager.Core.Identity;
using CampaignManager.Data.Identity;
using CampaignManager.Migrate.Catalogs;
using CampaignManager.Migrate.V1;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace CampaignManager.Migrate.Steps;

/// <summary>Люди: пользователи, их настройки, заявки на Хранителя; ключи Data Protection.</summary>
public static class PeopleStep
{
    /// <summary>
    /// «Регистрация» перенесённого пользователя (решение владельца 2026-10-03): v1 даты регистрации не хранил, поэтому
    /// берётся его первая запись там — кампания, которую он ведёт, место игрока в кампании, заявка на Хранителя или
    /// сценарий, который он создал. Записей нет — <c>null</c> (в админке пусто), а не день переноса.
    /// </summary>
    public static Dictionary<string, DateTimeOffset> FirstRecords(V1Database v1)
    {
        var first = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

        void Note(IEnumerable<System.Text.Json.Nodes.JsonObject> rows, string emailColumn)
        {
            foreach (var row in rows)
            {
                if (row.Text(emailColumn) is { } email && row.Time("CreatedAt") is { } at
                    && (!first.TryGetValue(email, out var known) || at < known))
                {
                    first[email] = at;
                }
            }
        }

        Note(v1.Campaigns, "KeeperEmail");
        Note(v1.CampaignPlayers, "PlayerEmail");
        Note(v1.KeeperApplications, "UserEmail");
        Note(v1.Scenarios, "CreatorEmail");
        return first;
    }

    public static void Run(MigrationState s)
    {
        var firstRecords = FirstRecords(s.V1);
        foreach (var row in s.V1.Users)
        {
            var email = row.Text("Email") ?? throw new InvalidOperationException($"У пользователя v1 {row.Str("Id")} нет почты.");
            var user = new User
            {
                Id = row.Guid("Id") ?? Guid.CreateVersion7(),
                Email = email,
                DisplayName = row.Text("UserName") ?? email,
                RegisteredAt = firstRecords.TryGetValue(email, out var firstAt) ? firstAt : null,
                // PlayerRole v1 лежит числом: 0 — игрок, 1 — Хранитель, 2 — администратор
                Role = row.Int("Role") switch
                {
                    1 => UserRole.Keeper,
                    2 => UserRole.Admin,
                    _ => UserRole.Player,
                },
            };
            if (!s.UserByEmail.TryAdd(email, user.Id))
            {
                throw new InvalidOperationException($"Почта пользователя v1 {row.Str("Id")} повторяется.");
            }

            s.Db.Users.Add(user);
        }

        s.Report.Count("identity.AspNetUsers", s.V1.Users.Count, "users", s.UserByEmail.Count, "auth0_sub пуст до первого входа");

        var preferences = 0;
        foreach (var row in s.V1.UserPreferences)
        {
            if (s.UserId(row.Str("UserEmail")) is not { } userId)
            {
                s.Report.Add(ReportSections.Warnings, "настройки пользователя, которого нет в identity, — не перенесены");
                continue;
            }

            foreach (var (key, value) in row.Obj("Preferences") ?? [])
            {
                if (OwnerDecisions.DeadPreferenceKeys.Contains(key))
                {
                    s.Report.Add(ReportSections.DroppedJunk, $"мёртвый ключ настроек `{key}`");
                    continue;
                }

                s.Db.UserPreferences.Add(new UserPreference
                {
                    UserId = userId,
                    Key = key,
                    Value = JsonDocument.Parse(value?.ToJsonString() ?? "null"),
                    UpdatedAt = row.Time("LastUpdated") ?? default,
                });
                preferences++;
            }
        }

        s.Report.Count("games.UserPreferences", s.V1.UserPreferences.Count, "user_preferences", preferences, "строка на ключ; мёртвые ключи выброшены");

        foreach (var row in s.V1.KeeperApplications)
        {
            var userId = s.UserId(row.Str("UserEmail"))
                ?? throw new InvalidOperationException($"Заявка {row.Str("Id")}: автора нет в identity.");
            s.Db.KeeperApplications.Add(new KeeperApplication
            {
                Id = row.Guid("Id") ?? Guid.CreateVersion7(),
                UserId = userId,
                Message = row.Str("Message") ?? "",
                Status = row.Enum<KeeperApplicationStatus>("Status") ?? KeeperApplicationStatus.Pending,
                ReviewedById = s.UserId(row.Str("ReviewedByEmail")),
                ReviewedAt = row.Time("ReviewedAt"),
                ReviewComment = row.Text("ReviewComment"),
                CreatedAt = row.Time("CreatedAt") ?? default,
                UpdatedAt = row.Time("LastUpdated") ?? default,
            });
        }

        s.Report.Count("games.KeeperApplications", s.V1.KeeperApplications.Count, "keeper_applications", s.V1.KeeperApplications.Count);

        foreach (var row in s.V1.DataProtectionKeys)
        {
            s.Db.DataProtectionKeys.Add(new DataProtectionKey { FriendlyName = row.Str("FriendlyName"), Xml = row.Str("Xml") });
        }

        s.Report.Count("games.DataProtectionKeys", s.V1.DataProtectionKeys.Count, "data_protection_keys", s.V1.DataProtectionKeys.Count, "куки входа переживут переключение");
    }
}
