using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Encounters;

/// <summary>Боевой манёвр (стр. 103) и «киношный» нокаут (необязательное правило, стр. 123).</summary>
public enum ManeuverType
{
    Disarm,
    KnockDown,
    Grapple,
    Push,

    /// <summary>Вырваться из захвата: успех освобождает того, кто вырывается.</summary>
    BreakFree,

    Disadvantage,

    /// <summary>Нокаут ударным оружием: 1 ПЗ и без сознания (необязательное правило, стр. 123).</summary>
    Knockout,
}

public sealed record ManeuverSetup
{
    public Guid AttackerId { get; init; }
    public Guid DefenderId { get; init; }
    public ManeuverType Type { get; init; }

    /// <summary>Навык манёвра: драка, иной навык ближнего боя или атака-манёвр твари; null — лучшая атака ближнего боя.</summary>
    public int? AttackSkill { get; init; }

    public DefenseReaction Reaction { get; init; }
    public int? DefenseSkill { get; init; }

    /// <summary>Комплекция, если Хранитель её поправил; null — из снимков.</summary>
    public int? AttackerBuild { get; init; }

    public int? DefenderBuild { get; init; }

    public int KeeperBonusDice { get; init; }
    public int KeeperPenaltyDice { get; init; }
    public int DefenderBonusDice { get; init; }
    public int DefenderPenaltyDice { get; init; }

    public D100Roll? AttackRoll { get; init; }
    public D100Roll? DefenseRoll { get; init; }
}

public static partial class CombatRules
{
    public static string NameOf(ManeuverType type) => type switch
    {
        ManeuverType.Disarm => "Разоружить",
        ManeuverType.KnockDown => "Сбить с ног",
        ManeuverType.Grapple => "Захват",
        ManeuverType.Push => "Толкнуть",
        ManeuverType.BreakFree => "Вырваться",
        ManeuverType.Disadvantage => "Невыгодное положение",
        ManeuverType.Knockout => "Нокаут",
        _ => type.ToString(),
    };

    /// <summary>
    /// Манёвр (стр. 103): разница Комплекции 3 и больше — невозможен (ход не тратится); меньше — по штрафной кости за
    /// пункт, не больше двух. Встречная проверка как в ближнем бою. Эффект — при «Применить»: повален, схвачен, разоружён,
    /// невыгодное положение; «Вырваться» освобождает самого вырвавшегося (в v1 снимало захват с цели, F-C08).
    /// </summary>
    public static AttackOutcome Maneuver(EncounterState state, ManeuverSetup setup, IDiceRoller dice)
    {
        var attacker = Require(state, setup.AttackerId);
        var defender = Require(state, setup.DefenderId);
        var name = NameOf(setup.Type);
        var attackerBuild = setup.AttackerBuild ?? attacker.Stats.Build;
        var defenderBuild = setup.DefenderBuild ?? defender.Stats.Build;
        var gap = defenderBuild - attackerBuild;

        if (setup.Type == ManeuverType.Knockout && !state.Combat.CinematicKnockout)
        {
            const string off = "Нокаут — необязательное правило (стр. 123): включите его в «Прочее».";
            return new AttackOutcome { Resolution = Resolution(EncounterLogKind.Maneuver, attacker.Id, off, [], []), Blocked = off };
        }

        if (gap >= 3)
        {
            var impossible = $"Манёвр «{name}» невозможен: Комплекция {attacker.Name} ({N(attackerBuild)}) ниже, чем у {defender.Name} ({N(defenderBuild)}), на 3 и больше (стр. 103).";
            return new AttackOutcome { Resolution = Resolution(EncounterLogKind.Maneuver, attacker.Id, impossible, [], []), Blocked = impossible };
        }

        var buildPenalty = Math.Clamp(gap, 0, 2);
        List<string> reasons = [];
        if (setup.KeeperBonusDice > 0)
            reasons.Add($"+{N(setup.KeeperBonusDice)} от Хранителя");
        if (setup.KeeperPenaltyDice > 0)
            reasons.Add($"−{N(setup.KeeperPenaltyDice)} от Хранителя");
        if (buildPenalty > 0)
            reasons.Add($"−{N(buildPenalty)} разница Комплекции ({N(attackerBuild)} против {N(defenderBuild)})");
        var modifiers = new AttackDice(Math.Max(0, setup.KeeperBonusDice), Math.Max(0, setup.KeeperPenaltyDice) + buildPenalty, reasons);

        var skill = setup.AttackSkill ?? BestMelee(attacker).Skill;
        var (roll, level, _) = Test(setup.AttackRoll, skill, dice, bonus: modifiers.BonusDice, penalty: modifiers.PenaltyDice);
        var defenseSkill = setup.DefenseSkill ?? (setup.Reaction == DefenseReaction.FightBack ? defender.Profile.FightBack : defender.Stats.Dodge);
        var defense = Test(setup.DefenseRoll, defenseSkill, dice, bonus: setup.DefenderBonusDice, penalty: setup.DefenderPenaltyDice);

        List<string> lines =
        [
            RollText($"{attacker.Name} («{name}»)", roll, skill, level),
            RollText($"{defender.Name} ({(setup.Reaction == DefenseReaction.FightBack ? "контратака" : "уклонение")})", defense.Roll, defenseSkill, defense.Level),
        ];
        if (reasons.Count > 0)
            lines.Add($"Кости: {string.Join(", ", reasons)}.");

        // Манёвр заменяет атаку и тратит её при любом исходе.
        List<EncounterEffect> effects = [Effect(EncounterEffectKind.Attack, attacker.Id, 1, flag: true)];
        if (level <= SuccessLevel.Failure)
        {
            return new AttackOutcome
            {
                Resolution = Resolution(EncounterLogKind.Maneuver, attacker.Id, $"{attacker.Name}: манёвр «{name}» не удался.", lines, effects),
                Modifiers = modifiers, Roll = roll, Level = level, DefenseRoll = defense.Roll, DefenseLevel = defense.Level,
            };
        }

        var succeeded = defense.Level <= SuccessLevel.Failure || AttackerWinsOpposed(level, defense.Level, setup.Reaction);
        effects.Add(Effect(EncounterEffectKind.Defense, defender.Id));
        if (succeeded)
            effects.AddRange(ManeuverEffects(setup.Type, attacker, defender));

        var title = succeeded
            ? $"{attacker.Name}: манёвр «{name}» против {defender.Name} удался."
            : $"{attacker.Name}: манёвр «{name}» не удался — {defender.Name} защитился.";
        return new AttackOutcome
        {
            Resolution = Resolution(EncounterLogKind.Maneuver, attacker.Id, title, lines, effects),
            Modifiers = modifiers, Roll = roll, Level = level, DefenseRoll = defense.Roll, DefenseLevel = defense.Level, Hit = succeeded,
        };
    }

    private static IEnumerable<EncounterEffect> ManeuverEffects(ManeuverType type, EncounterParticipant attacker, EncounterParticipant defender)
    {
        switch (type)
        {
            case ManeuverType.KnockDown:
            case ManeuverType.Push:
                yield return Effect(EncounterEffectKind.Prone, defender.Id, flag: true);
                break;
            case ManeuverType.Grapple:
                yield return new EncounterEffect { Kind = EncounterEffectKind.Grapple, ParticipantId = defender.Id, Flag = true, OtherId = attacker.Id };
                break;
            case ManeuverType.BreakFree:
                yield return Effect(EncounterEffectKind.Grapple, attacker.Id, flag: false, detail: "вырвался");
                break;
            case ManeuverType.Disarm:
                yield return Effect(EncounterEffectKind.Disarm, defender.Id, flag: true);
                break;
            case ManeuverType.Disadvantage:
                // Правила не задают эффект — отметка для Хранителя.
                yield return Effect(EncounterEffectKind.Disadvantage, defender.Id, flag: true);
                break;
            case ManeuverType.Knockout:
                yield return Effect(EncounterEffectKind.Damage, defender.Id, 1, detail: "нокаут");
                yield return Effect(EncounterEffectKind.Awake, defender.Id, flag: false, detail: "нокаут");
                break;
        }
    }
}
