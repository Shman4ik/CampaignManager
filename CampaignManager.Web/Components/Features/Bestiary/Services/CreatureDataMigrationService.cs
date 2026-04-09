using System.Text.RegularExpressions;
using CampaignManager.Web.Components.Features.Bestiary.Model;
using CampaignManager.Web.Utilities.DataBase;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Web.Components.Features.Bestiary.Services;

/// <summary>
///     Сервис миграции данных существ: парсинг CombatDescriptions в структурированные Attacks
/// </summary>
public sealed partial class CreatureDataMigrationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<CreatureDataMigrationService> logger)
{
    // Паттерн: "Ближний бой 75%, урон 1d8. Описание эффектов..."
    // или: "Дальний бой 40%, урон 2d6+2. Описание..."
    [GeneratedRegex(
        @"^(Ближний бой|Дальний бой|Огнестрельное оружие|Стрельба)\s+(\d+)%\s*,\s*урон\s+([^.]+?)\.?\s*(.*)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CombatDescriptionPattern();

    // Fallback: только навык и процент без "урон"
    // "Кусание 60%, 1d8+яд"
    [GeneratedRegex(
        @"^(\D+?)\s+(\d+)%\s*,?\s*(.*)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FallbackPattern();

    // Паттерн для извлечения формулы урона из свободного текста
    [GeneratedRegex(@"\d+[dDкК]\d+(?:\s*[+\-]\s*\d+[dDкК]?\d*)*", RegexOptions.IgnoreCase)]
    private static partial Regex DamageFormulaPattern();

    public async Task<int> MigrateAsync()
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        #pragma warning disable CS0612 // Obsolete
        var creatures = await dbContext.Creatures
            .Where(c => c.Attacks.Count == 0)
            .ToListAsync();
        #pragma warning restore CS0612

        var migratedCount = 0;

        foreach (var creature in creatures)
        {
            if (creature.CombatDescriptions.Count == 0)
                continue;

            var attacks = new List<CreatureAttack>();

            foreach (var (name, description) in creature.CombatDescriptions)
            {
                var attack = ParseCombatDescription(name, description);
                attacks.Add(attack);
            }

            creature.Attacks = attacks;
            migratedCount++;

            logger.LogInformation("Мигрировано существо {Name}: {Count} атак", creature.Name, attacks.Count);
        }

        if (migratedCount > 0)
            await dbContext.SaveChangesAsync();

        logger.LogInformation("Миграция завершена: обработано {Count} существ", migratedCount);
        return migratedCount;
    }

    internal static CreatureAttack ParseCombatDescription(string attackName, string description)
    {
        var attack = new CreatureAttack { Name = attackName };

        // Попытка 1: полный паттерн "Ближний бой 75%, урон 1d8. Описание"
        var match = CombatDescriptionPattern().Match(description);
        if (match.Success)
        {
            var combatType = match.Groups[1].Value;
            attack.SkillValue = int.Parse(match.Groups[2].Value);
            attack.DamageFormula = NormalizeDamageFormula(match.Groups[3].Value.Trim());
            attack.IsMelee = !combatType.Contains("Дальний", StringComparison.OrdinalIgnoreCase)
                             && !combatType.Contains("Стрельба", StringComparison.OrdinalIgnoreCase)
                             && !combatType.Contains("Огнестрельное", StringComparison.OrdinalIgnoreCase);
            attack.Description = string.IsNullOrWhiteSpace(match.Groups[4].Value)
                ? null
                : match.Groups[4].Value.Trim();
            return attack;
        }

        // Попытка 2: fallback паттерн "Кусание 60%, 1d8+яд"
        var fallbackMatch = FallbackPattern().Match(description);
        if (fallbackMatch.Success && int.TryParse(fallbackMatch.Groups[2].Value, out var skillValue))
        {
            attack.SkillValue = skillValue;
            var rest = fallbackMatch.Groups[3].Value.Trim();

            // Извлекаем формулу урона из оставшегося текста
            var damageMatch = DamageFormulaPattern().Match(rest);
            if (damageMatch.Success)
            {
                attack.DamageFormula = NormalizeDamageFormula(damageMatch.Value);
                var remaining = rest.Replace(damageMatch.Value, "").Trim(' ', ',', '.');
                attack.Description = string.IsNullOrWhiteSpace(remaining) ? null : remaining;
            }
            else
            {
                attack.Description = string.IsNullOrWhiteSpace(rest) ? null : rest;
            }

            return attack;
        }

        // Попытка 3: только формула урона из текста
        var damageOnly = DamageFormulaPattern().Match(description);
        if (damageOnly.Success)
        {
            attack.DamageFormula = NormalizeDamageFormula(damageOnly.Value);
        }

        attack.Description = description;
        return attack;
    }

    private static string NormalizeDamageFormula(string formula)
    {
        // Нормализуем: "1d8" → "1D8", "2к6" → "2D6"
        return formula
            .Replace('d', 'D')
            .Replace('к', 'D')
            .Replace('К', 'D')
            .Trim();
    }
}
