using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CampaignManager.Web.Components.Features.Characters.Services;

public sealed class OccupationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IMemoryCache cache,
    ILogger<OccupationService> logger)
{
    private const string OccupationsCacheKey = "AllOccupations";

    /// <summary>
    ///     Профессии, которые в книге переименованы относительно того, как они лежат в базе.
    ///     Нужны синхронизации: без этого апсерт по имени завёл бы вторую строку и оставил старую.
    /// </summary>
    private static readonly Dictionary<string, string> RenamedByRulebook = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Детектив"] = "Детектив полиции"
    };

    public Task<List<Occupation>> GetAllOccupationsAsync() =>
        CrudServiceHelper.GetAllCachedAsync<Occupation>(dbContextFactory, cache, OccupationsCacheKey, logger);

    public Task<Occupation?> GetByIdAsync(Guid id) =>
        CrudServiceHelper.GetByIdAsync<Occupation>(dbContextFactory, id, logger);

    public Task<Occupation?> CreateAsync(Occupation occupation) =>
        CrudServiceHelper.CreateAsync(dbContextFactory, cache, OccupationsCacheKey, occupation, logger);

    public Task<bool> UpdateAsync(Occupation occupation) =>
        CrudServiceHelper.UpdateAsync(dbContextFactory, cache, OccupationsCacheKey, occupation, logger);

    public Task<bool> DeleteAsync(Guid id) =>
        CrudServiceHelper.DeleteAsync<Occupation>(dbContextFactory, cache, OccupationsCacheKey, id, logger);

    /// <summary>
    ///     Что сделала синхронизация справочника с правилами. <c>Untouched</c> — профессии,
    ///     которых нет в эталонном списке: их синхронизация не трогает.
    /// </summary>
    public sealed record SyncResult(int Added, int Updated, int Unchanged, int Untouched, bool Failed = false);

    /// <summary>
    ///     Приводит справочник к списку «Примеры занятий» из книги правил (стр. 37–39):
    ///     апсерт по имени из <see cref="Occupation.GetDefaultOccupations" />.
    ///     Ничего не удаляет — профессии, заведённые Хранителем сверх списка, остаются как есть.
    ///     Миграции нигде не применяются автоматически, и postgres-доступ у нас только на чтение,
    ///     поэтому данные в живую базу приезжают именно так.
    /// </summary>
    public async Task<SyncResult> SyncWithRulebookAsync()
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var existing = await dbContext.Occupations.ToListAsync();

            var byName = new Dictionary<string, Occupation>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in existing)
                byName[row.Name] = row;

            var book = Occupation.GetDefaultOccupations();
            var bookNames = book.Select(o => o.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int added = 0, updated = 0, unchanged = 0;

            var oldNames = RenamedByRulebook
                .ToDictionary(r => r.Value, r => r.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var source in book)
            {
                if (!byName.TryGetValue(source.Name, out var target) &&
                    oldNames.TryGetValue(source.Name, out var oldName))
                    byName.TryGetValue(oldName, out target);

                if (target is null)
                {
                    source.Init();
                    dbContext.Occupations.Add(source);
                    added++;
                    continue;
                }

                if (IsSame(target, source))
                {
                    unchanged++;
                    continue;
                }

                Apply(source, target);
                target.LastUpdated = DateTimeOffset.UtcNow;
                updated++;
            }

            var untouched = existing.Count(o =>
                !bookNames.Contains(o.Name) && !RenamedByRulebook.ContainsKey(o.Name));

            if (added > 0 || updated > 0)
                await dbContext.SaveChangesAsync();

            cache.Remove(OccupationsCacheKey);

            logger.LogInformation(
                "Справочник профессий синхронизирован с книгой: добавлено {Added}, обновлено {Updated}, без изменений {Unchanged}, своих не тронуто {Untouched}",
                added, updated, unchanged, untouched);

            return new SyncResult(added, updated, unchanged, untouched);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось синхронизировать справочник профессий с книгой");
            return new SyncResult(0, 0, 0, 0, true);
        }
    }

    private static void Apply(Occupation source, Occupation target)
    {
        target.Name = source.Name;
        target.SkillPointFormula = source.SkillPointFormula;
        target.CreditRatingMin = source.CreditRatingMin;
        target.CreditRatingMax = source.CreditRatingMax;
        target.OccupationSkills = [.. source.OccupationSkills];
        target.SkillChoices = source.SkillChoices
            .Select(c => new OccupationSkillChoice { Count = c.Count, Options = [.. c.Options] })
            .ToList();
        target.FreeSkillSlots = source.FreeSkillSlots;
        target.SocialSkillSlots = source.SocialSkillSlots;
        target.IsModern = source.IsModern;
        target.IsLovecraftian = source.IsLovecraftian;
        target.Tags = source.Tags;
    }

    private static bool IsSame(Occupation target, Occupation source) =>
        string.Equals(target.Name, source.Name, StringComparison.Ordinal) &&
        target.SkillPointFormula == source.SkillPointFormula &&
        target.CreditRatingMin == source.CreditRatingMin &&
        target.CreditRatingMax == source.CreditRatingMax &&
        target.FreeSkillSlots == source.FreeSkillSlots &&
        target.SocialSkillSlots == source.SocialSkillSlots &&
        target.IsModern == source.IsModern &&
        target.IsLovecraftian == source.IsLovecraftian &&
        target.Tags == source.Tags &&
        target.OccupationSkills.SequenceEqual(source.OccupationSkills, StringComparer.Ordinal) &&
        target.SkillChoices.Count == source.SkillChoices.Count &&
        target.SkillChoices.Zip(source.SkillChoices).All(pair =>
            pair.First.Count == pair.Second.Count &&
            pair.First.Options.SequenceEqual(pair.Second.Options, StringComparer.Ordinal));
}
