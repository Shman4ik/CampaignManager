namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>
/// Настройка сотворения заклинания в бою (гл. 9, стр. 174–177; «Гримуар», стр. 241).
/// <para>
/// Цена в каталоге записана текстом, поэтому сюда приходят уже подтверждённые Хранителем
/// числа, а не формула. Лежит внутри <see cref="SpellcastInProgress" />, пока заклинание
/// творится несколько раундов, и вместе с участником уходит в снапшот боя — поэтому только
/// простые поля, без ссылок на сущности.
/// </para>
/// </summary>
public class SpellCastSetup
{
    public Guid CasterId { get; set; }

    /// <summary>Заклинание листа или каталога; null — Хранитель вписал своё.</summary>
    public Guid? SpellId { get; set; }

    public string SpellName { get; set; } = string.Empty;

    /// <summary>Стоимость как в описании — для журнала: откуда взялись числа.</summary>
    public string? CostText { get; set; }

    public string? CastingTimeText { get; set; }

    // ── Цена, подтверждённая Хранителем ────────────────────────────────
    public int MagicPointsCost { get; set; }
    public int SanityCost { get; set; }

    /// <summary>МОЩ тратится навсегда (стр. 174).</summary>
    public int PowerCost { get; set; }

    /// <summary>Явная цена в ПЗ («1 ПЗ за раунд»). Нехватка ПМ добавляется к ней сама.</summary>
    public int HitPointsCost { get; set; }

    /// <summary>
    /// Раундов на сотворение: 0 — мгновенно (на ЛВК+50), 1 — в этом раунде на ЛВК заклинателя,
    /// N — в раунде «текущий + N − 1» (стр. 241).
    /// </summary>
    public int CastingRounds { get; set; }

    // ── Цель и сопротивление ───────────────────────────────────────────
    public Guid? TargetId { get; set; }

    /// <summary>Заклинание требует встречной проверки МОЩ — решает Хранитель по описанию (стр. 241).</summary>
    public bool TargetResists { get; set; }

    // ── Проверка сотворения (стр. 175–176) ─────────────────────────────

    /// <summary>
    /// Сыщик творит выученное заклинание впервые — трудная проверка МОЩ. Персонажи Хранителя
    /// и чудовища её не проходят.
    /// </summary>
    public bool IsFirstCast { get; set; }

    /// <summary>Повторная проверка после провала: провал теперь — заклинание срабатывает, но цена ×1d6.</summary>
    public bool IsPushed { get; set; }

    // ── Ручные броски (null — бросить автоматически) ───────────────────
    public int? ManualCastingRoll { get; set; }
    public int? ManualCasterPowerRoll { get; set; }
    public int? ManualTargetPowerRoll { get; set; }

    /// <summary>1d6 — во сколько раз сверх обычного платить за провал повторной проверки.</summary>
    public int? ManualPushMultiplier { get; set; }

    /// <summary>Проверка ИНТ при потере 5+ рассудка за раз (стр. 152).</summary>
    public int? ManualIntRoll { get; set; }

    /// <summary>1d10 часов временного безумия.</summary>
    public int? ManualInsanityDurationRoll { get; set; }

    /// <summary>Проверка ВЫН, если расплата ПЗ за нехватку ПМ обернулась серьёзной раной.</summary>
    public int? ManualMajorWoundConRoll { get; set; }

    /// <summary>Копия без ручных бросков: их вписывают, когда заклинание срабатывает, а не когда начато.</summary>
    public SpellCastSetup WithoutRolls()
    {
        var copy = (SpellCastSetup)MemberwiseClone();
        copy.ManualCastingRoll = null;
        copy.ManualCasterPowerRoll = null;
        copy.ManualTargetPowerRoll = null;
        copy.ManualPushMultiplier = null;
        copy.ManualIntRoll = null;
        copy.ManualInsanityDurationRoll = null;
        copy.ManualMajorWoundConRoll = null;
        return copy;
    }
}

/// <summary>
/// Заклинание, которое участник творит несколько раундов. Лежит на <see cref="Combatant" />,
/// поэтому переживает паузу circuit вместе со списком участников.
/// </summary>
public class SpellcastInProgress
{
    public SpellCastSetup Setup { get; set; } = new();
    public int StartedRound { get; set; }

    /// <summary>Раунд, в котором заклинание срабатывает на ЛВК заклинателя.</summary>
    public int CompletesInRound { get; set; }

    /// <summary>
    /// Заклинателя ранили, пока он творил: сотворение сорвано, а стоимость в ПМ и рассудке
    /// всё равно платится (стр. 177).
    /// </summary>
    public bool Disrupted { get; set; }
}
