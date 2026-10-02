using CampaignManager.Contracts.Catalogs;
using CampaignManager.Core.Catalogs;
using CampaignManager.Data;
using CampaignManager.Data.Catalogs;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Catalogs.Stores;

/// <summary>
/// Навыки. Специализация — запись с родителем (<c>parent_id</c>), одна ступень: «Стрельба (пистолет)» →
/// «Стрельба». База — число или формула (<c>DEX/2</c> у Уклонения, <c>EDU</c> у родного языка).
/// </summary>
public sealed class SkillStore : CatalogStore<Skill, SkillDto>
{
    private static readonly string[] BaseFormulas = ["DEX/2", "EDU"];

    public override CatalogRoute Route => CatalogsRoutes.Skills;

    public override CatalogCodeTable Codes => SkillCodes.Table;

    public override string Noun => "Навык";

    public override string UsedBy => "оружием, профессиями или специализациями";

    public override DbSet<Skill> Set(CmDbContext db) => db.Skills;

    public override Skill New() => new() { Name = "" };

    public override Task<IReadOnlyList<SkillDto>> ToDtosAsync(CmDbContext db, IReadOnlyList<Skill> rows, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SkillDto>>(rows.Select(s => new SkillDto
        {
            Id = s.Id,
            Version = s.Version,
            Code = s.Code,
            Name = s.Name,
            Source = s.Source,
            ParentId = s.ParentId,
            BaseValue = s.BaseValue,
            BaseFormula = s.BaseFormula,
            Category = s.Category,
            IsUncommon = s.IsUncommon,
            Eras = s.Eras,
            Description = s.Description,
            UsageExamples = s.UsageExamples,
            FailureConsequences = s.FailureConsequences,
            OpposingSkills = s.OpposingSkills,
            TimeRequired = s.TimeRequired,
            CanRetry = s.CanRetry,
        }).ToList());

    public override async Task ApplyAsync(CmDbContext db, SkillDto dto, Skill entity, CatalogWrite write, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(dto.Category))
        {
            throw ApiProblemException.Invalid("Неизвестная категория навыка.");
        }

        var baseValue = Range(dto.BaseValue, 0, 99, "Базовое значение") ?? 0;
        var formula = Text(dto.BaseFormula)?.ToUpperInvariant();
        if (formula is not null && !BaseFormulas.Contains(formula))
        {
            throw ApiProblemException.Invalid($"Формула базы — {string.Join(" или ", BaseFormulas)}; иначе — число.");
        }

        if (dto.ParentId is { } parentId)
        {
            if (parentId == entity.Id)
            {
                throw ApiProblemException.Invalid("Навык не может быть специализацией самого себя.");
            }

            var parent = await db.Skills.AsNoTracking().Where(s => s.Id == parentId)
                .Select(s => new { s.ParentId }).SingleOrDefaultAsync(cancellationToken)
                ?? throw ApiProblemException.Invalid("Родительский навык не найден.");
            if (parent.ParentId is not null)
            {
                throw ApiProblemException.Invalid("Родитель сам специализация: у специализаций одна ступень.");
            }

            if (await db.Skills.AnyAsync(s => s.ParentId == entity.Id, cancellationToken))
            {
                throw ApiProblemException.Invalid("У навыка есть свои специализации — он не может стать специализацией.");
            }
        }

        var eras = Eras(dto.Eras);

        entity.ParentId = dto.ParentId;
        entity.BaseValue = baseValue;
        entity.BaseFormula = formula;
        entity.Category = dto.Category;
        entity.IsUncommon = dto.IsUncommon;
        entity.Eras = eras;
        entity.Description = dto.Description?.Trim() ?? "";
        entity.UsageExamples = Strings(dto.UsageExamples);
        entity.FailureConsequences = Strings(dto.FailureConsequences);
        entity.OpposingSkills = Strings(dto.OpposingSkills);
        entity.TimeRequired = Text(dto.TimeRequired);
        entity.CanRetry = dto.CanRetry;
    }
}
