using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Utilities.DataBase;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Components.Features.Characters.Services;

/// <summary>
/// Сидирует таблицу LlmKnowledgeEntries данными из ресурсных файлов CoC Rules.
/// Запускается один раз при старте приложения через DbInitializer.
/// </summary>
public sealed class LlmKnowledgeSeeder(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IWebHostEnvironment env,
    ILogger<LlmKnowledgeSeeder> logger)
{
    private static readonly (string Key, string Title, string FileName, int SortOrder)[] Entries =
    [
        ("character-creation", "CoC 7e — Создание персонажей", "character-creation.md", 1),
        ("skills-reference",   "CoC 7e — Справочник навыков",  "skills-reference.md",   2),
    ];

    public async Task SeedAsync()
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();

            var rulesDir = Path.Combine(env.ContentRootPath, "Resources", "CoCRules");

            foreach (var (key, title, fileName, sortOrder) in Entries)
            {
                var filePath = Path.Combine(rulesDir, fileName);
                if (!File.Exists(filePath))
                {
                    logger.LogWarning("LLM knowledge file not found, skipping: {FilePath}", filePath);
                    continue;
                }

                var existing = await db.LlmKnowledgeEntries
                    .FirstOrDefaultAsync(e => e.Key == key);

                var content = await File.ReadAllTextAsync(filePath);

                if (existing is null)
                {
                    var entry = new LlmKnowledgeEntry
                    {
                        Key = key,
                        Title = title,
                        Content = content,
                        SortOrder = sortOrder,
                        IsActive = true,
                    };
                    entry.Init();

                    db.LlmKnowledgeEntries.Add(entry);
                    logger.LogInformation("LLM knowledge entry inserted: {Key}", key);
                }
                else
                {
                    logger.LogDebug("LLM knowledge entry already exists, skipping: {Key}", key);
                }
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при сидировании LLM knowledge entries");
        }
    }
}
