using CampaignManager.Data.Catalogs;
using CampaignManager.Data.Files;
using CampaignManager.Data.Identity;
using CampaignManager.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignManager.Data.Music;

/// <summary>
/// Трек фонотеки: файл в MinIO или ролик YouTube — ровно одно из двух (CHECK). Фонотека — справочник
/// (общий <c>CatalogService</c> сервера: список с ETag, правка с <c>If-Match</c>, импорт, журнал правок),
/// поэтому трек — <see cref="CatalogEntry"/>. Кода книги и страницы-источника у трека нет: эти два свойства
/// модель не отображает (колонок нет), они всегда <c>null</c>.
/// </summary>
public sealed class MusicTrack : CatalogEntry
{
    public string? YoutubeId { get; set; }
    public Guid? FileId { get; set; }
    public int StartSeconds { get; set; }
    public bool Loop { get; set; } = true;
    public int Volume { get; set; } = 100;
    public List<string> Tags { get; set; } = [];
    public string? Notes { get; set; }
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
        // Кода книги и источника у трека нет — колонок под них тоже.
        entity.Ignore(t => t.Code);
        entity.Ignore(t => t.Source);
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
