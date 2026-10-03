using CampaignManager.Core.Characters;

namespace CampaignManager.UI.Characters;

/// <summary>Графа биографии: подпись, подсказка и где она лежит в документе.</summary>
public sealed record BiographyField(string Key, string Label, string Placeholder, Func<Biography, string> Get, Action<Biography, string> Set)
{
    /// <summary>Годится для самолечения в фазе развития (стр. 165): «поддерживающие» графы, не раны и не Мифы.</summary>
    public bool SupportsSelfHealing { get; init; }

    /// <summary>Длинный текст в Markdown (предыстория, заметки).</summary>
    public bool IsLong { get; init; }
}

/// <summary>
/// Графы биографии (стр. 40–43) — <b>одна таблица</b>: по ней рисуется блок биографии и строится выбор пункта
/// самолечения в фазе развития. В v1 соответствие «графа ↔ подпись» жило в четырёх местах (AUDIT, «Дубли»).
/// «Фобий» и «Магических предметов» здесь нет: фобии — записи панели рассудка, предметы — снаряжение (решение
/// владельца 2026-10-02). «Предыстория» и «Заметки» — раздельные графы (то же решение).
/// </summary>
public static class BiographyFields
{
    public static BiographyField KeyConnection { get; } = new("key", "Ключевая связь",
        "Самое важное, что держит сыщика в здравом уме", b => b.KeyConnection, (b, v) => b.KeyConnection = v);

    public static IReadOnlyList<BiographyField> Short { get; } =
    [
        new("appearance", "Описание", "Худощавый, немного заросший, неплохо одевается…", b => b.Appearance, (b, v) => b.Appearance = v),
        new("traits", "Черты характера", "Любит рептилий, осторожный, вежливый…", b => b.Traits, (b, v) => b.Traits = v) { SupportsSelfHealing = true },
        new("ideals", "Идеалы и принципы", "Поиск истины, защита невинных…", b => b.IdealsAndPrinciples, (b, v) => b.IdealsAndPrinciples = v) { SupportsSelfHealing = true },
        new("people", "Значимые люди", "Гвен — подруга по клубу египтологов…", b => b.SignificantPeople, (b, v) => b.SignificantPeople = v) { SupportsSelfHealing = true },
        new("places", "Важные места", "Рабочий кабинет в Аркхеме, книжная лавка…", b => b.ImportantPlaces, (b, v) => b.ImportantPlaces = v) { SupportsSelfHealing = true },
        new("possessions", "Ценное имущество", "Осколок метеора, найденный во Франции…", b => b.ValuablePossessions, (b, v) => b.ValuablePossessions = v) { SupportsSelfHealing = true },
        new("supernatural", "Встречи со сверхъестественным", "Встреча с крысиной тварью…", b => b.SupernaturalEncounters, (b, v) => b.SupernaturalEncounters = v),
        new("injuries", "Травмы и шрамы", "Рана от пули на левом плече…", b => b.Injuries, (b, v) => b.Injuries = v),
    ];

    public static IReadOnlyList<BiographyField> Long { get; } =
    [
        new("backstory", "Предыстория", "История жизни до начала приключений. Можно Markdown.", b => b.Backstory, (b, v) => b.Backstory = v) { IsLong = true },
        new("notes", "Заметки", "Заметки по ходу игры. Можно Markdown: заголовки, списки, *курсив*.", b => b.Notes, (b, v) => b.Notes = v) { IsLong = true },
    ];
}
