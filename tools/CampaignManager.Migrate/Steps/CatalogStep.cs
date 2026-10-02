using System.Text.Json.Nodes;
using CampaignManager.Core;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.Data.Catalogs;
using CampaignManager.Migrate.Catalogs;
using CampaignManager.Migrate.V1;

namespace CampaignManager.Migrate.Steps;

/// <summary>
/// Справочники. Код — только из явных таблиц <c>Core/Catalogs/*Codes</c>, имя книжной записи — из той же
/// таблицы (там исправлены ошибки перевода v1). Записи только современной эпохи не переносятся, у остальных
/// эпоха — классика (решение владельца 2026-10-02). Описания книги идут из базы, не из репозитория (D5).
/// </summary>
public static class CatalogStep
{
    private static readonly List<Era> Classic = [Era.Classic];

    public static async Task RunAsync(MigrationState s, CancellationToken cancellationToken)
    {
        Skills(s);
        Occupations(s);
        Weapons(s);
        Spells(s);
        Books(s);
        Items(s);
        await CreaturesAsync(s, cancellationToken);
    }

    /// <summary>Книжное имя записи по коду; null — самодельная (кода нет), тогда имя v1.</summary>
    private static string NameFor(CatalogCodeTable table, string? code, string v1Name, string catalog, MigrationReport report)
    {
        if (code is null)
        {
            return v1Name;
        }

        var name = table.BookNames[code];
        if (!string.Equals(name, v1Name, StringComparison.Ordinal))
        {
            report.Add(ReportSections.Renamed, $"{catalog}: «{v1Name}» → «{name}» (`{code}`)");
        }

        return name;
    }

    private static void Skills(MigrationState s)
    {
        List<(JsonObject Row, Skill Skill)> migrated = [];
        foreach (var row in s.V1.Skills)
        {
            var v1Name = row.Text("Name")!;
            if (!row.Bool("Is1920"))
            {
                s.Report.Add(ReportSections.DroppedModern, $"навык «{v1Name}»");
                continue;
            }

            var code = SkillCodes.FromName(v1Name)
                ?? throw new InvalidOperationException($"Навык v1 «{v1Name}» без кода в SkillCodes — дополнить таблицу.");
            var skill = new Skill
            {
                Id = row.Guid("Id")!.Value,
                Code = code,
                Name = SkillCodes.BookNames[code],
                BaseValue = row.Int("BaseValue") ?? 0,
                // База не число (стр. 57, 77): Core.SkillCatalog.BaseValueOf
                BaseFormula = code switch
                {
                    SkillCodes.Dodge => "DEX/2",
                    SkillCodes.LanguageOwn => "EDU",
                    _ => null,
                },
                Category = row.Enum<SkillCategory>("Category")
                    ?? throw new InvalidOperationException($"Навык «{v1Name}»: категория {row.Str("Category")}"),
                IsUncommon = row.Bool("IsUncommon"),
                Eras = [.. Classic],
                Description = row.Str("Description") ?? "",
                UsageExamples = row.Strings("UsageExamples"),
                FailureConsequences = row.Strings("FailureConsequences"),
                OpposingSkills = row.Strings("OpposingSkills"),
                TimeRequired = row.Text("TimeRequired"),
                CanRetry = row.Bool("CanRetry"),
                CreatedAt = row.Time("CreatedAt") ?? default,
                UpdatedAt = row.Time("LastUpdated") ?? default,
            };
            migrated.Add((row, skill));
            s.Skills[skill.Id] = skill;
        }

        foreach (var (row, skill) in migrated)
        {
            if (row.Guid("ParentSkillId") is { } parentId)
            {
                skill.ParentId = s.Skills.ContainsKey(parentId)
                    ? parentId
                    : throw new InvalidOperationException($"Навык «{skill.Name}»: родитель {parentId} не перенесён.");
            }

            s.Db.Skills.Add(skill);
        }

        s.SkillCatalog = new SkillCatalog(s.Skills.Values.Select(skill => new SkillDefinition(skill.Id, skill.Name)
        {
            Code = skill.Code,
            ParentId = skill.ParentId,
            BaseValue = skill.BaseValue,
            BaseFormula = skill.BaseFormula,
            Category = skill.Category,
        }));
        s.Resolver = new SkillResolver(s.SkillCatalog);
        s.Report.Count("games.Skills", s.V1.Skills.Count, "skills", s.Skills.Count, "минус только современные");
        ModernEraRemoved(s, "навыки", migrated.Count(m => m.Row.Bool("IsModern")));
    }

    private static void ModernEraRemoved(MigrationState s, string catalog, int count)
    {
        if (count > 0)
        {
            s.Report.Add(ReportSections.Fixed, $"{catalog}: у {count} записей с обеими эпохами снята современная (eras = Classic)");
        }
    }

    private static void Occupations(MigrationState s)
    {
        var slots = 0;
        foreach (var row in s.V1.Occupations)
        {
            var v1Name = row.Text("Name")!;
            if (row.Bool("IsModern"))
            {
                s.Report.Add(ReportSections.DroppedModern, $"профессия «{v1Name}»");
                continue;
            }

            var code = OccupationCodes.FromName(v1Name);
            if (code is null)
            {
                s.Report.Add(ReportSections.Homebrew, $"профессия «{v1Name}»");
            }

            var (min, max) = (row.Int("CreditRatingMin") ?? 0, row.Int("CreditRatingMax") ?? 0);
            var occupation = new Occupation
            {
                Id = row.Guid("Id")!.Value,
                Code = code,
                Name = NameFor(OccupationCodes.Table, code, v1Name, "профессия", s.Report),
                SkillPointsFormula = row.Enum<SkillPointsFormula>("SkillPointFormula")
                    ?? throw new InvalidOperationException($"Профессия «{v1Name}»: формула {row.Str("SkillPointFormula")}"),
                CreditRatingMin = min,
                CreditRatingMax = max,
                Eras = [.. Classic],
                IsLovecraftian = row.Bool("IsLovecraftian"),
                Tags = OccupationSlots.Tags(row.Int("Tags") ?? 0),
                CreatedAt = row.Time("CreatedAt") ?? default,
                UpdatedAt = row.Time("LastUpdated") ?? default,
            };
            occupation.Slots = OccupationSlots.Build(row, s.SkillCatalog, s.Resolver,
                problem => s.Report.Add(ReportSections.Warnings, $"профессия «{occupation.Name}»: {problem}"));
            foreach (var slot in occupation.Slots.Where(slot => slot.Kind == Core.Catalogs.OccupationSlotKind.Specialization))
            {
                s.Report.Add(ReportSections.Fixed,
                    $"профессия «{occupation.Name}»: специализация «{slot.Specialization}» без справочника — слот Specialization с родителем");
            }

            slots += occupation.Slots.Count;
            s.Occupations[occupation.Id] = occupation;
            s.Db.Occupations.Add(occupation);
        }

        s.Report.Count("games.Occupations", s.V1.Occupations.Count, "occupations", s.Occupations.Count, $"минус только современные; слотов — {slots}");
    }

    private static void Weapons(MigrationState s)
    {
        foreach (var row in s.V1.Weapons)
        {
            var v1Name = row.Text("Name")!;
            if (!row.Bool("Is1920"))
            {
                s.Report.Add(ReportSections.DroppedModern, $"оружие «{v1Name}»");
                continue;
            }

            var code = WeaponCodes.FromName(v1Name);
            if (code is null)
            {
                s.Report.Add(ReportSections.Homebrew, $"оружие «{v1Name}»");
            }

            var skillId = row.Guid("SkillId") is { } id && s.Skills.ContainsKey(id)
                ? id
                : s.Resolver.CatalogId(row.Str("Skill"))
                  ?? throw new InvalidOperationException($"Оружие «{v1Name}»: навык «{row.Str("Skill")}» не перенесён.");
            var damage = row.Str("Damage")?.Trim() ?? "";
            var range = WeaponStatsParser.ParseRange(row.Str("Range"));
            var attacks = WeaponStatsParser.ParseAttacks(row.Str("Attacks"));
            var ammo = WeaponStatsParser.ParseAmmo(row.Str("Ammo"));
            var cost = WeaponStatsParser.ParseCost(row.Str("Cost"));
            var weapon = new Weapon
            {
                Id = row.Guid("Id")!.Value,
                Code = code,
                Name = NameFor(WeaponCodes.Table, code, v1Name, "оружие", s.Report),
                Type = row.Enum<WeaponType>("Type") ?? WeaponType.Other,
                SkillId = skillId,
                Eras = [.. Classic],
                IsRare = row.Bool("IsRare"),
                IsImpaling = row.Bool("IsImpaling"),
                Damage = damage,
                DamageByRange = DamageByRange(damage, range),
                Range = row.Str("Range")?.Trim() ?? "",
                BaseRangeM = range.BaseMeters is > 0 and var meters ? meters : null,
                Attacks = row.Str("Attacks")?.Trim() ?? "",
                ShotsPerRound = attacks.ShotsPerRound,
                MaxShotsPerRound = attacks.MaxShotsPerRound,
                Ammo = row.Str("Ammo")?.Trim() ?? "",
                AmmoCapacity = ammo.Capacity is > 0 and var capacity ? capacity : null,
                AmmoCapacityOptions = ammo.CapacityOptions is { Count: > 0 } options ? [.. options] : null,
                SingleUse = ammo.IsSingleUse || attacks.IsSingleUse,
                Malfunction = WeaponStatsParser.ParseMalfunction(row.Str("Malfunction")),
                Cost = row.Str("Cost")?.Trim() ?? "",
                CostClassic = cost.Cost1920,
                // Современная цена — данные современной эпохи, которую владелец убрал
                CostModern = null,
                Notes = WeaponNotes(row.Str("Notes")),
                CreatedAt = row.Time("CreatedAt") ?? default,
                UpdatedAt = row.Time("LastUpdated") ?? default,
            };
            s.Weapons[weapon.Id] = weapon;
            s.Db.Weapons.Add(weapon);
        }

        s.Report.Count("games.Weapons", s.V1.Weapons.Count, "weapons", s.Weapons.Count, "минус только современные");
        ModernEraRemoved(s, "оружие", s.V1.Weapons.Count(row => row.Bool("Is1920") && row.Bool("IsModern")));
        if (s.V1.Weapons.Any(row => row.Bool("Is1920") && WeaponStatsParser.ParseCost(row.Str("Cost")).CostModern is not null))
        {
            s.Report.Add(ReportSections.Fixed, "оружие: cost_modern не заполняется (современная эпоха убрана); строка цены книги — как была");
        }
    }

    /// <summary>«1920-е, наши дни» в заметках оружия v1 — подпись эпохи, а не заметка.</summary>
    public static string WeaponNotes(string? notes)
    {
        var text = notes?.Trim() ?? "";
        return text is "1920-е" or "наши дни" or "1920-е, наши дни" ? "" : text;
    }

    /// <summary>Урон дробовика по дальностям: «2d6/1d6/1d3» + «10/20/50 метров» → [{10 м, 2d6}, …].</summary>
    public static List<RangeDamage>? DamageByRange(string damage, WeaponRangeInfo range)
    {
        var parsed = DamageFormulaParser.Parse(damage);
        if (parsed.RangeDamages is not { Count: > 0 } entries)
        {
            return null;
        }

        return entries.Select((entry, i) => new RangeDamage(
                range.Bands is { } bands && bands.Count == entries.Count ? $"{bands[i]} м" : entry.RangeLabel,
                entry.Damage.RawText))
            .ToList();
    }

    private static void Spells(MigrationState s)
    {
        foreach (var row in s.V1.Spells)
        {
            var v1Name = row.Text("Name")!;
            var code = SpellCodes.FromName(v1Name);
            if (code is null)
            {
                s.Report.Add(ReportSections.Homebrew, $"заклинание «{v1Name}»");
            }

            var spell = new Spell
            {
                Id = row.Guid("Id")!.Value,
                Code = code,
                Name = NameFor(SpellCodes.Table, code, v1Name, "заклинание", s.Report),
                AltNames = row.Strings("AlternativeNames"),
                SpellType = row.Text("SpellType") ?? "",
                Cost = row.Text("Cost"),
                CastingTime = row.Text("CastingTime"),
                Description = row.Str("Description") ?? "",
                CreatedAt = row.Time("CreatedAt") ?? default,
                UpdatedAt = row.Time("LastUpdated") ?? default,
            };
            s.Spells[spell.Id] = spell;
            s.Db.Spells.Add(spell);
        }

        s.Report.Count("games.Spells", s.V1.Spells.Count, "spells", s.Spells.Count);
    }

    private static void Books(MigrationState s)
    {
        var catalog = s.Spells.Values.Select(spell => new SpellData(spell.Id, spell.Name) { AlternativeNames = spell.AltNames }).ToList();
        var (links, matched) = (0, 0);
        foreach (var row in s.V1.Books)
        {
            var v1Name = row.Text("Name")!;
            var code = BookCodes.FromName(v1Name);
            if (code is null)
            {
                s.Report.Add(ReportSections.Homebrew, $"книга «{v1Name}»");
            }

            var book = new Book
            {
                Id = row.Guid("Id")!.Value,
                Code = code,
                Name = NameFor(BookCodes.Table, code, v1Name, "книга", s.Report),
                BookType = row.Enum<BookType>("BookType") ?? BookType.MythosBook,
                AltNames = row.Strings("AlternativeNames"),
                Language = row.Text("Language"),
                Year = row.Text("Year"),
                Author = row.Text("Author"),
                SanityLoss = row.Text("SanityLoss"),
                MythosInitial = row.Int("CthulhuMythosInitial"),
                MythosFull = row.Int("CthulhuMythosFull"),
                MythosRating = row.Int("MythosRating"),
                StudyWeeks = row.Int("StudyWeeks"),
                OccultismBonus = row.Int("OccultismBonus"),
                Description = row.Str("Description") ?? "",
                CreatedAt = row.Time("CreatedAt") ?? default,
                UpdatedAt = row.Time("LastUpdated") ?? default,
            };
            if (row.Text("ImageUrl") is not null)
            {
                s.Report.Add(ReportSections.Warnings, $"книга «{book.Name}»: обложка v1 не перенесена (в данных их не было)");
            }

            var ord = 0;
            foreach (var raw in row.Strings("PossibleSpells"))
            {
                var spell = SpellMatcher.Match(catalog, raw);
                book.Spells.Add(new BookSpell { Ord = ord++, RawName = raw, SpellId = spell?.Id });
                links++;
                matched += spell is null ? 0 : 1;
            }

            s.Db.Books.Add(book);
        }

        s.Report.Count("games.Books", s.V1.Books.Count, "books", s.V1.Books.Count);
        s.Report.Count("Books.PossibleSpells", links, "book_spells", links, $"с заклинанием каталога — {matched}, строкой книги — {links - matched}");
    }

    private static void Items(MigrationState s)
    {
        List<string> movedTypes = [];
        var movedFrom = "";
        foreach (var row in s.V1.Items)
        {
            var v1Name = row.Text("Name")!;
            if (OwnerDecisions.ModernItems.Contains(v1Name))
            {
                s.Report.Add(ReportSections.DroppedModern, $"предмет «{v1Name}» ({row.Text("Type")})");
                continue;
            }

            if (OwnerDecisions.HotelDuplicates.TryGetValue(v1Name, out var original))
            {
                s.Report.Add(ReportSections.DroppedDuplicates, $"предмет «{v1Name}» — повтор «{original}»");
                continue;
            }

            if (OwnerDecisions.ScenarioProps.Contains(v1Name))
            {
                s.ScenarioProps[v1Name] = row;
                continue;
            }

            var code = ItemCodes.FromName(v1Name);
            if (code is null)
            {
                s.Report.Add(ReportSections.Homebrew, $"предмет «{v1Name}»");
            }

            var type = row.Text("Type");
            if (type is not null && OwnerDecisions.ItemTypeFixes.TryGetValue(type, out var fixedType))
            {
                movedTypes.Add($"«{v1Name}»");
                movedFrom = $"«{type}» → «{fixedType}»";
                type = fixedType;
            }

            if (row.Str("Era") is { } era && era != "Classic" && era != "1")
            {
                s.Report.Add(ReportSections.Warnings, $"предмет «{v1Name}»: эпоха v1 «{era}» — записан классикой");
            }

            var item = new Item
            {
                Id = row.Guid("Id")!.Value,
                Code = code,
                Name = NameFor(ItemCodes.Table, code, v1Name, "предмет", s.Report),
                Type = type,
                Eras = [.. Classic],
                Description = row.Text("Description"),
                CreatedAt = row.Time("CreatedAt") ?? default,
                UpdatedAt = row.Time("LastUpdated") ?? default,
            };
            if (row.Text("ImageUrl") is not null)
            {
                s.Report.Add(ReportSections.Warnings, $"предмет «{item.Name}»: картинка v1 не перенесена (в данных их не было)");
            }

            s.ItemsByV1Name[v1Name] = item;
            s.Db.Items.Add(item);
        }

        if (movedTypes.Count > 0)
        {
            s.Report.Add(ReportSections.Fixed, $"предметы: раздел {movedFrom} — {movedTypes.Count} ({string.Join(", ", movedTypes.Order(StringComparer.Ordinal))})");
        }

        s.Report.Count("games.Items", s.V1.Items.Count, "items", s.ItemsByV1Name.Count,
            $"минус {OwnerDecisions.ModernItems.Count} современных, {OwnerDecisions.HotelDuplicates.Count} повтора, {s.ScenarioProps.Count} реквизита сценария");
    }

    private static async Task CreaturesAsync(MigrationState s, CancellationToken cancellationToken)
    {
        var images = 0;
        foreach (var row in s.V1.Creatures)
        {
            var v1Name = row.Text("Name")!;
            var code = CreatureCodes.FromName(v1Name);
            if (code is null)
            {
                s.Report.Add(ReportSections.Homebrew, $"тварь «{v1Name}»");
            }

            var statblock = StatblockConverter.Convert(row, s.Resolver.CatalogId);
            var creature = new Creature
            {
                Id = row.Guid("Id")!.Value,
                Code = code,
                Name = NameFor(CreatureCodes.Table, code, v1Name, "тварь", s.Report),
                Type = row.Enum<CreatureType>("Type") ?? CreatureType.Other,
                Description = row.Text("Description"),
                Statblock = CmJson.Write(statblock),
                StatblockVersion = Statblock.CurrentVersion,
                CreatedAt = row.Time("CreatedAt") ?? default,
                UpdatedAt = row.Time("LastUpdated") ?? default,
            };
            if ((row.Obj("CreatureCharacteristics") ?? []).Any(pair => StatblockConverter.LegacyKeys.Contains(pair.Key)))
            {
                s.Report.Add(ReportSections.DroppedJunk, $"тварь «{creature.Name}»: ключи наследия Appearance/Education/Luck/Constitutions");
            }

            var ord = 0;
            foreach (var image in row.Arr("Images").OfType<JsonObject>())
            {
                if (image.Text("Url") is not { } url)
                {
                    continue;
                }

                var file = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? s.ExternalFile(url)
                    : await s.StoredFileAsync(url, $"тварь «{creature.Name}»", cancellationToken);
                if (file is null)
                {
                    continue;
                }

                creature.Images.Add(new CreatureImage { Ord = ord++, FileId = file.Id, Caption = image.Text("Caption") });
                images++;
            }

            s.CreaturesByV1Name[v1Name] = (creature, StatblockConverter.Canonical(statblock));
            s.Db.Creatures.Add(creature);
        }

        var combatDescriptions = s.V1.Creatures.Count(row => row.Obj("CombatDescriptions") is { Count: > 0 });
        var imageUrls = s.V1.Creatures.Count(row => row.Text("ImageUrl") is not null);
        s.Report.Add(ReportSections.DroppedJunk, $"бестиарий: словарь CombatDescriptions у {combatDescriptions} тварей (источник правды — типизированные поля)");
        s.Report.Add(ReportSections.DroppedJunk, $"бестиарий: колонка ImageUrl у {imageUrls} тварей (уже перенесена в Images)");
        s.Report.Count("games.Creatures", s.V1.Creatures.Count, "creatures", s.CreaturesByV1Name.Count);
        s.Report.Count("Creatures.Images", s.V1.Creatures.Sum(row => row.Arr("Images").Count), "creature_images", images);
    }
}
