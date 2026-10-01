using CampaignManager.Data.Files;
using CampaignManager.Data.Identity;
using CampaignManager.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignManager.Data.Music;

/// <summary>Трек фонотеки: файл в MinIO или ролик YouTube — ровно одно из двух (CHECK).</summary>
public sealed class MusicTrack : ICreatedAt, IUpdatedAt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public string? YoutubeId { get; set; }
    public Guid? FileId { get; set; }
    public int StartSeconds { get; set; }
    public bool Loop { get; set; } = true;
    public int Volume { get; set; } = 100;
    public List<string> Tags { get; set; } = [];
    public string? Notes { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Системная <c>xmin</c>: фонотеку правят с двух устройств, как справочники.</summary>
    public uint Version { get; set; }
}

internal sealed class MusicTrackConfiguration : IEntityTypeConfiguration<MusicTrack>
{
    public void Configure(EntityTypeBuilder<MusicTrack> entity)
    {
        entity.ToTable("music_tracks", table =>
        {
            table.HasCheckConstraint("ck_music_tracks_volume", "volume BETWEEN 0 AND 100");
            table.HasCheckConstraint("ck_music_tracks_youtube_or_file", "(youtube_id IS NULL) <> (file_id IS NULL)");
        });
        entity.Property(t => t.StartSeconds).HasDbDefault(0);
        entity.Property(t => t.Loop).HasDbDefault(true);
        entity.Property(t => t.Volume).HasDbDefault(100);
        entity.PrimitiveCollection(t => t.Tags).HasDbDefaultSql(SchemaConventions.EmptyArray);
        entity.Property(t => t.Version).IsRowVersion();

        entity.HasOne<StoredFile>().WithMany().HasForeignKey(t => t.FileId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<User>().WithMany().HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(t => t.Tags).HasMethod("gin").HasDatabaseName("music_tracks_tags");
    }
}
