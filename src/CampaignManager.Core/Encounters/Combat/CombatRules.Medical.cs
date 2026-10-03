using CampaignManager.Core.Characters;
using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Encounters;

/// <summary>Недельная проверка лечения серьёзной раны: условия (стр. 119).</summary>
public sealed record RecoverySetup
{
    public Guid ParticipantId { get; init; }

    /// <summary>Хороший уход: врач с лучшей Медициной прошёл проверку перед проверкой ВЫН — бонусная кость.</summary>
    public bool MedicalCare { get; init; }

    /// <summary>Отдых в комфортной обстановке — бонусная кость.</summary>
    public bool Rested { get; init; }

    /// <summary>Плохие условия — штрафная кость.</summary>
    public bool PoorConditions { get; init; }

    public D100Roll? Roll { get; init; }

    /// <summary>Выпало на 1d3 (успех) или 2d3 (чрезвычайный); null — бросит правило.</summary>
    public int? HealRoll { get; init; }
}

/// <summary>
/// Первая помощь, Медицина, проверки ВЫН умирающих, лечение (гл. 6, стр. 118–120, схема стр. 119). В v1 всё это жило в
/// razor-панелях и правило участников мимо «Применить»; здесь — правила с эффектами, а сами раны — <see cref="WoundRules"/>.
/// </summary>
public static partial class CombatRules
{
    /// <summary>
    /// Первая помощь (стр. 118, навык — гл. 4): одна проверка на рану, следующие — повторные; умирающему успех даёт
    /// временную стабилизацию, остальным +1 ПЗ и сознание. Пока пострадавшему снова не нанесут урон, второй раз первая
    /// помощь не действует (стабилизация умирающего — исключение).
    /// </summary>
    public static EncounterResolution FirstAid(EncounterState state, Guid healerId, Guid targetId, D100Roll? roll, IDiceRoller dice, int? skill = null)
    {
        var healer = Require(state, healerId);
        var target = Require(state, targetId);
        if (FirstAidBlockReason(target) is { } blocked)
            return Resolution(EncounterLogKind.Medical, healer.Id, blocked, [], []);

        var value = skill ?? healer.Profile.FirstAid;
        var test = Test(roll, value, dice);
        var pushed = target.Combat.FirstAidTried && !target.Dying;
        List<string> lines = [RollText($"{healer.Name} (Первая помощь{(pushed ? ", повторная проверка" : "")})", test.Roll, value, test.Level)];
        if (!test.Passed)
        {
            lines.Add(target.Dying
                ? "Умирающему можно пытаться снова в следующем раунде — а пока проверка ВЫН в конце раунда."
                : pushed ? "Повторная проверка провалена." : "Следующая попытка — повторная проверка.");
            return Resolution(EncounterLogKind.Medical, healer.Id, $"{healer.Name} не смог помочь {target.Name}.", lines,
                [new EncounterEffect { Kind = EncounterEffectKind.Treated, ParticipantId = target.Id, Key = CombatEffects.TreatedFirstAidTried }]);
        }

        var outcome = WoundRules.FirstAid(EncounterParticipants.Wounds(target), target.MaxHitPoints);
        lines.Add(outcome.Text + ".");
        return Resolution(EncounterLogKind.Medical, healer.Id, $"{healer.Name} оказывает первую помощь {target.Name}: {outcome.Text}.", lines,
        [
            Effect(EncounterEffectKind.FirstAid, target.Id, 1, detail: healer.Name),
            new EncounterEffect { Kind = EncounterEffectKind.Treated, ParticipantId = target.Id, Key = CombatEffects.TreatedFirstAid },
        ]);
    }

    public static string? FirstAidBlockReason(EncounterParticipant target)
    {
        if (target.Dead)
            return $"{target.Name} мёртв.";
        if (target.Dying && target.Stabilized)
            return $"{target.Name} уже стабилизирован — теперь нужна Медицина.";
        if (!target.Dying && target.Combat.FirstAidReceived)
            return $"Первая помощь {target.Name} по этой ране уже оказана — снова только после нового урона.";
        if (!target.Dying && target.HitPoints >= target.MaxHitPoints && !target.Unconscious)
            return $"{target.Name} не ранен.";
        return null;
    }

    /// <summary>
    /// Медицина (стр. 118): не меньше часа; не в тот же день — трудная проверка. Успех — +1d3 ПЗ и сознание; умирающему —
    /// только после первой помощи, тогда «при смерти» снимается. Второй раз по той же ране не действует.
    /// </summary>
    public static EncounterResolution Medicine(EncounterState state, Guid doctorId, Guid targetId, D100Roll? roll, bool notSameDay,
        int? healRoll, IDiceRoller dice, int? skill = null)
    {
        var doctor = Require(state, doctorId);
        var target = Require(state, targetId);
        if (MedicineBlockReason(target) is { } blocked)
            return Resolution(EncounterLogKind.Medical, doctor.Id, blocked, [], []);

        var value = skill ?? doctor.Profile.Medicine;
        var difficulty = notSameDay ? Difficulty.Hard : Difficulty.Regular;
        var test = Test(roll, value, dice, difficulty);
        List<string> lines = [RollText($"{doctor.Name} (Медицина{(notSameDay ? ", трудная — не в тот же день" : "")})", test.Roll, value, test.Level)];
        if (!test.Passed)
            return Resolution(EncounterLogKind.Medical, doctor.Id, $"{doctor.Name} не смог вылечить {target.Name}.", lines, []);

        var amount = healRoll is >= 1 and <= WoundRules.HealDieSides ? healRoll.Value : dice.Die(WoundRules.HealDieSides);
        var outcome = WoundRules.Medicine(EncounterParticipants.Wounds(target), target.MaxHitPoints, amount);
        lines.Add($"1d3 → {N(amount)}: {outcome.Text}.");
        return Resolution(EncounterLogKind.Medical, doctor.Id, $"{doctor.Name} лечит {target.Name} Медициной: {outcome.Text}.", lines,
        [
            Effect(EncounterEffectKind.Medicine, target.Id, amount, detail: $"1d3 → {N(amount)}"),
            new EncounterEffect { Kind = EncounterEffectKind.Treated, ParticipantId = target.Id, Key = CombatEffects.TreatedMedicine },
        ]);
    }

    public static string? MedicineBlockReason(EncounterParticipant target)
    {
        if (target.Dead)
            return $"{target.Name} мёртв.";
        if (target.Dying && !target.Stabilized)
            return $"{target.Name} при смерти: сначала первая помощь, потом Медицина.";
        if (!target.Dying && target.Combat.MedicineReceived)
            return $"Медицина {target.Name} по этой ране уже оказана — снова только после нового урона.";
        if (!target.Dying && target.HitPoints >= target.MaxHitPoints && !target.Unconscious)
            return $"{target.Name} не ранен.";
        return null;
    }

    /// <summary>
    /// Умирающие, которым положена проверка ВЫН в конце этого раунда: при смерти, не стабилизированы, стали умирающими в
    /// прошлом раунде или раньше (первая проверка — в конце следующего раунда, стр. 118) и в этом раунде ещё не проверялись.
    /// </summary>
    public static IReadOnlyList<EncounterParticipant> DyingChecksDue(EncounterState state) =>
    [
        .. state.Participants.Where(p => p is { Dying: true, Stabilized: false, Dead: false }
                                         && state.Round > 0
                                         && (p.Combat.DyingSinceRound is not { } since || since < state.Round)
                                         && p.Combat.DyingCheckedRound != state.Round),
    ];

    /// <summary>
    /// Проверка ВЫН умирающего (стр. 118): не стабилизирован — в конце раунда, провал — смерть; стабилизирован — в конце
    /// часа, провал — снова при смерти. Уровень — <see cref="Check"/>: у ВЫН за 100 сотня — крах (F-C06).
    /// </summary>
    public static EncounterResolution DyingCheck(EncounterState state, Guid participantId, D100Roll? roll, IDiceRoller dice)
    {
        var p = Require(state, participantId);
        var test = Test(roll, p.Stats.Con, dice);
        var outcome = WoundRules.DyingCheck(EncounterParticipants.Wounds(p), test.Passed);
        var when = p.Stabilized ? "в конце часа" : "в конце раунда";
        return Resolution(EncounterLogKind.Medical, p.Id, $"{p.Name}: проверка ВЫН {when} — {outcome.Refusal ?? outcome.Text}.",
            [RollText($"{p.Name} (ВЫН)", test.Roll, p.Stats.Con, test.Level)],
            outcome.Refusal is null ? [new EncounterEffect { Kind = EncounterEffectKind.DyingCheck, ParticipantId = p.Id, Check = test.Passed }] : []);
    }

    /// <summary>
    /// Лечение серьёзной раны в конце недели (стр. 119): проверка ВЫН с костями за уход, отдых и плохие условия; успех
    /// +1d3, чрезвычайный +2d3 и отметка снята, провал — ничего, крах — осложнение на усмотрение Хранителя.
    /// </summary>
    public static EncounterResolution WeeklyRecovery(EncounterState state, RecoverySetup setup, IDiceRoller dice)
    {
        var p = Require(state, setup.ParticipantId);
        var (bonus, penalty) = WoundRules.RecoveryDice(setup.MedicalCare, setup.Rested, setup.PoorConditions);
        var test = Test(setup.Roll, p.Stats.Con, dice, bonus: bonus, penalty: penalty);
        var count = WoundRules.RecoveryDiceCount(test.Level);
        var amount = WoundRules.RecoveryAmount(test.Level, setup.HealRoll, dice);
        var outcome = WoundRules.WeeklyRecovery(EncounterParticipants.Wounds(p), p.MaxHitPoints, test.Level, amount);

        List<string> lines = [RollText($"{p.Name} (ВЫН, лечение)", test.Roll, p.Stats.Con, test.Level)];
        List<string> dicesWhy = [];
        if (setup.MedicalCare)
            dicesWhy.Add("+1 уход врача");
        if (setup.Rested)
            dicesWhy.Add("+1 отдых");
        if (setup.PoorConditions)
            dicesWhy.Add("−1 плохие условия");
        if (dicesWhy.Count > 0)
            lines.Add($"Кости: {string.Join(", ", dicesWhy)}.");
        if (count > 0)
            lines.Add($"{N(count)}d3 → {N(amount)}.");

        return Resolution(EncounterLogKind.Medical, p.Id, $"{p.Name}: неделя лечения серьёзной раны — {outcome.Refusal ?? outcome.Text}.", lines,
            outcome.Refusal is null && test.Level.IsSuccess()
                ? [new EncounterEffect { Kind = EncounterEffectKind.Recovery, ParticipantId = p.Id, Amount = amount, Level = test.Level }]
                : []);
    }

    /// <summary>Отдых без серьёзной раны: 1 ПЗ в день (стр. 119).</summary>
    public static EncounterResolution NaturalRecovery(EncounterState state, Guid participantId, int days)
    {
        var p = Require(state, participantId);
        var outcome = WoundRules.NaturalRecovery(EncounterParticipants.Wounds(p), p.MaxHitPoints, days);
        var gained = outcome.After.HitPoints - outcome.Before.HitPoints;
        return Resolution(EncounterLogKind.Medical, p.Id, $"{p.Name}: отдых — {outcome.Refusal ?? outcome.Text}.", [],
            outcome.Refusal is null && gained > 0 ? [Effect(EncounterEffectKind.Heal, p.Id, gained, detail: $"{N(days)} дн. отдыха")] : []);
    }
}
