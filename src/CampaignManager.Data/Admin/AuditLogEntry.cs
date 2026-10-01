using System.Text.Json;
using CampaignManager.Core.Admin;
using CampaignManager.Data.Identity;
using CampaignManager.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignManager.Data.Admin;

/// <summary>История правок справочников: только снимок «после», «до» — предыдущая запись.</summary>
public sealed class AuditLogEntry : ICreatedAt
{
    public long Id { get; set; }
    public required string EntityType { get; set; }
    public Guid EntityId { get; set; }
    public AuditAction Action { get; set; }
    public Guid? ActorId { get; set; }
    public JsonDocument? Snapshot { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> entity)
    {
        entity.ToTable("audit_log");
        entity.Property(e => e.Id).UseIdentityAlwaysColumn();
        entity.Property(e => e.Snapshot).HasColumnType("jsonb");
        entity.HasOne<User>().WithMany().HasForeignKey(e => e.ActorId).OnDelete(DeleteBehavior.SetNull);
        entity.HasIndex(e => new { e.EntityType, e.EntityId, e.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("audit_log_entity");
    }
}
