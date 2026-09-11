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

    /// <summary>
    ///     Выбор из перечисленного книгой списка: «Лазание либо Плавание», «любые два из:
    ///     Иностранный язык, Механика, Первая помощь», «четыре специализации следующих навыков…».
    /// </summary>
    Choice,

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
    /// <summary>
    ///     Навыки с широким спектром, попавшие в этот слот: из них игрок может вписать свою
    ///     специализацию («Язык, иностранный (латынь)»). У слота <see cref="OccupationSlotKind.Specialization" />
    ///     это единственный родитель, у <see cref="OccupationSlotKind.Choice" /> — те варианты списка,
    ///     что оказались родителями.
    /// </summary>
    public IReadOnlyList<string> CustomParents { get; init; } = [];

    /// <summary>Пояснение под слотом — например список, из которого книга просит выбрать.</summary>
    public string? Hint { get; init; }

    /// <summary>
    ///     Номер строки <see cref="Occupation.SkillChoices" />, из которой вырос слот, или -1.
    ///     Соседи по группе делят один пул: книга просит «четыре специализации», а не одну четырежды.
    /// </summary>
    public int ChoiceGroup { get; init; } = -1;

    /// <summary>Слоту нужен выбор игрока — конкретный навык заранее не известен.</summary>
    public bool NeedsChoice => Kind is not OccupationSlotKind.Fixed and not OccupationSlotKind.CreditRating;

    /// <summary>Специализацию можно вписать свою: книга не ограничивает их списком (стр. 52).</summary>
    public bool AllowsCustomName => CustomParents.Count > 0;

    /// <summary>Слот «любой навык» выбирается из всего списка навыков, а не из <see cref="Options" />.</summary>
    public bool ChoosesFromAllSkills => Kind is OccupationSlotKind.Any or OccupationSlotKind.Unresolved;

    /// <summary>Навык, который слот даёт без выбора.</summary>
    public string? FixedSkillName => Kind is OccupationSlotKind.Fixed or OccupationSlotKind.CreditRating
        ? Options.FirstOrDefault()
        : null;
}

/// <summary>
///     Раскладывает описание профессии на слоты навыков («Зов Ктулху» 7e, стр. 31, 38–39):
///     часть навыков названа прямо, часть выбирает игрок — социальный навык, «ещё один любой»,
///     специализация у навыков с широким спектром (Искусство/ремесло, Наука, Ближний бой,
///     Стрельба, Выживание, Язык иностранный) и выбор из перечисленного книгой списка.
/// </summary>
public static class OccupationSkillResolver
{
    public const string CreditRatingSkill = "Средства";
    public const string MythosSkill = "Мифы Ктулху";
    public const string OwnLanguageSkill = "Язык, родной";

    /// <summary>Профессиональных навыков у рода занятий всегда ровно столько плюс Средства (стр. 37).</summary>
    public const int RequiredSkillCount = 8;

    /// <summary>«Один социальный навык (Запугивание, Красноречие, Обаяние или Убеждение)» (стр. 38).</summary>
    public static readonly IReadOnlyList<string> SocialSkills =
        ["Запугивание", "Красноречие", "Обаяние", "Убеждение"];

    /// <summary>
    ///     Совместимость с уже сохранёнными справочниками: так профессии писались до того,
    ///     как их привели к именам из games."Skills". Новые данные сюда добавлять не нужно —
    ///     пишите имя навыка ровно так, как оно стоит в справочнике навыков.
    /// </summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Языки (родной)"] = OwnLanguageSkill,
        ["Языки (иностр.)"] = "Язык, иностранный",
        ["Вождение"] = "Вождение автомобиля",
        ["Упр. тяж. машинами"] = "Управление тяжёлыми машинами",
        ["Стрельба (винт./дроб.)"] = "Стрельба (винтовка/дробовик)"
    };

    /// <summary>
    ///     Сколько профессиональных навыков даёт род занятий. Средства не в счёт: у них свой
    ///     слот с диапазоном профессии. По книге должно выйти ровно <see cref="RequiredSkillCount" />.
    /// </summary>
    public static int ProfessionalSkillCount(Occupation occupation) =>
        occupation.OccupationSkills.Count(s =>
            !string.IsNullOrWhiteSpace(s) &&
            !string.Equals(Normalize(s), CreditRatingSkill, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Normalize(s), MythosSkill, StringComparison.OrdinalIgnoreCase))
        + occupation.SkillChoices.Sum(c => c.Count)
        + occupation.SocialSkillSlots
        + occupation.FreeSkillSlots;

    /// <summary>
    ///     Слоты профессии в том порядке, в каком их показывать игроку: сначала названные навыки,
    ///     затем выборы из списка, социальные слоты и свободные, Средства — всегда последними.
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
                    $"{name} (любая специализация)", name, specializations) { CustomParents = [name] });
                continue;
            }

            if (byName.Contains(name))
            {
                slots.Add(new OccupationSlot(OccupationSlotKind.Fixed, name, null, [name]));
                continue;
            }

            // «Язык, иностранный (латынь)» у Врача, «Искусство/ремесло (черчение)» у Инженера:
            // книга называет специализацию прямо, а в справочнике навыков её нет. Это по-прежнему
            // конкретный навык, а не выбор игрока, — на лист он попадёт новой специализацией.
            if (SpecializationParent(name, parents) is { } bookParent)
            {
                slots.Add(new OccupationSlot(OccupationSlotKind.Fixed, name, bookParent, [name]));
                continue;
            }

            slots.Add(new OccupationSlot(OccupationSlotKind.Unresolved, name, null, []));
        }

        for (var group = 0; group < occupation.SkillChoices.Count; group++)
        {
            var choice = occupation.SkillChoices[group];
            var (options, customParents) = ExpandOptions(choice.Options, parents, byName);
            if (options.Count == 0 && customParents.Count == 0)
                continue;

            // Пул на четыре слота (Преступник) выписывать в каждую строку незачем — он и так
            // весь лежит в выпадающем списке; подсказку показываем один раз, у первого слота.
            var pool = string.Join(" / ", choice.Options);

            for (var i = 0; i < choice.Count; i++)
            {
                var label = choice.Count == 1
                    ? string.Join(" либо ", choice.Options)
                    : $"Навык {i + 1} из {choice.Count} по выбору";

                slots.Add(new OccupationSlot(OccupationSlotKind.Choice, label, null, options)
                {
                    CustomParents = customParents,
                    ChoiceGroup = group,
                    Hint = choice.Count > 1 && i == 0 ? $"Выбор из: {pool}" : null
                });
            }
        }

        for (var i = 0; i < occupation.SocialSkillSlots; i++)
            slots.Add(new OccupationSlot(OccupationSlotKind.Social, "Социальный навык", null, SocialSkills));

        for (var i = 0; i < occupation.FreeSkillSlots; i++)
            slots.Add(new OccupationSlot(OccupationSlotKind.Any, "Любой навык на выбор", null, []));

        slots.Add(new OccupationSlot(OccupationSlotKind.CreditRating, CreditRatingSkill, null, [CreditRatingSkill]));

        return slots;
    }

    /// <summary>
    ///     Специализации, которые книга назвала прямо и которых нет в справочнике навыков:
    ///     их надо добавить на лист, иначе профессиональный навык просто исчезнет.
    ///     Ключ — имя навыка, значение — родитель.
    /// </summary>
    public static Dictionary<string, string> FixedSpecializations(IReadOnlyList<OccupationSlot> slots) =>
        slots
            .Where(s => s.Kind is OccupationSlotKind.Fixed && s.ParentSkillName is not null)
            .ToDictionary(s => s.Options[0], s => s.ParentSkillName!, StringComparer.Ordinal);

    /// <summary>
    ///     Вариант выбора «Ближний бой» означает любую его специализацию (книга просит
    ///     «четыре специализации следующих навыков»), поэтому родителей разворачиваем в список
    ///     специализаций и запоминаем, для кого игрок вправе вписать свою.
    /// </summary>
    private static (List<string> Options, List<string> CustomParents) ExpandOptions(
        IEnumerable<string> rawOptions,
        Dictionary<string, IReadOnlyList<string>> parents,
        HashSet<string> byName)
    {
        List<string> options = [];
        List<string> customParents = [];

        foreach (var raw in rawOptions)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var name = Normalize(raw);

            if (parents.TryGetValue(name, out var specializations))
            {
                customParents.Add(name);
                foreach (var specialization in specializations)
                    if (!options.Contains(specialization, StringComparer.Ordinal))
                        options.Add(specialization);
                continue;
            }

            if (byName.Contains(name) && !options.Contains(name, StringComparer.Ordinal))
                options.Add(name);
        }

        return (options, customParents);
    }

    /// <summary>Родитель для имени вида «Наука (химия)», если такой навык с широким спектром есть.</summary>
    private static string? SpecializationParent(string name, Dictionary<string, IReadOnlyList<string>> parents)
    {
        var bracket = name.IndexOf(" (", StringComparison.Ordinal);
        if (bracket <= 0 || !name.EndsWith(')'))
            return null;

        var parent = name[..bracket];
        return parents.ContainsKey(parent) ? parent : null;
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
