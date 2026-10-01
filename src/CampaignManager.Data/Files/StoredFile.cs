using CampaignManager.Data.Identity;
using CampaignManager.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignManager.Data.Files;

/// <summary>
/// Любая картинка, раздатка, трек или портрет (<c>cm.files</c>): объект MinIO (<see cref="StorageKey"/>)
/// либо внешний адрес (<see cref="ExternalUrl"/>) — ровно одно из двух. Копия листа делит файл с
/// оригиналом; сироты ищутся одним anti-join по FK.
/// </summary>
public sealed class StoredFile : ICreatedAt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string? StorageKey { get; set; }
    public string? ExternalUrl { get; set; }
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public string? OriginalName { get; set; }
    public Guid? UploadedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> entity)
    {
        entity.ToTable("files", table => table.HasCheckConstraint(
            "ck_files_storage_or_url", "(storage_key IS NULL) <> (external_url IS NULL)"));
        entity.HasIndex(f => f.StorageKey).IsUnique();
        entity.HasOne<User>().WithMany().HasForeignKey(f => f.UploadedById).OnDelete(DeleteBehavior.SetNull);
    }
}
