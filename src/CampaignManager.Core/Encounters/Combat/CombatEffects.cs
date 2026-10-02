using System.Globalization;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Encounters;

/// <summary>
/// Эффекты боя на снимке участника (T2.6b) — ветка <c>EncounterEngine.Describe</c>: она зовёт сюда всё, что не
/// касается общего ядра. Раны — только <see cref="WoundRules"/> (те же функции пишут лист в
/// <see cref="EncounterSheetEffects"/>), состояние боя — <see cref="CombatantState"/>. Возвращает строку предпросмотра
/// и журнала; null — эффект не боевой.
/// </summary>
public static class CombatEffects
{
    public static EffectPreview? Describe(int round, EncounterParticipant p, EncounterEffect effect)
    {
        var detail = string.IsNullOrWhiteSpace(effect.Detail) ? null : effect.Detail;
        var c = p.Combat;
        switch (effect.Kind)
        {
            case EncounterEffectKind.Damage:
            {
                var outcome = WoundRules.TakeDamage(EncounterParticipants.Wounds(p), p.MaxHitPoints, Math.Max(0, effect.Amount), effect.Check);
                EncounterParticipants.SetWounds(p, outcome.After);
                List<string> notes = [];
                if (outcome.Damage > 0 && !outcome.Before.Dead)
                {
                    // Новая рана: прицел потерян (стр. 111), первая помощь и Медицина — снова можно (гл. 4), сотворение сорвано (стр. 177).
                    c.Aiming = false;
                    c.FirstAidReceived = false;
                    c.FirstAidTried = false;
                    c.MedicineReceived = false;
                    if (outcome.FallsProne)
                        c.Prone = true;
                    if (c.Casting is { Disrupted: false } casting)
                    {
                        casting.Disrupted = true;
                        notes.Add($"сотворение «{casting.SpellName}» сорвано — ПМ и рассудок всё равно платятся");
                    }

                    if (outcome.After.Dying && (!outcome.Before.Dying || outcome.Before.Stabilized))
                        c.DyingSinceRound = round;
                }

                return Line(p, $"Урон {N(outcome.Damage)}", N(outcome.Before.HitPoints), N(outcome.After.HitPoints),
                    Join(detail, outcome.Note(), notes.Count > 0 ? string.Join("; ", notes) : null));
            }
            case EncounterEffectKind.Heal:
            {
                var before = EncounterParticipants.Wounds(p);
                var after = WoundRules.Heal(before, p.MaxHitPoints, effect.Amount);
                EncounterParticipants.SetWounds(p, after);
                return Line(p, $"Лечение {N(effect.Amount)}", N(before.HitPoints), N(after.HitPoints), Join(detail, before.Dead ? "мёртв — без изменений" : null));
            }
            case EncounterEffectKind.FirstAid:
                return Heal(p, "Первая помощь", WoundRules.FirstAid(EncounterParticipants.Wounds(p), p.MaxHitPoints), detail);
            case EncounterEffectKind.Medicine:
                return Heal(p, $"Медицина +{N(effect.Amount)}", WoundRules.Medicine(EncounterParticipants.Wounds(p), p.MaxHitPoints, effect.Amount), detail);
            case EncounterEffectKind.DyingCheck:
            {
                var wasStabilized = p.Stabilized;
                var line = Heal(p, effect.Check == true ? "ВЫН — успех" : "ВЫН — провал",
                    WoundRules.DyingCheck(EncounterParticipants.Wounds(p), effect.Check == true), detail);
                if (!wasStabilized)
                    c.DyingCheckedRound = round;
                else if (p.Dying && !p.Stabilized)
                    c.DyingSinceRound = round;
                return line;
            }
            case EncounterEffectKind.Recovery:
                return Heal(p, $"Лечение раны: {RulesText.Of(effect.Level ?? SuccessLevel.Failure)}",
                    WoundRules.WeeklyRecovery(EncounterParticipants.Wounds(p), p.MaxHitPoints, effect.Level ?? SuccessLevel.Failure, effect.Amount), detail);
            case EncounterEffectKind.Luck when p.Luck is { } luck:
            {
                p.Luck = Math.Clamp(luck + effect.Amount, 0, 99);
                if (effect.Amount < 0)
                    c.LuckSpentToStayConscious += -effect.Amount;
                return Line(p, "Удача", N(luck), N(p.Luck.Value), detail);
            }
            case EncounterEffectKind.Luck:
                return Line(p, "Удача", "—", "—", "у твари Удачи нет");
            case EncounterEffectKind.Awake:
            {
                var before = p.Unconscious;
                if (!p.Dead)
                    p.Unconscious = !effect.Flag;
                return Line(p, "Сознание", before ? "без сознания" : "в сознании", p.Unconscious ? "без сознания" : "в сознании",
                    Join(detail, effect.Flag ? "до конца раунда" : null));
            }
            case EncounterEffectKind.Attack:
            {
                c.Touch(round);
                var before = c.AttacksMade;
                c.AttacksMade += Math.Max(1, effect.Amount);
                var aim = effect.Flag && c.Aiming ? "прицел потрачен" : null;
                if (effect.Flag)
                    c.Aiming = false;
                return Line(p, "Атак за раунд", N(before), N(c.AttacksMade), Join(detail, aim));
            }
            case EncounterEffectKind.Defense:
            {
                c.Touch(round);
                var before = c.DefensesMade;
                c.DefensesMade++;
                return Line(p, "Защит за раунд", N(before), N(c.DefensesMade), detail);
            }
            case EncounterEffectKind.Autofire:
            {
                c.Touch(round);
                var before = c.AutofireChecks;
                c.AutofireChecks++;
                return Line(p, "Проверок очереди", N(before), N(c.AutofireChecks), detail);
            }
            case EncounterEffectKind.Ammo:
            {
                var key = effect.Key ?? "";
                var before = c.Ammo.TryGetValue(key, out var loaded) ? N(loaded) : "полный";
                c.Ammo[key] = Math.Max(0, effect.Amount);
                return Line(p, $"Патроны: {AttackName(p, key)}", before, N(c.Ammo[key]), detail);
            }
            case EncounterEffectKind.Jam:
            {
                var key = effect.Key ?? "";
                var before = c.JammedAttack == key ? $"заклинило ({N(c.JamRoundsLeft)})" : "исправно";
                if (effect.Amount > 0)
                {
                    c.JammedAttack = key;
                    c.JamRoundsLeft = effect.Amount;
                }
                else if (c.JammedAttack == key)
                {
                    c.JammedAttack = null;
                    c.JamRoundsLeft = 0;
                }

                return Line(p, AttackName(p, key), before, effect.Amount > 0 ? $"заклинило ({N(effect.Amount)})" : "исправно", detail);
            }
            case EncounterEffectKind.Aim:
                return Toggle(p, "Прицел", c.Aiming, effect.Flag, v => c.Aiming = v, "целится", "нет", detail);
            case EncounterEffectKind.Cover:
            {
                c.CoverRound = effect.Flag ? round : null;
                c.AttackBlockedRound = effect.Flag ? effect.Amount : null;
                return Line(p, "Укрытие", "нет", effect.Flag ? "укрылся" : "нет",
                    Join(detail, effect.Flag ? $"теряет атаку в раунде {N(effect.Amount)}" : null));
            }
            case EncounterEffectKind.Prone:
                return Toggle(p, "Положение", c.Prone, effect.Flag, v => c.Prone = v, "лежит", "на ногах", detail);
            case EncounterEffectKind.Grapple:
            {
                var before = c.GrappledBy is null ? "свободен" : "схвачен";
                c.GrappledBy = effect.Flag ? effect.OtherId : null;
                return Line(p, "Захват", before, effect.Flag ? "схвачен" : "свободен", detail);
            }
            case EncounterEffectKind.Disarm:
                return Toggle(p, "Оружие", c.Disarmed, effect.Flag, v => c.Disarmed = v, "выбито", "в руках", detail);
            case EncounterEffectKind.Disadvantage:
                return Toggle(p, "Положение", c.Disadvantage, effect.Flag, v => c.Disadvantage = v, "невыгодное", "обычное", detail);
            case EncounterEffectKind.Ready:
                return Toggle(p, "Огнестрел", c.FirearmReady, effect.Flag, v => c.FirearmReady = v, "наготове (+50)", "нет", detail);
            case EncounterEffectKind.Casting:
            {
                var before = c.Casting is { } was ? $"творит «{was.SpellName}»" : "—";
                c.Casting = effect.Casting is null ? null : effect.Casting with { };
                return Line(p, "Сотворение", before,
                    c.Casting is { } now ? $"творит «{now.SpellName}» до раунда {N(now.CompletesInRound)}" : "—", detail);
            }
            case EncounterEffectKind.Treated:
            {
                switch (effect.Key)
                {
                    case TreatedFirstAid:
                        c.FirstAidReceived = true;
                        c.FirstAidTried = true;
                        break;
                    case TreatedFirstAidTried:
                        c.FirstAidTried = true;
                        break;
                    case TreatedMedicine:
                        c.MedicineReceived = true;
                        break;
                }

                return Line(p, "Лечение по ране", "", effect.Key switch
                {
                    TreatedFirstAid => "первая помощь оказана",
                    TreatedFirstAidTried => "первую помощь пробовали",
                    _ => "Медицина оказана",
                }, detail);
            }
            default:
                return null;
        }
    }

    public const string TreatedFirstAid = "firstAid";
    public const string TreatedFirstAidTried = "firstAidTried";
    public const string TreatedMedicine = "medicine";

    private static EffectPreview Heal(EncounterParticipant p, string label, HealOutcome outcome, string? detail)
    {
        EncounterParticipants.SetWounds(p, outcome.After);
        return Line(p, label, N(outcome.Before.HitPoints), N(outcome.After.HitPoints),
            Join(detail, outcome.Refusal ?? outcome.Text, outcome.Refusal is null ? WoundRules.StateText(outcome.After) : null));
    }

    private static EffectPreview Toggle(EncounterParticipant p, string label, bool before, bool after, Action<bool> set,
        string on, string off, string? detail)
    {
        set(after);
        return Line(p, label, before ? on : off, after ? on : off, detail);
    }

    private static string AttackName(EncounterParticipant p, string key) =>
        p.Profile.Attacks.FirstOrDefault(a => a.Key == key)?.Name ?? "оружие";

    private static EffectPreview Line(EncounterParticipant p, string label, string before, string after, string? note) =>
        new(p.Id, p.Name, label, before, after, note);

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string? Join(params string?[] parts)
    {
        var present = parts.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        return present.Count == 0 ? null : string.Join("; ", present);
    }
}
