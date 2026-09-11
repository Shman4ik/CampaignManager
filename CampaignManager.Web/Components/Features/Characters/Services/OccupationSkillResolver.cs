using CampaignManager.Web.Components.Features.Characters.Model;

namespace CampaignManager.Web.Components.Features.Characters.Services;

/// <summary>Что за слот навыка даёт профессия (стр. 31, 38–39).</summary>
public enum OccupationSlotKind
{
    /// <summary>Конкретный навык: «Внимание».</summary>
    Fixed,

    /// <summary>Навык с широким спектром: игрок выбирает специализацию — «Искусство/ремесло (любое)».</summary>
    Specialization,

    /// <summary>«Один социальный навык (Запугивание, Красноречие, Обаяние или Убеждение)».</summary>
    Social,

    /// <summary>«И ещё один любой навык».</summary>
    Any,

    /// <summary>Средства: пункты в них вкладывают из той же суммы, но в пределах диапазона профессии.</summary>
    CreditRating,

    /// <summary>Навыка из описания профессии нет в справочнике — игрок выбирает замену сам.</summary>
    Unresolved
}

/// <summary>Один слот профессии: что предлагается выбрать и из чего.</summary>
public sealed record OccupationSlot(
    OccupationSlotKind Kind,
    string Label,
    string? ParentSkillName,
    IReadOnlyList<string> Options)
{
    /// <summary>Слоту нужен выбор игрока — конкретный навык заранее не известен.</summary>
    public bool NeedsChoice => Kind is not OccupationSlotKind.Fixed and not OccupationSlotKind.CreditRating;

    /// <summary>Специализацию можно вписать свою: книга не ограничивает их списком (стр. 52).</summary>
    public bool AllowsCustomName => Kind is OccupationSlotKind.Specialization;

    /// <summary>Слот «любой навык» выбирается из всего списка навыков, а не из <see cref="Options" />.</summary>
    public bool ChoosesFromAllSkills => Kind is OccupationSlotKind.Any or OccupationSlotKind.Unresolved;

    /// <summary>Навык, который слот даёт без выбора.</summary>
    public string? FixedSkillName => Kind is OccupationSlotKind.Fixed or OccupationSlotKind.CreditRating
        ? Options.FirstOrDefault()
        : null;
}

/// <summary>
///     Раскладывает описание профессии на слоты навыков («Зов Ктулху» 7e, стр. 31, 38–39):
///     часть навыков названа прямо, часть выбирает игрок — социальный навык, «ещё один любой»
///     и специализация у навыков с широким спектром (Искусство/ремесло, Наука, Ближний бой,
///     Стрельба, Выживание, Иностранный язык).
/// </summary>
public static class OccupationSkillResolver
{
    public const string CreditRatingSkill = "Средства";
    public const string MythosSkill = "Мифы Ктулху";
    public const string OwnLanguageSkill = "Язык, родной";

    /// <summary>«Один социальный навык (Запугивание, Красноречие, Обаяние или Убеждение)» (стр. 38).</summary>
    public static readonly IReadOnlyList<string> SocialSkills =
        ["Запугивание", "Красноречие", "Обаяние", "Убеждение"];

    /// <summary>
    ///     Справочник профессий пишет навыки короче, чем справочник навыков. Разойтись они могут
    ///     только здесь: сопоставление имён живёт в одном месте, иначе профессия молча теряет навык.
    /// </summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Языки (родной)"] = OwnLanguageSkill,
        ["Родной язык"] = OwnLanguageSkill,
        ["Языки (иностр.)"] = "Язык, иностранный",
        ["Иностранный язык"] = "Язык, иностранный",
        ["Языки"] = "Язык, иностранный",
        ["Вождение"] = "Вождение автомобиля",
        ["Вождение автомобиля или повозки"] = "Вождение автомобиля",
        ["Упр. тяж. машинами"] = "Управление тяжёлыми машинами",
        ["Стрельба (винт./дроб.)"] = "Стрельба (винтовка/дробовик)",
        ["Окультизм"] = "Оккультизм"
    };

    /// <summary>
    ///     Слоты профессии в том порядке, в каком их показывать игроку: сначала названные навыки,
    ///     затем социальные слоты, затем свободные, Средства — всегда последними.
    /// </summary>
    public static List<OccupationSlot> BuildSlots(Occupation occupation, IReadOnlyList<Skill> catalog)
    {
        var parents = ParentNames(catalog);
        var byName = catalog.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<OccupationSlot> slots = [];

        foreach (var raw in occupation.OccupationSkills)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var name = Normalize(raw);

            if (string.Equals(name, CreditRatingSkill, StringComparison.OrdinalIgnoreCase))
                continue; // Средства добавляем в конце, чтобы они всегда стояли на одном месте.

            if (string.Equals(name, MythosSkill, StringComparison.OrdinalIgnoreCase))
                continue; // Мифы Ктулху пунктами навыков не покупают (стр. 34).

            if (parents.TryGetValue(name, out var specializations))
            {
                slots.Add(new OccupationSlot(OccupationSlotKind.Specialization,
                    $"{name} (любая специализация)", name, specializations));
                continue;
            }

            slots.Add(byName.Contains(name)
                ? new OccupationSlot(OccupationSlotKind.Fixed, name, null, [name])
                : new OccupationSlot(OccupationSlotKind.Unresolved, name, null, []));
        }

        for (var i = 0; i < occupation.SocialSkillSlots; i++)
            slots.Add(new OccupationSlot(OccupationSlotKind.Social, "Социальный навык", null, SocialSkills));

        for (var i = 0; i < occupation.FreeSkillSlots; i++)
            slots.Add(new OccupationSlot(OccupationSlotKind.Any, "Любой навык на выбор", null, []));

        slots.Add(new OccupationSlot(OccupationSlotKind.CreditRating, CreditRatingSkill, null, [CreditRatingSkill]));

        return slots;
    }

    /// <summary>
    ///     Навыки, у которых в справочнике есть специализации: их нельзя взять «вообще»,
    ///     пункты вкладывают в конкретную специализацию (стр. 52).
    /// </summary>
    public static Dictionary<string, IReadOnlyList<string>> ParentNames(IReadOnlyList<Skill> catalog)
    {
        return catalog
            .Where(s => !string.IsNullOrWhiteSpace(s.ParentSkillName))
            .GroupBy(s => s.ParentSkillName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(s => s.Name).OrderBy(n => n).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Имя навыка из описания профессии в том виде, в каком оно лежит в справочнике навыков.</summary>
    public static string Normalize(string occupationSkillName)
    {
        var name = occupationSkillName.Trim();
        return Aliases.TryGetValue(name, out var alias) ? alias : name;
    }

    /// <summary>
    ///     Базовое значение для новой специализации, которой ещё нет на листе:
    ///     берём его у любой соседней специализации того же навыка, иначе 1% (стр. 52).
    /// </summary>
    public static Skill CreateSpecialization(string name, string parentSkillName, IReadOnlyList<Skill> catalog)
    {
        var sibling = catalog.FirstOrDefault(s =>
            string.Equals(s.ParentSkillName, parentSkillName, StringComparison.OrdinalIgnoreCase));

        var baseValue = sibling?.Value.Regular ?? 1;

        return new Skill
        {
            Name = name,
            BaseValue = $"{baseValue:00}%",
            Value = new AttributeValue(baseValue),
            ParentSkillName = parentSkillName
        };
    }
}
