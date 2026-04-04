using CampaignManager.Web.Utilities.DataBase;
using CampaignManager.Web.Utilities.Services;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Components.Features.Weapons.Services;

/// <summary>
/// Фоновый сервис, выполняющий одноразовую миграцию данных оружия:
/// заполняет поля IsImpaling и DamageInfo для всех записей, у которых DamageInfo IS NULL.
/// Запускается один раз при старте приложения и завершает работу.
/// </summary>
public sealed class WeaponDataMigrationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<WeaponDataMigrationService> logger) : IHostedService
{
    // Ключевые слова в именах проникающего оружия ближнего боя.
    // Огнестрельное оружие всегда проникающее (определяется по Type != Melee).
    private static readonly string[] ImpalingKeywords =
    [
        "нож", "кинжал", "меч", "рапир", "шпаг", "сабл", "копь", "пик",
        "штык", "клинок", "бритв", "игл", "шило", "стилет",
        "knife", "sword", "spear", "rapier", "dagger", "blade", "stiletto",
        "удавк", "garrot"
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await MigrateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при автоматической миграции данных оружия");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Загружаем только оружие, у которого ещё не заполнен DamageInfo
        var weapons = await db.Weapons
            .Where(w => w.DamageInfo == null)
            .ToListAsync(cancellationToken);

        if (weapons.Count == 0)
        {
            logger.LogInformation("Миграция данных оружия: нет записей для обработки");
            return;
        }

        logger.LogInformation("Миграция данных оружия: обрабатываем {Count} записей", weapons.Count);

        var parsed = 0;
        var unparsed = 0;

        foreach (var weapon in weapons)
        {
            // Заполнить DamageInfo
            weapon.DamageInfo = DamageFormulaParser.Parse(weapon.Damage);

            // Заполнить IsImpaling
            weapon.IsImpaling = DetermineIsImpaling(weapon);

            if (weapon.DamageInfo.IsParsed)
                parsed++;
            else
                unparsed++;
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Миграция данных оружия завершена: {Parsed} успешно, {Unparsed} требуют ручного ввода",
            parsed, unparsed);

        if (unparsed > 0)
        {
            var unparsedNames = weapons
                .Where(w => w.DamageInfo?.IsParsed == false)
                .Select(w => $"'{w.Name}' ({w.Damage})")
                .ToList();
            logger.LogWarning(
                "Оружие с нераспознанным уроном ({Count}): {List}",
                unparsed,
                string.Join(", ", unparsedNames));
        }
    }

    private static bool DetermineIsImpaling(Model.Weapon weapon)
    {
        // Всё огнестрельное и дальнобойное — проникающее
        if (weapon.Type != Model.WeaponType.Melee)
            return true;

        // Проверяем имя на ключевые слова проникающего оружия
        var name = weapon.Name.ToLowerInvariant();
        foreach (var keyword in ImpalingKeywords)
            if (name.Contains(keyword))
                return true;

        // Явная пометка "(пронз.)" в примечаниях
        var notes = weapon.Notes?.ToLowerInvariant() ?? string.Empty;
        if (notes.Contains("пронз")) return true;

        return false;
    }
}
