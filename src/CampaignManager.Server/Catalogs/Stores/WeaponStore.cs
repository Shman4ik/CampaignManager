using CampaignManager.Contracts.Catalogs;
using CampaignManager.Core.Catalogs;
using CampaignManager.Data;
using CampaignManager.Data.Catalogs;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Catalogs.Stores;

/// <summary>
/// Оружие (таблица XVII, стр. 401–405). Строки книги — как ввёл человек; числа для боя разбираются
/// здесь, один раз при записи (<see cref="WeaponStatsParser"/>, <see cref="DamageFormulaParser"/>), а не
/// на каждом чтении, как в v1. Неразобранная строка — не ошибка (самодельное оружие, «Шок»), а
/// предупреждение.
/// </summary>
public sealed class WeaponStore : CatalogStore<Weapon, WeaponDto>
{
    public override CatalogRoute Route => CatalogsRoutes.Weapons;

    public override CatalogCodeTable Codes => WeaponCodes.Table;

    public override string Noun => "Оружие";

    public override DbSet<Weapon> Set(CmDbContext db) => db.Weapons;

    public override bool HasImages => true;

    public override Guid? CoverOf(Weapon entity) => CatalogImages.Cover(entity.Images);

    public override void SetCover(CmDbContext db, Weapon entity, Guid fileId) =>
        CatalogImages.SetCover(db, entity.Images, fileId, ord => new WeaponImage { WeaponId = entity.Id, Ord = ord });

    public override IQueryable<Weapon> Query(CmDbContext db) => db.Weapons.Include(w => w.Images);

    // Лист хранит ссылку на оружие внутри документа (catalogWeaponId), а не внешним ключом: удалить можно, в листе
    // остаётся название и строка. Хранитель вправе знать, в скольких листах оно было (W10). jsonb печатает «"ключ": "значение"».
    public override bool UsersBlockDelete => false;

    public override string? UsersNote => "В листах останется название.";

    public override async Task<IReadOnlyList<string>> UsersOfAsync(CmDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var pattern = $"%\"catalogWeaponId\": \"{id}\"%";
        var names = await db.Database
            .SqlQuery<string>($"select coalesce(name, '') as \"Value\" from cm.characters where sheet::text like {pattern} order by 1")
            .ToListAsync(cancellationToken);
        return [.. names.Select(n => $"лист сыщика {(n.Length == 0 ? "без имени" : n)}")];
    }

    public override Weapon New() => new() { Name = "", Damage = "" };

    public override async Task<IReadOnlyList<WeaponDto>> ToDtosAsync(CmDbContext db, IReadOnlyList<Weapon> rows, CancellationToken cancellationToken)
    {
        var skillIds = rows.Select(w => w.SkillId).Distinct().ToList();
        var skills = await db.Skills.AsNoTracking().Where(s => skillIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
        return rows.Select(w => new WeaponDto
        {
            Id = w.Id,
            Version = w.Version,
            Code = w.Code,
            Name = w.Name,
            Source = w.Source,
            Type = w.Type,
            SkillId = w.SkillId,
            SkillName = skills.GetValueOrDefault(w.SkillId),
            Eras = w.Eras,
            IsRare = w.IsRare,
            IsImpaling = w.IsImpaling,
            Damage = w.Damage,
            Range = w.Range,
            Attacks = w.Attacks,
            Ammo = w.Ammo,
            Malfunction = w.Malfunction,
            Cost = w.Cost,
            Notes = w.Notes,
            SingleUse = w.SingleUse,
            Images = CatalogImages.ToDtos(w.Images),
            BaseRangeM = w.BaseRangeM,
            ShotsPerRound = w.ShotsPerRound,
            MaxShotsPerRound = w.MaxShotsPerRound,
            AmmoCapacity = w.AmmoCapacity,
            AmmoCapacityOptions = w.AmmoCapacityOptions?.ToList(),
            CostClassic = w.CostClassic,
            CostModern = w.CostModern,
            DamageByRange = w.DamageByRange?.Select(r => new RangeDamageDto(r.Range, r.Damage)).ToList(),
        }).ToList();
    }

    public override async Task ApplyAsync(CmDbContext db, WeaponDto dto, Weapon entity, CatalogWrite write, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(dto.Type))
        {
            throw ApiProblemException.Invalid("Неизвестный тип оружия.");
        }

        var damage = dto.Damage?.Trim() ?? "";
        if (damage.Length == 0)
        {
            throw ApiProblemException.Invalid("Нужен урон — как в книге: «1d6 + БкУ».");
        }

        var skillId = await ResolveSkillAsync(db, dto, cancellationToken);
        var malfunction = Range(dto.Malfunction, 1, 100, "Осечка");
        var eras = Eras(dto.Eras);
        var images = await CatalogImages.CheckAsync(db, dto.Images, write, cancellationToken);

        // Разбор — здесь и только здесь: бой читает готовые числа.
        var parsedDamage = DamageFormulaParser.Parse(damage);
        var range = WeaponStatsParser.ParseRange(dto.Range);
        var attacks = WeaponStatsParser.ParseAttacks(dto.Attacks);
        var ammo = WeaponStatsParser.ParseAmmo(dto.Ammo);
        var cost = WeaponStatsParser.ParseCost(dto.Cost);
        Warn(write, parsedDamage.IsParsed, "урон", damage);
        Warn(write, range.IsParsed, "дальность", dto.Range);
        Warn(write, attacks.IsParsed, "атаки", dto.Attacks);
        Warn(write, ammo.IsParsed, "боезапас", dto.Ammo);
        Warn(write, cost.IsParsed, "стоимость", dto.Cost);

        entity.Type = dto.Type;
        entity.SkillId = skillId;
        entity.Eras = eras;
        entity.IsRare = dto.IsRare;
        entity.IsImpaling = dto.IsImpaling;
        entity.Damage = damage;
        entity.DamageByRange = parsedDamage.RangeDamages is { Count: > 0 } byRange
            ? byRange.Select(r => new RangeDamage(r.RangeLabel, r.Damage.RawText)).ToList()
            : null;
        entity.Range = dto.Range?.Trim() ?? "";
        entity.BaseRangeM = range.BaseMeters;
        entity.Attacks = dto.Attacks?.Trim() ?? "";
        entity.ShotsPerRound = attacks.ShotsPerRound;
        entity.MaxShotsPerRound = attacks.MaxShotsPerRound;
        entity.Ammo = dto.Ammo?.Trim() ?? "";
        entity.AmmoCapacity = ammo.Capacity;
        entity.AmmoCapacityOptions = ammo.CapacityOptions?.ToArray();
        entity.SingleUse = dto.SingleUse || attacks.IsSingleUse || ammo.IsSingleUse;
        entity.Malfunction = malfunction;
        entity.Cost = dto.Cost?.Trim() ?? "";
        entity.CostClassic = cost.Cost1920;
        entity.CostModern = cost.CostModern;
        entity.Notes = dto.Notes?.Trim() ?? "";
        CatalogImages.Apply(db, entity.Images, images, ord => new WeaponImage { WeaponId = entity.Id, Ord = ord });
    }

    /// <summary>Навык по id, а в файле обмена из другой базы — по имени.</summary>
    private static async Task<Guid> ResolveSkillAsync(CmDbContext db, WeaponDto dto, CancellationToken cancellationToken)
    {
        if (dto.SkillId != Guid.Empty && await db.Skills.AnyAsync(s => s.Id == dto.SkillId, cancellationToken))
        {
            return dto.SkillId;
        }

        if (dto.SkillName is { Length: > 0 } name)
        {
            var lower = name.Trim().ToLowerInvariant();
            var found = await db.Skills.Where(s => s.Name.ToLower() == lower).Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (found is { } id)
            {
                return id;
            }
        }

        throw ApiProblemException.Invalid("Выберите навык оружия.");
    }

    private static void Warn(CatalogWrite write, bool parsed, string what, string? raw)
    {
        if (!parsed && !string.IsNullOrWhiteSpace(raw))
        {
            write.Warnings.Add($"«{raw.Trim()}» в поле «{what}» не разобрано — в бою придётся считать вручную.");
        }
    }
}
