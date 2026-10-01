using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Data;

/// <summary>
/// Схема <c>cm</c> (docs/v2/SCHEMA.md). Живёт в одной базе со схемами v1 <c>games</c> и
/// <c>identity</c>, но журнал миграций у неё свой — см. <see cref="CmDatabase"/>.
/// Сущности и конфигурации добавляет T1.2.
/// </summary>
public sealed class CmDbContext(DbContextOptions<CmDbContext> options) : DbContext(options)
{
    public const string Schema = "cm";

    /// <summary>Время Postgres: им <c>/api/v1/ping</c> проверяет, что база отвечает.</summary>
    public Task<DateTimeOffset> GetDatabaseTimeAsync(CancellationToken cancellationToken) =>
        Database.SqlQuery<DateTimeOffset>($"SELECT now() AS \"Value\"").SingleAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.HasDefaultSchema(Schema);
}
