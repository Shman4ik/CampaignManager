namespace CampaignManager.Core.Characters;

/// <summary>Остаётся ли сыщик в игре.</summary>
public enum InvestigatorFate
{
    InPlay,

    /// <summary>Погиб — отметка «Мёртв» (урон не меньше максимума ПЗ или не прошёл ВЫН при смерти).</summary>
    Dead,

    /// <summary>Рассудок 0 — неизлечимо безумен (стр. 154): из игры выбывает, как погибший.</summary>
    Insane,
}

/// <summary>
/// Гибель и выбывание сыщика (гл. 10, стр. 210–211): погибший или навсегда безумный сыщик выбывает, и игрок вне игры, пока не
/// введёт нового. Лист выбывшего получает статус «Выбыл» — тогда место игрока в кампании свободно (один активный сыщик), — и
/// эпилог: что с ним стало.
/// </summary>
public static class FateRules
{
    public static InvestigatorFate Of(CharacterSheet sheet) =>
        sheet.Condition.Dead ? InvestigatorFate.Dead
        : SanityRules.IsPermanentlyInsane(sheet) ? InvestigatorFate.Insane
        : InvestigatorFate.InPlay;

    /// <summary>
    /// Эпилог в хронику встречи: абзацем в конце, «**Эпилог: Имя.** текст». Пустой эпилог хронику не меняет.
    /// </summary>
    public static string AppendEpilogue(string? chronicle, string name, string epilogue)
    {
        var text = epilogue.Trim();
        if (text.Length == 0)
            return chronicle ?? "";

        var paragraph = $"**Эпилог: {name.Trim()}.** {text}";
        return string.IsNullOrWhiteSpace(chronicle) ? paragraph : $"{chronicle.TrimEnd()}\n\n{paragraph}";
    }
}
