namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
/// Тип боевого манёвра (CoC 7e, стр. 103)
/// </summary>
public enum ManeuverType
{
    /// <summary>Разоружить — выбить оружие или предмет из рук</summary>
    Disarm,

    /// <summary>Сбить с ног / повалить — противник оказывается в поваленном состоянии</summary>
    KnockDown,

    /// <summary>Захват / обездвижить — удерживать противника в захвате</summary>
    Grapple,

    /// <summary>Толкнуть — столкнуть с обрыва, выбить в окно, отбросить</summary>
    Push,

    /// <summary>Вырваться из захвата — схваченный персонаж пытается вырваться</summary>
    BreakFree,

    /// <summary>Поставить в невыгодное положение — Хранитель решает, чем это обернётся</summary>
    Disadvantage,

    /// <summary>
    /// «Киношный» нокаут ударным оружием: успех отправляет противника в
    /// беспамятство при 1 пункте урона. Необязательное правило (стр. 123).
    /// </summary>
    Knockout
}
