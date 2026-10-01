using CampaignManager.Data.Identity;
using CampaignManager.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignManager.Data.Catalogs;

/// <summary>
/// Общее у справочников (навыки, профессии, оружие, заклинания, книги, предметы, твари).
/// Наследование только в C#: каждая запись — своя таблица.
/// </summary>
public abstract class CatalogEntry : ICreatedAt, IUpdatedAt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Стабильный ключ книжной записи (синхронизация с правилами); null у самодельных.</summary>
    public string? Code { get; set; }

    /// <summary>Уникально без учёта регистра — индекс <c>lower(name)</c>.</summary>
    public required string Name { get; set; }

    /// <summary>Страница книги; null — самодельное.</summary>
    public string? Source { get; set; }

    public Guid? CreatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Системная <c>xmin</c>: ETag в API, конфликт правки — 409 (SCHEMA, правило 8).</summary>
    public uint Version { get; set; }
}

internal static class CatalogEntryConfiguration
{
    /// <summary>
    /// Таблица справочника: уникальный <c>code</c>, автор, <c>xmin</c> и триграммы по имени для поиска
    /// <c>ILIKE '%…%'</c>. Уникальность <c>lower(name)</c> EF выразить не может — она в миграции.
    /// </summary>
    public static EntityTypeBuilder<T> ToCatalogTable<T>(this EntityTypeBuilder<T> entity, string table, bool searchByName = true)
        where T : CatalogEntry
    {
        entity.ToTable(table);

        // Колонки базового класса EF ставит после своих; ключ и имя удобнее видеть первыми.
        entity.Property(e => e.Id).HasColumnOrder(0);
        entity.Property(e => e.Code).HasColumnOrder(1);
        entity.Property(e => e.Name).HasColumnOrder(2);

        entity.HasIndex(e => e.Code).IsUnique();
        entity.Property(e => e.Version).IsRowVersion();
        entity.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedById).OnDelete(DeleteBehavior.SetNull);
        if (!searchByName)
        {
            return entity;
        }

        entity.HasIndex(e => e.Name)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName($"{table}_name_trgm");
        return entity;
    }
}
