using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Encounters;

/// <summary>Настройка атаки ближнего боя. Броски — вписанные (null — бросит правило).</summary>
public sealed record MeleeAttackSetup
{
    public Guid AttackerId { get; init; }
    public Guid DefenderId { get; init; }

    /// <summary>Ключ атаки участника (<see cref="CombatAttack.Key"/>); null — драка.</summary>
    public string? AttackKey { get; init; }

    /// <summary>Навык атаки, если Хранитель его поправил; null — из атаки.</summary>
    public int? AttackSkill { get; init; }

    public DefenseReaction Reaction { get; init; }

    /// <summary>Навык защиты; null — Уклонение или контратака из снимка цели.</summary>
    public int? DefenseSkill { get; init; }

    /// <summary>Атака цели для контратаки; null — её лучшая атака ближнего боя (драка).</summary>
    public string? CounterAttackKey { get; init; }

    public SurpriseMode Surprise { get; init; }

    public int KeeperBonusDice { get; init; }
    public int KeeperPenaltyDice { get; init; }
    public int DefenderBonusDice { get; init; }
    public int DefenderPenaltyDice { get; init; }

    /// <summary>Броня преграды при ударе сквозь укрытие (стр. 125); складывается с бронёй цели.</summary>
    public int CoverArmor { get; init; }

    /// <summary>Магия, яд, утопление — мимо брони (стр. 106).</summary>
    public bool IgnoresArmor { get; init; }

    public D100Roll? AttackRoll { get; init; }
    public D100Roll? DefenseRoll { get; init; }
    public int? DamageRoll { get; init; }
    public int? DamageBonusRoll { get; init; }
    public int? ExtraImpaleRoll { get; init; }
    public int? CounterDamageRoll { get; init; }
    public int? CounterBonusRoll { get; init; }

    /// <summary>ВЫН цели при серьёзной ране — вписывается, как любой бросок (в v1 нельзя было, F-C05).</summary>
    public D100Roll? ConRoll { get; init; }

    /// <summary>ВЫН атакующего при серьёзной ране от контратаки.</summary>
    public D100Roll? CounterConRoll { get; init; }
}

/// <summary>Настройка стрельбы (и метания). Броски — вписанные (null — бросит правило).</summary>
public sealed record RangedAttackSetup
{
    public Guid AttackerId { get; init; }
    public Guid DefenderId { get; init; }
    public string? AttackKey { get; init; }
    public int? AttackSkill { get; init; }

    public RangeBand Range { get; init; }

    public SurpriseMode Surprise { get; init; }

    public int KeeperBonusDice { get; init; }
    public int KeeperPenaltyDice { get; init; }

    public bool PointBlank { get; init; }

    /// <summary>Прицел из настройки; прицел из состояния стрелка учитывается сам — один раз.</summary>
    public bool Aiming { get; init; }

    /// <summary>Цель укрылась от огня (из настройки; укрытие из состояния цели учитывается само — один раз).</summary>
    public bool TargetTakingCover { get; init; }

    public bool TargetBehindCover { get; init; }
    public bool TargetFastMoving { get; init; }
    public bool FiringIntoMelee { get; init; }
    public bool ReloadAndFire { get; init; }

    public FiringMode FiringMode { get; init; }

    /// <summary>
    /// Пуль в залпе очереди (<see cref="FiringMode.Volley"/>). Выстрел и серия из пистолета — одна пуля на проверку: каждый
    /// выстрел серии проверяют отдельно (стр. 111–112), поэтому вне очереди поле не читается.
    /// </summary>
    public int ShotsFired { get; init; } = 1;

    /// <summary>Номер проверки очереди в раунде с нуля; null — по счёту стрелка в этом раунде.</summary>
    public int? AutofireCheckIndex { get; init; }

    public int CoverArmor { get; init; }
    public bool IgnoresArmor { get; init; }

    public D100Roll? AttackRoll { get; init; }

    /// <summary>Уклонение цели от метательного оружия (стр. 106) — вписывается, как любой бросок.</summary>
    public D100Roll? DefenseRoll { get; init; }

    public int? DamageRoll { get; init; }
    public int? DamageBonusRoll { get; init; }
    public int? ExtraImpaleRoll { get; init; }

    /// <summary>
    /// Кости урона залпа очереди по порядку бросков: у обычной пули — её урон, у проникающей — добавочный бросок (максимум
    /// берётся сам). Не хватило — остальное бросит правило.
    /// </summary>
    public IReadOnlyList<int>? DamageRolls { get; init; }

    public D100Roll? ConRoll { get; init; }

    /// <summary>Раунды починки при осечке (1d6, стр. 113) — вписываются тоже.</summary>
    public int? JamRounds { get; init; }
}

/// <summary>Как сложился урон попавшей атаки.</summary>
public sealed record DamageRoll(
    string Formula, int Rolled, int Bonus, int Extra, int Raw, int Armor, int Total, bool Extreme, bool Critical, bool Impaling)
{
    public string Describe()
    {
        // «Урон: 1d3 = 2, бонус к урону −1 → 1.»: кости строчной d, знак — настоящим минусом, слово вместо «БкУ» (правила 11 и 13).
        List<string> parts = [$"{DiceNotation.Format(Formula)} = {CombatRules.N(Rolled)}"];
        if (Bonus != 0)
            parts.Add($"{Terms.DamageBonus} {Signed(Bonus)}");
        if (Extra > 0)
            parts.Add($"проникающая +{CombatRules.N(Extra)}");
        if (Armor > 0)
            parts.Add($"броня −{CombatRules.N(Armor)}");
        var text = $"Урон: {string.Join(", ", parts)}";
        if (parts.Count > 1)
            text += $" → {CombatRules.N(Total)}";
        if (Critical)
            text += " (критический успех: максимум)";
        else if (Extreme)
            text += Impaling ? " (чрезвычайный успех: максимум и проникающая рана)" : " (чрезвычайный успех: максимум)";
        return text + ".";
    }

    private static string Signed(int value) => value >= 0 ? $"+{CombatRules.N(value)}" : $"−{CombatRules.N(-value)}";
}

/// <summary>Итог атаки: результат для предпросмотра и числа, по которым его собрали (тесты и подписи).</summary>
public sealed record AttackOutcome
{
    public required EncounterResolution Resolution { get; init; }
    public AttackDice Modifiers { get; init; } = new(0, 0, []);
    public D100Roll? Roll { get; init; }
    public SuccessLevel Level { get; init; }
    public SuccessLevel Required { get; init; } = SuccessLevel.Regular;
    public bool Impossible { get; init; }
    public D100Roll? DefenseRoll { get; init; }
    public SuccessLevel DefenseLevel { get; init; } = SuccessLevel.Failure;
    public bool Hit { get; init; }
    public DamageRoll? Damage { get; init; }
    public DamageOutcome? Wound { get; init; }
    public DamageRoll? Counter { get; init; }
    public DamageOutcome? CounterWound { get; init; }
    public bool Malfunction { get; init; }
    public int? JamRounds { get; init; }
    public bool HitAllyOnFumble { get; init; }
    public Guid? HitAllyId { get; init; }

    /// <summary>Урон каждой попавшей пули залпа (стр. 115); у одиночного выстрела пусто — урон в <see cref="Damage"/>.</summary>
    public IReadOnlyList<DamageRoll> Bullets { get; init; } = [];

    /// <summary>Атака невозможна (Комплекция, заклинило, пустой магазин): ход не тратится, применять нечего.</summary>
    public string? Blocked { get; init; }

    public string Summary => Resolution.Title;
}

public static partial class CombatRules
{
    /// <summary>
    /// Ближний бой (стр. 101–106): встречная проверка с Уклонением или контратакой, внезапность, численное превосходство,
    /// урон с бонусом и максимумом при чрезвычайном успехе, броня, раны. Контратака-победа ранит атакующего.
    /// </summary>
    public static AttackOutcome Melee(EncounterState state, MeleeAttackSetup setup, IDiceRoller dice)
    {
        var attacker = Require(state, setup.AttackerId);
        var defender = Require(state, setup.DefenderId);
        var attack = AttackOf(attacker, setup.AttackKey);
        var skill = setup.AttackSkill ?? attack.Skill;
        var surprise = setup.Surprise;

        var modifiers = MeleeDice(state, setup);

        var (roll, level, _) = Test(setup.AttackRoll, skill, dice, bonus: modifiers.BonusDice, penalty: modifiers.PenaltyDice);
        List<string> lines = [RollText($"{attacker.Name} ({attack.Name})", roll, skill, level)];
        if (modifiers.Reasons.Count > 0)
            lines.Add($"Кости: {string.Join(", ", modifiers.Reasons)}.");
        List<EncounterEffect> effects = [Effect(EncounterEffectKind.Attack, attacker.Id, 1, flag: true)];

        // Защитник бросает, только если он начеку (стр. 104).
        D100Roll? defenseRoll = null;
        var defenseLevel = SuccessLevel.Failure;
        var defenseSkill = setup.DefenseSkill ?? (setup.Reaction == DefenseReaction.FightBack ? defender.Profile.FightBack : defender.Stats.Dodge);
        if (surprise == SurpriseMode.TargetReady)
        {
            var defense = Test(setup.DefenseRoll, defenseSkill, dice, bonus: setup.DefenderBonusDice, penalty: setup.DefenderPenaltyDice);
            defenseRoll = defense.Roll;
            defenseLevel = defense.Level;
            lines.Add(RollText($"{defender.Name} ({(setup.Reaction == DefenseReaction.FightBack ? "контратака" : "уклонение")})",
                defense.Roll, defenseSkill, defense.Level));
        }
        else
        {
            lines.Add(surprise == SurpriseMode.AutoHit
                ? $"{defender.Name} беззащитен — атака проходит сама, провал только при крахе (стр. 105)."
                : $"{defender.Name} застигнут врасплох — не защищается (стр. 105).");
        }

        AttackOutcome Miss(string title) => new()
        {
            Resolution = Resolution(EncounterLogKind.Attack, attacker.Id, title, lines, effects),
            Modifiers = modifiers, Roll = roll, Level = level, DefenseRoll = defenseRoll, DefenseLevel = defenseLevel,
        };

        bool hit;
        if (surprise == SurpriseMode.AutoHit)
        {
            if (level == SuccessLevel.Fumble)
                return Miss($"{attacker.Name}: крах при внезапной атаке — даже застигнутый врасплох {defender.Name} не пострадал.");
            hit = true;
        }
        else
        {
            if (level <= SuccessLevel.Failure)
                return Miss($"{attacker.Name} промахивается. Цель: {defender.Name} ({N(roll.Result)} против {N(skill)}).");

            hit = defenseLevel <= SuccessLevel.Failure || AttackerWinsOpposed(level, defenseLevel, setup.Reaction);
            // Численное превосходство считает только настоящие защиты (стр. 106).
            if (surprise == SurpriseMode.TargetReady)
                effects.Add(Effect(EncounterEffectKind.Defense, defender.Id));
        }

        if (!hit)
        {
            if (setup.Reaction != DefenseReaction.FightBack)
                return Miss(defenseRoll is { } dodge
                    ? $"{attacker.Name}: промах — {defender.Name} уклонился ({N(dodge.Result)} против {N(defenseSkill)})."
                    : $"{attacker.Name}: промах — {defender.Name} уклонился.");

            var counterAttack = setup.CounterAttackKey is { } key ? AttackOf(defender, key) : BestMelee(defender);
            var counter = RollDamage(counterAttack, defender.Stats.DamageBonus, SuccessLevel.Regular, canBeExtreme: false,
                impaling: false, setup.CounterDamageRoll, setup.CounterBonusRoll, null, attacker.Stats.Armor, dice);
            lines.Add($"Контратака ({counterAttack.Name}). {counter.Describe()}");
            DamageOutcome? counterWound = null;
            if (counter.Total > 0)
            {
                var (effect, wound, conLine) = DamageEffect(attacker, counter.Total, setup.CounterConRoll, dice, counter.Formula);
                effects.Add(effect);
                counterWound = wound;
                if (conLine is not null)
                    lines.Add(conLine);
            }

            return new AttackOutcome
            {
                Resolution = Resolution(EncounterLogKind.Attack, attacker.Id,
                    $"{defender.Name} отбивает атаку и контратакует {attacker.Name}" + WoundTitle(counter.Total, counterWound), lines, effects),
                Modifiers = modifiers, Roll = roll, Level = level, DefenseRoll = defenseRoll, DefenseLevel = defenseLevel,
                Counter = counter, CounterWound = counterWound,
            };
        }

        var damage = RollDamage(attack, attacker.Stats.DamageBonus, level, canBeExtreme: true, attack.Impaling,
            setup.DamageRoll, setup.DamageBonusRoll, setup.ExtraImpaleRoll, ArmorOf(defender, setup.CoverArmor, setup.IgnoresArmor), dice);
        return Hit(attacker, defender, attack, damage, setup.ConRoll, dice, lines, effects) with
        {
            Modifiers = modifiers, Roll = roll, Level = level, DefenseRoll = defenseRoll, DefenseLevel = defenseLevel,
        };
    }

    /// <summary>
    /// Стрельба и метание (стр. 110–114): без встречной проверки, дальность задаёт сложность, очередь её поднимает; осечка,
    /// патроны, шальная пуля при крахе в ближнем бою, проникающая рана на сверхдальней — только при критическом успехе.
    /// Прицел тратится любым выстрелом, попал он или нет (F-C07).
    /// </summary>
    public static AttackOutcome Ranged(EncounterState state, RangedAttackSetup setup, IDiceRoller dice)
    {
        var attacker = Require(state, setup.AttackerId);
        var defender = Require(state, setup.DefenderId);
        var attack = AttackOf(attacker, setup.AttackKey);
        var skill = setup.AttackSkill ?? attack.Skill;

        if (WeaponBlockReason(attacker, attack) is { } blocked)
        {
            return new AttackOutcome
            {
                Resolution = Resolution(EncounterLogKind.Attack, attacker.Id, blocked, [], []),
                Blocked = blocked,
            };
        }

        var autofireIndex = AutofireIndex(state, setup);
        var (required, impossible) = RangedRequirement(state, setup);
        var modifiers = RangedDice(state, setup);

        // Крах — от сложности, которую задала дальность: на большой дальности у навыка 60 нужно 30, и 96–100 уже крах (стр. 88).
        var (roll, level, _) = Test(setup.AttackRoll, skill, dice, DifficultyOf(required), modifiers.BonusDice, modifiers.PenaltyDice);
        List<string> lines = [RollText($"{attacker.Name} ({attack.Name})", roll, skill, level)];
        if (required > SuccessLevel.Regular)
            lines.Add($"Нужен {RulesText.Of(required)}{(setup.FiringMode == FiringMode.Volley && autofireIndex > 0 ? $" — проверка очереди №{N(autofireIndex + 1)}" : " — дальность")} (стр. 110, 114).");
        if (modifiers.Reasons.Count > 0)
            lines.Add($"Кости: {string.Join(", ", modifiers.Reasons)}.");

        // Ход, патроны и проверка очереди тратятся при любом исходе (стр. 111–114) — но только при «Применить».
        List<EncounterEffect> effects = [Effect(EncounterEffectKind.Attack, attacker.Id, 1, flag: true)];
        var bullets = setup.FiringMode == FiringMode.Volley ? Math.Max(1, setup.ShotsFired) : 1;
        if (AmmoLeft(attacker, attack) is { } loaded)
        {
            var left = Math.Max(0, loaded - bullets);
            effects.Add(new EncounterEffect { Kind = EncounterEffectKind.Ammo, ParticipantId = attacker.Id, Key = attack.Key, Amount = left });
        }

        if (setup.FiringMode == FiringMode.Volley)
            effects.Add(Effect(EncounterEffectKind.Autofire, attacker.Id));

        AttackOutcome Miss(string title) => new()
        {
            Resolution = Resolution(EncounterLogKind.Attack, attacker.Id, title, lines, effects),
            Modifiers = modifiers, Roll = roll, Level = level, Required = required, Impossible = impossible,
        };

        // Осечка: бросок не меньше порога — оружие не выстрелило; починка 1d6 раундов (стр. 113).
        if (attack.Malfunction is { } malfunction && roll.Result >= malfunction)
        {
            var rounds = setup.JamRounds is >= 1 and <= 6 ? setup.JamRounds.Value : dice.Die(6);
            effects.Add(new EncounterEffect
            {
                Kind = EncounterEffectKind.Jam, ParticipantId = attacker.Id, Key = attack.Key, Amount = rounds,
                Detail = $"осечка {N(roll.Result)} ≥ {N(malfunction)}",
            });
            lines.Add($"Осечка: {N(roll.Result)} ≥ {N(malfunction)}. Починка — {N(rounds)} р. и успех Механики или Стрельбы (стр. 113).");
            return Miss($"{attacker.Name}: осечка — {attack.Name} заклинило.") with { Malfunction = true, JamRounds = rounds };
        }

        if (impossible)
            return Miss($"{attacker.Name}: проверка очереди №{N(autofireIndex + 1)} — сложность выше критической, попадание невозможно (стр. 114).");

        if (level == SuccessLevel.Fumble && setup.FiringIntoMelee)
        {
            var ally = UnluckiestAlly(state, attacker, defender);
            var title = ally is null
                ? $"{attacker.Name}: крах при стрельбе в ближнем бою — пуля ушла в союзника: выберите пострадавшего по наименьшей Удаче (стр. 112)."
                : $"{attacker.Name}: крах при стрельбе в ближнем бою — пуля попала в {ally.Name} (наименьшая Удача, {N(ally.Luck ?? 0)}). Разыграйте урон отдельной атакой по нему (стр. 112).";
            return Miss(title) with { HitAllyOnFumble = true, HitAllyId = ally?.Id };
        }

        if (level < required)
        {
            var needed = required == SuccessLevel.Regular ? $"против {N(skill)}" : $"нужен {RulesText.Of(required)}";
            return Miss($"{attacker.Name} промахивается. Цель: {defender.Name} ({N(roll.Result)}, {needed}).");
        }

        // От метательного оружия уклоняются, как от удара (стр. 106): встречная проверка, ничья — уклонившемуся. Контратака —
        // только вплотную, а это уже ближний бой. Не начеку — не уклоняется.
        D100Roll? dodgeRoll = null;
        var dodgeLevel = SuccessLevel.Failure;
        if (attack.Thrown && setup.Surprise == SurpriseMode.TargetReady)
        {
            var dodge = Test(setup.DefenseRoll, defender.Stats.Dodge, dice);
            (dodgeRoll, dodgeLevel) = (dodge.Roll, dodge.Level);
            lines.Add(RollText($"{defender.Name} (уклонение)", dodge.Roll, defender.Stats.Dodge, dodge.Level));
            effects.Add(Effect(EncounterEffectKind.Defense, defender.Id));
            if (dodgeLevel > SuccessLevel.Failure && !AttackerWinsOpposed(level, dodgeLevel, DefenseReaction.Dodge))
                return Miss($"{attacker.Name}: промах — {defender.Name} уклонился ({N(dodge.Roll.Result)} против {N(defender.Stats.Dodge)}).") with
                {
                    DefenseRoll = dodgeRoll, DefenseLevel = dodgeLevel,
                };
        }

        var armor = ArmorOf(defender, setup.CoverArmor, setup.IgnoresArmor);
        if (setup.FiringMode == FiringMode.Volley)
            return Volley(attacker, defender, attack, setup, bullets, level, required, armor, dice, lines, effects) with
            {
                Modifiers = modifiers, Roll = roll, Level = level, Required = required,
            };

        // На сверхбольшой дальности проникающая рана — только при критическом успехе (стр. 110).
        var impaling = attack.Impaling && (setup.Range != RangeBand.Extreme || level == SuccessLevel.Critical);
        var damage = RollDamage(attack, attacker.Stats.DamageBonus, level, canBeExtreme: true, impaling,
            setup.DamageRoll, setup.DamageBonusRoll, setup.ExtraImpaleRoll, armor, dice, (int)setup.Range);
        return Hit(attacker, defender, attack, damage, setup.ConRoll, dice, lines, effects) with
        {
            Modifiers = modifiers, Roll = roll, Level = level, Required = required, DefenseRoll = dodgeRoll, DefenseLevel = dodgeLevel,
        };
    }

    /// <summary>
    /// Попавший залп очереди (стр. 115): успех — в цель попадает половина пуль (с округлением вниз, не меньше одной);
    /// чрезвычайный успех — все, и первая половина проникающие (максимум и добавочный бросок; у непроникающего оружия — максимум).
    /// При чрезвычайной сложности лучшее, что бывает, — успех без проникающих. Урон каждой пули бросается отдельно, броня
    /// вычитается из каждой; ранит залп одной суммой — как одна атака.
    /// </summary>
    private static AttackOutcome Volley(EncounterParticipant attacker, EncounterParticipant defender, CombatAttack attack,
        RangedAttackSetup setup, int bullets, SuccessLevel level, SuccessLevel required, int armor, IDiceRoller dice,
        List<string> lines, List<EncounterEffect> effects)
    {
        var allHit = level >= SuccessLevel.Extreme && required < SuccessLevel.Extreme;
        var hits = allHit ? bullets : Math.Max(1, bullets / 2);
        var extreme = allHit ? Math.Max(1, bullets / 2) : 0;
        lines.Add(allHit
            ? $"Попали все {N(bullets)} пуль залпа, проникающих — {N(extreme)} (чрезвычайный успех)."
            : $"Попала половина залпа: {N(hits)} из {N(bullets)}.");

        var entered = setup.DamageRolls ?? [];
        var next = 0;
        int? Entered() => next < entered.Count ? entered[next++] : null;

        List<DamageRoll> rolls = [];
        for (var i = 0; i < hits; i++)
        {
            var roll = i < extreme
                ? RollDamage(attack, attacker.Stats.DamageBonus, SuccessLevel.Extreme, canBeExtreme: true, attack.Impaling,
                    damageRoll: null, bonusRoll: null, extraRoll: attack.Impaling ? Entered() : null, armor, dice, (int)setup.Range)
                : RollDamage(attack, attacker.Stats.DamageBonus, SuccessLevel.Regular, canBeExtreme: false, impaling: false,
                    damageRoll: Entered(), bonusRoll: null, extraRoll: null, armor, dice, (int)setup.Range);
            rolls.Add(roll);
            lines.Add($"Пуля {N(i + 1)}. {roll.Describe()}");
        }

        var total = new DamageRoll(rolls[0].Formula, rolls.Sum(r => r.Rolled), rolls.Sum(r => r.Bonus), rolls.Sum(r => r.Extra),
            rolls.Sum(r => r.Raw), rolls.Sum(r => r.Armor), rolls.Sum(r => r.Total), extreme > 0, false, extreme > 0 && attack.Impaling);
        DamageOutcome? wound = null;
        if (total.Total > 0)
        {
            var (effect, outcome, conLine) = DamageEffect(defender, total.Total, setup.ConRoll, dice,
                $"{attack.Name}: {N(hits)} × {DiceNotation.Format(total.Formula)}");
            effects.Add(effect);
            wound = outcome;
            if (conLine is not null)
                lines.Add(conLine);
        }

        return new AttackOutcome
        {
            Resolution = Resolution(EncounterLogKind.Attack, attacker.Id,
                $"{attacker.Name} попадает: {N(hits)} из {N(bullets)}. Цель: {defender.Name} ({attack.Name})" + WoundTitle(total.Total, wound),
                lines, effects),
            Hit = true,
            Damage = total,
            Bullets = rolls,
            Wound = wound,
        };
    }

    /// <summary>Кости атаки ближнего боя — таблица <see cref="AttackModifiers"/> над участниками (панель показывает их до броска).</summary>
    public static AttackDice MeleeDice(EncounterState state, MeleeAttackSetup setup)
    {
        var defender = Require(state, setup.DefenderId);
        return AttackModifiers.Calculate(new AttackSituation
        {
            Kind = AttackKind.Melee,
            KeeperBonusDice = setup.KeeperBonusDice,
            KeeperPenaltyDice = setup.KeeperPenaltyDice,
            Surprise = setup.Surprise,
            TargetProne = defender.Combat.Prone,
            TargetDefensesThisRound = defender.Combat.DefensesIn(state.Round),
            TargetAttacksPerRound = defender.Profile.AttacksPerRound,
        });
    }

    /// <summary>
    /// Кости стрельбы. Прицел и укрытие — «из настройки или из состояния, один раз»; номер проверки очереди — по счёту
    /// стрелка в раунде, если его не задали.
    /// </summary>
    public static AttackDice RangedDice(EncounterState state, RangedAttackSetup setup)
    {
        var attacker = Require(state, setup.AttackerId);
        var defender = Require(state, setup.DefenderId);
        return AttackModifiers.Calculate(new AttackSituation
        {
            Kind = AttackKind.Ranged,
            KeeperBonusDice = setup.KeeperBonusDice,
            KeeperPenaltyDice = setup.KeeperPenaltyDice,
            Surprise = AttackModifiers.NormalizeSurprise(AttackKind.Ranged, setup.Surprise),
            PointBlank = setup.PointBlank,
            Aiming = setup.Aiming || attacker.Combat.Aiming,
            ShooterProne = attacker.Combat.Prone,
            TargetBuild = defender.Stats.Build,
            TargetTakingCover = setup.TargetTakingCover || defender.Combat.TakingCoverIn(state.Round),
            TargetBehindCover = setup.TargetBehindCover,
            TargetFastMoving = setup.TargetFastMoving,
            FiringIntoMelee = setup.FiringIntoMelee,
            FiringMode = setup.FiringMode,
            ReloadAndFire = setup.ReloadAndFire,
            TargetProne = defender.Combat.Prone,
            AutofireCheckIndex = AutofireIndex(state, setup),
        });
    }

    /// <summary>Номер проверки очереди в раунде (с нуля); не очередь — 0.</summary>
    public static int AutofireIndex(EncounterState state, RangedAttackSetup setup) =>
        setup.FiringMode == FiringMode.Volley ? setup.AutofireCheckIndex ?? Require(state, setup.AttackerId).Combat.AutofireIn(state.Round) : 0;

    /// <summary>Какой уровень успеха нужен этому выстрелу и возможен ли он (дальность и очередь, стр. 110, 114).</summary>
    public static (SuccessLevel Required, bool Impossible) RangedRequirement(EncounterState state, RangedAttackSetup setup)
    {
        var (required, impossible, _) = EscalateAutofire(AttackModifiers.RequiredDifficulty(setup.Range), AutofireIndex(state, setup));
        return (required, impossible);
    }

    /// <summary>
    /// Урон оружия (стр. 101, 106): обычный — бросок; чрезвычайный и критический успех (не при контратаке) — максимум
    /// костей и максимум БкУ, у проникающего — ещё бросок урона без БкУ. БкУ по виду атаки: полный, половина (с округлением
    /// вниз), нет. Броня цели и преграды снимает урон, но не ниже нуля.
    /// </summary>
    public static DamageRoll RollDamage(CombatAttack attack, string damageBonus, SuccessLevel level, bool canBeExtreme, bool impaling,
        int? damageRoll, int? bonusRoll, int? extraRoll, int armor, IDiceRoller dice, int rangeBand = 0)
    {
        var text = string.IsNullOrWhiteSpace(attack.Damage) ? CombatProfiles.BrawlDamage : attack.Damage.Trim();
        // Дробовик бьёт по полосе дальности (стр. 407): 0 — ближняя, 1 — средняя, 2 — большая; в описании — её формула.
        var info = DamageFormulaParser.Parse(text);
        var expression = info.At(rangeBand);
        if (info.IsByRange && expression is { RawText.Length: > 0 } band)
            text = band.RawText.Trim();
        var formula = DiceFormula.Parse(text);
        int Roll() => expression is { IsParsed: true } e ? e.Roll(dice) : formula.Roll(dice);
        int Max() => expression is { IsParsed: true } e ? e.Max : formula.Max;

        var extreme = canBeExtreme && level >= SuccessLevel.Extreme;
        var bonusFormula = DiceFormula.Parse(string.IsNullOrWhiteSpace(damageBonus) ? "0" : damageBonus);
        int rolled, bonus = 0, extra = 0;
        if (extreme)
        {
            rolled = Max();
            bonus = attack.DamageBonus switch
            {
                CreatureDamageBonusMode.Full or CreatureDamageBonusMode.OnlyBonus => bonusFormula.Max,
                CreatureDamageBonusMode.Half => bonusFormula.Max / 2,
                _ => 0,
            };
            if (impaling)
                extra = extraRoll ?? Roll();
        }
        else
        {
            rolled = damageRoll ?? Roll();
            if (attack.DamageBonus != CreatureDamageBonusMode.None)
            {
                var raw = bonusRoll ?? bonusFormula.Roll(dice);
                bonus = attack.DamageBonus == CreatureDamageBonusMode.Half ? raw / 2 : raw;
            }
        }

        var total = Math.Max(0, rolled + bonus + extra);
        var reduction = Math.Min(Math.Max(0, armor), total);
        return new DamageRoll(text, rolled, bonus, extra, total, reduction, total - reduction, extreme, extreme && level == SuccessLevel.Critical,
            extreme && impaling);
    }

    private static AttackOutcome Hit(EncounterParticipant attacker, EncounterParticipant defender, CombatAttack attack, DamageRoll damage,
        D100Roll? conRoll, IDiceRoller dice, List<string> lines, List<EncounterEffect> effects)
    {
        lines.Add(damage.Describe());
        DamageOutcome? wound = null;
        if (damage.Total > 0)
        {
            var (effect, outcome, conLine) = DamageEffect(defender, damage.Total, conRoll, dice, $"{attack.Name}: {damage.Formula}");
            effects.Add(effect);
            wound = outcome;
            if (conLine is not null)
                lines.Add(conLine);
        }

        return new AttackOutcome
        {
            Resolution = Resolution(EncounterLogKind.Attack, attacker.Id,
                $"{attacker.Name} попадает. Цель: {defender.Name} ({attack.Name})" + WoundTitle(damage.Total, wound), lines, effects),
            Hit = true,
            Damage = damage,
            Wound = wound,
        };
    }

    private static string WoundTitle(int total, DamageOutcome? wound) =>
        total <= 0 ? ". Урона нет." : $". Урон {N(total)}{(wound is { } w && w.Note() is { Length: > 0 } note ? $", {note}" : "")}.";

    private static int ArmorOf(EncounterParticipant defender, int coverArmor, bool ignores) =>
        ignores ? 0 : defender.Stats.Armor + Math.Max(0, coverArmor);

    private static CombatAttack BestMelee(EncounterParticipant p) =>
        p.Profile.Attacks.Where(a => a.Kind == CombatAttackKind.Melee).OrderByDescending(a => a.Skill).FirstOrDefault() ?? Unarmed(p);

    internal static EncounterResolution Resolution(EncounterLogKind kind, Guid? actorId, string title, List<string> lines, List<EncounterEffect> effects) =>
        new() { Kind = kind, ActorId = actorId, Title = title, Lines = lines, Effects = effects };
}
