using CampaignManager.Contracts.Catalogs;
using CampaignManager.Core;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Data;
using CampaignManager.Data.Catalogs;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Catalogs.Stores;

/// <summary>
/// Профессии (стр. 37–39): формула очков, диапазон Средств, слоты навыков (<c>occupation_slots</c>) вместо
/// четырёх полей v1. Сид — <see cref="OccupationSeed"/>; синхронизация идёт по коду, а не по имени, как
/// в v1 (там переименование профессии в коде заводило двойника).
/// </summary>
public sealed class OccupationStore : CatalogStore<Occupation, OccupationDto>
{
    public override CatalogRoute Route => CatalogsRoutes.Occupations;

    public override CatalogCodeTable Codes => OccupationCodes.Table;

    public override string Noun => "Профессия";

    public override bool HasSeed => true;

    public override DbSet<Occupation> Set(CmDbContext db) => db.Occupations;

    public override IQueryable<Occupation> Query(CmDbContext db) =>
        db.Occupations.Include(o => o.Slots).ThenInclude(s => s.Options);

    public override Occupation New() => new() { Name = "" };

    public override async Task<IReadOnlyList<OccupationDto>> ToDtosAsync(CmDbContext db, IReadOnlyList<Occupation> rows, CancellationToken cancellationToken)
    {
        var catalog = await SkillCatalogAsync(db, cancellationToken);
        var names = catalog.Skills.ToDictionary(s => s.Id, s => s.Name);
        return rows.Select(o => new OccupationDto
        {
            Id = o.Id,
            Version = o.Version,
            Code = o.Code,
            Name = o.Name,
            Source = o.Source,
            SkillPointsFormula = o.SkillPointsFormula,
            CreditRatingMin = o.CreditRatingMin,
            CreditRatingMax = o.CreditRatingMax,
            Eras = o.Eras,
            IsLovecraftian = o.IsLovecraftian,
            Tags = o.Tags,
            Slots = o.Slots.OrderBy(s => s.Ord).Select(s => new OccupationSlotDto
            {
                Kind = s.Kind,
                SkillId = s.SkillId,
                SkillName = s.SkillId is { } id ? names.GetValueOrDefault(id) : null,
                Specialization = s.Specialization,
                ChooseCount = s.ChooseCount,
                Options = s.Options.Select(x => x.SkillId).ToList(),
                OptionNames = s.Options.Select(x => names.GetValueOrDefault(x.SkillId) ?? "").ToList(),
            }).ToList(),
            ProfessionalSkillCount = OccupationRules.ProfessionalSkillCount(Definition(o.Slots), catalog),
        }).ToList();
    }

    /// <summary>Слоты в виде, который понимают правила Core (одна копия счёта навыков профессии).</summary>
    private static OccupationDefinition Definition(IEnumerable<OccupationSlot> slots) =>
        new(Guid.Empty, "", SkillPointsFormula.Edu4)
        {
            Slots = [.. slots.OrderBy(s => s.Ord).Select(s => new OccupationSlotDefinition(s.Kind)
            {
                SkillId = s.SkillId,
                Specialization = s.Specialization,
                ChooseCount = s.ChooseCount,
                Options = [.. s.Options.Select(o => o.SkillId)],
            })],
        };

    public override async Task ApplyAsync(CmDbContext db, OccupationDto dto, Occupation entity, CatalogWrite write, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(dto.SkillPointsFormula))
        {
            throw ApiProblemException.Invalid("Неизвестная формула очков навыков.");
        }

        var min = Range(dto.CreditRatingMin, 0, 99, "Средства от")!.Value;
        var max = Range(dto.CreditRatingMax, 0, 99, "Средства до")!.Value;
        if (min > max)
        {
            throw ApiProblemException.Invalid("Средства: нижняя граница больше верхней.");
        }

        var eras = Eras(dto.Eras);
        var catalog = await SkillCatalogAsync(db, cancellationToken);
        var skills = catalog.Skills.ToDictionary(s => s.Id, s => s.Name);
        var byName = skills.ToLookup(s => s.Value.ToLowerInvariant(), s => s.Key);
        var slots = (dto.Slots ?? []).Select((slot, i) => Slot(slot, i + 1, skills, byName)).ToList();
        var count = OccupationRules.ProfessionalSkillCount(Definition(slots), catalog);
        if (count != OccupationRules.RequiredSkillCount)
        {
            write.Warnings.Add($"профессиональных навыков {count}, а по книге — ровно {OccupationRules.RequiredSkillCount} плюс Средства (стр. 37).");
        }

        entity.SkillPointsFormula = dto.SkillPointsFormula;
        entity.CreditRatingMin = min;
        entity.CreditRatingMax = max;
        entity.Eras = eras;
        entity.IsLovecraftian = dto.IsLovecraftian;
        entity.Tags = Strings(dto.Tags);

        // У слота свой id: старые уходят целиком (варианты — каскадом), новые встают на их место.
        foreach (var old in entity.Slots.ToList())
        {
            entity.Slots.Remove(old);
            db.Remove(old);
        }

        // Явный Add: ключ слота задан заранее (uuid v7), и найденный через навигацию у уже
        // отслеживаемой профессии EF счёл бы существующим — UPDATE вместо INSERT, и варианты упали бы на FK.
        foreach (var slot in slots)
        {
            slot.OccupationId = entity.Id;
            entity.Slots.Add(slot);
            db.Add(slot);
        }
    }

    /// <summary>Строки DTO → слот с проверкой того же правила, что CHECK в базе.</summary>
    private static OccupationSlot Slot(OccupationSlotDto dto, int ord, Dictionary<Guid, string> skills, ILookup<string, Guid> byName)
    {
        if (!Enum.IsDefined(dto.Kind))
        {
            throw ApiProblemException.Invalid($"Слот {ord}: неизвестный вид.");
        }

        Guid Skill(Guid? id, string? name, string what)
        {
            if (id is { } known && skills.ContainsKey(known))
            {
                return known;
            }

            if (name is { Length: > 0 } && byName[name.Trim().ToLowerInvariant()].FirstOrDefault() is var found && found != Guid.Empty)
            {
                return found;
            }

            throw ApiProblemException.Invalid($"Слот {ord}: {what} не найден в справочнике навыков.");
        }

        var slot = new OccupationSlot { Ord = ord, Kind = dto.Kind };
        switch (dto.Kind)
        {
            case OccupationSlotKind.Skill:
            case OccupationSlotKind.AnySpecialization:
                slot.SkillId = Skill(dto.SkillId, dto.SkillName, "навык");
                break;
            case OccupationSlotKind.Specialization:
                slot.SkillId = Skill(dto.SkillId, dto.SkillName, "родительский навык");
                slot.Specialization = Text(dto.Specialization)
                    ?? throw ApiProblemException.Invalid($"Слот {ord}: впишите специализацию («латынь»).");
                break;
            case OccupationSlotKind.Choice:
                var options = dto.Options.Select((id, i) => Skill(id, dto.OptionNames.ElementAtOrDefault(i), "вариант"))
                    .Concat(dto.Options.Count == 0 ? dto.OptionNames.Select(n => Skill(null, n, $"вариант «{n}»")) : [])
                    .Distinct().ToList();
                if (options.Count < 2)
                {
                    throw ApiProblemException.Invalid($"Слот {ord}: выбору нужно хотя бы два варианта.");
                }

                if (dto.ChooseCount < 1 || dto.ChooseCount > options.Count)
                {
                    throw ApiProblemException.Invalid($"Слот {ord}: выбрать можно от 1 до {options.Count}.");
                }

                slot.ChooseCount = dto.ChooseCount;
                slot.Options = options.Select(id => new OccupationSlotOption { SlotId = slot.Id, SkillId = id }).ToList();
                break;
        }

        return slot;
    }

    public override async Task<IReadOnlyList<OccupationDto>?> SeedAsync(CmDbContext db, CancellationToken cancellationToken)
    {
        // Навыки сида — по коду справочника: id зависит от базы, код — нет.
        var byCode = await db.Skills.AsNoTracking().Where(s => s.Code != null)
            .ToDictionaryAsync(s => s.Code!, s => (s.Id, s.Name), cancellationToken);

        OccupationSlotDto Dto(OccupationSeedSlot slot) => slot.Kind switch
        {
            OccupationSlotKind.Choice => new OccupationSlotDto
            {
                Kind = slot.Kind,
                ChooseCount = slot.ChooseCount,
                Options = [.. slot.Options.Select(o => o.Code is not null && byCode.TryGetValue(o.Code, out var s) ? s.Id : Guid.Empty)],
                OptionNames = [.. slot.Options.Select(o => o.Name)],
            },
            OccupationSlotKind.Social or OccupationSlotKind.Free => new OccupationSlotDto { Kind = slot.Kind },
            _ => new OccupationSlotDto
            {
                Kind = slot.Kind,
                SkillId = slot.SkillCode is not null && byCode.TryGetValue(slot.SkillCode, out var found) ? found.Id : Guid.Empty,
                SkillName = slot.SkillCode is not null && byCode.TryGetValue(slot.SkillCode, out var named) ? named.Name : slot.SkillName,
                Specialization = slot.Specialization,
            },
        };

        return OccupationSeed.Rows.Select(row => new OccupationDto
        {
            Code = row.Code,
            Name = row.Name,
            SkillPointsFormula = row.Formula,
            CreditRatingMin = row.CreditRatingMin,
            CreditRatingMax = row.CreditRatingMax,
            Eras = [.. row.Eras.Where(Enum.IsDefined)],
            IsLovecraftian = row.IsLovecraftian,
            Tags = OccupationTags.Translate(row.Tags),
            Slots = [.. OccupationSeed.SlotsOf(row).Select(Dto)],
        }).ToList();
    }

    private static async Task<SkillCatalog> SkillCatalogAsync(CmDbContext db, CancellationToken cancellationToken) =>
        new(await db.Skills.AsNoTracking()
            .Select(s => new SkillDefinition(s.Id, s.Name) { Code = s.Code, ParentId = s.ParentId, BaseValue = s.BaseValue, BaseFormula = s.BaseFormula, Category = s.Category })
            .ToListAsync(cancellationToken));
}
