namespace CampaignManager.Web.Components.Features.Characters.Model;

/// <summary>
///     Привыкание к ужасному (стр. 167): сколько рассудка сыщик уже потерял за конкретный вид
///     тварей Мифов. Дойдя до предела, он перестаёт терять рассудок при новых встречах,
///     но каждая фаза развития снижает накопленное на 1 — «время лечит».
/// </summary>
public class MythosHabituation
{
    /// <summary>Существо из бестиария; null — Хранитель завёл запись вручную.</summary>
    public Guid? CreatureId { get; set; }

    public required string CreatureName { get; set; }

    /// <summary>Предел: максимум рассудка за встречу с этим видом (6 при «0/1d6»).</summary>
    public int MaxLoss { get; set; }

    /// <summary>Сколько уже потеряно за встречи с этим видом.</summary>
    public int LostSanity { get; set; }

    /// <summary>Запись потери из бестиария как есть («0/1d6») — чтобы Хранитель видел, откуда предел.</summary>
    public string? SanityLossFormula { get; set; }

    /// <summary>Предел выбран — новые встречи с этим видом рассудка уже не отнимают.</summary>
    public bool IsHabituated => MaxLoss > 0 && LostSanity >= MaxLoss;

    /// <summary>Сколько ещё можно потерять за этот вид.</summary>
    public int Remaining => MaxLoss > 0 ? Math.Max(0, MaxLoss - LostSanity) : int.MaxValue;
}
