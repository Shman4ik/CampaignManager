namespace CampaignManager.Core.Dice;

/// <summary>Как цель отвечает на атаку в ближнем бою (стр. 103–105).</summary>
public enum DefenceKind
{
    /// <summary>Уклонение: ничья — за защитником.</summary>
    Dodge,

    /// <summary>Контратака (и ответный манёвр): ничья — за атакующим.</summary>
    FightBack,
}

/// <summary>
/// Встречная проверка атаки и защиты (схема боя, стр. 104): побеждает больший уровень успеха; ничья при уклонении — за
/// защитником, при контратаке — за атакующим; провалили оба — урона нет. Одна копия на бой и погоню.
/// </summary>
public static class Opposed
{
    public static bool AttackerWins(SuccessLevel attack, SuccessLevel defence, DefenceKind kind)
    {
        if (!attack.IsSuccess())
            return false;
        if (attack != defence)
            return attack > defence;
        return kind == DefenceKind.FightBack;
    }

    /// <summary>Защитник при контратаке победил — его удар достиг цели.</summary>
    public static bool DefenderStrikes(SuccessLevel attack, SuccessLevel defence, DefenceKind kind) =>
        kind == DefenceKind.FightBack && defence.IsSuccess() && defence > attack;
}
