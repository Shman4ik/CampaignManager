using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Characters;

/// <summary>
/// Состояние ран одного существа: ПЗ и отметки. Один и тот же тип у листа (<see cref="SheetCondition"/> +
/// <see cref="CurrentValues.HitPoints"/>) и у снимка участника сцены — поэтому правило ран одно (в v1 их было три:
/// лист, бой и погоня, и они расходились в мгновенной смерти, F-S02).
/// </summary>
/// <param name="HitPoints">Текущие ПЗ (у стабилизированного — тот самый временный 1 ПЗ).</param>
/// <param name="MajorWound">Отметка «Серьёзная рана» (стр. 117).</param>
/// <param name="Unconscious">Без сознания: 0 ПЗ, провал ВЫН при серьёзной ране, при смерти.</param>
/// <param name="Dying">«При смерти»: 0 ПЗ при серьёзной ране (стр. 118).</param>
/// <param name="Stabilized">Временная стабилизация первой помощью: ждёт Медицину, ВЫН раз в час (стр. 118–119).</param>
/// <param name="Dead">Мёртв: урон одной атаки ≥ максимума ПЗ или провал ВЫН умирающего.</param>
public readonly record struct WoundStatus(
    int HitPoints,
    bool MajorWound = false,
    bool Unconscious = false,
    bool Dying = false,
    bool Stabilized = false,
    bool Dead = false);

/// <summary>Что сделала одна атака (<see cref="WoundRules.TakeDamage"/>).</summary>
public sealed record DamageOutcome(
    WoundStatus Before,
    WoundStatus After,
    int Damage,
    bool InstantDeath,
    bool MajorWound,
    bool? ConPassed)
{
    /// <summary>Серьёзная рана валит с ног (стр. 117).</summary>
    public bool FallsProne => MajorWound && !InstantDeath;

    /// <summary>Нужна проверка ВЫН (серьёзная рана), а её исхода не дали — Хранитель проведёт её сам.</summary>
    public bool ConCheckMissing => MajorWound && !InstantDeath && ConPassed is null;

    /// <summary>Подпись последствий для журнала и предпросмотра: «серьёзная рана, ВЫН — провал, без сознания».</summary>
    public string Note()
    {
        if (InstantDeath)
            return "мгновенная смерть (урон не меньше максимума ПЗ)";

        List<string> notes = [];
        if (MajorWound)
        {
            notes.Add("серьёзная рана, падает");
            notes.Add(ConPassed switch
            {
                true => "ВЫН — успех",
                false => "ВЫН — провал",
                null => "нужна проверка ВЫН",
            });
        }

        notes.Add(WoundRules.StateText(After) ?? "");
        return string.Join(", ", notes.Where(n => n.Length > 0));
    }
}

/// <summary>Почему лечение не подействовало; null — подействовало.</summary>
public sealed record HealOutcome(WoundStatus Before, WoundStatus After, string? Refusal, string Text);

/// <summary>Недельная проверка лечения на листе (<see cref="WoundRules.ApplyWeeklyRecovery"/>): бросок ВЫН, уровень, выпавшее на 1d3/2d3.</summary>
public sealed record RecoveryCheck(D100Roll Roll, SuccessLevel Level, int Amount, HealOutcome Outcome);

/// <summary>
/// Раны и лечение (гл. 6, стр. 117–120) — <b>один конвейер</b> для листа и сцены по схеме получения урона ширмы
/// Хранителя (стр. 119) и решению владельца 2026-10-02 (F-S02):
/// <list type="number">
/// <item>урон одной атаки ≥ максимума ПЗ — мгновенная смерть; ≥ половины — серьёзная рана: падает, проверка ВЫН, при
/// провале без сознания;</item>
/// <item>ПЗ упали до 0: без отметки «Серьёзная рана» — без сознания (выздоравливает 1 ПЗ в день, первая помощь +1,
/// Медицина +1d3); с отметкой — «при смерти» и 0 ПЗ;</item>
/// <item>при смерти: первая помощь в этом раунде — временная стабилизация (+1 ПЗ); нет — проверка ВЫН в конце следующего
/// раунда и каждого за ним, провал — смерть;</item>
/// <item>стабилизирован: Медицина в течение часа — «при смерти» снимается, +1d3 ПЗ, дальше лечение серьёзной раны; нет —
/// ВЫН в конце каждого часа, провал — снова при смерти (временный ПЗ потерян);</item>
/// <item>лечение серьёзной раны — ВЫН в конце недели: провал — ничего, успех +1d3, чрезвычайный +2d3 и отметка снята.</item>
/// </list>
/// Всё — над <see cref="WoundStatus"/>: лист и снимок участника лишь читают и пишут свои поля
/// (<see cref="Read(CharacterSheet)"/>, <see cref="Write(CharacterSheet, WoundStatus)"/>).
/// </summary>
public static class WoundRules
{
    /// <summary>Кости Медицины и лечения серьёзной раны (стр. 118–119): вписанное — через <see cref="EnteredDiceRoller.Total"/>.</summary>
    public const int HealDieSides = 3;

    /// <summary>
    /// Урон, с которого рана серьёзная: «равен или больше половины максимальных ПЗ». При 13 ПЗ половина —
    /// 6,5, рана начинается с 7: округление вверх.
    /// </summary>
    public static int MajorWoundThreshold(int maxHitPoints) => Math.Max(1, (maxHitPoints + 1) / 2);

    public static bool IsMajorWound(int damage, int maxHitPoints) => damage >= MajorWoundThreshold(maxHitPoints);

    /// <summary>
    /// Урон одной атаки, от которого умирают на месте: «равен или больше» максимума ПЗ (F-S02, решение владельца по
    /// официальному тексту 7e; в русском переводе список на стр. 118 говорит «больше» — ошибка перевода).
    /// </summary>
    public static bool IsInstantDeath(int damage, int maxHitPoints) => damage > 0 && damage >= Math.Max(1, maxHitPoints);

    /// <summary>
    /// Одна атака (стр. 117–118, схема стр. 119). <paramref name="conPassed"/> — исход проверки ВЫН при серьёзной ране
    /// (бросок вписан или брошен тем, кто зовёт); null — не проводили, сознание не теряется, а
    /// <see cref="DamageOutcome.ConCheckMissing"/> напомнит. Дальнейший урон при 0 ПЗ не записывается; новая рана
    /// снимает стабилизацию.
    /// </summary>
    public static DamageOutcome TakeDamage(WoundStatus status, int maxHitPoints, int damage, bool? conPassed = null)
    {
        if (damage <= 0 || status.Dead)
            return new DamageOutcome(status, status, Math.Max(0, damage), false, false, null);

        if (IsInstantDeath(damage, maxHitPoints))
        {
            var dead = status with { HitPoints = 0, Dead = true, Dying = false, Stabilized = false, Unconscious = false };
            return new DamageOutcome(status, dead, damage, true, false, null);
        }

        var major = IsMajorWound(damage, maxHitPoints);
        var after = status with
        {
            HitPoints = Math.Max(0, status.HitPoints - damage),
            MajorWound = status.MajorWound || major,
            Stabilized = false,
        };

        if (major && conPassed == false)
            after = after with { Unconscious = true };

        if (after.HitPoints == 0)
            after = after.MajorWound
                ? after with { Dying = true, Unconscious = true }
                : after with { Unconscious = true };

        return new DamageOutcome(status, after, damage, false, major, major ? conPassed : null);
    }

    /// <summary>
    /// Первая помощь, успешная проверка (стр. 118 и навык, гл. 4): умирающему — временная стабилизация, 1 ПЗ
    /// (отметка «при смерти» остаётся до Медицины); остальным — +1 ПЗ и в сознание.
    /// </summary>
    public static HealOutcome FirstAid(WoundStatus status, int maxHitPoints)
    {
        if (status.Dead)
            return new HealOutcome(status, status, "мёртвому первая помощь не поможет", "без изменений");

        if (status.Dying)
        {
            if (status.Stabilized)
                return new HealOutcome(status, status, "уже стабилизирован — теперь нужна Медицина", "без изменений");

            var stabilized = status with { Stabilized = true, HitPoints = Math.Max(status.HitPoints, 1) };
            return new HealOutcome(status, stabilized, null, "временная стабилизация: 1 ПЗ, проверка ВЫН раз в час, нужна Медицина");
        }

        var healed = status with { HitPoints = Math.Min(Math.Max(maxHitPoints, 0), status.HitPoints + 1), Unconscious = false };
        return new HealOutcome(status, healed, null, "+1 ПЗ, в сознании");
    }

    /// <summary>
    /// Медицина, успешная проверка (стр. 118): +<paramref name="amount"/> (1d3) ПЗ и в сознание. Умирающему — только
    /// после первой помощи: тогда «при смерти» снимается и начинается лечение серьёзной раны.
    /// </summary>
    public static HealOutcome Medicine(WoundStatus status, int maxHitPoints, int amount)
    {
        if (status.Dead)
            return new HealOutcome(status, status, "мёртвому Медицина не поможет", "без изменений");

        if (status.Dying && !status.Stabilized)
            return new HealOutcome(status, status, "умирающему сначала нужна первая помощь (стр. 118)", "без изменений");

        var healed = status with
        {
            HitPoints = Math.Min(Math.Max(maxHitPoints, 0), status.HitPoints + Math.Max(0, amount)),
            Dying = false,
            Stabilized = false,
            Unconscious = false,
        };
        var text = status.Dying
            ? $"«при смерти» снято, +{amount} ПЗ; дальше — проверка лечения серьёзной раны раз в неделю"
            : $"+{amount} ПЗ, в сознании";
        return new HealOutcome(status, healed, null, text);
    }

    /// <summary>
    /// Проверка ВЫН умирающего (стр. 118, схема стр. 119). Не стабилизирован — в конце раунда: провал — смерть, успех —
    /// доживает до следующего. Стабилизирован — в конце часа: провал — временный ПЗ потерян, снова при смерти (нужна
    /// первая помощь и проверки каждый раунд), успех — ждёт Медицину дальше.
    /// </summary>
    public static HealOutcome DyingCheck(WoundStatus status, bool conPassed)
    {
        if (status.Dead || !status.Dying)
            return new HealOutcome(status, status, "не при смерти — проверка не нужна", "без изменений");

        if (status.Stabilized)
        {
            if (conPassed)
                return new HealOutcome(status, status, null, "стабилизирован ещё час — ждёт Медицину");

            var worse = status with { Stabilized = false, HitPoints = 0, Unconscious = true };
            return new HealOutcome(status, worse, null, "состояние ухудшилось: временный ПЗ потерян, снова при смерти");
        }

        if (conPassed)
            return new HealOutcome(status, status, null, "доживает до следующего раунда — нужна первая помощь");

        var dead = status with { Dead = true, Dying = false, Unconscious = false, HitPoints = 0 };
        return new HealOutcome(status, dead, null, "провал — смерть");
    }

    /// <summary>Сколько бонусных и штрафных костей у проверки лечения серьёзной раны (стр. 119).</summary>
    public static (int Bonus, int Penalty) RecoveryDice(bool medicalCare, bool rested, bool poorConditions) =>
        ((medicalCare ? 1 : 0) + (rested ? 1 : 0), poorConditions ? 1 : 0);

    /// <summary>Сколько костей 1d3 даёт уровень проверки лечения: провал — 0, успех — 1, чрезвычайный и выше — 2.</summary>
    public static int RecoveryDiceCount(SuccessLevel level) =>
        level >= SuccessLevel.Extreme ? 2 : level.IsSuccess() ? 1 : 0;

    /// <summary>
    /// Сколько ПЗ дали кости лечения серьёзной раны: провал — 0; иначе <paramref name="entered"/>, если такая сумма
    /// возможна на <see cref="RecoveryDiceCount"/>d3, а нет — бросок <paramref name="dice"/>. Одна копия для боя и листа.
    /// </summary>
    public static int RecoveryAmount(SuccessLevel level, int? entered, IDiceRoller dice)
    {
        var count = RecoveryDiceCount(level);
        if (count == 0)
            return 0;

        return entered is { } sum && sum >= count && sum <= count * HealDieSides ? sum : dice.Roll(count, HealDieSides);
    }

    /// <summary>
    /// Недельная проверка лечения серьёзной раны (стр. 119): <paramref name="level"/> — уровень проверки ВЫН,
    /// <paramref name="amount"/> — выпавшее на 1d3 (успех) или 2d3 (чрезвычайный). Чрезвычайный снимает отметку; крах —
    /// осложнение на усмотрение Хранителя (пример с Сесилом).
    /// </summary>
    public static HealOutcome WeeklyRecovery(WoundStatus status, int maxHitPoints, SuccessLevel level, int amount)
    {
        if (status.Dead)
            return new HealOutcome(status, status, "мёртв", "без изменений");
        if (!status.MajorWound)
            return new HealOutcome(status, status, "серьёзной раны нет — лечится по 1 ПЗ в день", "без изменений");
        if (status.Dying)
            return new HealOutcome(status, status, "сначала первая помощь и Медицина", "без изменений");

        if (!level.IsSuccess())
        {
            var text = level == SuccessLevel.Fumble
                ? "крах — выздоровления нет, рана осложнилась: последствия решает Хранитель"
                : "провал — за эту неделю выздоровления нет";
            return new HealOutcome(status, status, null, text);
        }

        var extreme = level >= SuccessLevel.Extreme;
        var healed = status with
        {
            HitPoints = Math.Min(Math.Max(maxHitPoints, 0), status.HitPoints + Math.Max(0, amount)),
            MajorWound = !extreme,
            Unconscious = false,
        };
        return new HealOutcome(status, healed, null,
            extreme ? $"чрезвычайный успех: +{amount} ПЗ, отметка «Серьёзная рана» снята" : $"+{amount} ПЗ");
    }

    /// <summary>
    /// Естественное выздоровление от обычного урона (стр. 119): 1 ПЗ в день, пока нет отметки «Серьёзная рана». При
    /// серьёзной ране — только недельная проверка.
    /// </summary>
    public static HealOutcome NaturalRecovery(WoundStatus status, int maxHitPoints, int days)
    {
        if (status.Dead)
            return new HealOutcome(status, status, "мёртв", "без изменений");
        if (status.MajorWound || status.Dying)
            return new HealOutcome(status, status, "при серьёзной ране — только недельная проверка лечения", "без изменений");

        var healed = status with { HitPoints = Math.Min(Math.Max(maxHitPoints, 0), status.HitPoints + Math.Max(0, days)) };
        if (healed.HitPoints > 0)
            healed = healed with { Unconscious = false };
        return new HealOutcome(status, healed, null, $"+{healed.HitPoints - status.HitPoints} ПЗ за {days} дн.");
    }

    /// <summary>
    /// Лечение руками Хранителя (эффект «Лечение»): ПЗ до максимума; поднявшийся выше нуля приходит в себя и больше не при
    /// смерти. Мёртвых не поднимает.
    /// </summary>
    public static WoundStatus Heal(WoundStatus status, int maxHitPoints, int amount)
    {
        if (status.Dead)
            return status;

        var healed = status with { HitPoints = Math.Min(Math.Max(maxHitPoints, 0), status.HitPoints + Math.Max(0, amount)) };
        return healed.HitPoints > 0 ? healed with { Dying = false, Stabilized = false, Unconscious = false } : healed;
    }

    /// <summary>
    /// ПЗ поправили руками (лист): выше нуля — в сознании и не при смерти; ноль — без сознания, с серьёзной раной — при
    /// смерти (стр. 118). Смерть руками не снимается этим путём — только отметкой.
    /// </summary>
    public static WoundStatus Settle(WoundStatus status)
    {
        if (status.Dead)
            return status;

        return status.HitPoints > 0
            ? status with { Unconscious = false, Dying = false, Stabilized = false }
            : status with { Unconscious = true, Dying = status.MajorWound, Stabilized = false };
    }

    /// <summary>Состояние словами для журнала: «мёртв», «при смерти, стабилизирован», «без сознания»; null — в порядке.</summary>
    public static string? StateText(WoundStatus status) => status switch
    {
        { Dead: true } => "мёртв",
        { Dying: true, Stabilized: true } => "при смерти, стабилизирован",
        { Dying: true } => "при смерти",
        { Unconscious: true } => "без сознания",
        _ => null,
    };

    public static WoundStatus Read(CharacterSheet sheet) => new(
        sheet.Current.HitPoints,
        sheet.Condition.MajorWound,
        sheet.Condition.Unconscious,
        sheet.Condition.Dying,
        sheet.Condition.Stabilized,
        sheet.Condition.Dead);

    public static void Write(CharacterSheet sheet, WoundStatus status)
    {
        sheet.Current.HitPoints = status.HitPoints;
        sheet.Condition.MajorWound = status.MajorWound;
        sheet.Condition.Unconscious = status.Unconscious;
        sheet.Condition.Dying = status.Dying;
        sheet.Condition.Stabilized = status.Stabilized;
        sheet.Condition.Dead = status.Dead;
    }

    /// <summary>Одна атака по листу (без проверки ВЫН — её исход вписывает игрок отметкой «Без сознания»).</summary>
    public static DamageOutcome ApplyDamage(CharacterSheet sheet, int damage, bool? conPassed = null)
    {
        var outcome = TakeDamage(Read(sheet), DerivedAttributeRules.MaxHitPoints(sheet), damage, conPassed);
        Write(sheet, outcome.After);
        return outcome;
    }

    /// <summary>
    /// Недельная проверка лечения серьёзной раны по листу (стр. 119): <paramref name="roll"/> — бросок ВЫН листа, уже с
    /// костями ухода и условий (<see cref="RecoveryDice"/>; брошен или вписан), <paramref name="healRoll"/> — выпавшее на
    /// 1d3/2d3 (null — бросит <paramref name="dice"/>). Отказ (нет раны, при смерти) лист не меняет.
    /// </summary>
    public static RecoveryCheck ApplyWeeklyRecovery(CharacterSheet sheet, D100Roll roll, int? healRoll, IDiceRoller dice)
    {
        var level = Check.Evaluate(roll.Result, sheet.Characteristics[Characteristic.CON]);
        var status = Read(sheet);
        var amount = status.MajorWound && !status.Dying && !status.Dead ? RecoveryAmount(level, healRoll, dice) : 0;
        var outcome = WeeklyRecovery(status, DerivedAttributeRules.MaxHitPoints(sheet), level, amount);
        if (outcome.Refusal is null)
            Write(sheet, outcome.After);
        return new RecoveryCheck(roll, level, amount, outcome);
    }

    /// <summary>Отдых без серьёзной раны по листу: 1 ПЗ в день (стр. 119). Отказ лист не меняет.</summary>
    public static HealOutcome ApplyNaturalRecovery(CharacterSheet sheet, int days)
    {
        var outcome = NaturalRecovery(Read(sheet), DerivedAttributeRules.MaxHitPoints(sheet), days);
        if (outcome.Refusal is null)
            Write(sheet, outcome.After);
        return outcome;
    }

    /// <summary>ПЗ листа поправили руками — состояние следует (<see cref="Settle"/>).</summary>
    public static void UpdateConsciousness(CharacterSheet sheet) => Write(sheet, Settle(Read(sheet)));
}
