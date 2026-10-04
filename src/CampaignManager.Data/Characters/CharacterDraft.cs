using System.Text.Json;
using CampaignManager.Data.Campaigns;
using CampaignManager.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignManager.Data.Characters;

/// <summary>
/// Черновик помощника создания сыщика игрока в кампании — один на участника (ключ — пара «кампания, игрок»). Пишет
/// только сам игрок, с любого устройства; Хранитель кампании видит, что сыщика создают и на каком шаге.
/// <para>
/// Составной FK на <c>campaign_members</c> с <c>ON DELETE CASCADE</c>: вышел из кампании, исключён, кампанию удалили —
/// черновик уходит вместе с участием, сервису помнить об этом не нужно. Черновики НПС, прегенов и библиотеки живут в
/// <c>localStorage</c> браузера.
/// </para>
/// </summary>
public sealed class CharacterDraft : ICreatedAt, IUpdatedAt
{
    public Guid CampaignId { get; set; }

    public Guid OwnerId { get; set; }

    /// <summary>Документ — <c>Core.Characters.InvestigatorDraft</c>: <c>CmJson.ReadDraft(Draft, DraftVersion)</c> / <c>CmJson.Write</c>.</summary>
    public required JsonDocument Draft { get; set; }

    public int DraftVersion { get; set; }

    /// <summary>
    /// Шаг помощника (<c>CreationStep</c>, 0–6) — копия <c>StepIndex</c> документа колонкой: главная и страница кампании
    /// показывают его Хранителю, не читая документ.
    /// </summary>
    public int Step { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Системная <c>xmin</c>: черновик пишут с двух устройств — устаревшая запись получает 409, а не затирает.</summary>
    public uint Version { get; set; }
}

internal sealed class CharacterDraftConfiguration : IEntityTypeConfiguration<CharacterDraft>
{
    public void Configure(EntityTypeBuilder<CharacterDraft> entity)
    {
        entity.ToTable("character_drafts", table => table.HasCheckConstraint("ck_character_drafts_step", "step BETWEEN 0 AND 6"));
        entity.HasKey(d => new { d.CampaignId, d.OwnerId });
        entity.Property(d => d.Draft).HasColumnType("jsonb");
        entity.Property(d => d.Version).IsRowVersion();

        entity.HasOne<CampaignMember>().WithMany()
            .HasForeignKey(d => new { d.CampaignId, d.OwnerId })
            .HasPrincipalKey(m => new { m.CampaignId, m.UserId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
