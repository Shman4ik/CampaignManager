using CampaignManager.Data.Admin;
using CampaignManager.Data.Campaigns;
using CampaignManager.Data.Catalogs;
using CampaignManager.Data.Characters;
using CampaignManager.Data.Encounters;
using CampaignManager.Data.Files;
using CampaignManager.Data.Identity;
using CampaignManager.Data.Infrastructure;
using CampaignManager.Data.Music;
using CampaignManager.Data.Scenarios;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Data;

/// <summary>
/// Схема <c>cm</c> (docs/v2/SCHEMA.md). Живёт в одной базе со схемами v1 <c>games</c> и
/// <c>identity</c>, но журнал миграций у неё свой — см. <see cref="CmDatabase"/>.
/// Сущности и их конфигурации — по папкам модулей; общие правила (enum → текст + CHECK,
/// <c>created_at</c>/<c>updated_at</c>) — в <see cref="SchemaConventions"/>.
/// </summary>
public sealed class CmDbContext(DbContextOptions<CmDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public const string Schema = "cm";

    public DbSet<User> Users => Set<User>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<KeeperApplication> KeeperApplications => Set<KeeperApplication>();

    public DbSet<StoredFile> Files => Set<StoredFile>();

    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignMember> CampaignMembers => Set<CampaignMember>();
    public DbSet<ScenarioRun> ScenarioRuns => Set<ScenarioRun>();
    public DbSet<RunReservation> RunReservations => Set<RunReservation>();
    public DbSet<CampaignSession> CampaignSessions => Set<CampaignSession>();

    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Occupation> Occupations => Set<Occupation>();
    public DbSet<OccupationSlot> OccupationSlots => Set<OccupationSlot>();
    public DbSet<OccupationSlotOption> OccupationSlotOptions => Set<OccupationSlotOption>();
    public DbSet<Weapon> Weapons => Set<Weapon>();
    public DbSet<Spell> Spells => Set<Spell>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookSpell> BookSpells => Set<BookSpell>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Creature> Creatures => Set<Creature>();
    public DbSet<CreatureImage> CreatureImages => Set<CreatureImage>();
    public DbSet<WeaponImage> WeaponImages => Set<WeaponImage>();
    public DbSet<OccupationImage> OccupationImages => Set<OccupationImage>();

    public DbSet<MusicTrack> MusicTracks => Set<MusicTrack>();

    public DbSet<Scenario> Scenarios => Set<Scenario>();
    public DbSet<ScenarioLocation> ScenarioLocations => Set<ScenarioLocation>();
    public DbSet<ScenarioCheck> ScenarioChecks => Set<ScenarioCheck>();
    public DbSet<ScenarioKeyFact> ScenarioKeyFacts => Set<ScenarioKeyFact>();
    public DbSet<ScenarioHandout> ScenarioHandouts => Set<ScenarioHandout>();
    public DbSet<ScenarioCreature> ScenarioCreatures => Set<ScenarioCreature>();
    public DbSet<ScenarioItem> ScenarioItems => Set<ScenarioItem>();
    public DbSet<ScenarioNpc> ScenarioNpcs => Set<ScenarioNpc>();
    public DbSet<LocationTrack> LocationTracks => Set<LocationTrack>();

    public DbSet<Character> Characters => Set<Character>();

    public DbSet<Encounter> Encounters => Set<Encounter>();

    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    /// <summary>Ключи Data Protection: копируются из v1, чтобы куки входа пережили переключение.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <summary>Время Postgres: им <c>/api/v1/ping</c> проверяет, что база отвечает.</summary>
    public Task<DateTimeOffset> GetDatabaseTimeAsync(CancellationToken cancellationToken) =>
        Database.SqlQuery<DateTimeOffset>($"SELECT now() AS \"Value\"").SingleAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CmDbContext).Assembly);
        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_keys");

        // Последними: обходят уже собранную модель.
        modelBuilder.MapTimestamps();
        modelBuilder.MapEnumsAsCheckedText();
    }
}
