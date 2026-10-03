using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Files;
using CampaignManager.Core.Catalogs;
using CampaignManager.Data;
using CampaignManager.Data.Catalogs;
using CampaignManager.Server.Platform;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Server.Catalogs.Stores;

/// <summary>
/// Заклинания. Стоимость и время сотворения — текст книги; подсказку для боя из них разбирает
/// <c>SpellStatsReader</c> на клиенте, это подсказка, а не правда.
/// </summary>
public sealed class SpellStore : CatalogStore<Spell, SpellDto>
{
    public override CatalogRoute Route => CatalogsRoutes.Spells;

    public override CatalogCodeTable Codes => SpellCodes.Table;

    public override string Noun => "Заклинание";

    public override DbSet<Spell> Set(CmDbContext db) => db.Spells;

    public override Spell New() => new() { Name = "", SpellType = "" };

    public override Task<IReadOnlyList<SpellDto>> ToDtosAsync(CmDbContext db, IReadOnlyList<Spell> rows, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SpellDto>>(rows.Select(s => new SpellDto
        {
            Id = s.Id,
            Version = s.Version,
            Code = s.Code,
            Name = s.Name,
            Source = s.Source,
            AltNames = s.AltNames,
            SpellType = s.SpellType,
            Cost = s.Cost,
            CastingTime = s.CastingTime,
            Description = s.Description,
        }).ToList());

    public override Task ApplyAsync(CmDbContext db, SpellDto dto, Spell entity, CatalogWrite write, CancellationToken cancellationToken)
    {
        var type = dto.SpellType?.Trim() ?? "";
        if (type.Length == 0)
        {
            throw ApiProblemException.Invalid("Нужен тип заклинания: «Призыв», «Контакт»…");
        }

        entity.SpellType = type;
        // Другие названия — без самого имени и без повторов: по ним книга Мифов сопоставляет заклинания.
        entity.AltNames = Strings(dto.AltNames)
            .Where(n => !string.Equals(n, dto.Name?.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        entity.Cost = Text(dto.Cost);
        entity.CastingTime = Text(dto.CastingTime);
        entity.Description = dto.Description?.Trim() ?? "";
        return Task.CompletedTask;
    }
}

/// <summary>Предметы: тип — свободный текст, картинка — файл.</summary>
public sealed class ItemStore : CatalogStore<Item, ItemDto>
{
    public override CatalogRoute Route => CatalogsRoutes.Items;

    public override CatalogCodeTable Codes => ItemCodes.Table;

    public override string Noun => "Предмет";

    public override string UsedBy => "сценариями";

    public override DbSet<Item> Set(CmDbContext db) => db.Items;

    public override Item New() => new() { Name = "" };

    public override Task<IReadOnlyList<ItemDto>> ToDtosAsync(CmDbContext db, IReadOnlyList<Item> rows, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ItemDto>>(rows.Select(i => new ItemDto
        {
            Id = i.Id,
            Version = i.Version,
            Code = i.Code,
            Name = i.Name,
            Source = i.Source,
            Type = i.Type,
            Eras = i.Eras,
            Description = i.Description,
            Price = i.Price,
            ImageFileId = i.ImageFileId,
            ImageUrl = i.ImageFileId is { } file ? FilesRoutes.Content(file) : null,
        }).ToList());

    public override async Task ApplyAsync(CmDbContext db, ItemDto dto, Item entity, CatalogWrite write, CancellationToken cancellationToken)
    {
        var eras = Eras(dto.Eras);
        var image = await ImageAsync(db, dto.ImageFileId, write, cancellationToken);

        entity.Type = Text(dto.Type);
        entity.Eras = eras;
        entity.Description = Text(dto.Description);
        entity.Price = Price(dto.Price);
        entity.ImageFileId = image;
    }

    private static decimal? Price(decimal? price)
    {
        if (price is not { } value)
        {
            return null;
        }

        return value is < 0 or > 9_999_999_999m
            ? throw ApiProblemException.Invalid("Цена — от 0 до 9 999 999 999 долларов.")
            : decimal.Round(value, 2);
    }

    /// <summary>Картинка из файла обмена другой базы не найдётся — в импорте её просто нет.</summary>
    internal static async Task<Guid?> ImageAsync(CmDbContext db, Guid? fileId, CatalogWrite write, CancellationToken cancellationToken)
    {
        if (fileId is not { } id)
        {
            return null;
        }

        if (await db.Files.AnyAsync(f => f.Id == id, cancellationToken))
        {
            return id;
        }

        if (write.IsImport)
        {
            write.Warnings.Add("картинки из файла нет в этой базе — запись без неё.");
            return null;
        }

        throw ApiProblemException.Invalid("Картинка не найдена: загрузите её заново.");
    }
}

/// <summary>
/// Книги. Возможные заклинания — строки книги в её порядке (<c>book_spells</c>); несвязанные сервер
/// сопоставляет со справочником при записи — только точное совпадение имени или другого названия
/// (<see cref="SpellMatcher"/>), нечёткого поиска нет: не нашлось — Хранитель свяжет руками.
/// </summary>
public sealed class BookStore : CatalogStore<Book, BookDto>
{
    public override CatalogRoute Route => CatalogsRoutes.Books;

    public override CatalogCodeTable Codes => BookCodes.Table;

    public override string Noun => "Книга";

    public override DbSet<Book> Set(CmDbContext db) => db.Books;

    public override IQueryable<Book> Query(CmDbContext db) => db.Books.Include(b => b.Spells);

    public override Book New() => new() { Name = "" };

    public override Task<IReadOnlyList<BookDto>> ToDtosAsync(CmDbContext db, IReadOnlyList<Book> rows, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BookDto>>(rows.Select(b => new BookDto
        {
            Id = b.Id,
            Version = b.Version,
            Code = b.Code,
            Name = b.Name,
            Source = b.Source,
            BookType = b.BookType,
            AltNames = b.AltNames,
            Language = b.Language,
            Year = b.Year,
            Author = b.Author,
            SanityLoss = b.SanityLoss,
            MythosInitial = b.MythosInitial,
            MythosFull = b.MythosFull,
            MythosRating = b.MythosRating,
            StudyWeeks = b.StudyWeeks,
            OccultismBonus = b.OccultismBonus,
            Description = b.Description,
            ImageFileId = b.ImageFileId,
            ImageUrl = b.ImageFileId is { } file ? FilesRoutes.Content(file) : null,
            Spells = b.Spells.OrderBy(s => s.Ord).Select(s => new BookSpellDto(s.RawName, s.SpellId)).ToList(),
        }).ToList());

    public override async Task ApplyAsync(CmDbContext db, BookDto dto, Book entity, CatalogWrite write, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(dto.BookType))
        {
            throw ApiProblemException.Invalid("Неизвестная категория книги.");
        }

        var mythosInitial = Range(dto.MythosInitial, 0, 99, "МКН");
        var mythosFull = Range(dto.MythosFull, 0, 99, "МКП");
        var mythosRating = Range(dto.MythosRating, 0, 99, "ЗМ");
        var studyWeeks = Range(dto.StudyWeeks, 0, 1000, "Недель на изучение");
        var occultism = Range(dto.OccultismBonus, 0, 99, "Прибавка к Оккультизму");
        var image = await ItemStore.ImageAsync(db, dto.ImageFileId, write, cancellationToken);
        var spells = await MatchSpellsAsync(db, dto.Spells, cancellationToken);

        entity.BookType = dto.BookType;
        entity.AltNames = Strings(dto.AltNames);
        entity.Language = Text(dto.Language);
        entity.Year = Text(dto.Year);
        entity.Author = Text(dto.Author);
        entity.SanityLoss = Text(dto.SanityLoss);
        entity.MythosInitial = mythosInitial;
        entity.MythosFull = mythosFull;
        entity.MythosRating = mythosRating;
        entity.StudyWeeks = studyWeeks;
        entity.OccultismBonus = occultism;
        entity.Description = dto.Description?.Trim() ?? "";
        entity.ImageFileId = image;

        // Ключ строки — (книга, порядковый номер): правим на месте, лишние удаляем, новые добавляем.
        // Удалить и добавить строку с тем же ключом в одном сохранении EF не даст.
        for (var ord = 0; ord < spells.Count; ord++)
        {
            var current = entity.Spells.FirstOrDefault(s => s.Ord == ord);
            if (current is null)
            {
                var added = new BookSpell { BookId = entity.Id, Ord = ord, RawName = spells[ord].RawName, SpellId = spells[ord].SpellId };
                entity.Spells.Add(added);
                db.Add(added);
            }
            else
            {
                current.RawName = spells[ord].RawName;
                current.SpellId = spells[ord].SpellId;
            }
        }

        foreach (var extra in entity.Spells.Where(s => s.Ord >= spells.Count).ToList())
        {
            entity.Spells.Remove(extra);
        }
    }

    private static async Task<List<BookSpellDto>> MatchSpellsAsync(CmDbContext db, IEnumerable<BookSpellDto>? incoming, CancellationToken cancellationToken)
    {
        var rows = (incoming ?? []).Where(s => !string.IsNullOrWhiteSpace(s.RawName))
            .Select(s => s with { RawName = s.RawName.Trim() }).ToList();
        if (rows.Count == 0)
        {
            return rows;
        }

        var catalog = await db.Spells.AsNoTracking()
            .Select(s => new SpellData(s.Id, s.Name) { AlternativeNames = s.AltNames })
            .ToListAsync(cancellationToken);
        var known = catalog.Select(s => s.Id).ToHashSet();
        return rows.Select(s => s.SpellId is { } id && known.Contains(id)
            ? s
            : s with { SpellId = SpellMatcher.Match(catalog, s.RawName)?.Id }).ToList();
    }
}
