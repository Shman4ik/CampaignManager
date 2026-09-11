using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Skills.Services;
using CampaignManager.Web.Components.Shared.Model;

namespace CampaignManager.Web.Components.Features.Characters.Services;

/// <summary>
///     Собирает лист сыщика из черновика помощника создания («Зов Ктулху» 7e, глава 3).
///     Все формулы берутся из <see cref="DerivedAttributeRules" />, <see cref="FinanceRules" /> и
///     <see cref="InvestigatorCreationRules" /> — здесь только сборка, своей арифметики нет.
/// </summary>
public sealed class InvestigatorFactory(SkillService skillService)
{
    /// <summary>Справочник навыков для выбранной эпохи — основа листа и список для выбора в мастере.</summary>
    public Task<SkillsModel> BuildCatalogAsync(bool modernEra) =>
        skillService.BuildDefaultSkillsModelAsync(modernEra ? Eras.Modern : Eras.Classic);

    /// <summary>
    ///     Базовый шанс навыка: у Уклонения это половина ЛВК, у родного языка — ОБР (стр. 57, 77),
    ///     у остальных — значение из справочника.
    /// </summary>
    public static int BaseValue(Skill catalogSkill, Characteristics characteristics) => catalogSkill.Name switch
    {
        "Уклонение" => DerivedAttributeRules.ComputeDodge(characteristics),
        OccupationSkillResolver.OwnLanguageSkill => characteristics.Education.Regular,
        _ => catalogSkill.Value.Regular
    };

    public static Characteristics BuildCharacteristics(InvestigatorDraft draft)
    {
        var band = InvestigatorCreationRules.BandFor(draft.Age);
        return new Characteristics
        {
            Strength = new AttributeValue(draft.Value(CharacteristicKey.Strength, band)),
            Constitution = new AttributeValue(draft.Value(CharacteristicKey.Constitution, band)),
            Size = new AttributeValue(draft.Value(CharacteristicKey.Size, band)),
            Dexterity = new AttributeValue(draft.Value(CharacteristicKey.Dexterity, band)),
            Appearance = new AttributeValue(draft.Value(CharacteristicKey.Appearance, band)),
            Intelligence = new AttributeValue(draft.Value(CharacteristicKey.Intelligence, band)),
            Power = new AttributeValue(draft.Value(CharacteristicKey.Power, band)),
            Education = new AttributeValue(draft.Value(CharacteristicKey.Education, band))
        };
    }

    /// <summary>Очки личного интереса: ИНТ × 2 (стр. 34).</summary>
    public static int PersonalPointsFor(Characteristics characteristics) =>
        characteristics.Intelligence.Regular * 2;

    /// <summary>
    ///     Очки профессиональных навыков. Когда формула профессии даёт выбор, берём выбранную
    ///     игроком характеристику — книга предлагает выбор, а не максимум (стр. 38–39).
    /// </summary>
    public static int OccupationPointsFor(Occupation? occupation, Characteristics c, CharacteristicKey? choice)
    {
        if (occupation is null)
            return 0;

        var edu = c.Education.Regular;

        return choice is { } key
            ? edu * 2 + Read(c, key) * 2
            : occupation.CalculateSkillPoints(c);
    }

    /// <summary>Характеристики, между которыми формула профессии предлагает выбрать (стр. 38–39).</summary>
    public static IReadOnlyList<CharacteristicKey> FormulaChoices(OccupationSkillPointFormula formula) => formula switch
    {
        OccupationSkillPointFormula.Edu2DexOrStr2 => [CharacteristicKey.Dexterity, CharacteristicKey.Strength],
        OccupationSkillPointFormula.Edu2AppOrPow2 => [CharacteristicKey.Appearance, CharacteristicKey.Power],
        OccupationSkillPointFormula.Edu2DexOrPow2 => [CharacteristicKey.Dexterity, CharacteristicKey.Power],
        OccupationSkillPointFormula.Edu2AppOrDexOrStr2 =>
            [CharacteristicKey.Appearance, CharacteristicKey.Dexterity, CharacteristicKey.Strength],
        _ => []
    };

    public static int Read(Characteristics c, CharacteristicKey key) => key switch
    {
        CharacteristicKey.Strength => c.Strength.Regular,
        CharacteristicKey.Constitution => c.Constitution.Regular,
        CharacteristicKey.Size => c.Size.Regular,
        CharacteristicKey.Dexterity => c.Dexterity.Regular,
        CharacteristicKey.Appearance => c.Appearance.Regular,
        CharacteristicKey.Intelligence => c.Intelligence.Regular,
        CharacteristicKey.Power => c.Power.Regular,
        _ => c.Education.Regular
    };

    /// <summary>
    ///     Навыки, которые игрок уже закрепил за слотами профессии. Пустые строки — слоты,
    ///     где выбор ещё не сделан.
    /// </summary>
    public static List<string> ChosenOccupationSkills(IReadOnlyList<OccupationSlot> slots, InvestigatorDraft draft)
    {
        List<string> chosen = [];

        for (var i = 0; i < slots.Count; i++)
        {
            var name = slots[i].FixedSkillName ?? (i < draft.SlotChoices.Count ? draft.SlotChoices[i] : "");
            if (!string.IsNullOrWhiteSpace(name) && !chosen.Contains(name, StringComparer.Ordinal))
                chosen.Add(name);
        }

        return chosen;
    }

    /// <summary>Вторичные атрибуты для показа прямо в мастере — считает всё тот же <see cref="DerivedAttributeRules" />.</summary>
    public sealed record DerivedPreview(
        int HitPoints,
        int MagicPoints,
        int Sanity,
        int MoveRate,
        string Build,
        string DamageBonus,
        int Dodge);

    public static DerivedPreview Preview(InvestigatorDraft draft)
    {
        var c = BuildCharacteristics(draft);
        var (build, damageBonus) = DerivedAttributeRules.ComputeBuildAndDamageBonus(c);

        return new DerivedPreview(
            DerivedAttributeRules.ComputeMaxHitPoints(c),
            DerivedAttributeRules.ComputeMaxMagicPoints(c),
            c.Power.Regular,
            DerivedAttributeRules.ComputeMoveRate(c, draft.Age),
            build,
            damageBonus,
            DerivedAttributeRules.ComputeDodge(c));
    }

    /// <summary>
    ///     Полный список навыков листа: справочник эпохи плюс специализации, которые игрок
    ///     добавил сам (латынь у врача, химия у профессора и т. п.).
    /// </summary>
    public static SkillsModel BuildSkillList(SkillsModel catalog, InvestigatorDraft draft)
    {
        var result = new SkillsModel
        {
            SkillGroups = catalog.SkillGroups
                .Select(g => new SkillGroup
                {
                    Name = g.Name,
                    Skills = g.Skills.Select(CopySkill).ToList()
                })
                .ToList()
        };

        var all = result.SkillGroups.SelectMany(g => g.Skills).ToList();

        foreach (var (name, parent) in draft.AddedSpecializations)
        {
            if (all.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
                continue;

            var group = result.SkillGroups.FirstOrDefault(g =>
                            g.Skills.Any(s => string.Equals(s.ParentSkillName, parent, StringComparison.OrdinalIgnoreCase)))
                        ?? result.SkillGroups.First();

            group.Skills.Add(OccupationSkillResolver.CreateSpecialization(name, parent, all));
        }

        return result;
    }

    private static Skill CopySkill(Skill source) => new()
    {
        Name = source.Name,
        BaseValue = source.BaseValue,
        Value = new AttributeValue(source.Value.Regular),
        SkillModelId = source.SkillModelId,
        ParentSkillName = source.ParentSkillName
    };

    /// <summary>Готовый лист сыщика: характеристики, вторичные атрибуты, навыки, биография и деньги.</summary>
    public Character Build(InvestigatorDraft draft, SkillsModel catalog, Occupation? occupation)
    {
        var characteristics = BuildCharacteristics(draft);

        var character = new Character
        {
            Characteristics = characteristics,
            Skills = BuildSkillList(catalog, draft),
            PersonalInfo = new PersonalInfo
            {
                Name = draft.Info.Name,
                PlayerName = draft.Info.PlayerName,
                Gender = draft.Info.Gender,
                Age = draft.Age,
                Birthplace = draft.Info.Birthplace,
                Residence = draft.Info.Residence,
                Occupation = occupation?.Name ?? draft.OccupationName
            },
            Biography = draft.Biography,
            Equipment = new Equipment { Items = draft.Equipment },
            Backstory = string.Empty,
            Notes = string.Empty
        };

        ApplySkillValues(character, draft);

        // ПЗ, ПМ, Рассудок, СКО, комплексия, БкУ и уклонение — по общим правилам главы 3.
        DerivedAttributeRules.InitializeNewSheet(character);
        character.DerivedAttributes.Luck = new AttributeWithMaxValue(draft.Luck, DerivedAttributeRules.MaxLuck);

        ApplyFinances(character, draft);

        return character;
    }

    /// <summary>
    ///     Раскладывает вложенные пункты по навыкам. Родной язык и Уклонение получают базу от
    ///     характеристик, поэтому считаются здесь же, а не остаются нулями из справочника.
    /// </summary>
    private static void ApplySkillValues(Character character, InvestigatorDraft draft)
    {
        foreach (var skill in character.Skills.SkillGroups.SelectMany(g => g.Skills))
        {
            var value = BaseValue(skill, character.Characteristics)
                        + draft.OccupationPoints.GetValueOrDefault(skill.Name)
                        + draft.PersonalPoints.GetValueOrDefault(skill.Name);

            if (string.Equals(skill.Name, OccupationSkillResolver.CreditRatingSkill, StringComparison.Ordinal))
                value = draft.CreditRating;

            skill.Value.Regular = Math.Min(value, 99);
            skill.Value.UpdateDerived();
        }

        // Уклонение живёт и навыком, и боевым параметром — хозяин навык (см. Characters/CLAUDE.md).
        DerivedAttributeRules.SyncDodgeFromSkill(character);
    }

    /// <summary>Наличные, активы и карманные деньги по таблице II (стр. 45).</summary>
    private static void ApplyFinances(Character character, InvestigatorDraft draft)
    {
        var tier = FinanceRules.GetTier(draft.CreditRating, draft.ModernEra);

        character.Finances = new Finances
        {
            Cash = tier.CashText,
            PocketMoney = tier.PocketMoneyText,
            Assets = tier.Assets is null ? [] : [tier.AssetsText]
        };
    }
}
