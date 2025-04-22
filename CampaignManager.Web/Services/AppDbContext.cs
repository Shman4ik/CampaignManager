using CampaignManager.Web.Companies.Models;
using CampaignManager.Web.Model;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Services;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Campaign> Campaigns { get; set; }
    public DbSet<CharacterStorageDto> CharacterStorage { get; set; }
    public DbSet<CampaignPlayer> CampaignPlayers { get; set; }
    public DbSet<CloseCombatWeapon> CloseCombatWeapons { get; set; }
    public DbSet<RangedCombatWeapon> RangedCombatWeapons { get; set; }

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
        modelBuilder.Entity<CloseCombatWeapon>(entity =>
        {
            entity.ToTable("CloseCombatWeapons");
            entity.Property(w => w.Type)
                .HasDefaultValue(WeaponType.CloseCombat)
                .HasConversion<string>();
            entity.HasIndex(w => w.Name).IsUnique();
        });

        modelBuilder.Entity<RangedCombatWeapon>(entity =>
        {
            entity.ToTable("RangedCombatWeapons");
            entity.Property(w => w.Type)
                .HasDefaultValue(WeaponType.RangedCombat)
                .HasConversion<string>();
            entity.HasIndex(w => w.Name).IsUnique();
        });
    }
}