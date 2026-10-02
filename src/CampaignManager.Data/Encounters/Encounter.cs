using System.Text.Json;
using CampaignManager.Core.Encounters;
using CampaignManager.Data.Campaigns;
using CampaignManager.Data.Identity;
using CampaignManager.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignManager.Data.Encounters;

/// <summary>
/// Живое состояние стола: бой и погоня одной таблицей. У Хранителя не больше одного активного <b>боя</b> на кампанию —
/// в том числе вне кампании (<c>NULLS NOT DISTINCT</c>). Погонь — сколько угодно: разделившихся ведут отдельными
/// погонями (стр. 142, решение владельца 2026-10-02).
/// </summary>
public sealed class Encounter : ICreatedAt, IUpdatedAt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid KeeperId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? RunId { get; set; }
    public EncounterKind Kind { get; set; }
    public EncounterStatus Status { get; set; }

    /// <summary>Снимок движка сцены — <c>Core.Encounters.EncounterState</c> (каркас T1.7, наполняет T2.6).</summary>
    public required JsonDocument State { get; set; }

    public int StateVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Системная <c>xmin</c>: сцену ведут с двух устройств.</summary>
    public uint Version { get; set; }
}

internal sealed class EncounterConfiguration : IEntityTypeConfiguration<Encounter>
{
    public void Configure(EntityTypeBuilder<Encounter> entity)
    {
        entity.ToTable("encounters");
        entity.Property(e => e.Status).HasDbDefault(EncounterStatus.Active);
        entity.Property(e => e.State).HasColumnType("jsonb");
        entity.Property(e => e.Version).IsRowVersion();

        entity.HasOne<User>().WithMany().HasForeignKey(e => e.KeeperId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Campaign>().WithMany().HasForeignKey(e => e.CampaignId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<ScenarioRun>().WithMany().HasForeignKey(e => e.RunId).OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => new { e.KeeperId, e.CampaignId })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("status = 'Active' AND kind = 'Combat'")
            .HasDatabaseName("encounters_one_active_combat");
    }
}
