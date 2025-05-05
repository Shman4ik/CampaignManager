using CampaignManager.Web.Companies.Models;
using CampaignManager.Web.Model;
using CampaignManager.Web.Scenarios.Models;
using CampaignManager.Web.SpellComponents;
using CampaignManager.Web.Weapons;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Services;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Campaign> Campaigns { get; set; }
    public DbSet<CharacterStorageDto> CharacterStorage { get; set; }
    public DbSet<CampaignPlayer> CampaignPlayers { get; set; }
    public DbSet<Weapon> Weapons { get; set; }
    public DbSet<Spell> Spells { get; set; }
    
    // Scenario Management DbSets
    public DbSet<Scenario> Scenarios { get; set; }
    public DbSet<ScenarioNpc> ScenarioNpcs { get; set; }
    public DbSet<Creature> Creatures { get; set; }
    public DbSet<Item> Items { get; set; }
    public DbSet<ScenarioCreature> ScenarioCreatures { get; set; }
    public DbSet<ScenarioItem> ScenarioItems { get; set; }

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

            // Индекс для быстрого поиска персонажей по игроку в кампании
            entity.HasIndex(c => c.CampaignPlayerId);

            // Сохранение статуса в виде строки в базе данных
            entity.Property(c => c.Status)
                .HasDefaultValue(CharacterStatus.Active)
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
        
        // Scenario Management Configuration
        
        // Scenario Configuration
        modelBuilder.Entity<Scenario>(entity =>
        {
            entity.ToTable("Scenarios");
            entity.Property(s => s.Name).IsRequired();
            entity.Property(s => s.IsTemplate).HasDefaultValue(false);
            
            // Index for quick lookup by creator
            entity.HasIndex(s => s.CreatorEmail);
            
            // Relationship with Campaign (optional)
            entity.HasOne(s => s.Campaign)
                .WithMany()
                .HasForeignKey(s => s.CampaignId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });
        
        // ScenarioNpc Configuration
        modelBuilder.Entity<ScenarioNpc>(entity =>
        {
            entity.ToTable("ScenarioNpcs");
            entity.Property(n => n.Name).IsRequired();
            
            // Relationship with Scenario
            entity.HasOne(n => n.Scenario)
                .WithMany(s => s.Npcs)
                .HasForeignKey(n => n.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Optional relationship with Character
            entity.HasOne(n => n.Character)
                .WithOne()
                .HasForeignKey<ScenarioNpc>(n => n.CharacterId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });
        
        // Creature Configuration
        modelBuilder.Entity<Creature>(entity =>
        {
            entity.ToTable("Creatures");
            entity.Property(c => c.Name).IsRequired();
            entity.HasIndex(c => c.Name).IsUnique();
            
            // Store stats as JSON
            entity.Property(c => c.Stats)
                .HasColumnType("jsonb");
        });
        
        // Item Configuration
        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("Items");
            entity.Property(i => i.Name).IsRequired();
            entity.HasIndex(i => i.Name).IsUnique();
        });
        
        // ScenarioCreature Configuration (Junction Table)
        modelBuilder.Entity<ScenarioCreature>(entity =>
        {
            entity.ToTable("ScenarioCreatures");
            
            // Composite key for the junction table
            entity.HasKey(sc => new { sc.ScenarioId, sc.CreatureId, sc.Id });
            
            // Relationship with Scenario
            entity.HasOne(sc => sc.Scenario)
                .WithMany(s => s.ScenarioCreatures)
                .HasForeignKey(sc => sc.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Relationship with Creature
            entity.HasOne(sc => sc.Creature)
                .WithMany(c => c.ScenarioCreatures)
                .HasForeignKey(sc => sc.CreatureId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // ScenarioItem Configuration (Junction Table)
        modelBuilder.Entity<ScenarioItem>(entity =>
        {
            entity.ToTable("ScenarioItems");
            
            // Composite key for the junction table
            entity.HasKey(si => new { si.ScenarioId, si.ItemId, si.Id });
            
            // Relationship with Scenario
            entity.HasOne(si => si.Scenario)
                .WithMany(s => s.ScenarioItems)
                .HasForeignKey(si => si.ScenarioId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Relationship with Item
            entity.HasOne(si => si.Item)
                .WithMany(i => i.ScenarioItems)
                .HasForeignKey(si => si.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}