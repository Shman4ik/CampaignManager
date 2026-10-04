using System.Text.Json;
using CampaignManager.Core;
using CampaignManager.Core.Catalogs;
using CampaignManager.Data.Files;
using CampaignManager.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignManager.Data.Catalogs;

public sealed class Skill : CatalogEntry
{
    /// <summary>Специализация → родитель («Язык, иностранный» → «Язык, иностранный (латынь)»).</summary>
    public Guid? ParentId { get; set; }

    public int BaseValue { get; set; }

    /// <summary>Когда база не число: <c>DEX/2</c>, <c>EDU</c>.</summary>
    public string? BaseFormula { get; set; }

    public SkillCategory Category { get; set; }
    public bool IsUncommon { get; set; }
    public List<Era> Eras { get; set; } = [Era.Classic, Era.Modern];
    public string Description { get; set; } = "";
    public List<string> UsageExamples { get; set; } = [];
    public List<string> FailureConsequences { get; set; } = [];
    public List<string> OpposingSkills { get; set; } = [];
    public string? TimeRequired { get; set; }
    public bool CanRetry { get; set; }
}

public sealed class Occupation : CatalogEntry
{
    public SkillPointsFormula SkillPointsFormula { get; set; }
    public int CreditRatingMin { get; set; }
    public int CreditRatingMax { get; set; }
    public List<Era> Eras { get; set; } = [Era.Classic, Era.Modern];
    public bool IsLovecraftian { get; set; }

    /// <summary>В v1 — битовая маска int.</summary>
    public List<string> Tags { get; set; } = [];

    public List<OccupationSlot> Slots { get; set; } = [];

    /// <summary>Первая по <see cref="OccupationImage.Ord"/> — обложка карточки.</summary>
    public List<OccupationImage> Images { get; set; } = [];
}

/// <summary>
/// Слот навыка профессии — вместо четырёх полей v1 (<c>OccupationSkills</c>, <c>SkillChoices</c>,
/// <c>SocialSkillSlots</c>, <c>FreeSkillSlots</c>). Какие поля заполнены, решает <see cref="Kind"/> (CHECK).
/// </summary>
public sealed class OccupationSlot
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OccupationId { get; set; }
    public int Ord { get; set; }
    public OccupationSlotKind Kind { get; set; }

    /// <summary><c>Skill</c> — сам навык; <c>(Any)Specialization</c> — родитель.</summary>
    public Guid? SkillId { get; set; }

    /// <summary><c>Specialization</c>: «латынь», «черчение».</summary>
    public string? Specialization { get; set; }

    public int ChooseCount { get; set; } = 1;

    /// <summary>Варианты слота <c>Choice</c>; родитель разворачивается в специализации.</summary>
    public List<OccupationSlotOption> Options { get; set; } = [];
}

public sealed class OccupationSlotOption
{
    public Guid SlotId { get; set; }
    public Guid SkillId { get; set; }
}

/// <summary>
/// Оружие: строки книги — для показа, числа для боя — отдельными колонками, разобранными один раз
/// при вводе (а не на каждом чтении). Ступени совместимости из v1 нет.
/// </summary>
public sealed class Weapon : CatalogEntry
{
    public WeaponType Type { get; set; }
    public Guid SkillId { get; set; }
    public List<Era> Eras { get; set; } = [Era.Classic, Era.Modern];
    public bool IsRare { get; set; }
    public bool IsImpaling { get; set; }

    /// <summary>«1d6 + БкУ», как в книге; разбирает Core.</summary>
    public required string Damage { get; set; }

    /// <summary>Дробовики: урон по дистанциям.</summary>
    public List<RangeDamage>? DamageByRange { get; set; }

    public string Range { get; set; } = "";
    public int? BaseRangeM { get; set; }
    public string Attacks { get; set; } = "";
    public int? ShotsPerRound { get; set; }
    public int? MaxShotsPerRound { get; set; }
    public string Ammo { get; set; } = "";
    public int? AmmoCapacity { get; set; }
    public int[]? AmmoCapacityOptions { get; set; }
    public bool SingleUse { get; set; }

    /// <summary>Осечка; «00» книги = 100.</summary>
    public int? Malfunction { get; set; }

    public string Cost { get; set; } = "";
    public decimal? CostClassic { get; set; }
    public decimal? CostModern { get; set; }
    public string Notes { get; set; } = "";

    /// <summary>Первая по <see cref="WeaponImage.Ord"/> — обложка.</summary>
    public List<WeaponImage> Images { get; set; } = [];
}

/// <summary>Урон дробовика на дистанции: <c>{"range":"10 м","damage":"4d6"}</c>.</summary>
public sealed record RangeDamage(string Range, string Damage);

public sealed class Spell : CatalogEntry
{
    public List<string> AltNames { get; set; } = [];
    public required string SpellType { get; set; }

    /// <summary>Текст книги; подсказку для боя разбирает Core.</summary>
    public string? Cost { get; set; }

    public string? CastingTime { get; set; }
    public string Description { get; set; } = "";
}

public sealed class Book : CatalogEntry
{
    public BookType BookType { get; set; }
    public List<string> AltNames { get; set; } = [];
    public string? Language { get; set; }
    public string? Year { get; set; }
    public string? Author { get; set; }
    public string? SanityLoss { get; set; }
    public int? MythosInitial { get; set; }
    public int? MythosFull { get; set; }
    public int? MythosRating { get; set; }
    public int? StudyWeeks { get; set; }
    public int? OccultismBonus { get; set; }
    public string Description { get; set; } = "";
    public Guid? ImageFileId { get; set; }

    public List<BookSpell> Spells { get; set; } = [];
}

/// <summary>
/// Заклинание из книги. Сопоставление с каталогом делается один раз, при вводе; несопоставленное
/// остаётся строкой книги (<see cref="SpellId"/> null) — Хранитель свяжет руками.
/// </summary>
public sealed class BookSpell
{
    public Guid BookId { get; set; }
    public int Ord { get; set; }
    public required string RawName { get; set; }
    public Guid? SpellId { get; set; }
}

public sealed class Item : CatalogEntry
{
    public string? Type { get; set; }
    public List<Era> Eras { get; set; } = [Era.Classic, Era.Modern];
    public string? Description { get; set; }

    /// <summary>Цена в долларах 1920-х (как <see cref="Weapon.CostClassic"/>); null — не указана.</summary>
    public decimal? Price { get; set; }

    public Guid? ImageFileId { get; set; }
}

/// <summary>Тварь бестиария. Статблок — документ Core <c>Statblock</c>: <c>CmJson.ReadStatblock(Statblock, StatblockVersion)</c> / <c>CmJson.Write</c>.</summary>
public sealed class Creature : CatalogEntry
{
    public CreatureType Type { get; set; }
    public string? Description { get; set; }
    public required JsonDocument Statblock { get; set; }
    public int StatblockVersion { get; set; }

    /// <summary>Первая по <see cref="CreatureImage.Ord"/> — обложка.</summary>
    public List<CreatureImage> Images { get; set; } = [];
}

/// <summary>Картинка записи справочника: у каждого справочника своя таблица, ключ — (запись, порядок).</summary>
public interface ICatalogImage
{
    int Ord { get; set; }
    Guid FileId { get; set; }
    string? Caption { get; set; }
}

public sealed class CreatureImage : ICatalogImage
{
    public Guid CreatureId { get; set; }
    public int Ord { get; set; }
    public Guid FileId { get; set; }
    public string? Caption { get; set; }
}

public sealed class WeaponImage : ICatalogImage
{
    public Guid WeaponId { get; set; }
    public int Ord { get; set; }
    public Guid FileId { get; set; }
    public string? Caption { get; set; }
}

public sealed class OccupationImage : ICatalogImage
{
    public Guid OccupationId { get; set; }
    public int Ord { get; set; }
    public Guid FileId { get; set; }
    public string? Caption { get; set; }
}

internal sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> entity)
    {
        entity.ToCatalogTable("skills");
        entity.HasOne<Skill>().WithMany().HasForeignKey(s => s.ParentId).OnDelete(DeleteBehavior.Restrict);
        entity.Property(s => s.BaseValue).HasDbDefault(0);
        entity.Property(s => s.IsUncommon).HasDbDefault(false);
        entity.PrimitiveCollection(s => s.Eras).HasDbDefaultSql(SchemaConventions.AllEras);
        entity.Property(s => s.Description).HasDbDefault("");
        entity.PrimitiveCollection(s => s.UsageExamples).HasDbDefaultSql(SchemaConventions.EmptyArray);
        entity.PrimitiveCollection(s => s.FailureConsequences).HasDbDefaultSql(SchemaConventions.EmptyArray);
        entity.PrimitiveCollection(s => s.OpposingSkills).HasDbDefaultSql(SchemaConventions.EmptyArray);
        entity.Property(s => s.CanRetry).HasDbDefault(false);
    }
}

internal sealed class OccupationConfiguration : IEntityTypeConfiguration<Occupation>
{
    public void Configure(EntityTypeBuilder<Occupation> entity)
    {
        entity.ToCatalogTable("occupations", searchByName: false);
        entity.ToTable(table => table.HasCheckConstraint("ck_occupations_credit_rating",
            "credit_rating_min BETWEEN 0 AND 99 AND credit_rating_max BETWEEN credit_rating_min AND 99"));
        entity.PrimitiveCollection(o => o.Eras).HasDbDefaultSql(SchemaConventions.AllEras);
        entity.Property(o => o.IsLovecraftian).HasDbDefault(false);
        entity.PrimitiveCollection(o => o.Tags).HasDbDefaultSql(SchemaConventions.EmptyArray);
    }
}

internal sealed class OccupationSlotConfiguration : IEntityTypeConfiguration<OccupationSlot>
{
    public void Configure(EntityTypeBuilder<OccupationSlot> entity)
    {
        entity.ToTable("occupation_slots", table =>
        {
            table.HasCheckConstraint("ck_occupation_slots_choose_count", "choose_count >= 1");
            table.HasCheckConstraint("ck_occupation_slots_kind_fields", """
                (kind IN ('Skill', 'AnySpecialization') AND skill_id IS NOT NULL AND specialization IS NULL)
                OR (kind = 'Specialization' AND skill_id IS NOT NULL AND specialization IS NOT NULL)
                OR (kind IN ('Choice', 'Social', 'Free') AND skill_id IS NULL AND specialization IS NULL)
                """);
        });
        entity.Property(s => s.ChooseCount).HasDbDefault(1);
        entity.HasIndex(s => new { s.OccupationId, s.Ord }).IsUnique();

        entity.HasOne<Occupation>().WithMany(o => o.Slots).HasForeignKey(s => s.OccupationId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Skill>().WithMany().HasForeignKey(s => s.SkillId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OccupationSlotOptionConfiguration : IEntityTypeConfiguration<OccupationSlotOption>
{
    public void Configure(EntityTypeBuilder<OccupationSlotOption> entity)
    {
        entity.ToTable("occupation_slot_options");
        entity.HasKey(o => new { o.SlotId, o.SkillId });
        entity.HasOne<OccupationSlot>().WithMany(s => s.Options).HasForeignKey(o => o.SlotId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Skill>().WithMany().HasForeignKey(o => o.SkillId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WeaponConfiguration : IEntityTypeConfiguration<Weapon>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Weapon> entity)
    {
        entity.ToCatalogTable("weapons");
        entity.ToTable(table => table.HasCheckConstraint("ck_weapons_malfunction", "malfunction BETWEEN 1 AND 100"));
        entity.HasOne<Skill>().WithMany().HasForeignKey(w => w.SkillId).OnDelete(DeleteBehavior.Restrict);

        entity.PrimitiveCollection(w => w.Eras).HasDbDefaultSql(SchemaConventions.AllEras);
        entity.Property(w => w.IsRare).HasDbDefault(false);
        entity.Property(w => w.IsImpaling).HasDbDefault(false);
        entity.Property(w => w.Range).HasDbDefault("");
        entity.Property(w => w.Attacks).HasDbDefault("");
        entity.Property(w => w.Ammo).HasDbDefault("");
        entity.Property(w => w.SingleUse).HasDbDefault(false);
        entity.Property(w => w.Cost).HasDbDefault("");
        entity.Property(w => w.Notes).HasDbDefault("");
        entity.Property(w => w.CostClassic).HasPrecision(12, 2);
        entity.Property(w => w.CostModern).HasPrecision(12, 2);

        entity.Property(w => w.DamageByRange)
            .HasColumnType("jsonb")
            .HasConversion(
                list => JsonSerializer.Serialize(list, Json),
                json => JsonSerializer.Deserialize<List<RangeDamage>>(json, Json),
                new ValueComparer<List<RangeDamage>?>(
                    (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
                    list => list == null ? 0 : list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                    list => list == null ? null : list.ToList()));
    }
}

internal sealed class SpellConfiguration : IEntityTypeConfiguration<Spell>
{
    public void Configure(EntityTypeBuilder<Spell> entity)
    {
        entity.ToCatalogTable("spells");
        entity.PrimitiveCollection(s => s.AltNames).HasDbDefaultSql(SchemaConventions.EmptyArray);
        entity.Property(s => s.Description).HasDbDefault("");
        entity.HasIndex(s => s.AltNames).HasMethod("gin").HasDatabaseName("spells_alt_names");
    }
}

internal sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> entity)
    {
        entity.ToCatalogTable("books");
        entity.PrimitiveCollection(b => b.AltNames).HasDbDefaultSql(SchemaConventions.EmptyArray);
        entity.Property(b => b.Description).HasDbDefault("");
        entity.HasOne<StoredFile>().WithMany().HasForeignKey(b => b.ImageFileId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class BookSpellConfiguration : IEntityTypeConfiguration<BookSpell>
{
    public void Configure(EntityTypeBuilder<BookSpell> entity)
    {
        entity.ToTable("book_spells");
        entity.HasKey(s => new { s.BookId, s.Ord });
        entity.HasOne<Book>().WithMany(b => b.Spells).HasForeignKey(s => s.BookId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Spell>().WithMany().HasForeignKey(s => s.SpellId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> entity)
    {
        entity.ToCatalogTable("items");
        entity.Property(i => i.Price).HasPrecision(12, 2);
        entity.PrimitiveCollection(i => i.Eras).HasDbDefaultSql(SchemaConventions.AllEras);
        entity.HasOne<StoredFile>().WithMany().HasForeignKey(i => i.ImageFileId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class CreatureConfiguration : IEntityTypeConfiguration<Creature>
{
    public void Configure(EntityTypeBuilder<Creature> entity)
    {
        entity.ToCatalogTable("creatures");
        entity.Property(c => c.Type).HasDbDefault(CreatureType.Other);
        entity.Property(c => c.Statblock).HasColumnType("jsonb");
    }
}

internal sealed class CreatureImageConfiguration : IEntityTypeConfiguration<CreatureImage>
{
    public void Configure(EntityTypeBuilder<CreatureImage> entity)
    {
        entity.ToTable("creature_images");
        entity.HasKey(i => new { i.CreatureId, i.Ord });
        entity.HasOne<Creature>().WithMany(c => c.Images).HasForeignKey(i => i.CreatureId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<StoredFile>().WithMany().HasForeignKey(i => i.FileId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WeaponImageConfiguration : IEntityTypeConfiguration<WeaponImage>
{
    public void Configure(EntityTypeBuilder<WeaponImage> entity)
    {
        entity.ToTable("weapon_images");
        entity.HasKey(i => new { i.WeaponId, i.Ord });
        entity.HasOne<Weapon>().WithMany(w => w.Images).HasForeignKey(i => i.WeaponId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<StoredFile>().WithMany().HasForeignKey(i => i.FileId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OccupationImageConfiguration : IEntityTypeConfiguration<OccupationImage>
{
    public void Configure(EntityTypeBuilder<OccupationImage> entity)
    {
        entity.ToTable("occupation_images");
        entity.HasKey(i => new { i.OccupationId, i.Ord });
        entity.HasOne<Occupation>().WithMany(o => o.Images).HasForeignKey(i => i.OccupationId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<StoredFile>().WithMany().HasForeignKey(i => i.FileId).OnDelete(DeleteBehavior.Restrict);
    }
}
