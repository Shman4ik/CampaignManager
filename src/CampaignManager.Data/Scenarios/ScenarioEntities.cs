using System.Text.Json;
using CampaignManager.Core;
using CampaignManager.Core.Scenarios;
using CampaignManager.Data.Catalogs;
using CampaignManager.Data.Characters;
using CampaignManager.Data.Files;
using CampaignManager.Data.Identity;
using CampaignManager.Data.Infrastructure;
using CampaignManager.Data.Music;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignManager.Data.Scenarios;

/// <summary>
/// Сценарий — содержимое (общая библиотека Хранителей). Игра в кампании — <c>scenario_runs</c>:
/// шаблоны в кампании больше не копируются, переделка под свою группу — явный форк
/// (<see cref="SourceScenarioId"/>).
/// </summary>
public sealed class Scenario : ICreatedAt, IUpdatedAt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }

    /// <summary>v1 <c>Description</c>.</summary>
    public string? Summary { get; set; }

    /// <summary>v1 <c>Journal</c>: основной текст Хранителя в Markdown.</summary>
    public string? BodyMd { get; set; }

    /// <summary>v1 <c>Location</c>: «Бостон, Массачусетс, США».</summary>
    public string? Setting { get; set; }

    public Era? Era { get; set; }

    /// <summary>v1 <c>Era</c> как текст: «Июнь 1925 года», «1931».</summary>
    public string? SettingDate { get; set; }

    public Guid? AuthorId { get; set; }
    public Guid? SourceScenarioId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Системная <c>xmin</c>: ETag в API, конфликт правки — 409.</summary>
    public uint Version { get; set; }

    public List<ScenarioLocation> Locations { get; set; } = [];
    public List<ScenarioKeyFact> KeyFacts { get; set; } = [];
    public List<ScenarioHandout> Handouts { get; set; } = [];
    public List<ScenarioCreature> Creatures { get; set; } = [];
    public List<ScenarioItem> Items { get; set; } = [];
    public List<ScenarioNpc> Npcs { get; set; } = [];
}

public sealed class ScenarioLocation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ScenarioId { get; set; }
    public Guid? ParentId { get; set; }
    public int Ord { get; set; }
    public required string Name { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }

    /// <summary>Настроение локации для фонотеки; прибитые треки — <c>location_tracks</c>.</summary>
    public List<string> MusicTags { get; set; } = [];

    public List<ScenarioCheck> Checks { get; set; } = [];
}

/// <summary>Проверка в локации: навык (<see cref="SkillId"/>), характеристика или Удача.</summary>
public sealed class ScenarioCheck
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid LocationId { get; set; }
    public int Ord { get; set; }
    public CheckTarget TargetKind { get; set; }
    public Guid? SkillId { get; set; }
    public Characteristic? Characteristic { get; set; }
    public Difficulty Difficulty { get; set; }
    public string? OnSuccess { get; set; }
    public string? OnFailure { get; set; }
}

public sealed class ScenarioKeyFact
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ScenarioId { get; set; }
    public int Ord { get; set; }
    public KeyFactType Type { get; set; }
    public required string Title { get; set; }
    public string? Content { get; set; }
}

public sealed class ScenarioHandout
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ScenarioId { get; set; }
    public int Ord { get; set; }

    /// <summary>Пометка Хранителя, игрокам не показывается.</summary>
    public required string Name { get; set; }

    /// <summary>То, что видят игроки (v1 <c>Description</c>).</summary>
    public string? PlayerText { get; set; }

    /// <summary>Только Хранителю.</summary>
    public string? KeeperNote { get; set; }

    public Guid? FileId { get; set; }
}

/// <summary>
/// Тварь сценария — ссылка на бестиарий плюс необязательная своя версия статблока: исправление в
/// бестиарии доходит до сценариев, где тварь не переделывали. Без ссылки обязательны имя и статблок.
/// </summary>
public sealed class ScenarioCreature
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ScenarioId { get; set; }
    public int Ord { get; set; }
    public Guid? CreatureId { get; set; }

    /// <summary>Переопределение имени («Вожак культистов»).</summary>
    public string? Name { get; set; }

    /// <summary>null — как в бестиарии.</summary>
    public JsonDocument? Statblock { get; set; }

    public int? StatblockVersion { get; set; }
    public int Count { get; set; } = 1;

    /// <summary>«в подвале особняка» — так положение задают на деле.</summary>
    public string? LocationNote { get; set; }

    public string? Notes { get; set; }
}

public sealed class ScenarioItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ScenarioId { get; set; }
    public int Ord { get; set; }
    public Guid? ItemId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? LocationNote { get; set; }
    public string? Notes { get; set; }
}

/// <summary>НПС в составе сценария — связь, а не копия листа. Роль и количество — у появления.</summary>
public sealed class ScenarioNpc
{
    public Guid ScenarioId { get; set; }
    public Guid CharacterId { get; set; }
    public NpcRole Role { get; set; }
    public int Count { get; set; } = 1;
    public string? Notes { get; set; }
}

/// <summary>Трек, прибитый к локации.</summary>
public sealed class LocationTrack
{
    public Guid LocationId { get; set; }
    public Guid TrackId { get; set; }
}

internal sealed class ScenarioConfiguration : IEntityTypeConfiguration<Scenario>
{
    public void Configure(EntityTypeBuilder<Scenario> entity)
    {
        entity.ToTable("scenarios");
        entity.Property(s => s.Version).IsRowVersion();
        entity.HasOne<User>().WithMany().HasForeignKey(s => s.AuthorId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne<Scenario>().WithMany().HasForeignKey(s => s.SourceScenarioId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ScenarioLocationConfiguration : IEntityTypeConfiguration<ScenarioLocation>
{
    public void Configure(EntityTypeBuilder<ScenarioLocation> entity)
    {
        entity.ToTable("scenario_locations");
        entity.PrimitiveCollection(l => l.MusicTags).HasDbDefaultSql(SchemaConventions.EmptyArray);
        entity.HasOne<Scenario>().WithMany(s => s.Locations).HasForeignKey(l => l.ScenarioId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<ScenarioLocation>().WithMany().HasForeignKey(l => l.ParentId).OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(l => new { l.ScenarioId, l.Ord }).HasDatabaseName("scenario_locations_order");
    }
}

internal sealed class ScenarioCheckConfiguration : IEntityTypeConfiguration<ScenarioCheck>
{
    public void Configure(EntityTypeBuilder<ScenarioCheck> entity)
    {
        entity.ToTable("scenario_checks", table =>
        {
            table.HasCheckConstraint("ck_scenario_checks_skill", "(target_kind = 'Skill') = (skill_id IS NOT NULL)");
            table.HasCheckConstraint("ck_scenario_checks_characteristic_target",
                "(target_kind = 'Characteristic') = (characteristic IS NOT NULL)");
        });
        entity.Property(c => c.Difficulty).HasDbDefault(Difficulty.Regular);
        entity.HasOne<ScenarioLocation>().WithMany(l => l.Checks).HasForeignKey(c => c.LocationId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Skill>().WithMany().HasForeignKey(c => c.SkillId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ScenarioKeyFactConfiguration : IEntityTypeConfiguration<ScenarioKeyFact>
{
    public void Configure(EntityTypeBuilder<ScenarioKeyFact> entity)
    {
        entity.ToTable("scenario_key_facts");
        entity.HasOne<Scenario>().WithMany(s => s.KeyFacts).HasForeignKey(f => f.ScenarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ScenarioHandoutConfiguration : IEntityTypeConfiguration<ScenarioHandout>
{
    public void Configure(EntityTypeBuilder<ScenarioHandout> entity)
    {
        entity.ToTable("scenario_handouts");
        entity.HasOne<Scenario>().WithMany(s => s.Handouts).HasForeignKey(h => h.ScenarioId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<StoredFile>().WithMany().HasForeignKey(h => h.FileId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ScenarioCreatureConfiguration : IEntityTypeConfiguration<ScenarioCreature>
{
    public void Configure(EntityTypeBuilder<ScenarioCreature> entity)
    {
        entity.ToTable("scenario_creatures", table =>
        {
            table.HasCheckConstraint("ck_scenario_creatures_count", "count >= 1");
            table.HasCheckConstraint("ck_scenario_creatures_source",
                "creature_id IS NOT NULL OR (name IS NOT NULL AND statblock IS NOT NULL)");
        });
        entity.Property(c => c.Statblock).HasColumnType("jsonb");
        entity.Property(c => c.Count).HasDbDefault(1);
        entity.HasOne<Scenario>().WithMany(s => s.Creatures).HasForeignKey(c => c.ScenarioId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Creature>().WithMany().HasForeignKey(c => c.CreatureId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ScenarioItemConfiguration : IEntityTypeConfiguration<ScenarioItem>
{
    public void Configure(EntityTypeBuilder<ScenarioItem> entity)
    {
        entity.ToTable("scenario_items", table =>
            table.HasCheckConstraint("ck_scenario_items_source", "item_id IS NOT NULL OR name IS NOT NULL"));
        entity.HasOne<Scenario>().WithMany(s => s.Items).HasForeignKey(i => i.ScenarioId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Item>().WithMany().HasForeignKey(i => i.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ScenarioNpcConfiguration : IEntityTypeConfiguration<ScenarioNpc>
{
    public void Configure(EntityTypeBuilder<ScenarioNpc> entity)
    {
        entity.ToTable("scenario_npcs", table => table.HasCheckConstraint("ck_scenario_npcs_count", "count >= 1"));
        entity.HasKey(n => new { n.ScenarioId, n.CharacterId });
        entity.Property(n => n.Role).HasDbDefault(NpcRole.Neutral);
        entity.Property(n => n.Count).HasDbDefault(1);
        entity.HasOne<Scenario>().WithMany(s => s.Npcs).HasForeignKey(n => n.ScenarioId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Character>().WithMany().HasForeignKey(n => n.CharacterId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LocationTrackConfiguration : IEntityTypeConfiguration<LocationTrack>
{
    public void Configure(EntityTypeBuilder<LocationTrack> entity)
    {
        entity.ToTable("location_tracks");
        entity.HasKey(t => new { t.LocationId, t.TrackId });
        entity.HasOne<ScenarioLocation>().WithMany().HasForeignKey(t => t.LocationId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<MusicTrack>().WithMany().HasForeignKey(t => t.TrackId).OnDelete(DeleteBehavior.Cascade);
    }
}
