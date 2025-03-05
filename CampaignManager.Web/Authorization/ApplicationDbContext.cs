using CampaignManager.Web.Model;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<ApplicationUser> ApplicationUsers { get; set; }
    public DbSet<Campaign> Campaigns { get; set; }
    public DbSet<CharacterStorageDto> Characters { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("Dev");

        // Настройка Campaign
        modelBuilder.Entity<Campaign>()
            .HasOne(c => c.Keeper)
            .WithMany()
            .HasForeignKey(c => c.KeeperId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Campaign>()
            .HasMany(c => c.Players)
            .WithMany()
            .UsingEntity(j => j.ToTable("CampaignPlayers"));

        // Настройка CharacterStorageDto
        modelBuilder.Entity<CharacterStorageDto>(entity =>
        {
            entity.ToTable("Characters");

            // Настройка связи с игроком
            entity.Property<string>("PlayerId");
            entity.HasOne<ApplicationUser>()
                  .WithMany()
                  .HasForeignKey("PlayerId")
                  .OnDelete(DeleteBehavior.Cascade);

            // Настройка связи с кампанией
            entity.Property<Guid?>("CampaignId");
            entity.HasOne<Campaign>()
                  .WithMany()
                  .HasForeignKey("CampaignId")
                  .OnDelete(DeleteBehavior.SetNull);

            // Используем JSONB для хранения данных персонажа
            entity.Property(c => c.CharacterData)
                  .HasColumnType("jsonb")
                  .HasComment("JSON-представление персонажа со всеми характеристиками");

            // Добавляем индекс для более быстрого поиска
            entity.HasIndex("PlayerId", "CampaignId");
        });
    }
}