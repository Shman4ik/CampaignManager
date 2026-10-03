using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Files;
using CampaignManager.Core.Campaigns;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Documents;
using CampaignManager.Data;
using CampaignManager.Data.Characters;
using CampaignManager.Server.Access;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Characters;

/// <summary>
/// Строки <see cref="CharacterSummaryDto"/> — одни для библиотеки <c>/npcs</c> и для состава и прегенов сценария (T2.5a):
/// карточка у них одна (<c>CharacterSummaryCard</c>). Видимые строки отбирает правило листа
/// (<see cref="AccessPolicy.ForCharacter"/>) по уже прочитанной строке, без запроса на каждую.
/// </summary>
public static class CharacterSummaries
{
    /// <summary>Видимые пользователю листы из <paramref name="query"/>, по имени.</summary>
    public static async Task<IReadOnlyList<CharacterSummaryDto>> ReadAsync(CmDbContext dbContext, IQueryable<Character> query,
        SignedInUser user, CancellationToken cancellationToken)
    {
        var rows = await query.AsNoTracking()
            .Select(c => new
            {
                c.Id,
                c.Kind,
                c.Status,
                c.Version,
                c.OwnerId,
                c.CampaignId,
                c.ScenarioId,
                c.PortraitFileId,
                c.Sheet,
                c.SheetVersion,
                CampaignName = dbContext.Campaigns.Where(x => x.Id == c.CampaignId).Select(x => x.Name).FirstOrDefault(),
                KeepsCampaign = c.CampaignId != null && dbContext.CampaignMembers.Any(m =>
                    m.CampaignId == c.CampaignId && m.UserId == user.Id && m.Role == CampaignRole.Keeper),
                ScenarioName = dbContext.Scenarios.Where(x => x.Id == c.ScenarioId).Select(x => x.Name).FirstOrDefault(),
                Casts = dbContext.ScenarioNpcs.Where(n => n.CharacterId == c.Id)
                    .Join(dbContext.Scenarios, n => n.ScenarioId, s => s.Id, (n, s) => new { s.Name, n.Role, n.Count }).ToList(),
            })
            .ToListAsync(cancellationToken);

        // Боевые навыки справочника — для строки карточки «ПЗ 11 · Ближний бой 40%». Уклонение не в счёт: это защита, не нападение.
        var combat = await dbContext.Skills.AsNoTracking()
            .Where(k => (k.Category == SkillCategory.CombatGeneral || k.Category == SkillCategory.CombatFirearms)
                        && k.Code != SkillCodes.Dodge)
            .ToDictionaryAsync(k => k.Id, k => k.Name, cancellationToken);

        return
        [
            .. rows
                .Select(r => (Row: r, Rights: AccessPolicy.ForCharacter(user, r.Kind, r.OwnerId, r.CampaignId, r.KeepsCampaign)))
                .Where(x => x.Rights.CanRead)
                .Select(x =>
                {
                    var r = x.Row;
                    var sheet = CmJson.ReadSheet(r.Sheet, r.SheetVersion);
                    var backstory = sheet.Biography.Backstory.Trim();
                    var best = sheet.Skills.Where(k => k.SkillId is { } id && combat.ContainsKey(id)).MaxBy(k => k.Value);
                    return new CharacterSummaryDto
                    {
                        Id = r.Id,
                        Kind = r.Kind,
                        Status = r.Status,
                        Version = r.Version,
                        Name = sheet.Personal.Name,
                        Occupation = Blank(sheet.Personal.Occupation),
                        Age = sheet.Personal.Age,
                        Gender = Blank(sheet.Personal.Gender),
                        Residence = Blank(sheet.Personal.Residence),
                        Backstory = backstory.Length > CharacterLimits.SummaryBackstoryLength
                            ? backstory[..CharacterLimits.SummaryBackstoryLength].TrimEnd() + "…"
                            : Blank(backstory),
                        PortraitUrl = r.PortraitFileId is { } file ? FilesRoutes.Content(file) : null,
                        CampaignId = r.CampaignId,
                        CampaignName = r.CampaignName,
                        ScenarioId = r.ScenarioId,
                        ScenarioName = r.ScenarioName,
                        CastIn = [.. r.Casts.Select(c => c.Name).Order(StringComparer.CurrentCulture)],
                        Casts = [.. r.Casts.OrderBy(c => c.Name, StringComparer.CurrentCulture).Select(c => new CharacterCastDto(c.Name, c.Role, c.Count))],
                        HitPoints = sheet.Current.HitPoints,
                        CombatSkill = best is null ? null : combat[best.SkillId!.Value],
                        CombatValue = best?.Value ?? 0,
                        CanEdit = x.Rights.CanEdit,
                    };
                })
                .OrderBy(s => s.Name, StringComparer.CurrentCulture),
        ];
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
