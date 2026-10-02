using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.Core.Identity;
using CampaignManager.Core.Scenarios;
using CampaignManager.Data;
using CampaignManager.Migrate.Files;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CampaignManager.Migrate.Tests;

/// <summary>Хранилище без MinIO: объекта «missing» нет, остальные есть.</summary>
internal sealed class FakeFileStore : IFileStore
{
    public Task<StoredObject?> EnsureAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(key.Contains("missing", StringComparison.Ordinal) ? null : new StoredObject(1234, "application/octet-stream"));
}

/// <summary>
/// Своя база на сервере из <c>CM_TEST_DB</c> (D7): v1 из <see cref="V1Fixture"/>, схема <c>cm</c> — миграциями.
/// Ветку Neon dev сюда не подставлять.
/// </summary>
public sealed class MigrationDatabase : IAsyncLifetime
{
    public const string Variable = "CM_TEST_DB";

    public string? ConnectionString { get; private set; }

    public async ValueTask InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable(Variable) is not { Length: > 0 } server)
        {
            return;
        }

        var builder = new NpgsqlConnectionStringBuilder(server) { Database = $"cm_migrate_{Guid.NewGuid():N}" };
        await using (var admin = new NpgsqlConnection(server))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {builder.Database}", admin);
            await create.ExecuteNonQueryAsync();
        }

        ConnectionString = builder.ConnectionString;
        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        await V1Fixture.CreateAsync(ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (ConnectionString is null || Environment.GetEnvironmentVariable(Variable) is not { Length: > 0 } server)
        {
            return;
        }

        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(server);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS {new NpgsqlConnectionStringBuilder(ConnectionString).Database} WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    public CmDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CmDbContext>();
        CmDatabase.Configure(options, ConnectionString!);
        return new CmDbContext(options.Options);
    }
}

/// <summary>Перенос целиком на маленькой v1: счётчики, решения владельца, перезапуск.</summary>
public sealed class MigratorTests(MigrationDatabase database) : IClassFixture<MigrationDatabase>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private void SkipIfMissing() =>
        Assert.SkipWhen(database.ConnectionString is null, $"Нет {MigrationDatabase.Variable}: тест с базой пропущен.");

    private Task<MigrationReport> MigrateAsync(bool reset = true) =>
        new Migrator(database.ConnectionString!, new FakeFileStore(), new MigrationOptions()).RunAsync(reset, Token);

    [Fact]
    public async Task Migrates_everything_by_schema_rules_and_owner_decisions()
    {
        SkipIfMissing();
        var report = await MigrateAsync();
        await using var db = database.CreateContext();

        Assert.Equal(3, await db.Users.CountAsync(u => u.Email != "new@example.test", Token));
        Assert.Equal(UserRole.Admin, (await db.Users.SingleAsync(u => u.Email == "admin@example.test", Token)).Role);
        Assert.Equal(["ui.lastCharacterId"], await db.UserPreferences.Select(p => p.Key).ToListAsync(Token));

        // Кампания ваншота: Хранитель — участник с ролью, его строка «игрока» выброшена, псевдоним игрока сохранён
        var campaign = await db.Campaigns.Include(c => c.Members).SingleAsync(Token);
        Assert.Equal((CampaignKind.OneShot, CampaignStatus.Completed, Era.Classic), (campaign.Kind, campaign.Status, campaign.Era));
        Assert.Equal([CampaignRole.Keeper, CampaignRole.Player], campaign.Members.OrderBy(m => m.Role).Select(m => m.Role).Reverse());
        Assert.Equal("Псевдоним", campaign.Members.Single(m => m.Role == CampaignRole.Player).DisplayName);

        // Справочники: современное не переехало, коды из таблиц Core, имена исправлены
        Assert.Equal(11, await db.Skills.CountAsync(Token));
        Assert.False(await db.Skills.AnyAsync(s => s.Code == null, Token));
        Assert.Equal("DEX/2", (await db.Skills.SingleAsync(s => s.Code == SkillCodes.Dodge, Token)).BaseFormula);
        Assert.All(await db.Skills.ToListAsync(Token), s => Assert.Equal([Era.Classic], s.Eras));

        var occupation = await db.Occupations.Include(o => o.Slots).ThenInclude(s => s.Options).SingleAsync(Token);
        Assert.Equal("occupation.private-investigator", occupation.Code);
        Assert.Equal(["Academic", "Social"], occupation.Tags);
        Assert.Equal(
            [OccupationSlotKind.AnySpecialization, OccupationSlotKind.Specialization, OccupationSlotKind.Skill,
             OccupationSlotKind.Choice, OccupationSlotKind.Social, OccupationSlotKind.Free],
            occupation.Slots.OrderBy(s => s.Ord).Select(s => s.Kind));
        Assert.Equal(2, occupation.Slots.Single(s => s.Kind == OccupationSlotKind.Choice).Options.Count);

        var weapon = await db.Weapons.SingleAsync(Token);
        Assert.Equal(("weapon.38-or-9mm-revolver", 25m, (decimal?)null, "", 100, 15), (weapon.Code, weapon.CostClassic!.Value, weapon.CostModern, weapon.Notes, weapon.Malfunction!.Value, weapon.BaseRangeM!.Value));
        Assert.Equal(V1Fixture.Handgun, weapon.SkillId);

        var book = await db.Books.Include(b => b.Spells).SingleAsync(Token);
        Assert.Equal(1, book.Spells.Count(s => s.SpellId != null));
        Assert.Equal(2, book.Spells.Count);

        var items = (await db.Items.ToListAsync(Token)).OrderBy(i => i.Name, StringComparer.Ordinal).ToList();
        Assert.Equal(["«Форд» Model T", "Аккордеон", "Фляга"], items.Select(i => i.Name));
        Assert.Equal("Развлечения", items.Single(i => i.Name == "Аккордеон").Type);
        Assert.Equal("item.canteen", items.Single(i => i.Name == "Фляга").Code);

        var creature = await db.Creatures.Include(c => c.Images).SingleAsync(Token);
        Assert.Equal("creature.deep-one", creature.Code);
        Assert.Equal("в воде", Assert.Single(creature.Images).Caption); // второго объекта нет в хранилище
        var statblock = CmJson.ReadStatblock(creature.Statblock, creature.StatblockVersion);
        Assert.Equal("рвёт", Assert.Single(statblock.Attacks).Description);

        Assert.Equal(2, await db.MusicTracks.CountAsync(Token));
        Assert.Equal(["атмосфера"], (await db.MusicTracks.SingleAsync(t => t.FileId != null, Token)).Tags);
        Assert.Equal(2, await db.Files.CountAsync(Token));

        // Сценарии: копия в кампании — форк шаблона и прохождение; дата — из пояса Хранителя
        var copy = await db.Scenarios.SingleAsync(s => s.Id == V1Fixture.Copy, Token);
        Assert.Equal(V1Fixture.Template, copy.SourceScenarioId);
        var run = await db.ScenarioRuns.SingleAsync(Token);
        Assert.Equal((V1Fixture.Copy, ScenarioRunStatus.Finished, true), (run.ScenarioId, run.Status, run.SignupOpen));
        Assert.Equal(new DateTimeOffset(2026, 4, 13, 16, 0, 0, TimeSpan.Zero), run.ScheduledAt);

        var template = await db.Scenarios
            .Include(s => s.Locations).ThenInclude(l => l.Checks)
            .Include(s => s.Handouts).Include(s => s.Creatures).Include(s => s.Items)
            .SingleAsync(s => s.Id == V1Fixture.Template, Token);
        Assert.Equal(Era.Classic, template.Era);
        Assert.Equal("Июнь 1925 года", template.SettingDate);
        var house = template.Locations.Single(l => l.ParentId is null);
        Assert.Equal(house.Id, template.Locations.Single(l => l.ParentId is not null).ParentId);
        Assert.Equal(
            [(CheckTarget.Luck, Difficulty.Regular), (CheckTarget.Characteristic, Difficulty.Hard), (CheckTarget.Skill, Difficulty.Extreme)],
            house.Checks.OrderBy(c => c.Ord).Select(c => (c.TargetKind, c.Difficulty)));
        Assert.Equal(Characteristic.STR, house.Checks.Single(c => c.TargetKind == CheckTarget.Characteristic).Characteristic);
        Assert.Null(Assert.Single(template.Handouts).FileId); // адрес-заглушка example.com не перенесён
        Assert.Equal(1, await db.LocationTracks.CountAsync(Token));

        var deepOne = template.Creatures.Single(c => c.CreatureId is not null);
        Assert.Null(deepOne.Statblock); // совпадает с бестиарием — переопределения нет
        Assert.Equal("в подвале", deepOne.LocationNote);
        Assert.NotNull(template.Creatures.Single(c => c.CreatureId is null).Statblock);

        // Реквизит — предмет сценария без справочника; книжный предмет — ссылкой
        var prop = template.Items.Single(i => i.ItemId is null);
        Assert.Equal(("Шип Гла'аки", "шип из сценария", "в гробу", "главное"), (prop.Name, prop.Description, prop.LocationNote, prop.Notes));
        Assert.Null(template.Items.Single(i => i.ItemId is not null).Description); // как в справочнике

        // Листы: забронированный преген — лист игрока в кампании ваншота; НПС из книги — с overrides
        var characters = await db.Characters.ToDictionaryAsync(c => c.Id, Token);
        Assert.Equal((CharacterKind.Player, CharacterStatus.Retired, V1Fixture.Campaign), (characters[V1Fixture.Investigator].Kind, characters[V1Fixture.Investigator].Status, characters[V1Fixture.Investigator].CampaignId!.Value));
        var reserved = characters[V1Fixture.ReservedPregen];
        Assert.Equal((CharacterKind.Player, (Guid?)null, V1Fixture.Campaign), (reserved.Kind, reserved.ScenarioId, reserved.CampaignId!.Value));
        Assert.Equal(reserved.OwnerId, characters[V1Fixture.Investigator].OwnerId);
        Assert.Equal("Хелен Райт", reserved.Name);
        var npc = CmJson.ReadSheet(characters[V1Fixture.Npc].Sheet, characters[V1Fixture.Npc].SheetVersion);
        Assert.Equal(7, npc.Overrides.MaxHitPoints);
        Assert.Equal(2, (await db.ScenarioNpcs.SingleAsync(Token)).Count);

        Assert.Equal(1, await db.AuditLog.CountAsync(e => e.ActorId == null, Token));
        Assert.Equal(1, await db.DataProtectionKeys.CountAsync(Token));

        // Отчёт перечисляет отброшенное и исправленное
        Assert.Contains(report.Sections[ReportSections.DroppedModern], l => l.Contains("АК-74", StringComparison.Ordinal));
        Assert.Contains(report.Sections[ReportSections.DroppedModern], l => l.Contains("Хакер", StringComparison.Ordinal));
        Assert.Contains(report.Sections[ReportSections.DroppedModern], l => l.Contains("Смартфон", StringComparison.Ordinal));
        Assert.Single(report.Sections[ReportSections.DroppedDuplicates]);
        Assert.Contains(report.Sections[ReportSections.Renamed], l => l.Contains("«Флаг (1 метр)» → «Фляга»", StringComparison.Ordinal));
        Assert.Single(report.Sections[ReportSections.MovedToScenario]);
        Assert.Contains(report.Sections[ReportSections.Files], l => l.Contains("missing.jpg", StringComparison.Ordinal));
        Assert.Contains(report.Sections[ReportSections.Warnings], l => l.Contains("Хиромантия", StringComparison.Ordinal));
        Assert.DoesNotContain("@example.test", report.ToMarkdown(DateTimeOffset.UnixEpoch), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rerun_with_reset_gives_same_result_and_keeps_sign_in()
    {
        SkipIfMissing();
        await MigrateAsync();
        await using (var db = database.CreateContext())
        {
            // Пользователь вошёл в среду после переноса, и ещё один, которого в v1 не было
            var player = await db.Users.SingleAsync(u => u.Email == "player@example.test", Token);
            player.Auth0Sub = "google-oauth2|player";
            db.Users.Add(new Data.Identity.User { Email = "new@example.test", DisplayName = "Новичок", Auth0Sub = "auth0|new" });
            await db.SaveChangesAsync(Token);
        }

        await MigrateAsync();

        await using var after = database.CreateContext();
        Assert.Equal(4, await after.Users.CountAsync(Token));
        Assert.Equal("google-oauth2|player", (await after.Users.SingleAsync(u => u.Email == "player@example.test", Token)).Auth0Sub);
        Assert.Equal(3, await after.Characters.CountAsync(Token));
        Assert.Equal(11, await after.Skills.CountAsync(Token));
    }

    [Fact]
    public async Task Without_reset_refuses_non_empty_cm()
    {
        SkipIfMissing();
        await MigrateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => MigrateAsync(reset: false));
        Assert.Contains("--reset", error.Message, StringComparison.Ordinal);
    }
}
