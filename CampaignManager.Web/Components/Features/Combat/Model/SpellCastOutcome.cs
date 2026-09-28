namespace CampaignManager.Web.Components.Features.Combat.Model;

/// <summary>Что произошло с заклинанием в этой записи журнала.</summary>
public enum SpellCastPhase
{
    /// <summary>Заклинание сработало (или не сработало по проверке) — цена уплачена.</summary>
    Cast,

    /// <summary>Начато долгое сотворение: цена будет уплачена, когда оно закончится или сорвётся.</summary>
    Started,

    /// <summary>Сотворение сорвано: эффекта нет, ПМ и рассудок уплачены (стр. 177).</summary>
    Interrupted
}

/// <summary>Исход встречной проверки МОЩ (стр. 241).</summary>
public enum SpellResistance
{
    NotResisted,
    CasterWins,
    TargetWins,

    /// <summary>Равные уровни успеха и равная МОЩ — Хранитель решает, как задело обоих.</summary>
    Both
}

/// <summary>
/// Подробности сотворения заклинания внутри <see cref="CombatActionResult" />. Броски встречной
/// проверки МОЩ лежат в общих полях результата (Attacker*/Defender*), а рассудок заклинателя —
/// в полях Sanity*: их считает та же проверка ИНТ и порогов безумия, что и проверку Рассудка.
/// </summary>
public class SpellCastOutcome
{
    public string SpellName { get; set; } = string.Empty;
    public SpellCastPhase Phase { get; set; }
    public string? CostText { get; set; }
    public string? CastingTimeText { get; set; }
    public int CastingRounds { get; set; }
    public int CompletesInRound { get; set; }

    // ── Уплаченная цена ────────────────────────────────────────────────
    public int MagicPointsBefore { get; set; }
    public int MagicPointsAfter { get; set; }
    public int MagicPointsPaid { get; set; }

    /// <summary>Не хватило ПМ — столько же снято с ПЗ (стр. 174).</summary>
    public int MagicPointsShortfall { get; set; }

    /// <summary>Всего снято ПЗ: явная цена плюс нехватка ПМ.</summary>
    public int HitPointsPaid { get; set; }

    public int SanityPaid { get; set; }

    public int PowerBefore { get; set; }
    public int PowerAfter { get; set; }
    public int PowerPaid { get; set; }

    /// <summary>У существ рассудка нет — цену в рассудке с них не берём.</summary>
    public bool SanitySkipped { get; set; }

    // ── Проверка сотворения ────────────────────────────────────────────
    public bool IsFirstCast { get; set; }
    public int? CastingRoll { get; set; }

    /// <summary>МОЩ заклинателя, против которой шла трудная проверка.</summary>
    public int CastingPower { get; set; }

    public SuccessLevel? CastingLevel { get; set; }
    public bool CastingPassed { get; set; }
    public bool IsPushed { get; set; }

    /// <summary>1d6 за провал повторной проверки: цена уплачена ещё столько раз.</summary>
    public int? PushMultiplier { get; set; }

    public bool TakesEffect { get; set; }

    // ── Сопротивление цели ─────────────────────────────────────────────
    public bool TargetResists { get; set; }
    public SpellResistance Resistance { get; set; }

    /// <summary>Разница МОЩ 100 и больше — бросать не нужно (стр. 241).</summary>
    public bool ResistanceAutomatic { get; set; }

    public int CasterPower { get; set; }
    public int TargetPower { get; set; }
}
