using System.Text.Json;
using CampaignManager.Core.Identity;
using CampaignManager.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CampaignManager.Data.Identity;

/// <summary>
/// Человек. Везде, где v1 хранил почту или имя строкой, в 2.0 — <c>*_id → users</c> (SCHEMA, правило 3).
/// <see cref="Auth0Sub"/> пуст у перенесённых до первого входа: вход ищет по нему, затем по почте.
/// </summary>
public sealed class User : ICreatedAt, IUpdatedAt
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string? Auth0Sub { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public UserRole Role { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// Когда человек появился в системе — то, что администратор читает как «Регистрация». У заведённого при входе — момент
    /// заведения; у перенесённого из v1 — дата его первой записи там (кампания, место игрока, заявка, сценарий), а если
    /// записей нет — пусто: <see cref="CreatedAt"/> у таких — день переноса, а не регистрации.
    /// </summary>
    public DateTimeOffset? RegisteredAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Настройка пользователя — строка на ключ: два устройства не затирают ключи друг друга.</summary>
public sealed class UserPreference : IUpdatedAt
{
    public Guid UserId { get; set; }
    public required string Key { get; set; }
    public required JsonDocument Value { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class KeeperApplication : ICreatedAt, IUpdatedAt
{
    public const int MaxMessageLength = 1000;

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public required string Message { get; set; }
    public KeeperApplicationStatus Status { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("users");
        // Конвенция делает из Auth0Sub «auth0sub»: цифра не считается границей слова.
        entity.Property(u => u.Auth0Sub).HasColumnName("auth0_sub");
        entity.Property(u => u.Email).HasColumnType("citext");
        entity.HasIndex(u => u.Email).IsUnique();
        entity.HasIndex(u => u.Auth0Sub).IsUnique();
        entity.Property(u => u.Role).HasDbDefault(UserRole.Player);
    }
}

internal sealed class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> entity)
    {
        entity.ToTable("user_preferences");
        entity.HasKey(p => new { p.UserId, p.Key });
        entity.Property(p => p.Value).HasColumnType("jsonb");
        entity.HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class KeeperApplicationConfiguration : IEntityTypeConfiguration<KeeperApplication>
{
    public void Configure(EntityTypeBuilder<KeeperApplication> entity)
    {
        entity.ToTable("keeper_applications", table => table.HasCheckConstraint(
            "ck_keeper_applications_message_length", $"length(message) <= {KeeperApplication.MaxMessageLength}"));
        entity.Property(a => a.Status).HasDbDefault(KeeperApplicationStatus.Pending);

        entity.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<User>().WithMany().HasForeignKey(a => a.ReviewedById).OnDelete(DeleteBehavior.SetNull);

        // Одна заявка на рассмотрении у пользователя; отклонённых может быть сколько угодно.
        entity.HasIndex(a => a.UserId)
            .IsUnique()
            .HasFilter("status = 'Pending'")
            .HasDatabaseName("keeper_applications_one_pending");
    }
}
