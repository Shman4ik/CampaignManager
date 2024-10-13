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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("Dev");

        modelBuilder.Entity<Campaign>()
            .HasOne(c => c.Keeper)
            .WithMany()
            .HasForeignKey(c => c.KeeperId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Campaign>()
            .HasMany(c => c.Players)
            .WithMany()
            .UsingEntity(j => j.ToTable("CampaignPlayers"));
    }
}