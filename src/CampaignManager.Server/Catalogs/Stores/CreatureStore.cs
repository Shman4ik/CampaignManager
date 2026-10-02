using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Files;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Dice;
using CampaignManager.Core.Documents;
using CampaignManager.Data;
using CampaignManager.Data.Catalogs;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Catalogs.Stores;

/// <summary>
/// Бестиарий: статблок — документ Core (<see cref="Statblock"/>), картинки — файлы (<c>creature_images</c>,
/// первая — обложка). Предупреждения, а не отказ: тварь без атак (привидение, рой) или без ПЗ
/// сохраняется, но Хранитель видит, что в бою она поведёт себя не так, как ждёшь.
/// </summary>
public sealed class CreatureStore : CatalogStore<Creature, CreatureDto>
{
    public override CatalogRoute Route => CatalogsRoutes.Creatures;

    public override CatalogCodeTable Codes => CreatureCodes.Table;

    public override string Noun => "Существо";

    public override string UsedBy => "сценариями";

    public override DbSet<Creature> Set(CmDbContext db) => db.Creatures;

    public override IQueryable<Creature> Query(CmDbContext db) => db.Creatures.Include(c => c.Images);

    public override Creature New() => new()
    {
        Name = "",
        Statblock = CmJson.Write(new Statblock()),
        StatblockVersion = Statblock.CurrentVersion,
    };

    public override Task<IReadOnlyList<CreatureDto>> ToDtosAsync(CmDbContext db, IReadOnlyList<Creature> rows, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CreatureDto>>(rows.Select(c => new CreatureDto
        {
            Id = c.Id,
            Version = c.Version,
            Code = c.Code,
            Name = c.Name,
            Source = c.Source,
            Type = c.Type,
            Description = c.Description,
            Statblock = CmJson.ReadStatblock(c.Statblock, c.StatblockVersion),
            Images = c.Images.OrderBy(i => i.Ord)
                .Select(i => new CreatureImageDto(i.FileId, FilesRoutes.Content(i.FileId), i.Caption)).ToList(),
        }).ToList());

    public override async Task ApplyAsync(CmDbContext db, CreatureDto dto, Creature entity, CatalogWrite write, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(dto.Type))
        {
            throw ApiProblemException.Invalid("Неизвестный тип существа.");
        }

        var statblock = dto.Statblock ?? throw ApiProblemException.Invalid("Нет статблока.");
        var images = await ImagesAsync(db, dto.Images, write, cancellationToken);
        CheckStatblock(statblock, write);

        entity.Type = dto.Type;
        entity.Description = Text(dto.Description);
        entity.Statblock = CmJson.Write(statblock);
        entity.StatblockVersion = Statblock.CurrentVersion;

        // Ключ картинки — (тварь, порядок): как у заклинаний книги, правим на месте.
        for (var ord = 0; ord < images.Count; ord++)
        {
            var current = entity.Images.FirstOrDefault(i => i.Ord == ord);
            if (current is null)
            {
                var added = new CreatureImage { CreatureId = entity.Id, Ord = ord, FileId = images[ord].FileId, Caption = Text(images[ord].Caption) };
                entity.Images.Add(added);
                db.Add(added);
            }
            else
            {
                current.FileId = images[ord].FileId;
                current.Caption = Text(images[ord].Caption);
            }
        }

        foreach (var extra in entity.Images.Where(i => i.Ord >= images.Count).ToList())
        {
            entity.Images.Remove(extra);
        }
    }

    /// <summary>
    /// Что бой не бросит (стр. 277–280): формула урона и бонус к урону — только то, что понимает
    /// <see cref="DiceFormula"/>; «урон равен БкУ» — без своих костей.
    /// </summary>
    private static void CheckStatblock(Statblock statblock, CatalogWrite write)
    {
        if (statblock.HitPoints <= 0)
        {
            write.Warnings.Add("нет ПЗ — в бою существо не получит урона.");
        }

        if (statblock.Attacks.Count == 0)
        {
            write.Warnings.Add("нет атак — в бою существу нечем бить.");
        }

        if (!Rollable(statblock.DamageBonus))
        {
            write.Warnings.Add($"бонус к урону «{statblock.DamageBonus}» бой не бросит.");
        }

        foreach (var attack in statblock.Attacks)
        {
            if (attack.DamageBonusMode != CreatureDamageBonusMode.OnlyBonus && !Rollable(attack.Damage))
            {
                write.Warnings.Add($"урон атаки «{attack.Name}» («{attack.Damage}») бой не бросит.");
            }
        }

        if (!string.IsNullOrWhiteSpace(statblock.SanityLoss) && SanityLossFormula.MaxLoss(statblock.SanityLoss) == 0
            && statblock.SanityLoss.Trim() != "0/0")
        {
            write.Warnings.Add($"потерю рассудка «{statblock.SanityLoss}» не разобрать — предел привыкания придётся вписать вручную.");
        }
    }

    private static bool Rollable(string? formula) =>
        string.IsNullOrWhiteSpace(formula) || DiceFormula.Parse(formula).IsValid;

    private static async Task<List<CreatureImageDto>> ImagesAsync(
        CmDbContext db,
        IEnumerable<CreatureImageDto>? incoming,
        CatalogWrite write,
        CancellationToken cancellationToken)
    {
        var images = (incoming ?? []).ToList();
        if (images.Count == 0)
        {
            return images;
        }

        var ids = images.Select(i => i.FileId).Distinct().ToList();
        var known = await db.Files.Where(f => ids.Contains(f.Id)).Select(f => f.Id).ToListAsync(cancellationToken);
        if (known.Count == ids.Count)
        {
            return images;
        }

        if (!write.IsImport)
        {
            throw ApiProblemException.Invalid("Картинка не найдена: загрузите её заново.");
        }

        write.Warnings.Add("часть картинок из файла в этой базе не найдена — они пропущены.");
        return images.Where(i => known.Contains(i.FileId)).ToList();
    }
}
