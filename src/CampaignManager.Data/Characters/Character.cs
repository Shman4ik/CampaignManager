using System.Text.Json;
using CampaignManager.Core.Characters;
using CampaignManager.Data.Campaigns;
using CampaignManager.Data.Files;
using CampaignManager.Data.Identity;
using CampaignManager.Data.Infrastructure;
using CampaignManager.Data.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignManager.Data.Characters;

/// <summary>
/// Лист — документ (<see cref="Sheet"/>); кто он и чей — колонки и ограничения:
/// <list type="bullet">
/// <item><c>Player</c>: <see cref="OwnerId"/> — игрок; <see cref="CampaignId"/> — кампания, где он играет
/// (null — ещё не в кампании). Составной FK требует, чтобы игрок был её участником; удалили участника —
/// лист остаётся у игрока без кампании.</item>
/// <item><c>Pregen</c>: <see cref="ScenarioId"/> — преген сценария; null — заготовка в библиотеке.</item>
/// <item><c>Npc</c>: <see cref="CampaignId"/> — НПС кампании; null — общая библиотека.</item>
/// </list>
/// </summary>
public sealed class Character : ICreatedAt, IUpdatedAt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public CharacterKind Kind { get; set; }
    public CharacterStatus Status { get; set; }
    public Guid? OwnerId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? ScenarioId { get; set; }

    /// <summary>Откуда скопирован (бронь прегена).</summary>
    public Guid? OriginCharacterId { get; set; }

    public Guid? PortraitFileId { get; set; }

    /// <summary>Документ листа — <c>Core.Characters.CharacterSheet</c>: <c>CmJson.ReadSheet(Sheet, SheetVersion)</c> / <c>CmJson.Write</c>.</summary>
    public required JsonDocument Sheet { get; set; }

    public int SheetVersion { get; set; }

    /// <summary>Generated-колонка из <c>sheet.personal.name</c>: по ней ищут и сортируют.</summary>
    public string? Name { get; private set; }

    /// <summary>Generated-колонка из <c>sheet.personal.occupation</c>.</summary>
    public string? Occupation { get; private set; }

    public Guid? CreatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Системная <c>xmin</c>: лист правят с двух устройств — конфликт даёт 409, а не перезапись.</summary>
    public uint Version { get; set; }
}

internal sealed class CharacterConfiguration : IEntityTypeConfiguration<Character>
{
    /// <summary>
    /// Составной FK «игрок — участник кампании». <c>ON DELETE SET NULL (campaign_id)</c> EF выразить не
    /// может (обнулил бы и <c>owner_id</c>, а это нарушит CHECK вида), поэтому в модели он без действия,
    /// а в миграции пересоздан SQL-ом с этим именем.
    /// </summary>
    public const string MemberForeignKey = "fk_characters_campaign_members_campaign_id_owner_id";

    public void Configure(EntityTypeBuilder<Character> entity)
    {
        entity.ToTable("characters", table => table.HasCheckConstraint("ck_characters_owner", """
            (kind = 'Player' AND owner_id IS NOT NULL AND scenario_id IS NULL)
            OR (kind = 'Pregen' AND owner_id IS NULL AND campaign_id IS NULL)
            OR (kind = 'Npc' AND owner_id IS NULL AND scenario_id IS NULL)
            """));
        entity.Property(c => c.Status).HasDbDefault(CharacterStatus.Active);
        entity.Property(c => c.Sheet).HasColumnType("jsonb");
        entity.Property(c => c.Name).HasComputedColumnSql("sheet #>> '{personal,name}'", stored: true);
        entity.Property(c => c.Occupation).HasComputedColumnSql("sheet #>> '{personal,occupation}'", stored: true);
        entity.Property(c => c.Version).IsRowVersion();

        entity.HasOne<User>().WithMany().HasForeignKey(c => c.OwnerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Campaign>().WithMany().HasForeignKey(c => c.CampaignId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne<Scenario>().WithMany().HasForeignKey(c => c.ScenarioId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne<Character>().WithMany().HasForeignKey(c => c.OriginCharacterId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne<StoredFile>().WithMany().HasForeignKey(c => c.PortraitFileId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne<User>().WithMany().HasForeignKey(c => c.CreatedById).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne<CampaignMember>().WithMany()
            .HasForeignKey(c => new { c.CampaignId, c.OwnerId })
            .HasPrincipalKey(m => new { m.CampaignId, m.UserId })
            .OnDelete(DeleteBehavior.ClientNoAction)
            .HasConstraintName(MemberForeignKey);

        // У игрока не больше одного активного листа в кампании.
        entity.HasIndex(c => new { c.CampaignId, c.OwnerId })
            .IsUnique()
            .HasFilter("kind = 'Player' AND status = 'Active' AND campaign_id IS NOT NULL")
            .HasDatabaseName("characters_one_active_sheet");
        entity.HasIndex(c => new { c.Kind, c.Status }).HasDatabaseName("characters_kind_status");
        entity.HasIndex(c => c.OwnerId).HasFilter("owner_id IS NOT NULL").HasDatabaseName("characters_owner");
        entity.HasIndex(c => c.CampaignId).HasFilter("campaign_id IS NOT NULL").HasDatabaseName("characters_campaign");
        entity.HasIndex(c => c.ScenarioId).HasFilter("scenario_id IS NOT NULL").HasDatabaseName("characters_scenario");
    }
}
