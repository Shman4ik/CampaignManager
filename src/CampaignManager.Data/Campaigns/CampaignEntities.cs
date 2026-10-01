using CampaignManager.Core;
using CampaignManager.Core.Campaigns;
using CampaignManager.Data.Characters;
using CampaignManager.Data.Identity;
using CampaignManager.Data.Infrastructure;
using CampaignManager.Data.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignManager.Data.Campaigns;

public sealed class Campaign : ICreatedAt, IUpdatedAt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public CampaignKind Kind { get; set; }
    public CampaignStatus Status { get; set; }
    public Era Era { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<CampaignMember> Members { get; set; } = [];
}

/// <summary>
/// Участник кампании. Хранитель — тоже участник, с ролью <see cref="CampaignRole.Keeper"/>, и ровно
/// один (частичный уникальный индекс): «может ли он это видеть» = «участник ли он».
/// </summary>
public sealed class CampaignMember : ICreatedAt
{
    public Guid CampaignId { get; set; }
    public Guid UserId { get; set; }
    public CampaignRole Role { get; set; }

    /// <summary>Псевдоним в этой кампании; null — <c>users.display_name</c>.</summary>
    public string? DisplayName { get; set; }

    public DateTimeOffset JoinedAt { get; set; }

    DateTimeOffset ICreatedAt.CreatedAt
    {
        get => JoinedAt;
        set => JoinedAt = value;
    }
}

/// <summary>Прохождение сценария в кампании: дата, анонс, запись на ваншот. Содержимое не копируется.</summary>
public sealed class ScenarioRun : ICreatedAt, IUpdatedAt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ScenarioId { get; set; }
    public Guid CampaignId { get; set; }
    public ScenarioRunStatus Status { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public string? Announcement { get; set; }

    /// <summary>Ваншот: игроки бронируют прегенов.</summary>
    public bool SignupOpen { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Бронь прегена: лист прегена копируется в лист игрока (<see cref="CharacterId"/>,
/// <c>origin_character_id</c> = преген), а сам преген остаётся для следующего прохождения.
/// </summary>
public sealed class RunReservation : ICreatedAt
{
    public Guid RunId { get; set; }
    public Guid PregenId { get; set; }
    public Guid UserId { get; set; }
    public Guid? CharacterId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Встреча в журнале кампании.</summary>
public sealed class CampaignSession : ICreatedAt, IUpdatedAt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CampaignId { get; set; }
    public Guid? RunId { get; set; }
    public int Number { get; set; }
    public DateOnly SessionDate { get; set; }
    public string? Title { get; set; }

    /// <summary>Видно всем участникам.</summary>
    public string? Summary { get; set; }

    /// <summary>Только Хранителю: вырезает сервер, а не разметка.</summary>
    public string? KeeperNotes { get; set; }

    public bool ScenarioCompleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> entity)
    {
        entity.ToTable("campaigns");
        entity.Property(c => c.Kind).HasDbDefault(CampaignKind.Campaign);
        entity.Property(c => c.Status).HasDbDefault(CampaignStatus.Planning);
        entity.Property(c => c.Era).HasDbDefault(Era.Classic);
        entity.HasOne<User>().WithMany().HasForeignKey(c => c.CreatedById).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class CampaignMemberConfiguration : IEntityTypeConfiguration<CampaignMember>
{
    public void Configure(EntityTypeBuilder<CampaignMember> entity)
    {
        entity.ToTable("campaign_members");
        entity.HasKey(m => new { m.CampaignId, m.UserId });
        entity.Property(m => m.JoinedAt).HasDbDefaultSql("now()");

        entity.HasOne<Campaign>().WithMany(c => c.Members).HasForeignKey(m => m.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(m => m.CampaignId)
            .IsUnique()
            .HasFilter("role = 'Keeper'")
            .HasDatabaseName("campaign_members_one_keeper");
    }
}

internal sealed class ScenarioRunConfiguration : IEntityTypeConfiguration<ScenarioRun>
{
    public void Configure(EntityTypeBuilder<ScenarioRun> entity)
    {
        entity.ToTable("scenario_runs");
        entity.Property(r => r.Status).HasDbDefault(ScenarioRunStatus.Planned);
        entity.Property(r => r.SignupOpen).HasDbDefault(false);

        entity.HasOne<Scenario>().WithMany().HasForeignKey(r => r.ScenarioId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Campaign>().WithMany().HasForeignKey(r => r.CampaignId).OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(r => r.CampaignId).HasDatabaseName("scenario_runs_campaign");
        entity.HasIndex(r => r.ScheduledAt).HasFilter("signup_open").HasDatabaseName("scenario_runs_open");
    }
}

internal sealed class RunReservationConfiguration : IEntityTypeConfiguration<RunReservation>
{
    public void Configure(EntityTypeBuilder<RunReservation> entity)
    {
        entity.ToTable("run_reservations");
        entity.HasKey(r => new { r.RunId, r.PregenId });
        entity.HasIndex(r => new { r.RunId, r.UserId }).IsUnique();

        entity.HasOne<ScenarioRun>().WithMany().HasForeignKey(r => r.RunId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Character>().WithMany().HasForeignKey(r => r.PregenId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Character>().WithMany().HasForeignKey(r => r.CharacterId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class CampaignSessionConfiguration : IEntityTypeConfiguration<CampaignSession>
{
    public void Configure(EntityTypeBuilder<CampaignSession> entity)
    {
        entity.ToTable("campaign_sessions");
        entity.Property(s => s.ScenarioCompleted).HasDbDefault(false);

        entity.HasOne<Campaign>().WithMany().HasForeignKey(s => s.CampaignId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<ScenarioRun>().WithMany().HasForeignKey(s => s.RunId).OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(s => new { s.CampaignId, s.SessionDate, s.Number })
            .IsDescending(false, true, true)
            .HasDatabaseName("campaign_sessions_journal");
    }
}
