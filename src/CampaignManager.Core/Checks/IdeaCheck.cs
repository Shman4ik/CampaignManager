using CampaignManager.Core.Characters;

namespace CampaignManager.Core.Checks;

/// <summary>Как Хранитель подавал пропущенную зацепку (стр. 197): от этого — сложность проверки Идеи.</summary>
public enum IdeaClue
{
    /// <summary>Не упоминалась — обычная: нельзя ждать, что игроки угадают то, о чём не слышали.</summary>
    NotMentioned,

    /// <summary>Упоминалась, но не подчёркивалась — трудная; сложность по умолчанию.</summary>
    Mentioned,

    /// <summary>Хранитель прямо указал на неё или игроки сами её обсуждали — чрезвычайная.</summary>
    Emphasized,
}

/// <summary>
/// Проверка Идеи (гл. 10, стр. 197–198): возвращает застрявшее расследование в русло. Зацепку сыщики получат при любом исходе —
/// проверка решает, как: успех — спокойно, провал — в гуще событий. Чем заметнее была зацепка, тем выше сложность: игроки
/// упустили свой шанс. Бросает ИНТ один игрок — тот, у чьего сыщика ИНТ выше всех.
/// </summary>
public static class IdeaCheck
{
    /// <summary>Ключ цели проверки — характеристика ИНТ (<see cref="CheckSubject.Key"/>).</summary>
    public const string SubjectKey = "char:INT";

    public static Difficulty DifficultyOf(IdeaClue clue) => clue switch
    {
        IdeaClue.NotMentioned => Difficulty.Regular,
        IdeaClue.Mentioned => Difficulty.Hard,
        _ => Difficulty.Extreme,
    };

    /// <summary>Кто бросает: живой сыщик с наибольшим ИНТ; при равенстве — первый в списке; живых нет — никто.</summary>
    public static T? Roller<T>(IEnumerable<T> party, Func<T, CharacterSheet> sheet) where T : class =>
        party.Where(member => !sheet(member).Condition.Dead).MaxBy(member => sheet(member).Characteristics.Int);
}
