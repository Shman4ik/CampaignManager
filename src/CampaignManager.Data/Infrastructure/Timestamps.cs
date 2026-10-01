using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CampaignManager.Data.Infrastructure;

/// <summary>Строка с <c>created_at</c>: время ставит <see cref="TimestampsInterceptor"/>.</summary>
public interface ICreatedAt
{
    DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Строка с <c>updated_at</c>: время ставит <see cref="TimestampsInterceptor"/>.</summary>
public interface IUpdatedAt
{
    DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// <c>created_at</c>/<c>updated_at</c> (SCHEMA, правило 7). В v1 их ставил <c>.Init()</c> руками, и
/// забытый вызов оставлял нули. Явно заданный <c>created_at</c> не перетирается — перенос из v1
/// сохраняет исходное время.
/// </summary>
public sealed class TimestampsInterceptor(TimeProvider time) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // Перехватчик зовётся раньше, чем SaveChanges сам ищет изменения: без этого правленая
        // строка здесь ещё Unchanged и updated_at не сдвинулся бы.
        context.ChangeTracker.DetectChanges();

        var now = time.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Added)
            {
                if (entry.Entity is ICreatedAt created && created.CreatedAt == default)
                {
                    created.CreatedAt = now;
                }

                if (entry.Entity is IUpdatedAt added && added.UpdatedAt == default)
                {
                    added.UpdatedAt = now;
                }
            }
            else if (entry.State is EntityState.Modified && entry.Entity is IUpdatedAt updated)
            {
                updated.UpdatedAt = now;
            }
        }
    }
}
