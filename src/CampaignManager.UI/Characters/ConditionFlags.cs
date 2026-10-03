using CampaignManager.Core.Characters;

namespace CampaignManager.UI.Characters;

/// <summary>Отметка тела: подпись, иконка включённой, чтение и запись со следствиями.</summary>
public sealed record ConditionFlag(string Key, string Label, string Icon, Func<SheetCondition, bool> Get, Action<CharacterSheet, bool> Set);

/// <summary>
/// Отметки тела — одна таблица для «Состояния» листа и полосы режима «Игра»: следствия переключения (рана пересчитывает
/// сознание, снятое «При смерти» снимает стабилизацию) живут здесь, а не в разметке двух мест. Отметок безумия здесь
/// нет — их переключает только блок «Рассудок» (решение владельца, #157).
/// </summary>
public static class ConditionFlags
{
    public static IReadOnlyList<ConditionFlag> Body { get; } =
    [
        new("major-wound", "Серьёзная рана", "fa-heart-crack", c => c.MajorWound, (sheet, on) =>
        {
            sheet.Condition.MajorWound = on;
            WoundRules.UpdateConsciousness(sheet);
        }),
        new("unconscious", "Без сознания", "fa-moon", c => c.Unconscious, (sheet, on) => sheet.Condition.Unconscious = on),
        new("dying", "При смерти", "fa-skull", c => c.Dying, (sheet, on) =>
        {
            sheet.Condition.Dying = on;
            if (!on)
                sheet.Condition.Stabilized = false;
        }),
        new("stabilized", "Стабилизирован", "fa-kit-medical", c => c.Stabilized, (sheet, on) => sheet.Condition.Stabilized = on),
        new("dead", "Мёртв", "fa-cross", c => c.Dead, (sheet, on) => sheet.Condition.Dead = on),
    ];
}
