namespace CampaignManager.UI.Shared;

/// <summary>Сортировка списка снаружи таблицы: ключ колонки и направление.</summary>
public sealed record DataSort(string Key, bool Descending);

/// <summary>С какой ширины <c>DataTable</c> показывает таблицу вместо карточек.</summary>
public enum TableBreakpoint
{
    /// <summary>768 — таблица из четырёх-пяти колонок помещается и в портрет iPad.</summary>
    Md,

    /// <summary>1024 — портрет iPad ровно на границе.</summary>
    Lg,

    /// <summary>1280 — девять колонок (оружие, книги) в портрет iPad не помещаются: там карточки.</summary>
    Xl,
}
