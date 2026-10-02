namespace CampaignManager.Core.Encounters;

/// <summary>Вид атаки: модификаторы ближнего боя и стрельбы разные.</summary>
public enum AttackKind
{
    Melee,
    Ranged,
}

/// <summary>Готовность цели (стр. 104–105).</summary>
public enum SurpriseMode
{
    /// <summary>Цель начеку: уклоняется или контратакует.</summary>
    TargetReady,

    /// <summary>Цель не готова, но исход не предрешён — бонусная кость.</summary>
    BonusDie,

    /// <summary>Цель беззащитна — атака попадает сама, провал только при крахе. В стрельбе не бывает.</summary>
    AutoHit,
}

/// <summary>Режим огня (стр. 112–114).</summary>
public enum FiringMode
{
    Single,

    /// <summary>Несколько выстрелов из пистолета за раунд — штрафная кость каждому.</summary>
    PistolBurst,

    /// <summary>Автоматическая стрельба очередями — нарастающие штрафные кости.</summary>
    Volley,
}

/// <summary>Дальность стрельбы относительно базовой дальности оружия (стр. 110).</summary>
public enum RangeBand
{
    Base,
    Long,
    Extreme,
}

/// <summary>Модификатор атаки — строка таблицы <see cref="AttackModifiers.Table"/>.</summary>
public enum AttackModifier
{
    Surprise,
    MeleeTargetProne,
    NumericalSuperiority,
    PointBlank,
    Aiming,
    ShooterProne,
    LargeTarget,
    TargetTakingCover,
    TargetBehindCover,
    TargetFastMoving,
    FiringIntoMelee,
    PistolBurst,
    ReloadAndFire,
    SmallTarget,
    RangedTargetProne,
    AutofireFollowUp,
}

/// <summary>
/// Обстоятельства атаки — всё, от чего зависят бонусные и штрафные кости. Бой (T2.6b) собирает его из
/// участников и настроек атаки; памятка ширмы — из примера строки таблицы.
/// </summary>
public sealed record AttackSituation
{
    public AttackKind Kind { get; init; }

    /// <summary>Кости Хранителя «от себя» — добавляются как есть (отрицательные — ноль).</summary>
    public int KeeperBonusDice { get; init; }

    public int KeeperPenaltyDice { get; init; }

    public SurpriseMode Surprise { get; init; }

    public bool TargetProne { get; init; }

    /// <summary>Сколько раз цель уже уклонялась или контратаковала в этом раунде.</summary>
    public int TargetDefensesThisRound { get; init; }

    /// <summary>Атак за раунд у цели — столько же у неё защит до численного превосходства (стр. 106, 279).</summary>
    public int TargetAttacksPerRound { get; init; } = 1;

    public bool PointBlank { get; init; }

    /// <summary>Стрелок целился прошлый ход (из настройки атаки или из состояния участника — учитывается один раз).</summary>
    public bool Aiming { get; init; }

    public bool ShooterProne { get; init; }

    /// <summary>Комплекция цели: 4 и больше — крупная, −2 и меньше — мелкая.</summary>
    public int TargetBuild { get; init; }

    /// <summary>Цель укрылась от огня (из настройки или из состояния цели — учитывается один раз).</summary>
    public bool TargetTakingCover { get; init; }

    public bool TargetBehindCover { get; init; }

    public bool TargetFastMoving { get; init; }

    public bool FiringIntoMelee { get; init; }

    public FiringMode FiringMode { get; init; }

    public bool ReloadAndFire { get; init; }

    /// <summary>Номер проверки очереди в раунде с нуля: первая идёт без штрафа (стр. 114).</summary>
    public int AutofireCheckIndex { get; init; }
}

/// <summary>Итог модификаторов: бонусные и штрафные кости (до взаимного погашения) и причины словами.</summary>
public sealed record AttackDice(int BonusDice, int PenaltyDice, IReadOnlyList<string> Reasons)
{
    /// <summary>Итог после погашения: &gt; 0 — бонусные, &lt; 0 — штрафные.</summary>
    public int Net => BonusDice - PenaltyDice;
}

/// <summary>
/// Строка таблицы модификаторов. <paramref name="Kind"/> null — для любой атаки (внезапность).
/// <paramref name="Dice"/> — сколько костей даёт обстоятельство (0 — не положено), <paramref name="Bonus"/> —
/// бонусные они или штрафные. <paramref name="Example"/> — обстоятельство, в котором положена ровно эта строка:
/// по нему памятка ширмы показывает, что даёт модификатор, а тест следит, чтобы у примера была одна причина.
/// </summary>
public sealed record AttackModifierRule(
    AttackModifier Id,
    AttackKind? Kind,
    bool Bonus,
    Func<AttackSituation, int> Dice,
    Func<AttackSituation, string> Reason,
    string When,
    string Page,
    AttackSituation Example)
{
    /// <summary>Причина со знаком и числом костей: «+1 стрельба в упор», «−2 проверка №3…».</summary>
    public string Describe(AttackSituation situation)
    {
        var dice = Dice(situation);
        return $"{(Bonus ? "+" : "−")}{dice} {Reason(situation)}";
    }

    public bool AppliesTo(AttackKind kind) => Kind is null || Kind == kind;
}

/// <summary>
/// Бонусные и штрафные кости атаки (стр. 105–114) — <b>таблица</b>, одна на приложение: по ней считает бой
/// (T2.6b) и строит памятку ширма Хранителя. В v1 это был метод <c>CombatService.CalculateAttackModifiers</c> с
/// цепочкой <c>if</c>, а памятка ширмы опрашивала его по одному условию, чтобы не разойтись с боем. Новый
/// модификатор — новая строка таблицы с примером: бой и памятка получат его сразу.
/// <para>
/// Кости складываются как есть; гасит их друг другом бросок (<see cref="Dice.D100.Roll"/>), и больше двух в
/// одну сторону он не бросит (стр. 89).
/// </para>
/// </summary>
public static class AttackModifiers
{
    private static readonly AttackSituation Melee = new() { Kind = AttackKind.Melee };
    private static readonly AttackSituation Ranged = new() { Kind = AttackKind.Ranged };

    /// <summary>Строки таблицы в порядке, в котором причины попадают в журнал атаки.</summary>
    public static IReadOnlyList<AttackModifierRule> Table { get; } =
    [
        Row(AttackModifier.Surprise, null, true,
            s => NormalizeSurprise(s.Kind, s.Surprise) is SurpriseMode.BonusDie ? 1 : 0,
            "цель застигнута врасплох", "цель не готова к атаке, но исход не предрешён", "105",
            Melee with { Surprise = SurpriseMode.BonusDie }),
        Row(AttackModifier.MeleeTargetProne, AttackKind.Melee, true, s => s.TargetProne ? 1 : 0,
            "цель повалена", "цель повалена на землю", "113",
            Melee with { TargetProne = true }),
        Row(AttackModifier.NumericalSuperiority, AttackKind.Melee, true,
            s => HasNumericalSuperiority(s.TargetDefensesThisRound, s.TargetAttacksPerRound) ? 1 : 0,
            "численное превосходство", "цель уже потратила все защиты раунда (численное превосходство)", "106",
            Melee with { TargetDefensesThisRound = 1 }),

        Row(AttackModifier.PointBlank, AttackKind.Ranged, true, s => s.PointBlank ? 1 : 0,
            "стрельба в упор", "цель не дальше 1/15 ЛВК стрелка в метрах", "111–112",
            Ranged with { PointBlank = true }),
        Row(AttackModifier.Aiming, AttackKind.Ranged, true, s => s.Aiming ? 1 : 0,
            "прицеливание", "прошлый ход целился и с тех пор не двигался и не ранен", "111–112",
            Ranged with { Aiming = true }),
        Row(AttackModifier.ShooterProne, AttackKind.Ranged, true, s => s.ShooterProne ? 1 : 0,
            "стрельба лёжа", "стрелок лежит", "111–112",
            Ranged with { ShooterProne = true }),
        Row(AttackModifier.LargeTarget, AttackKind.Ranged, true, s => s.TargetBuild >= 4 ? 1 : 0,
            "крупная цель", "Комплекция цели 4 и больше", "111–112",
            Ranged with { TargetBuild = 4 }),
        Row(AttackModifier.TargetTakingCover, AttackKind.Ranged, false, s => s.TargetTakingCover ? 1 : 0,
            "цель укрылась от огня", "цель укрылась от огня (прошла Уклонение и теряет следующую атаку)", "111–112",
            Ranged with { TargetTakingCover = true }),
        Row(AttackModifier.TargetBehindCover, AttackKind.Ranged, false, s => s.TargetBehindCover ? 1 : 0,
            "частичное укрытие", "цель хотя бы наполовину за преградой", "111–112",
            Ranged with { TargetBehindCover = true }),
        Row(AttackModifier.TargetFastMoving, AttackKind.Ranged, false, s => s.TargetFastMoving ? 1 : 0,
            "быстро движущаяся цель", "цель бежит или едет на полной скорости, СКО 8+", "111–112",
            Ranged with { TargetFastMoving = true }),
        Row(AttackModifier.FiringIntoMelee, AttackKind.Ranged, false, s => s.FiringIntoMelee ? 1 : 0,
            "стрельба в ближнем бою", "цель сцепилась в ближнем бою; при крахе пуля уходит союзнику с меньшей Удачей", "111–112",
            Ranged with { FiringIntoMelee = true }),
        Row(AttackModifier.PistolBurst, AttackKind.Ranged, false, s => s.FiringMode is FiringMode.PistolBurst ? 1 : 0,
            "серия выстрелов", "серия из пистолета — к каждому выстрелу", "111–112",
            Ranged with { FiringMode = FiringMode.PistolBurst }),
        Row(AttackModifier.ReloadAndFire, AttackKind.Ranged, false, s => s.ReloadAndFire ? 1 : 0,
            "зарядка и выстрел", "зарядил патрон и выстрелил в том же раунде", "111–112",
            Ranged with { ReloadAndFire = true }),
        Row(AttackModifier.SmallTarget, AttackKind.Ranged, false, s => s.TargetBuild <= -2 ? 1 : 0,
            "мелкая цель", "Комплекция цели −2 и меньше", "111–112",
            Ranged with { TargetBuild = -2 }),
        Row(AttackModifier.RangedTargetProne, AttackKind.Ranged, false, s => s.TargetProne && !s.PointBlank ? 1 : 0,
            "цель лежит", "цель лежит (кроме стрельбы в упор)", "111–112",
            Ranged with { TargetProne = true }),
        new(AttackModifier.AutofireFollowUp, AttackKind.Ranged, false,
            s => s is { FiringMode: FiringMode.Volley, AutofireCheckIndex: > 0 } ? Math.Min(s.AutofireCheckIndex, 2) : 0,
            s => $"проверка №{s.AutofireCheckIndex + 1} при автоматической стрельбе",
            "каждая следующая проверка очереди; с третьей штрафной — сложность на уровень выше", "114",
            Ranged with { FiringMode = FiringMode.Volley, AutofireCheckIndex = 1 }),
    ];

    /// <summary>
    /// Кости атаки: кости Хранителя плюс каждая положенная строка таблицы. Причины — в порядке таблицы, кости
    /// Хранителя первыми.
    /// </summary>
    public static AttackDice Calculate(AttackSituation situation)
    {
        List<string> reasons = [];
        var bonus = Math.Max(0, situation.KeeperBonusDice);
        var penalty = Math.Max(0, situation.KeeperPenaltyDice);

        if (bonus > 0)
            reasons.Add($"+{bonus} от Хранителя");
        if (penalty > 0)
            reasons.Add($"−{penalty} от Хранителя");

        foreach (var rule in Table)
        {
            if (!rule.AppliesTo(situation.Kind))
                continue;

            var dice = rule.Dice(situation);
            if (dice <= 0)
                continue;

            if (rule.Bonus)
                bonus += dice;
            else
                penalty += dice;
            reasons.Add(rule.Describe(situation));
        }

        return new AttackDice(bonus, penalty, reasons);
    }

    /// <summary>Строки, положенные этому виду атаки (памятка ширмы: ближний бой и стрельба).</summary>
    public static IReadOnlyList<AttackModifierRule> For(AttackKind kind) => Table.Where(r => r.AppliesTo(kind)).ToList();

    /// <summary>
    /// В стрельбе автоматического попадания нет — «всегда нужно делать бросок на попадание» (стр. 105): оно
    /// понижается до бонусной кости.
    /// </summary>
    public static SurpriseMode NormalizeSurprise(AttackKind kind, SurpriseMode mode) =>
        kind is AttackKind.Ranged && mode is SurpriseMode.AutoHit ? SurpriseMode.BonusDie : mode;

    /// <summary>
    /// Численное превосходство (стр. 106): цель уже уклонялась или контратаковала столько раз, сколько у неё
    /// атак за раунд (ноль атак считается как одна).
    /// </summary>
    public static bool HasNumericalSuperiority(int defensesThisRound, int attacksPerRound) =>
        defensesThisRound > 0 && defensesThisRound >= Math.Max(1, attacksPerRound);

    /// <summary>Дальность задаёт сложность, а не урезает навык (стр. 110).</summary>
    public static Difficulty RequiredDifficulty(RangeBand range) => range switch
    {
        RangeBand.Long => Difficulty.Hard,
        RangeBand.Extreme => Difficulty.Extreme,
        _ => Difficulty.Regular,
    };

    private static AttackModifierRule Row(
        AttackModifier id, AttackKind? kind, bool bonus, Func<AttackSituation, int> dice,
        string reason, string when, string page, AttackSituation example) =>
        new(id, kind, bonus, dice, _ => reason, when, page, example);
}
