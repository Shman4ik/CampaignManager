using CampaignManager.Web.Components.Features.Admin.Model;
using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Components.Features.Campaigns.Models;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Items.Model;
using CampaignManager.Web.Components.Features.Scenarios.Model;
using CampaignManager.Web.Components.Features.Skills.Model;
using CampaignManager.Web.Components.Features.Spells.Model;
using CampaignManager.Web.Components.Features.Weapons.Model;
using CampaignManager.Web.Components.Features.Wiki.Model;
using CampaignManager.Web.Model;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Utilities.DataBase;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    // Required by IDataProtectionKeyContext — keys stored in DB survive container restarts
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    public DbSet<Campaign> Campaigns { get; set; } = null!;
    public DbSet<CharacterStorageDto> CharacterStorage { get; set; } = null!;
    public DbSet<CampaignPlayer> CampaignPlayers { get; set; } = null!;
    public DbSet<Weapon> Weapons { get; set; } = null!;
    public DbSet<Spell> Spells { get; set; } = null!;
    public DbSet<SkillModel> Skills { get; set; } = null!;

    // Scenario Management DbSets
    public DbSet<Scenario> Scenarios { get; set; } = null!;
    public DbSet<Creature> Creatures { get; set; } = null!;
    public DbSet<Item> Items { get; set; } = null!;
    public DbSet<Occupation> Occupations { get; set; } = null!;

    // Admin DbSets
    public DbSet<KeeperApplication> KeeperApplications { get; set; } = null!;

    // Wiki Audit DbSets
    public DbSet<EditHistoryEntry> EditHistoryEntries { get; set; } = null!;

    // User Preferences
    public DbSet<UserPreferences> UserPreferences { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("games");

        modelBuilder.Entity<Campaign>(entity =>
        {
            // Обязательные поля
            entity.Property(c => c.Name).IsRequired();
            entity.Property(c => c.CreatedAt).IsRequired();
            entity.Property(c => c.LastUpdated).IsRequired();
            entity.Property(c => c.Status)
                .HasDefaultValue(CampaignStatus.Planning)
                .HasConversion<string>();

            // Индекс для быстрого поиска кампаний по ведущему
            entity.HasIndex(c => c.KeeperEmail);
        });

        modelBuilder.Entity<CampaignPlayer>(entity =>
        {
            entity.ToTable("CampaignPlayers");

            // Связь с кампанией
            entity.HasOne(cp => cp.Campaign)
                .WithMany(c => c.Players)
                .HasForeignKey(cp => cp.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            // Составной уникальный индекс - игрок может быть в кампании только один раз
            entity
                .HasIndex(cp => new { cp.PlayerEmail, cp.CampaignId })
                .IsUnique();
        });

        // Настройка Character
        modelBuilder.Entity<CharacterStorageDto>(entity =>
        {
            entity.ToTable("Characters");

            // Связь с CampaignPlayer (игрок в контексте кампании)
            entity.HasOne(c => c.CampaignPlayer)
                .WithMany(cp => cp.Characters)
                .HasForeignKey(c => c.CampaignPlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Связь со сценарием (шаблоны персонажей, привязанные к сценарию)
            entity.HasOne(c => c.Scenario)
                .WithMany(s => s.Npcs)
                .HasForeignKey(c => c.ScenarioId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // Индекс для быстрого поиска персонажей по игроку в кампании
            entity.HasIndex(c => c.CampaignPlayerId);

            // Сохранение статуса в виде строки в базе данных
            entity.Property(c => c.Status)
                .HasConversion<string>();

            // Используем JSONB для хранения данных персонажа
            entity.Property(c => c.Character)
                .HasColumnType("jsonb")
                .HasComment("JSON-представление персонажа со всеми характеристиками");
        });

        // Настройка оружия
        modelBuilder.Entity<Weapon>(entity =>
        {
            entity.ToTable("Weapons");
            entity.Property(w => w.Type)
                .HasDefaultValue(WeaponType.Melee)
                .HasConversion<string>();
            entity.HasIndex(w => w.Name).IsUnique();
            entity.Property(w => w.IsImpaling)
                .HasDefaultValue(false);
            entity.Property(w => w.DamageInfo)
                .HasColumnType("jsonb")
                .HasComment("Структурированная информация об уроне (авто-парсинг поля Damage)");
        });

        // Настройка заклинаний
        modelBuilder.Entity<Spell>(entity =>
        {
            entity.ToTable("Spells");
            entity.Property(s => s.Name).IsRequired();
            entity.HasIndex(s => s.Name).IsUnique();

            // Настройка для хранения списка альтернативных имен
            entity.Property(s => s.AlternativeNames)
                .HasColumnType("jsonb");
        });

        // Настройка навыков
        modelBuilder.Entity<SkillModel>(entity =>
        {
            entity.ToTable("Skills");
            entity.Property(s => s.Name).IsRequired();
            entity.HasIndex(s => s.Name).IsUnique();

            // Store Category as string
            entity.Property(s => s.Category)
                .HasConversion<string>();

            // Store lists as JSONB
            entity.Property(s => s.UsageExamples)
                .HasColumnType("jsonb");

            entity.Property(s => s.FailureConsequences)
                .HasColumnType("jsonb");

            entity.Property(s => s.OpposingSkills)
                .HasColumnType("jsonb");

            // Self-referencing FK for specializations
            entity.HasOne<SkillModel>()
                .WithMany()
                .HasForeignKey(s => s.ParentSkillId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });

        // Scenario Management Configuration

        // Scenario Configuration
        modelBuilder.Entity<Scenario>(entity =>
        {
            entity.ToTable("Scenarios");
            entity.Property(s => s.Name).IsRequired();
            entity.Property(s => s.IsTemplate).HasDefaultValue(false);

            // Index for quick lookup by creator
            entity.HasIndex(s => s.CreatorEmail);

            entity.Property(c => c.ScenarioCreatures).HasColumnType("jsonb");
            entity.Property(c => c.ScenarioItems).HasColumnType("jsonb");
            entity.Property(s => s.Locations).HasColumnType("jsonb");
            entity.Property(s => s.KeyFacts).HasColumnType("jsonb");
            entity.Property(s => s.Handouts).HasColumnType("jsonb");

            // Relationship with Campaign (optional)
            entity.HasOne(s => s.Campaign)
                .WithMany()
                .HasForeignKey(s => s.CampaignId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });
        // Creature Configuration
        modelBuilder.Entity<Creature>(entity =>
        {
            entity.ToTable("Creatures");
            entity.Property(c => c.Name).IsRequired();
            entity.HasIndex(c => c.Name).IsUnique();

            // Store CreatureCharacteristics as JSONB
            entity.Property(c => c.CreatureCharacteristics)
                .HasColumnType("jsonb");

            // Store CombatDescriptions as JSONB
            entity.Property(c => c.CombatDescriptions)
                .HasColumnType("jsonb");

            // Store SpecialAbilities as JSONB
            entity.Property(c => c.SpecialAbilities)
                .HasColumnType("jsonb");

            // Store Type as string
            entity.Property(c => c.Type)
                .HasDefaultValue(CreatureType.Other)
                .HasConversion<string>();
        });

        // Item Configuration
        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("Items");
            entity.Property(i => i.Name).IsRequired();
            entity.HasIndex(i => i.Name).IsUnique();
            entity.Property(i => i.Era).HasConversion<string>();
        });

        // Occupation Configuration
        modelBuilder.Entity<Occupation>(entity =>
        {
            entity.ToTable("Occupations");
            entity.Property(o => o.Name).IsRequired();
            entity.HasIndex(o => o.Name).IsUnique();
            entity.Property(o => o.SkillPointFormula).HasConversion<string>();
            entity.Property(o => o.OccupationSkills).HasColumnType("jsonb");
        });

        // KeeperApplication Configuration
        modelBuilder.Entity<KeeperApplication>(entity =>
        {
            entity.ToTable("KeeperApplications");
            entity.Property(a => a.UserEmail).IsRequired();
            entity.Property(a => a.Status).HasConversion<string>();

            // Only one pending application per user
            entity.HasIndex(a => new { a.UserEmail, a.Status })
                .HasFilter("\"Status\" = 'Pending'")
                .IsUnique();
        });

        // UserPreferences Configuration
        modelBuilder.Entity<UserPreferences>(entity =>
        {
            entity.ToTable("UserPreferences");
            entity.Property(p => p.UserEmail).IsRequired();
            entity.HasIndex(p => p.UserEmail).IsUnique();
            entity.Property(p => p.Preferences).HasColumnType("jsonb");
        });

        // EditHistoryEntry Configuration
        modelBuilder.Entity<EditHistoryEntry>(entity =>
        {
            entity.ToTable("EditHistoryEntries");
            entity.Property(e => e.EntityType).IsRequired();
            entity.Property(e => e.Action).HasConversion<string>();
            entity.Property(e => e.SnapshotJson).HasColumnType("jsonb");
            entity.Property(e => e.PreviousSnapshotJson).HasColumnType("jsonb");

            entity.HasIndex(e => new { e.EntityType, e.EntityId, e.CreatedAt })
                .IsDescending(false, false, true);
        });
    }
}