using CampaignManager.Core.Characters;
using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Encounters;

/// <summary>Исход встречной проверки МОЩ (стр. 241).</summary>
public enum SpellResistance
{
    NotResisted,
    CasterWins,
    TargetWins,

    /// <summary>Ничья при равной МОЩ — Хранитель решает, как задело обоих.</summary>
    Both,
}

/// <summary>
/// Сотворение заклинания в бою (гл. 9, стр. 174–177; «Гримуар», стр. 241). Цена — числа, подтверждённые Хранителем
/// (текст книги подсказывает <c>SpellStatsReader</c>, но правдой не считается).
/// </summary>
public sealed record SpellCastSetup
{
    public Guid CasterId { get; init; }
    public string SpellName { get; init; } = "";
    public Guid? TargetId { get; init; }

    public int MagicPoints { get; init; }
    public int Sanity { get; init; }
    public int Power { get; init; }
    public int HitPoints { get; init; }

    /// <summary>Раундов сотворения: 0 — мгновенно (ЛВК+50), 1 — в этом раунде, N — сработает в раунде «текущий + N − 1».</summary>
    public int CastingRounds { get; init; } = 1;

    /// <summary>Первое сотворение сыщиком — трудная проверка МОЩ (стр. 176).</summary>
    public bool FirstCast { get; init; }

    /// <summary>Повторная проверка первого сотворения: провал — срабатывает, но цена ×(1 + 1d6).</summary>
    public bool Pushed { get; init; }

    /// <summary>Цель сопротивляется — встречная проверка МОЩ (по описанию заклинания).</summary>
    public bool TargetResists { get; init; }

    public D100Roll? CastingRoll { get; init; }
    public int? PushMultiplier { get; init; }
    public D100Roll? CasterPowerRoll { get; init; }
    public D100Roll? TargetPowerRoll { get; init; }

    /// <summary>ВЫН при серьёзной ране от расплаты ПЗ.</summary>
    public D100Roll? ConRoll { get; init; }

    /// <summary>ИНТ при потере рассудка 5+.</summary>
    public D100Roll? IntRoll { get; init; }
}

/// <summary>Итог сотворения: результат и числа (тесты и подписи).</summary>
public sealed record SpellOutcome
{
    public required EncounterResolution Resolution { get; init; }
    public bool Started { get; init; }
    public bool Interrupted { get; init; }
    public bool TakesEffect { get; init; }
    public bool Works { get; init; }
    public SuccessLevel? CastingLevel { get; init; }
    public int Multiplier { get; init; } = 1;
    public int MagicPointsPaid { get; init; }
    public int MagicPointsShortfall { get; init; }
    public int HitPointsPaid { get; init; }
    public int PowerPaid { get; init; }
    public int SanityPaid { get; init; }
    public bool SanitySkipped { get; init; }
    public SpellResistance Resistance { get; init; }
    public bool ResistanceAutomatic { get; init; }
    public DamageOutcome? Wound { get; init; }
    public string Summary => Resolution.Title;
}

public static partial class CombatRules
{
    /// <summary>Разница МОЩ, при которой встречную проверку не бросают (стр. 241).</summary>
    public const int AutomaticPowerGap = 100;

    /// <summary>Раунд, в котором заклинание сработает: мгновенное и однораундовое — в этом, N раундов — «текущий + N − 1» (стр. 241).</summary>
    public static int SpellCompletionRound(int round, int castingRounds) => Math.Max(1, round) + Math.Max(0, castingRounds - 1);

    /// <summary>
    /// Встречная проверка МОЩ (стр. 241): лучший уровень побеждает, при равенстве — у кого МОЩ выше, при равной МОЩ —
    /// задело обоих; разница 100 и больше решает без броска.
    /// </summary>
    public static SpellResistance OpposedPower(SuccessLevel casterLevel, int casterPower, SuccessLevel targetLevel, int targetPower)
    {
        if (casterPower - targetPower >= AutomaticPowerGap)
            return SpellResistance.CasterWins;
        if (targetPower - casterPower >= AutomaticPowerGap)
            return SpellResistance.TargetWins;
        if (casterLevel != targetLevel)
            return casterLevel > targetLevel ? SpellResistance.CasterWins : SpellResistance.TargetWins;
        if (casterPower != targetPower)
            return casterPower > targetPower ? SpellResistance.CasterWins : SpellResistance.TargetWins;
        return SpellResistance.Both;
    }

    /// <summary>
    /// Заклинание сейчас: мгновенное, однораундовое — сотворение и цена; от двух раундов — начало долгого сотворения (цена —
    /// когда сработает или сорвётся, атаковать пока нельзя).
    /// </summary>
    public static SpellOutcome CastSpell(EncounterState state, SpellCastSetup setup, IDiceRoller dice)
    {
        var caster = Require(state, setup.CasterId);
        if (setup.CastingRounds >= 2)
            return StartCasting(state, caster, setup);

        return Cast(state, caster, setup, dice, clearCasting: caster.Combat.Casting is not null);
    }

    /// <summary>
    /// Долгое сотворение дошло до своего раунда: сработало (цена, проверки) или было сорвано раной — тогда эффекта нет, а ПМ
    /// и рассудок платятся (стр. 177).
    /// </summary>
    public static SpellOutcome CompleteCasting(EncounterState state, Guid casterId, SpellCastSetup rolls, IDiceRoller dice)
    {
        var caster = Require(state, casterId);
        if (caster.Combat.Casting is not { } casting)
            throw new InvalidOperationException($"{caster.Name} ничего не творит.");

        var setup = rolls with
        {
            CasterId = casterId,
            SpellName = casting.SpellName,
            TargetId = casting.TargetId,
            MagicPoints = casting.MagicPoints,
            Sanity = casting.Sanity,
            Power = casting.Power,
            HitPoints = casting.HitPoints,
            TargetResists = casting.TargetResists,
            CastingRounds = 1,
        };
        return casting.Disrupted ? Interrupt(state, caster, setup, dice) : Cast(state, caster, setup, dice, clearCasting: true);
    }

    /// <summary>Бросить долгое сотворение по своей воле или из-за раны: эффекта нет, ПМ и рассудок уплачены (стр. 177).</summary>
    public static SpellOutcome InterruptCasting(EncounterState state, Guid casterId, SpellCastSetup rolls, IDiceRoller dice)
    {
        var caster = Require(state, casterId);
        if (caster.Combat.Casting is not { } casting)
            throw new InvalidOperationException($"{caster.Name} ничего не творит.");

        return Interrupt(state, caster, rolls with
        {
            CasterId = casterId, SpellName = casting.SpellName, TargetId = casting.TargetId,
            MagicPoints = casting.MagicPoints, Sanity = casting.Sanity,
        }, dice);
    }

    private static SpellOutcome StartCasting(EncounterState state, EncounterParticipant caster, SpellCastSetup setup)
    {
        var completes = SpellCompletionRound(state.Round, setup.CastingRounds);
        var target = setup.TargetId is { } id ? state.Find(id) : null;
        var casting = new SpellCasting
        {
            SpellName = setup.SpellName,
            TargetId = target?.Id,
            StartedRound = state.Round,
            CompletesInRound = completes,
            MagicPoints = Math.Max(0, setup.MagicPoints),
            Sanity = Math.Max(0, setup.Sanity),
            Power = Math.Max(0, setup.Power),
            HitPoints = Math.Max(0, setup.HitPoints),
            TargetResists = setup.TargetResists,
        };
        var title = $"{caster.Name} начинает творить «{setup.SpellName}»{(target is null ? "" : $" на {target.Name}")} — {N(setup.CastingRounds)} р., " +
                    $"сработает в раунде {N(completes)} на ЛВК заклинателя.";
        return new SpellOutcome
        {
            Resolution = Resolution(EncounterLogKind.Spell, caster.Id, title,
                ["Пока он занят, удар или выстрел сорвут сотворение: эффекта не будет, ПМ и рассудок всё равно платятся (стр. 177, 241)."],
                [new EncounterEffect { Kind = EncounterEffectKind.Casting, ParticipantId = caster.Id, Casting = casting }]),
            Started = true,
        };
    }

    private static SpellOutcome Cast(EncounterState state, EncounterParticipant caster, SpellCastSetup setup, IDiceRoller dice, bool clearCasting)
    {
        var target = setup.TargetId is { } id ? state.Find(id) : null;
        List<string> lines = [];
        List<EncounterEffect> effects = [];
        if (clearCasting)
            effects.Add(new EncounterEffect { Kind = EncounterEffectKind.Casting, ParticipantId = caster.Id });

        // 1. Первое сотворение — трудная проверка МОЩ (стр. 176); крах — от половины МОЩ (стр. 88).
        var takesEffect = true;
        var multiplier = 1;
        SuccessLevel? castingLevel = null;
        if (setup.FirstCast)
        {
            var (roll, level, passed) = Test(setup.CastingRoll, caster.Stats.Pow, dice, Difficulty.Hard);
            castingLevel = level;
            lines.Add(RollText($"Первое сотворение{(setup.Pushed ? ", повторная проверка" : "")} — МОЩ {caster.Name}", roll, caster.Stats.Pow, level) +
                      (passed ? ": трудная проверка пройдена, дальше это заклинание творится без проверки." : ""));
            if (!passed && setup.Pushed)
            {
                var extra = setup.PushMultiplier is >= 1 and <= 6 ? setup.PushMultiplier.Value : dice.Die(6);
                multiplier += extra;
                lines.Add($"Повторная проверка провалена: заклинание срабатывает, но цена ещё ×{N(extra)} (1d6); побочный эффект — по списку книги (стр. 176).");
            }
            else if (!passed)
            {
                takesEffect = false;
                lines.Add("Трудная проверка провалена: ничего не происходит, цена уплачена. Новая попытка — повторная проверка (стр. 176).");
            }
        }

        // 2. Цена — при любом исходе проверки.
        var cost = PayCost(caster, setup.MagicPoints * multiplier, setup.Sanity * multiplier, setup.Power * multiplier, setup.HitPoints * multiplier,
            setup.ConRoll, setup.IntRoll, dice, lines, effects);

        // 3. Сопротивление цели — встречная МОЩ (стр. 241).
        var works = takesEffect;
        var resistance = SpellResistance.NotResisted;
        var automatic = false;
        if (takesEffect && target is not null && setup.TargetResists)
        {
            if (Math.Abs(caster.Stats.Pow - target.Stats.Pow) >= AutomaticPowerGap)
            {
                automatic = true;
                resistance = OpposedPower(SuccessLevel.Failure, caster.Stats.Pow, SuccessLevel.Failure, target.Stats.Pow);
                lines.Add($"Разница МОЩ {N(caster.Stats.Pow)} и {N(target.Stats.Pow)} — не меньше {N(AutomaticPowerGap)}: без броска.");
            }
            else
            {
                var casterTest = Test(setup.CasterPowerRoll, caster.Stats.Pow, dice);
                var targetTest = Test(setup.TargetPowerRoll, target.Stats.Pow, dice);
                resistance = OpposedPower(casterTest.Level, caster.Stats.Pow, targetTest.Level, target.Stats.Pow);
                lines.Add($"Встречная МОЩ: {RollText(caster.Name, casterTest.Roll, caster.Stats.Pow, casterTest.Level)}; " +
                          $"{RollText(target.Name, targetTest.Roll, target.Stats.Pow, targetTest.Level)}.");
            }

            works = resistance is SpellResistance.CasterWins or SpellResistance.Both;
            lines.Add(resistance switch
            {
                SpellResistance.CasterWins => $"{target.Name} не устоял. Победа даёт заклинателю проверку повышения МОЩ (стр. 177–178).",
                SpellResistance.TargetWins => $"{target.Name} устоял — заклинание его не взяло.",
                _ => "Ничья при равной МОЩ — Хранитель решает, как заклинание задело обоих.",
            });
        }

        if (setup.CastingRounds == 0 && takesEffect)
            lines.Add("Мгновенное заклинание срабатывает на ЛВК+50, как огнестрел наготове (стр. 241).");
        if (works && target is not null)
            lines.Add("Эффект заклинания на цель (урон, чары) Хранитель проводит отдельно — «Эффект Хранителя».");

        var head = $"{caster.Name} творит «{setup.SpellName}»{(target is null ? "" : $" на {target.Name}")}: " +
                   (!takesEffect ? "не сработало" : works ? "сработало" : "цель устояла");
        return new SpellOutcome
        {
            Resolution = Resolution(EncounterLogKind.Spell, caster.Id, head + CostTitle(cost), lines, effects),
            TakesEffect = takesEffect, Works = works, CastingLevel = castingLevel, Multiplier = multiplier,
            MagicPointsPaid = cost.MagicPoints, MagicPointsShortfall = cost.Shortfall, HitPointsPaid = cost.HitPoints,
            PowerPaid = cost.Power, SanityPaid = cost.Sanity, SanitySkipped = cost.SanitySkipped,
            Resistance = resistance, ResistanceAutomatic = automatic, Wound = cost.Wound,
        };
    }

    private static SpellOutcome Interrupt(EncounterState state, EncounterParticipant caster, SpellCastSetup setup, IDiceRoller dice)
    {
        List<string> lines = [];
        List<EncounterEffect> effects = [new EncounterEffect { Kind = EncounterEffectKind.Casting, ParticipantId = caster.Id }];
        var cost = PayCost(caster, setup.MagicPoints, setup.Sanity, 0, 0, setup.ConRoll, setup.IntRoll, dice, lines, effects);
        lines.Add("Последствия срыва Хранитель может взять из списка побочных эффектов повторной проверки (стр. 176–177).");
        return new SpellOutcome
        {
            Resolution = Resolution(EncounterLogKind.Spell, caster.Id, $"{caster.Name}: сотворение «{setup.SpellName}» сорвано — эффекта нет" + CostTitle(cost), lines, effects),
            Interrupted = true,
            MagicPointsPaid = cost.MagicPoints, MagicPointsShortfall = cost.Shortfall, HitPointsPaid = cost.HitPoints,
            SanityPaid = cost.Sanity, SanitySkipped = cost.SanitySkipped, Wound = cost.Wound,
        };
    }

    private sealed record SpellCost(int MagicPoints, int Shortfall, int HitPoints, int Power, int Sanity, bool SanitySkipped, DamageOutcome? Wound);

    /// <summary>
    /// Цена (стр. 174–177): ПМ, нехватка — с ПЗ (рана по общему правилу, пример с Мэтью); явные ПЗ; МОЩ навсегда (максимум
    /// ПМ на листе следует сам); рассудок — с проверкой ИНТ, у твари рассудка нет.
    /// </summary>
    private static SpellCost PayCost(EncounterParticipant caster, int magicCost, int sanityCost, int powerCost, int hitPointsCost,
        D100Roll? conRoll, D100Roll? intRoll, IDiceRoller dice, List<string> lines, List<EncounterEffect> effects)
    {
        magicCost = Math.Max(0, magicCost);
        var paid = Math.Min(magicCost, Math.Max(0, caster.MagicPoints));
        var shortfall = magicCost - paid;
        if (paid > 0)
            effects.Add(Effect(EncounterEffectKind.MagicPoints, caster.Id, -paid, detail: "цена заклинания"));
        if (shortfall > 0)
            lines.Add($"Не хватило {N(shortfall)} ПМ — они сняты с ПЗ: раны, язвы, кровь из глаз и ушей (стр. 174).");

        var hitPoints = shortfall + Math.Max(0, hitPointsCost);
        DamageOutcome? wound = null;
        if (hitPoints > 0)
        {
            var (effect, outcome, conLine) = DamageEffect(caster, hitPoints, conRoll, dice, "цена заклинания");
            effects.Add(effect);
            wound = outcome;
            if (conLine is not null)
                lines.Add(conLine);
        }

        if (powerCost > 0)
        {
            effects.Add(Effect(EncounterEffectKind.Power, caster.Id, -powerCost, detail: "цена заклинания"));
            lines.Add($"МОЩ −{N(powerCost)} навсегда.");
        }

        var sanityPaid = 0;
        var skipped = false;
        if (sanityCost > 0)
        {
            if (caster.Sanity is null)
            {
                skipped = true;
                lines.Add("Рассудка у твари нет — эта часть цены пропущена.");
            }
            else
            {
                sanityPaid = sanityCost;
                bool? intPassed = null;
                if (sanityCost >= 5)
                {
                    var (roll, level, passed) = Test(intRoll, caster.Stats.Int, dice);
                    intPassed = passed;
                    lines.Add($"Потеряно 5+ за раз — {RollText("ИНТ", roll, caster.Stats.Int, level)}: {(passed ? "временное безумие" : "безумия нет")}.");
                }

                effects.Add(new EncounterEffect
                {
                    Kind = EncounterEffectKind.SanityLoss, ParticipantId = caster.Id, Amount = sanityCost, Check = intPassed, Detail = "цена заклинания",
                });
            }
        }

        return new SpellCost(paid, shortfall, hitPoints, powerCost, sanityPaid, skipped, wound);
    }

    private static string CostTitle(SpellCost cost)
    {
        List<string> parts = [];
        if (cost.MagicPoints > 0)
            parts.Add($"ПМ −{N(cost.MagicPoints)}");
        if (cost.HitPoints > 0)
            parts.Add($"ПЗ −{N(cost.HitPoints)}");
        if (cost.Sanity > 0)
            parts.Add($"Рассудок −{N(cost.Sanity)}");
        if (cost.Power > 0)
            parts.Add($"МОЩ −{N(cost.Power)}");
        return parts.Count > 0 ? $". Цена: {string.Join(", ", parts)}." : ". Цены нет.";
    }
}
