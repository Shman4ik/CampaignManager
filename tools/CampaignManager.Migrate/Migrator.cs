using CampaignManager.Data;
using CampaignManager.Data.Identity;
using CampaignManager.Migrate.Files;
using CampaignManager.Migrate.Steps;
using CampaignManager.Migrate.V1;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CampaignManager.Migrate;

/// <summary>
/// Перенос целиком, одной транзакцией: чтение v1 → (очистка <c>cm</c>) → шаги → проверки → коммит. Упал любой
/// шаг или проверка — <c>cm</c> остаётся, какой был. Схемы v1 только читаются.
/// </summary>
public sealed class Migrator(string connectionString, IFileStore files, MigrationOptions options)
{
    public async Task<MigrationReport> RunAsync(bool reset, CancellationToken cancellationToken)
    {
        var report = new MigrationReport();

        await using var source = NpgsqlDataSource.Create(connectionString);
        var v1 = await V1Database.ReadAsync(source, cancellationToken);

        var builder = new DbContextOptionsBuilder<CmDbContext>();
        CmDatabase.Configure(builder, connectionString);
        await using var db = new CmDbContext(builder.Options);

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            throw new InvalidOperationException(
                $"Схема cm не догнала миграции ({string.Join(", ", pending)}): сначала dotnet ef database update --project src/CampaignManager.Data с CM_DB.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Кто уже входил в эту среду (beta на ветке dev): после очистки вход сохраняется — sub и время входа
        // возвращаются перенесённому пользователю с той же почтой, а не найденный в v1 остаётся как был
        var signedIn = await db.Users.AsNoTracking().Where(u => u.Auth0Sub != null).ToListAsync(cancellationToken);

        if (reset)
        {
            await TruncateAsync(db, cancellationToken);
        }
        else if (await db.Users.AnyAsync(cancellationToken) || await db.Skills.AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException("Схема cm не пуста: перенос заново — с --reset.");
        }

        var state = new MigrationState(v1, db, files, report, options);
        PeopleStep.Run(state);
        await CatalogStep.RunAsync(state, cancellationToken);
        await MusicStep.RunAsync(state, cancellationToken);
        CampaignStep.Run(state);
        await ScenarioStep.RunAsync(state, cancellationToken);
        CharacterStep.Run(state);
        ScenarioStep.Npcs(state);
        AuditStep.Run(state);
        KeepSignedIn(state, signedIn);

        report.Count("объекты MinIO", 0, "files", db.Files.Local.Count,
            $"картинок тварей и треков; не нашлось в хранилище — {state.MissingFiles}");

        await db.SaveChangesAsync(cancellationToken);
        await VerifyAsync(db, report, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return report;
    }

    private static void KeepSignedIn(MigrationState s, List<User> signedIn)
    {
        foreach (var previous in signedIn)
        {
            if (s.Db.Users.Local.FirstOrDefault(u => string.Equals(u.Email, previous.Email, StringComparison.OrdinalIgnoreCase)) is { } user)
            {
                user.Auth0Sub = previous.Auth0Sub;
                user.LastLoginAt = previous.LastLoginAt;
                s.Report.Add(ReportSections.Fixed, $"пользователь «{user.DisplayName}» уже входил в эту среду — вход (auth0_sub) сохранён");
            }
            else
            {
                s.Db.Users.Add(new User
                {
                    Id = previous.Id,
                    Auth0Sub = previous.Auth0Sub,
                    Email = previous.Email,
                    DisplayName = previous.DisplayName,
                    Role = previous.Role,
                    LastLoginAt = previous.LastLoginAt,
                    RegisteredAt = previous.RegisteredAt,
                    CreatedAt = previous.CreatedAt,
                    UpdatedAt = previous.UpdatedAt,
                });
                s.Report.Add(ReportSections.Warnings, $"пользователь «{previous.DisplayName}» есть только в cm этой среды (вошёл после v1) — сохранён");
            }
        }
    }

    /// <summary>Всё в <c>cm</c>, кроме журнала миграций.</summary>
    private static async Task TruncateAsync(CmDbContext db, CancellationToken cancellationToken)
    {
        var tables = await db.Database.SqlQuery<string>($"""
            SELECT table_name AS "Value" FROM information_schema.tables
            WHERE table_schema = 'cm' AND table_type = 'BASE TABLE' AND table_name <> '__ef_migrations_history'
            """).ToListAsync(cancellationToken);
        if (tables.Count > 0)
        {
            var list = string.Join(", ", tables.Select(t => $"cm.\"{t}\""));
#pragma warning disable EF1002 // имена таблиц — из information_schema, не из ввода
            await db.Database.ExecuteSqlRawAsync($"TRUNCATE {list} RESTART IDENTITY CASCADE", cancellationToken);
#pragma warning restore EF1002
        }
    }

    /// <summary>Проверки SCHEMA, «Проверки после переноса»: упавшая проверка откатывает перенос.</summary>
    private static async Task VerifyAsync(CmDbContext db, MigrationReport report, CancellationToken cancellationToken)
    {
        var keeperless = await db.Database.SqlQuery<Guid>($"""
            SELECT c.id AS "Value" FROM cm.campaigns c
            WHERE (SELECT count(*) FROM cm.campaign_members m WHERE m.campaign_id = c.id AND m.role = 'Keeper') <> 1
            """).ToListAsync(cancellationToken);
        if (keeperless.Count > 0)
        {
            throw new InvalidOperationException($"У кампаний {string.Join(", ", keeperless)} не ровно один Хранитель.");
        }

        var orphans = await db.Database.SqlQuery<Guid>($"""
            SELECT f.id AS "Value" FROM cm.files f
            WHERE NOT EXISTS (SELECT 1 FROM cm.characters        WHERE portrait_file_id = f.id)
              AND NOT EXISTS (SELECT 1 FROM cm.books             WHERE image_file_id = f.id)
              AND NOT EXISTS (SELECT 1 FROM cm.items             WHERE image_file_id = f.id)
              AND NOT EXISTS (SELECT 1 FROM cm.creature_images   WHERE file_id = f.id)
              AND NOT EXISTS (SELECT 1 FROM cm.weapon_images     WHERE file_id = f.id)
              AND NOT EXISTS (SELECT 1 FROM cm.occupation_images WHERE file_id = f.id)
              AND NOT EXISTS (SELECT 1 FROM cm.scenario_handouts WHERE file_id = f.id)
              AND NOT EXISTS (SELECT 1 FROM cm.music_tracks      WHERE file_id = f.id)
            """).ToListAsync(cancellationToken);
        if (orphans.Count > 0)
        {
            throw new InvalidOperationException($"После переноса {orphans.Count} строк files ни на что не ссылаются.");
        }

        report.Add(ReportSections.Checks, "у каждой кампании ровно один Хранитель");
        report.Add(ReportSections.Checks, "строк files без ссылок нет");
        report.Add(ReportSections.Checks, "каждый лист прочитан типами Core (CmJson.ReadSheet), статблоки — тоже при сравнении");
        report.Add(ReportSections.Checks, "владелец листа, членство игрока, один активный лист, CHECK enum'ов — ограничения схемы приняли вставку");
    }
}
