using System.Globalization;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Characters;

/// <summary>
/// Персонаж из файла сценария (формат импорта v1, T2.5d): то, что напечатано в книге. ПЗ, ПМ, БкУ, Комплекция,
/// Скорость и Уклонение — как в статблоке; что расходится с формулой, становится <see cref="SheetOverrides"/>.
/// </summary>
public sealed record ImportedCharacter
{
    public string Name { get; init; } = "";
    public string? Occupation { get; init; }
    public int Age { get; init; }
    public string? Gender { get; init; }
    public string? Backstory { get; init; }
    public Characteristics Characteristics { get; init; } = new();
    public int HitPoints { get; init; }
    public int MagicPoints { get; init; }

    /// <summary>Текущий Рассудок — книга печатает у НПС его, а не максимум.</summary>
    public int Sanity { get; init; }

    public int Luck { get; init; }
    public string? DamageBonus { get; init; }
    public string? Build { get; init; }
    public int MoveSpeed { get; init; }
    public int Dodge { get; init; }

    /// <summary>Навыки «имя → значение» как в книге: имя справочника, старое написание v1 или «Родитель (уточнение)».</summary>
    public IReadOnlyDictionary<string, int> Skills { get; init; } = new Dictionary<string, int>();

    // ── Сверх формата v1 (T2.5d): то, что экспорт отдаёт у прегенов, — иначе преген терял оружие и биографию ──

    public string? Birthplace { get; init; }
    public string? Residence { get; init; }

    /// <summary>Графы биографии; <see cref="Backstory"/>, если задана, побеждает предысторию отсюда.</summary>
    public Biography? Biography { get; init; }

    public IReadOnlyList<ImportedWeapon> Weapons { get; init; } = [];

    /// <summary>Заклинания — свой экземпляр листа; ссылку на справочник (<see cref="SheetSpell.CatalogSpellId"/>) ставит вызывающий.</summary>
    public IReadOnlyList<SheetSpell> Spells { get; init; } = [];

    public IReadOnlyList<EquipmentItem> Equipment { get; init; } = [];

    public Finances? Finances { get; init; }
}

/// <summary>
/// Оружие из файла: текст книги и навык по имени (как навыки листа). <see cref="CatalogWeaponId"/> — запись справочника с
/// тем же именем, её находит вызывающий: в Core справочника оружия нет.
/// </summary>
public sealed record ImportedWeapon
{
    public string Name { get; init; } = "";

    /// <summary>Навык по имени справочника или старому написанию; не нашёлся — оружие без навыка.</summary>
    public string? Skill { get; init; }

    public string Damage { get; init; } = "";
    public string Range { get; init; } = "";
    public string Attacks { get; init; } = "";
    public string Ammo { get; init; } = "";
    public string Malfunction { get; init; } = "";
    public bool Impaling { get; init; }
    public string Notes { get; init; } = "";
    public Guid? CatalogWeaponId { get; init; }
}

/// <summary>Лист импорта и навыки, которых нет в справочнике (они легли на лист своими навыками — отчёт импорта).</summary>
public sealed record ImportedSheet(CharacterSheet Sheet, IReadOnlyList<string> OwnSkills);

/// <summary>
/// Один конструктор листа вместо пяти v1 (генератор, фабрика помощника, быстрый НПС, импорт сценария, чистый лист —
/// AUDIT, «Персонажи и НПС → Дубли»). Режимы: <see cref="Blank"/>, <see cref="FromDraft"/>, <see cref="QuickNpc"/>,
/// <see cref="FromImport"/>; случайный сыщик — не свой режим, а черновик помощника (<see cref="RandomDraft"/>), который
/// дальше идёт тем же <see cref="FromDraft"/>.
/// <para>
/// Устройство у всех режимов одно, поэтому листы неотличимы:
/// <list type="bullet">
/// <item>навыки — <b>только ссылки на справочник</b> (своё — строкой с именем, специализация вне справочника — с
/// родителем); строка заводится, только если значение выше базы (<see cref="SkillCatalog.IsAboveBase"/>) — остальное лист показывает базой
/// справочника, как пустую графу бланка;</item>
/// <item>родной язык = ОБР и Уклонение = ½ ЛВК — формулы справочника (<see cref="SkillCatalog.BaseValueOf"/>), а не
/// копия числа: в v1 их выставляли три сборщика из пяти;</item>
/// <item>вторичные атрибуты — <see cref="DerivedAttributeRules"/>; ПЗ и ПМ на максимуме, Рассудок = МОЩ
/// (<see cref="DerivedAttributeRules.InitializeNewSheet"/>); напечатанное в книге и расходящееся с формулой — в
/// <see cref="SheetOverrides"/> (в том числе при импорте — v1 брал его мимо правил).</item>
/// </list>
/// </para>
/// </summary>
public static class SheetBuilder
{
    /// <summary>Чистый лист: характеристики нулями, навыки на базе справочника — заполняет человек на листе.</summary>
    public static CharacterSheet Blank(SkillCatalog catalog, string name = "")
    {
        var sheet = new CharacterSheet { Personal = new PersonalInfo { Name = name.Trim() } };
        Finish(sheet, catalog, luck: 0);
        return sheet;
    }

    /// <summary>Лист из черновика помощника (глава 3): характеристики, навыки, биография, деньги по таблице II.</summary>
    public static CharacterSheet FromDraft(InvestigatorDraft draft, SkillCatalog catalog, OccupationDefinition? occupation) =>
        FromDraft(new CreationPlan(draft, catalog, occupation));

    public static CharacterSheet FromDraft(CreationPlan plan)
    {
        var draft = plan.Draft;
        var catalog = plan.Catalog;
        plan.SyncSlots();

        var sheet = new CharacterSheet
        {
            Personal = new PersonalInfo
            {
                Name = draft.Personal.Name.Trim(),
                Occupation = plan.Occupation?.Name ?? draft.OccupationName,
                OccupationId = plan.Occupation?.Id,
                Age = draft.Age,
                Gender = draft.Personal.Gender.Trim(),
                Birthplace = draft.Personal.Birthplace.Trim(),
                Residence = draft.Personal.Residence.Trim(),
            },
            Characteristics = draft.BuildCharacteristics(),
            Biography = draft.Biography with { },
            Equipment = [.. draft.Equipment.Where(e => !string.IsNullOrWhiteSpace(e.Name)).Select(e => e with { })],
            Weapons = [.. draft.Weapons.Select(w => w with { })],
            Finances = FinanceRules.ForNewInvestigator(draft.CreditRating, draft.Era),
        };

        sheet.Biography.KeyConnection = BiographyTables.Find(draft.KeyConnectionSection)?.Read(draft.Biography) ?? "";

        var keys = draft.OccupationPoints.Keys
            .Concat(draft.PersonalPoints.Keys)
            .Concat(draft.AddedSpecializations.Keys)
            .Distinct(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            var value = plan.Total(key);
            if (CreationPlan.SkillIdOf(key) is { } id)
            {
                if (catalog.Find(id) is { } skill)
                    SetSkill(sheet, catalog, skill, value);
            }
            else if (draft.AddedSpecializations.TryGetValue(key, out var parent) && catalog.Find(parent) is not null)
            {
                var row = OccupationRules.NewSpecialization(parent, key, catalog);
                row.Value = value;
                sheet.Skills.Add(row);
            }
        }

        if (plan.CreditRating is { } credit)
            SetSkill(sheet, catalog, credit, draft.CreditRating);

        Finish(sheet, catalog, draft.Luck);
        return sheet;
    }

    /// <summary>
    /// Быстрый НПС (гл. 10): имя, занятие, характеристики, ключевые навыки, оружие из справочника, заметка в
    /// предысторию. Остальные навыки — база справочника.
    /// </summary>
    public static CharacterSheet QuickNpc(QuickNpcDraft draft, SkillCatalog catalog)
    {
        var sheet = new CharacterSheet
        {
            Personal = new PersonalInfo
            {
                Name = draft.Name.Trim(),
                Occupation = draft.Occupation.Trim(),
                Age = Math.Max(0, draft.Age),
                Gender = NameTables.GenderText(draft.Gender),
            },
            Characteristics = ClampCharacteristics(draft.Characteristics),
            Biography = new Biography { Backstory = draft.Note.Trim() },
        };

        foreach (var row in draft.Skills)
        {
            var value = QuickNpcRules.ClampSkill(row.Value);
            if (catalog.Find(row.SkillId) is { } skill)
                SetSkill(sheet, catalog, skill, value);
            else if (!string.IsNullOrWhiteSpace(row.Name))
                SetOwnSkill(sheet, catalog, row.Name, value);
        }

        if (draft.Weapon is { } weapon)
            sheet.Weapons.Add(SheetCopies.Weapon(weapon));

        Finish(sheet, catalog, Math.Clamp(draft.Luck, 0, DerivedAttributeRules.MaxLuck));
        return sheet;
    }

    /// <summary>
    /// НПС или преген из файла сценария. Навык — по имени справочника и старым написаниям v1 (без регистра и «ё»),
    /// «Родитель (уточнение)» — специализацией, остальное — своим навыком (в v1 — группой «Особые навыки» с базой «—»).
    /// Напечатанные ПЗ, ПМ, БкУ, Комплекция и Скорость, расходящиеся с формулой, — <see cref="SheetOverrides"/>;
    /// Уклонение выше базы — строкой навыка (в v1 оставалось 0, а первая правка характеристики затирала импорт).
    /// </summary>
    public static ImportedSheet FromImport(ImportedCharacter data, SkillCatalog catalog)
    {
        var characteristics = ClampCharacteristics(data.Characteristics);
        var biography = data.Biography is { } given ? given with { } : new Biography();
        if (!string.IsNullOrWhiteSpace(data.Backstory))
            biography.Backstory = data.Backstory.Trim();

        var sheet = new CharacterSheet
        {
            Personal = new PersonalInfo
            {
                Name = data.Name.Trim(),
                Occupation = data.Occupation?.Trim() ?? "",
                Age = Math.Max(0, data.Age),
                Gender = data.Gender?.Trim() ?? "",
                Birthplace = data.Birthplace?.Trim() ?? "",
                Residence = data.Residence?.Trim() ?? "",
            },
            Characteristics = characteristics,
            Biography = biography,
            Spells = [.. data.Spells.Select(s => s with { AlternativeNames = [.. s.AlternativeNames] })],
            Equipment = [.. data.Equipment.Select(e => e with { })],
            Finances = data.Finances is { } finances ? finances with { } : new Finances(),
        };

        var resolver = new SkillNameResolver(catalog);
        foreach (var weapon in data.Weapons)
        {
            sheet.Weapons.Add(new SheetWeapon
            {
                CatalogWeaponId = weapon.CatalogWeaponId,
                Name = weapon.Name.Trim(),
                SkillId = resolver.CatalogId(weapon.Skill),
                Damage = weapon.Damage,
                Range = weapon.Range,
                Attacks = weapon.Attacks,
                Ammo = weapon.Ammo,
                Malfunction = weapon.Malfunction,
                Impaling = weapon.Impaling,
                Notes = weapon.Notes,
            });
        }

        List<string> own = [];
        foreach (var (rawName, rawValue) in data.Skills)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                continue;

            var value = Math.Clamp(rawValue, 0, 999);
            switch (resolver.Resolve(rawName))
            {
                case SkillMatch.Catalog { Skill: var skill } when !catalog.IsParent(skill.Id):
                    SetSkill(sheet, catalog, skill, value);
                    break;
                case SkillMatch.Specialization { Parent: var parent, Name: var name }:
                    var row = OccupationRules.NewSpecialization(parent.Id, name, catalog);
                    row.Value = value;
                    sheet.Skills.Add(row);
                    break;
                default:
                    SetOwnSkill(sheet, catalog, rawName, value);
                    own.Add(rawName.Trim());
                    break;
            }
        }

        if (data.Dodge > 0 && catalog.FindByCode(SkillCodes.Dodge) is { } dodge)
            SetSkill(sheet, catalog, dodge, data.Dodge);

        var (build, damageBonus) = DerivedAttributeRules.ComputeBuildAndDamageBonus(characteristics);
        var o = sheet.Overrides;
        if (data.HitPoints > 0 && data.HitPoints != DerivedAttributeRules.ComputeMaxHitPoints(characteristics))
            o.MaxHitPoints = data.HitPoints;
        if (data.MagicPoints > 0 && data.MagicPoints != DerivedAttributeRules.ComputeMaxMagicPoints(characteristics))
            o.MaxMagicPoints = data.MagicPoints;
        if (NormalizeDamageBonus(data.DamageBonus) is { } printedBonus && printedBonus != NormalizeDamageBonus(damageBonus))
            o.DamageBonus = data.DamageBonus!.Trim();
        if (int.TryParse(data.Build?.Trim().Replace('−', '-'), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var printedBuild)
            && printedBuild != build)
            o.Build = printedBuild;
        if (data.MoveSpeed > 0 && data.MoveSpeed != DerivedAttributeRules.ComputeMoveRate(characteristics, sheet.Personal.Age))
            o.Move = data.MoveSpeed;

        Finish(sheet, catalog, Math.Clamp(data.Luck, 0, DerivedAttributeRules.MaxLuck));
        if (data.Sanity > 0)
            sheet.Current.Sanity = Math.Min(data.Sanity, DerivedAttributeRules.Compute(sheet, catalog).MaxSanity);

        return new ImportedSheet(sheet, own);
    }

    /// <summary>
    /// Обратное <see cref="FromImport"/> — лист в формат файла сценария (экспорт, T2.5d). Пишется только то, что
    /// <see cref="FromImport"/> читает, и так, чтобы импорт экспорта дал тот же экспорт: ПЗ, ПМ, БкУ, Комплекция,
    /// Скорость и Уклонение — итоговые (с поправками книги), навыки — только строки <b>выше базы</b> (база — пустая
    /// графа бланка, импорт её строкой не заведёт), Уклонение — полем, а не навыком. Отметки развития, состояние
    /// (раны, безумие), книги Мифов и знакомые сыщики в формат не входят: это игра, а не заготовка.
    /// </summary>
    public static ImportedCharacter ToImport(CharacterSheet sheet, SkillCatalog catalog)
    {
        var derived = DerivedAttributeRules.Compute(sheet, catalog);
        var dodge = catalog.FindByCode(SkillCodes.Dodge);
        var skills = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in sheet.Skills)
        {
            if (dodge is not null && row.SkillId == dodge.Id)
                continue;

            if (catalog.Find(row.SkillId) is { } known && !SkillCatalog.IsAboveBase(known, row.Value, sheet.Characteristics))
                continue;

            var name = row.DisplayName(catalog).Trim();
            if (name.Length > 0)
                skills[name] = row.Value;
        }

        // Незнакомые графы старых листов (Extra) — не заготовка: в файл не идут.
        var biography = sheet.Biography with { Backstory = "", Extra = null };
        var onlyBackstory = biography == new Biography();
        var finances = sheet.Finances;
        var noFinances = finances.Cash is null && finances.PocketMoney is null
                                               && string.IsNullOrWhiteSpace(finances.Assets) && string.IsNullOrWhiteSpace(finances.Note);

        return new ImportedCharacter
        {
            Name = sheet.Personal.Name,
            Occupation = Blank(sheet.Personal.Occupation),
            Age = sheet.Personal.Age,
            Gender = Blank(sheet.Personal.Gender),
            Backstory = Blank(sheet.Biography.Backstory),
            Characteristics = sheet.Characteristics with { },
            HitPoints = derived.MaxHitPoints,
            MagicPoints = derived.MaxMagicPoints,
            Sanity = sheet.Current.Sanity,
            Luck = sheet.Current.Luck,
            DamageBonus = derived.DamageBonus,
            Build = derived.Build.ToString(CultureInfo.InvariantCulture),
            MoveSpeed = derived.Move,
            Dodge = derived.Dodge,
            Skills = skills,
            Birthplace = Blank(sheet.Personal.Birthplace),
            Residence = Blank(sheet.Personal.Residence),
            Biography = onlyBackstory ? null : biography,
            Weapons =
            [
                .. sheet.Weapons.Select(w => new ImportedWeapon
                {
                    Name = w.Name,
                    Skill = catalog.Find(w.SkillId)?.Name,
                    Damage = w.Damage,
                    Range = w.Range,
                    Attacks = w.Attacks,
                    Ammo = w.Ammo,
                    Malfunction = w.Malfunction,
                    Impaling = w.Impaling,
                    Notes = w.Notes,
                    CatalogWeaponId = w.CatalogWeaponId,
                }),
            ],
            Spells = [.. sheet.Spells.Select(s => s with { AlternativeNames = [.. s.AlternativeNames] })],
            Equipment = [.. sheet.Equipment.Select(e => e with { })],
            Finances = noFinances ? null : finances with { },
        };

        static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Случайный сыщик — черновик помощника, заполненный по тем же правилам главы 3, что и вручную, и открытый на
    /// «Итоге» (D6.4): стандартные броски, вычет за возраст, проверки ОБР, Удача, профессия, выбор слотов, пункты,
    /// биография по спискам. Отдельного генератора и журнала генерации нет — журнал заменяют поля черновика,
    /// а любой шаг игрок может открыть и переиграть.
    /// </summary>
    /// <param name="occupations">Профессии эпохи (пусто — без профессии, шаг останется непройденным).</param>
    public static InvestigatorDraft RandomDraft(
        SkillCatalog catalog, IReadOnlyList<OccupationDefinition> occupations, Era era, IDiceRoller dice)
    {
        var draft = new InvestigatorDraft { Era = era, Method = CreationMethod.Standard };
        draft.SetAge(dice.Roll(4, 10) + 15);
        draft.RollAll(dice);

        var band = InvestigatorCreationRules.BandFor(draft.Age);
        for (var guard = 0; draft.RemainingAgePenalty(band) > 0 && guard < 1000; guard++)
            draft.ChangeAgePenalty(band.PenaltyTargets[dice.Next(0, band.PenaltyTargets.Count)], 1);

        while (draft.CanAddEducationCheck)
            draft.AddEducationCheck(dice);
        draft.RollLuck(dice);

        CreationPlan? plan = null;
        if (occupations.Count > 0)
        {
            var occupation = occupations[dice.Next(0, occupations.Count)];
            CreationPlan.SelectOccupation(draft, occupation);
            plan = new CreationPlan(draft, catalog, occupation);
            plan.SyncSlots();

            // Где формула даёт выбор, случайный сыщик берёт лучшую характеристику — книга оставляет выбор игроку.
            var characteristics = plan.Characteristics;
            draft.FormulaChoice = plan.FormulaChoices.Count > 0
                ? plan.FormulaChoices.OrderByDescending(k => characteristics[k]).First()
                : null;

            for (var i = 0; i < plan.Slots.Count; i++)
            {
                if (!plan.Slots[i].NeedsChoice)
                    continue;

                var options = plan.OptionsFor(i);
                if (options.Count > 0)
                    plan.SetChoice(i, options[dice.Next(0, options.Count)]);
            }

            plan.SetCreditRating(dice.Next(plan.CreditMin, (plan.CreditMin + plan.CreditMax) / 2 + 1));
            Spread(plan, plan.OccupationSkillKeys, plan.OccupationBudget - draft.CreditRating, occupation: true, dice);
        }

        plan ??= new CreationPlan(draft, catalog, null);
        var personal = plan.SkillKeys.Where(plan.CanTakePoints).ToList();
        Spread(plan, [.. Enumerable.Range(0, Math.Min(6, personal.Count)).Select(_ => personal[dice.Next(0, personal.Count)]).Distinct()],
            plan.PersonalBudget, occupation: false, dice);

        var gender = dice.Next(0, 2) == 0 ? NameGender.Male : NameGender.Female;
        draft.Personal.Name = NameTables.RandomName(era, gender, dice);
        draft.Personal.Gender = NameTables.GenderText(gender);

        foreach (var section in BiographyTables.Sections)
        {
            if (section.Key == "appearance")
            {
                section.Write(draft.Biography, string.Join(", ", Enumerable.Range(0, 2)
                    .Select(_ => section.Options[dice.Next(0, section.Options.Count)]).Distinct()));
                continue;
            }

            section.Append(draft.Biography, BiographyTables.Entry(section.Options, dice.Die(section.Options.Count)));
            if (section.Key == "people")
            {
                section.Append(draft.Biography, BiographyTables.Entry(BiographyTables.SignificantPeopleReasons,
                    dice.Die(BiographyTables.SignificantPeopleReasons.Count)));
            }
        }

        draft.KeyConnectionSection = BiographyTables.Sections[dice.Next(0, BiographyTables.Sections.Count)].Key;
        draft.StepIndex = (int)CreationStep.Summary;
        draft.IsRandom = true;
        return draft;
    }

    /// <summary>
    /// Запись навыка справочника: строка заводится или правится; значение не выше базы строки не требует
    /// (<see cref="SkillCatalog.IsAboveBase"/>) — «Язык, родной: 0» из файла сценария не перекрывает ОБР.
    /// </summary>
    private static void SetSkill(CharacterSheet sheet, SkillCatalog catalog, SkillDefinition skill, int value)
    {
        var existing = sheet.Entry(skill.Id);
        if (existing is not null)
        {
            existing.Value = value;
            if (existing.AddsNothing(catalog, sheet.Characteristics))
                sheet.Skills.Remove(existing);
            return;
        }

        if (SkillCatalog.IsAboveBase(skill, value, sheet.Characteristics))
            sheet.Skills.Add(new SheetSkill { SkillId = skill.Id, Value = value });
    }

    private static void SetOwnSkill(CharacterSheet sheet, SkillCatalog catalog, string name, int value)
    {
        var trimmed = name.Trim();
        var existing = sheet.Skills.FirstOrDefault(s => s.SkillId is null
                                                        && string.Equals(s.DisplayName(catalog), trimmed, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            existing.Value = value;
        else
            sheet.Skills.Add(new SheetSkill { Name = trimmed, Value = value });
    }

    /// <summary>Общий конец всех режимов: производные по правилам, ПЗ и ПМ на максимуме, Рассудок = МОЩ, Удача.</summary>
    private static void Finish(CharacterSheet sheet, SkillCatalog catalog, int luck)
    {
        DerivedAttributeRules.InitializeNewSheet(sheet, catalog);
        sheet.Current.Luck = Math.Clamp(luck, 0, DerivedAttributeRules.MaxLuck);
    }

    private static Characteristics ClampCharacteristics(Characteristics source)
    {
        var result = new Characteristics();
        foreach (var key in Enum.GetValues<Characteristic>())
            result[key] = Math.Clamp(source[key], 0, 999);
        return result;
    }

    /// <summary>«+1d4», «+1Д4», «−1», « 0 » — одно написание для сравнения с формулой; пусто — null.</summary>
    private static string? NormalizeDamageBonus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Replace(" ", "", StringComparison.Ordinal).Replace('−', '-').Replace('–', '-')
            .Replace('д', 'D').Replace('Д', 'D').Replace('d', 'D');
        if (text is "+0" or "-0" or "0" or "нет")
            return "0";
        return char.IsDigit(text[0]) && text.Contains('D', StringComparison.Ordinal) ? "+" + text : text;
    }

    /// <summary>Раскладка пунктов случайного сыщика: по 5–10 в случайные навыки, не выше 75% (лимит книги, стр. 46).</summary>
    private static void Spread(CreationPlan plan, IReadOnlyList<string> keys, int budget, bool occupation, IDiceRoller dice)
    {
        const int softCap = 75;
        var remaining = budget;
        for (var guard = 0; remaining > 0 && keys.Count > 0 && guard < 500; guard++)
        {
            var open = keys.Where(k => plan.Total(k) < softCap).ToList();
            if (open.Count == 0)
                break;

            var key = open[dice.Next(0, open.Count)];
            var step = Math.Min(remaining, Math.Min(dice.Next(5, 11), softCap - plan.Total(key)));
            var points = plan.Draft.OccupationPoints.GetValueOrDefault(key);
            var personal = plan.Draft.PersonalPoints.GetValueOrDefault(key);
            if (occupation)
                plan.SetOccupationPoints(key, points + step);
            else
                plan.SetPersonalPoints(key, personal + step);

            var spent = (occupation ? plan.Draft.OccupationPoints.GetValueOrDefault(key) - points
                : plan.Draft.PersonalPoints.GetValueOrDefault(key) - personal);
            if (spent <= 0)
                break;
            remaining -= spent;
        }
    }
}
