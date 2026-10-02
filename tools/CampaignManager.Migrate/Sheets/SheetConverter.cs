using System.Globalization;
using System.Text.Json.Nodes;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Migrate.Catalogs;
using CampaignManager.Migrate.V1;

namespace CampaignManager.Migrate.Sheets;

/// <summary>Что перенос узнал о листе, кроме самого документа: для отчёта.</summary>
public sealed class SheetNotes
{
    /// <summary>Навыки без справочника: «имя» → как легли (своя специализация или самодельный).</summary>
    public List<string> UnmatchedSkills { get; } = [];

    /// <summary>Написания v1, которые нашлись по старому имени.</summary>
    public List<string> MappedSkills { get; } = [];

    /// <summary>Навыки только современной эпохи на базовом значении — выброшены.</summary>
    public List<string> DroppedSkills { get; } = [];

    /// <summary>Значения книги, не совпавшие с вычисленными (у НПС ушли в overrides).</summary>
    public List<string> Overrides { get; } = [];

    /// <summary>Расхождения у сыщиков и прегенов: вычисленное побеждает, в отчёт.</summary>
    public List<string> Discrepancies { get; } = [];

    /// <summary>
    /// Непустые графы биографии v1, которых в 2.0 нет («Фобии», «Магические предметы» — решение владельца
    /// 2026-10-02): текст отброшен, в отчёт — какие графы были заполнены.
    /// </summary>
    public List<string> DroppedBiography { get; } = [];

    public int CheckedDropped { get; set; }

    public int WeaponsLinked { get; set; }
}

/// <summary>
/// Лист v1 (<c>Characters.Character</c>, PascalCase) → <see cref="CharacterSheet"/> версии 1 (SCHEMA,
/// «Персонажи»). Не апкастер Core: ему нужны справочник навыков и старые написания имён.
/// <list type="bullet">
/// <item>в документ идёт только то, что вводит человек: половины, пятые, максимумы, БкУ, Комплекция,
/// Скорость и Уклонение-зеркало вычисляются; у НПС расхождение с формулой уходит в <c>overrides</c>;</item>
/// <item><c>Id</c> внутри JSON, <c>CharacterType</c>, <c>NewSkillName</c>/<c>NewSkillBaseValue</c>, группы
/// навыков и <c>BaseValue</c>-строки не переносятся; <c>PlayerName</c> — из владельца строки;</item>
/// <item>навык — по <c>SkillModelId</c>, затем по имени (<see cref="SkillNameResolver"/>), иначе самодельный;</item>
/// <item>оружие — текст книги и <c>catalogWeaponId</c>; разобранные блоки v1 не переносятся;</item>
/// <item>деньги — числом, нечисловой остаток — в заметку финансов.</item>
/// </list>
/// </summary>
public sealed class SheetConverter(
    SkillCatalog catalog,
    SkillNameResolver resolver,
    IReadOnlyDictionary<Guid, (string Name, Guid SkillId)> catalogWeapons,
    IReadOnlyDictionary<Guid, string> catalogSpells,
    IReadOnlyDictionary<string, Guid> occupationsByName)
{
    /// <summary>Навыки только современной эпохи (убраны из справочника): на базе — выбросить, выше — свой навык.</summary>
    private static readonly IReadOnlyDictionary<string, int> ModernSkillBases = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["Работа с компьютером"] = 5,
        ["Электроника"] = 1,
        ["Ближний бой (бензопила)"] = 10,
    };

    /// <summary>Сокращения навыка в оружии листа v1 (стандартный лист и генератор).</summary>
    private static readonly IReadOnlyDictionary<string, string> WeaponSkillShorthand = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Стрельба (П)"] = "skill.firearms.handgun",
        ["Стрельба (В/Д)"] = "skill.firearms.rifle-shotgun",
        ["Стрельба (ПП)"] = "skill.firearms.submachine-gun",
        ["Ближний бой"] = "skill.fighting.brawl",
    };

    public (CharacterSheet Sheet, SheetNotes Notes) Convert(JsonNode v1, bool isNpc)
    {
        var notes = new SheetNotes();
        var personal = v1.Obj("PersonalInfo");
        var occupation = personal.Text("Occupation") ?? "";
        var sheet = new CharacterSheet
        {
            Personal = new PersonalInfo
            {
                Name = personal.Text("Name") ?? "",
                Occupation = occupation,
                OccupationId = occupationsByName.TryGetValue(CatalogCodeTable.NormalizeName(occupation), out var occupationId) ? occupationId : null,
                Age = personal.Int("Age") ?? 0,
                Gender = personal.Text("Gender") ?? "",
                Birthplace = personal.Text("Birthplace") ?? "",
                Residence = personal.Text("Residence") ?? "",
            },
            Characteristics = Characteristics(v1.Obj("Characteristics")),
        };

        var derived = v1.Obj("DerivedAttributes");
        sheet.Current = new CurrentValues
        {
            HitPoints = derived.Obj("HitPoints").Int("Value") ?? 0,
            MagicPoints = derived.Obj("MagicPoints").Int("Value") ?? 0,
            Sanity = derived.Obj("Sanity").Int("Value") ?? 0,
            Luck = derived.Obj("Luck").Int("Value") ?? 0,
        };

        Skills(v1, sheet, isNpc, notes);
        Overrides(v1, sheet, isNpc, notes);
        Weapons(v1, sheet, notes);
        Spells(v1, sheet);

        foreach (var item in v1.Obj("Equipment").Arr("Items").OfType<JsonObject>())
        {
            if (item.Text("Name") is { } name)
            {
                sheet.Equipment.Add(new EquipmentItem { Name = name, Description = item.Text("Description") ?? "" });
            }
        }

        sheet.Finances = Finances(v1.Obj("Finances"));
        sheet.Biography = Biography(v1, notes);
        sheet.Condition = Condition(v1.Obj("State"));

        foreach (var condition in v1.Arr("InsanityConditions").OfType<JsonObject>())
        {
            sheet.InsanityConditions.Add(new InsanityCondition
            {
                Id = condition.Guid("Id") ?? Guid.CreateVersion7(),
                Kind = condition.Enum<InsanityConditionKind>("Kind") ?? InsanityConditionKind.Phobia,
                Name = condition.Text("Name") ?? "",
                Description = condition.Text("Description"),
                AcquiredAt = condition.Time("AcquiredAt") ?? default,
                Active = condition["Active"] is null || condition.Bool("Active"),
            });
        }

        foreach (var book in v1.Arr("MythosBooks").OfType<JsonObject>())
        {
            sheet.MythosBooks.Add(MythosBook(book));
        }

        foreach (var fellow in v1.Arr("FellowInvestigators").OfType<JsonObject>())
        {
            sheet.FellowInvestigators.Add(new FellowInvestigator
            {
                CharacterId = fellow.Guid("CharacterId"),
                Name = fellow.Text("Name") ?? "",
                PlayerName = fellow.Text("PlayerName") ?? "",
                Note = fellow.Text("Note") ?? "",
            });
        }

        return (sheet, notes);
    }

    private static Characteristics Characteristics(JsonObject? c) => new()
    {
        Str = c.Obj("Strength").Int("Regular") ?? 0,
        Con = c.Obj("Constitution").Int("Regular") ?? 0,
        Siz = c.Obj("Size").Int("Regular") ?? 0,
        Dex = c.Obj("Dexterity").Int("Regular") ?? 0,
        App = c.Obj("Appearance").Int("Regular") ?? 0,
        Int = c.Obj("Intelligence").Int("Regular") ?? 0,
        Pow = c.Obj("Power").Int("Regular") ?? 0,
        Edu = c.Obj("Education").Int("Regular") ?? 0,
    };

    private void Skills(JsonNode v1, CharacterSheet sheet, bool isNpc, SheetNotes notes)
    {
        foreach (var group in v1.Obj("Skills").Arr("SkillGroups").OfType<JsonObject>())
        {
            foreach (var skill in group.Arr("Skills").OfType<JsonObject>())
            {
                var name = skill.Text("Name");
                if (name is null)
                {
                    continue;
                }

                var value = skill.Obj("Value").Int("Regular") ?? 0;
                // Отметку у НПС ставил импорт сценария (у всех его навыков разом) — для НПС она смысла не имеет
                var used = skill.Bool("IsUsed");
                if (used && isNpc)
                {
                    notes.CheckedDropped++;
                    used = false;
                }

                if (ModernSkillBases.TryGetValue(name, out var modernBase) && value <= modernBase && !used)
                {
                    notes.DroppedSkills.Add(name);
                    continue;
                }

                var entry = new SheetSkill { Value = value, Checked = used };
                if (skill.Guid("SkillModelId") is { } modelId && catalog.Find(modelId) is { } byId)
                {
                    entry.SkillId = byId.Id;
                }
                else
                {
                    switch (resolver.Resolve(name))
                    {
                        case SkillMatch.Catalog { Skill: var found }:
                            entry.SkillId = found.Id;
                            if (!string.Equals(found.Name, name, StringComparison.OrdinalIgnoreCase))
                            {
                                notes.MappedSkills.Add($"«{name}» → «{found.Name}»");
                            }

                            break;
                        case SkillMatch.Specialization { Parent: var parent, Name: var specialization }:
                            entry.ParentSkillId = parent.Id;
                            entry.Name = specialization;
                            notes.UnmatchedSkills.Add($"«{name}» → своя специализация «{parent.Name} ({specialization})»");
                            break;
                        default:
                            entry.Name = name;
                            notes.UnmatchedSkills.Add($"«{name}» (группа «{group.Text("Name")}») → самодельный навык");
                            break;
                    }
                }

                // Повтор того же навыка справочника на листе v1 — оставляем большее значение
                if (entry.SkillId is { } id && sheet.Skills.FirstOrDefault(s => s.SkillId == id) is { } existing)
                {
                    existing.Value = Math.Max(existing.Value, entry.Value);
                    existing.Checked |= entry.Checked;
                    notes.MappedSkills.Add($"«{name}» повторял навык на листе — слит, значение {existing.Value}");
                    continue;
                }

                sheet.Skills.Add(entry);
            }
        }
    }

    /// <summary>
    /// Максимумы и прочее вычисляемое v1 против формулы Core. У НПС (лист из книги) расхождение — значение
    /// книги, оно уходит в <see cref="SheetOverrides"/>; у сыщиков и прегенов побеждает формула, разница — в отчёт.
    /// «Пустые» значения v1 (максимум Рассудка 99 по умолчанию, пустые БкУ и Комплекция) расхождением не считаются.
    /// </summary>
    private void Overrides(JsonNode v1, CharacterSheet sheet, bool isNpc, SheetNotes notes)
    {
        var computed = DerivedAttributeRules.Compute(sheet, catalog);
        var derived = v1.Obj("DerivedAttributes");
        var personal = v1.Obj("PersonalInfo");
        var o = new SheetOverrides();

        void Compare(string what, string? v1Value, string computedValue, Action apply)
        {
            if (v1Value is null || string.Equals(Normalize(v1Value), Normalize(computedValue), StringComparison.Ordinal))
            {
                return;
            }

            if (isNpc)
            {
                apply();
                notes.Overrides.Add($"{what}: книга {v1Value}, формула {computedValue}");
            }
            else
            {
                notes.Discrepancies.Add($"{what}: в v1 {v1Value}, по формуле {computedValue}");
            }
        }

        // Максимум 0 — v1 его не заполнил (лист НПС из импорта), а не «ноль ПЗ»
        var maxHp = derived.Obj("HitPoints").Int("MaxValue") is > 0 and var hp ? hp : (int?)null;
        Compare("ПЗ max", maxHp?.ToString(CultureInfo.InvariantCulture), computed.MaxHitPoints.ToString(CultureInfo.InvariantCulture),
            () => o.MaxHitPoints = maxHp);
        var maxMp = derived.Obj("MagicPoints").Int("MaxValue") is > 0 and var mp ? mp : (int?)null;
        Compare("ПМ max", maxMp?.ToString(CultureInfo.InvariantCulture), computed.MaxMagicPoints.ToString(CultureInfo.InvariantCulture),
            () => o.MaxMagicPoints = maxMp);
        // Книга печатает у НПС текущий Рассудок, не максимум; v1 клал его в оба поля (55/55) или оставлял 99.
        // Максимум из книги — только если он отличается и от 99, и от текущего.
        var sanity = derived.Obj("Sanity");
        var maxSanity = sanity.Int("MaxValue") is { } san && san is not (99 or 0) && san != sanity.Int("Value") ? san : (int?)null;
        Compare("Рассудок max", maxSanity?.ToString(CultureInfo.InvariantCulture),
            computed.MaxSanity.ToString(CultureInfo.InvariantCulture), () => o.MaxSanity = maxSanity);
        var damageBonus = personal.Text("DamageBonus");
        Compare("БкУ", damageBonus, computed.DamageBonus, () => o.DamageBonus = damageBonus);
        var build = personal.Text("Build");
        Compare("Комплекция", build, computed.Build.ToString(CultureInfo.InvariantCulture),
            () => o.Build = int.TryParse(build, NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) ? b : null);
        var move = personal.Int("MoveSpeed");
        Compare("Скорость", move is null or 0 ? null : move.Value.ToString(CultureInfo.InvariantCulture),
            computed.Move.ToString(CultureInfo.InvariantCulture), () => o.Move = move);

        sheet.Overrides = o;
    }

    /// <summary>«+1D4», «1d4», «+1d4» — одно и то же; «0» и «+0» тоже.</summary>
    private static string Normalize(string value) =>
        value.Replace(" ", "", StringComparison.Ordinal).Replace('д', 'd').Replace('Д', 'd').TrimStart('+').ToLowerInvariant();

    private void Weapons(JsonNode v1, CharacterSheet sheet, SheetNotes notes)
    {
        foreach (var weapon in v1.Arr("Weapons").OfType<JsonObject>())
        {
            var name = weapon.Text("Name") ?? "";
            var catalogId = weapon.Guid("CatalogWeaponId") is { } id && catalogWeapons.ContainsKey(id)
                ? id
                : catalogWeapons.FirstOrDefault(pair => string.Equals(pair.Value.Name, name, StringComparison.OrdinalIgnoreCase)).Key;
            Guid? linked = catalogId == Guid.Empty ? null : catalogId;
            notes.WeaponsLinked += linked is null ? 0 : 1;

            var skillName = weapon.Text("Skill");
            Guid? skillId = weapon.Guid("SkillId") is { } sid && catalog.Find(sid) is not null ? sid
                : skillName is not null && WeaponSkillShorthand.TryGetValue(skillName, out var code) ? catalog.FindByCode(code)?.Id
                : resolver.CatalogId(skillName)
                  ?? (linked is { } weaponId ? catalogWeapons[weaponId].SkillId : null);

            sheet.Weapons.Add(new SheetWeapon
            {
                RowId = weapon.Guid("Id") ?? Guid.CreateVersion7(),
                CatalogWeaponId = linked,
                Name = name,
                SkillId = skillId,
                Damage = weapon.Text("Damage") ?? "",
                Range = weapon.Text("Range") ?? "",
                Attacks = weapon.Text("Attacks") ?? "",
                Ammo = weapon.Text("Ammo") ?? "",
                Malfunction = weapon.Text("Malfunction") ?? "",
                Impaling = weapon.Bool("IsImpaling"),
                Notes = Steps.CatalogStep.WeaponNotes(weapon.Str("Notes")),
            });
        }
    }

    private void Spells(JsonNode v1, CharacterSheet sheet)
    {
        foreach (var spell in v1.Arr("Spells").OfType<JsonObject>())
        {
            var name = spell.Text("Name") ?? "";
            Guid? catalogId = spell.Guid("Id") is { } id && catalogSpells.ContainsKey(id) ? id
                : catalogSpells.FirstOrDefault(pair => string.Equals(pair.Value, name, StringComparison.OrdinalIgnoreCase)).Key is var byName && byName != Guid.Empty ? byName
                : null;
            sheet.Spells.Add(new SheetSpell
            {
                CatalogSpellId = catalogId,
                Name = name,
                AlternativeNames = spell.Strings("AlternativeNames"),
                Cost = spell.Text("Cost") ?? "",
                CastingTime = spell.Text("CastingTime") ?? "",
                Description = spell.Str("Description") ?? "",
            });
        }
    }

    /// <summary>Деньги v1 строкой («$120», «80 долларов», «110») → число; нечисловое — в заметку.</summary>
    public static Finances Finances(JsonObject? v1)
    {
        var finances = new Finances
        {
            Cash = TryParseMoney(v1.Text("Cash")),
            PocketMoney = TryParseMoney(v1.Text("PocketMoney")),
            Assets = string.Join("; ", v1.Strings("Assets")),
        };
        List<string> unparsed = [];
        if (v1.Text("Cash") is { } cash && finances.Cash is null)
        {
            unparsed.Add($"Наличные: {cash}");
        }

        if (v1.Text("PocketMoney") is { } pocket && finances.PocketMoney is null)
        {
            unparsed.Add($"Карманные: {pocket}");
        }

        finances.Note = string.Join("; ", unparsed);
        return finances;
    }

    /// <summary>
    /// Разбор денег v1 (<c>FinanceRules.TryParseMoney</c>): цифры, точка и запятая; несколько точек —
    /// не число («1.234.56» — мусор ручного ввода). Остался только переносу (rules-findings F-S06).
    /// </summary>
    public static decimal? TryParseMoney(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var digits = new string(text.Where(ch => char.IsDigit(ch) || ch is '.' or ',').ToArray()).Replace(',', '.');
        if (digits.Count(ch => ch == '.') > 1)
        {
            return null;
        }

        return decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    /// <summary>Графы v1, которых в 2.0 нет: текст выбрасывается, в отчёт — подпись графы.</summary>
    private static readonly (string Key, string Label)[] DroppedBiographyFields =
    [
        ("Phobias", "Фобии"),
        ("MagicalItems", "Магические предметы"),
    ];

    private static Biography Biography(JsonNode v1, SheetNotes notes)
    {
        var b = v1.Obj("Biography");
        foreach (var (key, label) in DroppedBiographyFields)
        {
            if (!string.IsNullOrWhiteSpace(b.Str(key)))
            {
                notes.DroppedBiography.Add(label);
            }
        }

        return new Biography
        {
            Appearance = b.Str("Appearance") ?? "",
            Traits = b.Str("Traits") ?? "",
            IdealsAndPrinciples = b.Str("IdealsAndPrinciples") ?? "",
            SignificantPeople = b.Str("SignificantPeople")?.Trim() ?? "",
            ImportantPlaces = b.Str("ImportantPlaces") ?? "",
            ValuablePossessions = b.Str("ValuablePossessions") ?? "",
            SupernaturalEncounters = b.Str("SupernaturalEncounters") ?? "",
            Injuries = b.Str("Injuries") ?? "",
            KeyConnection = b.Str("KeyConnection") ?? "",
            Backstory = v1.Str("Backstory") ?? "",
            Notes = v1.Str("Notes") ?? "",
        };
    }

    private static SheetCondition Condition(JsonObject? state)
    {
        var condition = new SheetCondition
        {
            Unconscious = state.Bool("IsUnconscious"),
            MajorWound = state.Bool("HasSeriousInjury"),
            Dying = state.Bool("IsDying"),
            TemporaryInsanity = state.Bool("HasTemporaryInsanity"),
            IndefiniteInsanity = state.Bool("HasIndefiniteInsanity"),
            TemporaryInsanityStartedAt = state.Time("TemporaryInsanityStartedAt"),
            IndefiniteInsanityStartedAt = state.Time("IndefiniteInsanityStartedAt"),
            SanityLostToday = state.Int("SanityLossEpisode") ?? 0,
            LastSanityLoss = state.Int("LastSanityLoss") ?? 0,
            MythosInsanityCount = state.Int("MythosInsanityCount") ?? 0,
            BoutDue = state.Bool("InsanityBoutDue"),
        };
        if (state.Obj("LastInsanityBout") is { } bout)
        {
            condition.LastBout = new InsanityBout
            {
                Mode = bout.Enum<InsanityBoutMode>("Mode") ?? InsanityBoutMode.RealTime,
                Roll = bout.Int("Roll") ?? 0,
                Duration = bout.Int("Duration"),
                RolledAt = bout.Time("RolledAt") ?? default,
            };
        }

        foreach (var habituation in state.Arr("MythosHabituations").OfType<JsonObject>())
        {
            condition.Habituations.Add(new MythosHabituation
            {
                CreatureId = habituation.Guid("CreatureId"),
                CreatureName = habituation.Text("CreatureName") ?? "",
                MaxLoss = habituation.Int("MaxLoss") ?? 0,
                LostSanity = habituation.Int("LostSanity") ?? 0,
                SanityLossFormula = habituation.Text("SanityLossFormula"),
            });
        }

        return condition;
    }

    private static MythosBookRecord MythosBook(JsonObject book)
    {
        var record = new MythosBookRecord
        {
            Id = book.Guid("Id") ?? Guid.CreateVersion7(),
            BookId = book.Guid("BookId"),
            Name = book.Text("Name") ?? "",
            Language = book.Text("Language"),
            SanityLoss = book.Text("SanityLoss"),
            MythosInitial = book.Int("MythosInitial") ?? 0,
            MythosFull = book.Int("MythosFull") ?? 0,
            MythosRating = book.Int("MythosRating") ?? 0,
            StudyWeeks = book.Int("StudyWeeks"),
            PossibleSpells = book.Strings("PossibleSpells"),
            Stage = book.Enum<MythosBookStage>("Stage") ?? MythosBookStage.NotRead,
            FullStudyCount = book.Int("FullStudyCount") ?? 0,
            Note = book.Text("Note") ?? "",
        };
        foreach (var reading in book.Arr("Readings").OfType<JsonObject>())
        {
            record.Readings.Add(new MythosBookReadingEntry
            {
                Stage = reading.Enum<MythosBookStage>("Stage") ?? MythosBookStage.InitialReading,
                GameDate = reading.Text("GameDate"),
                SanityLost = reading.Int("SanityLost") ?? 0,
                MythosGained = reading.Int("MythosGained") ?? 0,
                Disbelieved = reading.Bool("Disbelieved"),
                SpellsLearned = reading.Strings("SpellsLearned"),
            });
        }

        return record;
    }
}
