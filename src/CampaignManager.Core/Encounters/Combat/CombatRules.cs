using System.Globalization;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Encounters;

/// <summary>
/// Правила боя (гл. 6, T2.6b) на ядре сцены. Каждое правило <b>разрешает</b> действие и возвращает
/// <see cref="EncounterResolution"/> — строки и эффекты, ничего не меняя; применяет их только
/// <see cref="EncounterEngine.Apply(EncounterState, DateTimeOffset)"/>. Поэтому «Отменить» не тратит ни ход, ни патрон, ни
/// защиту цели (F-C02, в v1 всё это менялось ещё при разрешении).
/// <para>
/// Любой бросок можно вписать: d100 приходит <see cref="D100Roll"/> (вписанный — <see cref="D100Roll.Entered"/>), сумма
/// костей — числом; не пришло — бросает <see cref="IDiceRoller"/>. Уровни — только <see cref="Check"/>: проверки ВЫН и ИНТ
/// в бою v1 считали <c>roll &lt;= value</c> и расходились на 01 и 100 (F-C06). Кости атаки — таблица
/// <see cref="AttackModifiers"/>, общая с памяткой ширмы. Раны — <see cref="WoundRules"/>, общий с листом.
/// </para>
/// </summary>
public static partial class CombatRules
{
    /// <summary>Безоружная атака, если у участника не нашлось выбранной (стр. 66): драка 1D3 + БкУ.</summary>
    public static CombatAttack Unarmed(EncounterParticipant p) =>
        p.Profile.Attacks.FirstOrDefault(a => a.Key == CombatProfiles.BrawlKey)
        ?? new CombatAttack
        {
            Key = CombatProfiles.BrawlKey,
            Name = "Драка (без оружия)",
            Skill = p.Profile.FightBack,
            Damage = CombatProfiles.BrawlDamage,
            Kind = CombatAttackKind.Melee,
            DamageBonus = Catalogs.CreatureDamageBonusMode.Full,
        };

    public static CombatAttack AttackOf(EncounterParticipant p, string? key) =>
        p.Profile.Attacks.FirstOrDefault(a => a.Key == key) ?? Unarmed(p);

    /// <summary>
    /// Почему участник не может атаковать; null — может. Творит заклинание (стр. 241), укрывался от огня (стр. 111), исчерпал
    /// атаки раунда (стр. 100), выбыл из боя.
    /// </summary>
    public static string? AttackBlockReason(EncounterState state, EncounterParticipant p)
    {
        if (p.Dead)
            return $"{p.Name} мёртв.";
        if (p.Dying || p.Unconscious)
            return $"{p.Name} без сознания.";
        if (p.Combat.Casting is { } casting)
            return $"{p.Name} творит «{casting.SpellName}» (сработает в раунде {N(casting.CompletesInRound)}). Атаковать можно, только бросив сотворение — вкладка «Заклинание».";
        if (p.Combat.AttackBlockedRound == state.Round && state.Round > 0)
            return $"{p.Name} укрывался от огня и теряет атаку в этом раунде (стр. 111). До следующей атаки он может только уклоняться.";

        var limit = Math.Max(1, p.Profile.AttacksPerRound);
        var made = p.Combat.AttacksIn(state.Round);
        if (made >= limit)
            return $"{p.Name} уже совершил все свои атаки за раунд ({N(made)} из {N(limit)}).";
        return null;
    }

    /// <summary>Почему из этого оружия нельзя стрелять: заклинило, магазин пуст.</summary>
    public static string? WeaponBlockReason(EncounterParticipant p, CombatAttack attack)
    {
        if (p.Combat.JammedAttack == attack.Key)
            return $"{attack.Name} заклинило: починка ещё {N(p.Combat.JamRoundsLeft)} р. (Механика или Стрельба, стр. 113).";
        if (attack.IsRanged && AmmoLeft(p, attack) == 0)
            return $"{attack.Name}: магазин пуст — перезарядите.";
        return null;
    }

    /// <summary>Патронов в магазине; null — оружие без магазина.</summary>
    public static int? AmmoLeft(EncounterParticipant p, CombatAttack attack) =>
        attack.AmmoCapacity is { } capacity ? p.Combat.Ammo.TryGetValue(attack.Key, out var left) ? left : capacity : null;

    /// <summary>
    /// Разрешение встречной проверки (стр. 101): лучший уровень побеждает; ничья при уклонении — защитнику, при контратаке —
    /// атакующему.
    /// </summary>
    public static bool AttackerWinsOpposed(SuccessLevel attacker, SuccessLevel defender, DefenseReaction reaction)
    {
        if (attacker != defender)
            return attacker > defender;
        return reaction == DefenseReaction.FightBack;
    }

    /// <summary>Размер очереди при непрерывном огне: навык / 10, но не меньше трёх (стр. 112).</summary>
    public static int VolleySize(int firearmSkill) => Math.Max(3, firearmSkill / 10);

    /// <summary>
    /// Нарастающая сложность очереди (стр. 114): первая проверка в раунде — как обычно; каждая следующая — штрафная кость,
    /// а когда штрафных набирается три, остаётся две и сложность поднимается на уровень: обычная → трудная →
    /// чрезвычайная → критическая (только 01) → невозможно.
    /// </summary>
    public static (SuccessLevel Required, bool Impossible, int ExtraPenaltyDice) EscalateAutofire(Difficulty baseDifficulty, int checkIndex)
    {
        var baseLevel = Check.Required(baseDifficulty);
        if (checkIndex <= 0)
            return (baseLevel, false, 0);

        var extraPenalty = Math.Min(checkIndex, 2);
        var levelsUp = Math.Max(0, checkIndex - 2);
        var index = (int)baseLevel - (int)SuccessLevel.Regular + levelsUp;
        return index >= 4
            ? (SuccessLevel.Critical, true, extraPenalty)
            : ((SuccessLevel)((int)SuccessLevel.Regular + index), false, extraPenalty);
    }

    /// <summary>Сложность для порога краха (стр. 88): у требования «критический» — та же, что у чрезвычайного.</summary>
    public static Difficulty DifficultyOf(SuccessLevel required) => required switch
    {
        SuccessLevel.Hard => Difficulty.Hard,
        >= SuccessLevel.Extreme => Difficulty.Extreme,
        _ => Difficulty.Regular,
    };

    /// <summary>Союзник стрелка с наименьшей Удачей — в него уходит пуля при крахе в ближнем бою (стр. 112). У тварей Удачи нет.</summary>
    public static EncounterParticipant? UnluckiestAlly(EncounterState state, EncounterParticipant shooter, EncounterParticipant target) =>
        state.Participants
            .Where(p => p.Id != shooter.Id && p.Id != target.Id && !p.Dead && p.Side == shooter.Side && p.Luck is not null)
            .OrderBy(p => p.Luck)
            .FirstOrDefault();

    /// <summary>Проверка d100 со сложностью: уровень и пройдена ли.</summary>
    internal static (D100Roll Roll, SuccessLevel Level, bool Passed) Test(D100Roll? entered, int value, IDiceRoller dice,
        Difficulty difficulty = Difficulty.Regular, int bonus = 0, int penalty = 0)
    {
        var roll = entered ?? D100.Roll(dice, bonus, penalty);
        var level = Check.Evaluate(roll.Result, value, difficulty);
        return (roll, level, Check.Passes(level, difficulty));
    }

    internal static string RollText(string who, D100Roll roll, int value, SuccessLevel level) =>
        $"{who}: {N(roll.Result)} против {N(value)} — {RulesText.Of(level)}{RulesText.RollDetail(roll)}";

    internal static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

    internal static EncounterParticipant Require(EncounterState state, Guid id) =>
        state.Find(id) ?? throw new ArgumentException("Участника нет в сцене.", nameof(id));

    internal static EncounterEffect Effect(EncounterEffectKind kind, Guid participantId, int amount = 0, bool flag = false,
        string? detail = null) =>
        new() { Kind = kind, ParticipantId = participantId, Amount = amount, Flag = flag, Detail = detail };

    /// <summary>Урон по цели эффектом: ВЫН при серьёзной ране — вписанная или брошенная (проверка через <see cref="Check"/>).</summary>
    internal static (EncounterEffect Effect, DamageOutcome Wound, string? ConLine) DamageEffect(
        EncounterParticipant target, int amount, D100Roll? conRoll, IDiceRoller dice, string? detail)
    {
        bool? passed = null;
        string? line = null;
        if (WoundRules.IsMajorWound(amount, target.MaxHitPoints) && !WoundRules.IsInstantDeath(amount, target.MaxHitPoints) && !target.Dead)
        {
            var (roll, level, ok) = Test(conRoll, target.Stats.Con, dice);
            passed = ok;
            line = $"Серьёзная рана — {RollText($"ВЫН {target.Name}", roll, target.Stats.Con, level)}: {(ok ? "в сознании" : "теряет сознание")}.";
        }

        var wound = WoundRules.TakeDamage(EncounterParticipants.Wounds(target), target.MaxHitPoints, amount, passed);
        var effect = new EncounterEffect
        {
            Kind = EncounterEffectKind.Damage, ParticipantId = target.Id, Amount = amount, Check = passed, Detail = detail,
        };
        return (effect, wound, line);
    }
}

/// <summary>Как цель встречает атаку ближнего боя (стр. 101).</summary>
public enum DefenseReaction
{
    Dodge,
    FightBack,
}
