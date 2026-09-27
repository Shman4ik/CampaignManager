using CampaignManager.Web.Components.Features.Combat.Model;
using CampaignManager.Web.Components.Features.Combat.Services;

namespace CampaignManager.Web.Components.Features.KeeperScreen.Services;

/// <summary>Строка памятки: что даёт модификатор (слово боевого движка) и когда он положен.</summary>
public sealed record DiceModifierRow(string Effect, string Condition);

/// <summary>Строка дальности стрельбы: предел, нужный уровень успеха и его название.</summary>
public sealed record RangeRow(string Distance, SuccessLevel Required, string DifficultyName, string Note);

/// <summary>
///     Памятка по бонусным и штрафным костям боя для ширмы Хранителя.
///     <para>
///         Своего списка модификаторов здесь <b>нет</b>: каждая строка получена вопросом к боевому
///         движку — <see cref="CombatService.CalculateAttackModifiers" /> с одним включённым условием, —
///         поэтому памятка не может разойтись с тем, что бой применяет на самом деле. Своё здесь только
///         условие, когда модификатор положен (пересказ книги, стр. 105–114).
///     </para>
/// </summary>
public static class CombatModifierReference
{
    public static IReadOnlyList<RangeRow> Ranges() =>
    [
        Range(RangeLevel.Base, "в пределах базовой дальности оружия", ""),
        Range(RangeLevel.Long, "до двух базовых", ""),
        Range(RangeLevel.Extreme, "до четырёх базовых", "проникающая рана — только при 01")
    ];

    public static IReadOnlyList<DiceModifierRow> Firearms() =>
    [
        Ranged(new AttackSetup { IsPointBlank = true }, "цель не дальше 1/15 ЛВК стрелка в метрах"),
        Ranged(new AttackSetup { IsAiming = true }, "прошлый ход целился и с тех пор не двигался и не ранен"),
        Ranged(new AttackSetup(), "стрелок лежит", attacker: new Combatant { IsProne = true }),
        Ranged(new AttackSetup(), "Комплекция цели 4 и больше", defender: new Combatant { Build = 4 }),
        Ranged(new AttackSetup { IsTargetTakingCover = true },
            "цель укрылась от огня (прошла Уклонение и теряет следующую атаку)"),
        Ranged(new AttackSetup { IsTargetBehindCover = true }, "цель хотя бы наполовину за преградой"),
        Ranged(new AttackSetup { IsTargetFastMoving = true }, "цель бежит или едет на полной скорости, СКО 8+"),
        Ranged(new AttackSetup { IsFiringIntoMelee = true },
            "цель сцепилась в ближнем бою; при крахе пуля уходит союзнику с меньшей Удачей"),
        Ranged(new AttackSetup { FiringMode = FiringMode.PistolBurst }, "серия из пистолета — к каждому выстрелу"),
        Ranged(new AttackSetup { IsReloadAndFire = true }, "зарядил патрон и выстрелил в том же раунде"),
        Ranged(new AttackSetup(), "Комплекция цели −2 и меньше", defender: new Combatant { Build = -2 }),
        Ranged(new AttackSetup(), "цель лежит (кроме стрельбы в упор)", defender: new Combatant { IsProne = true }),
        Ranged(new AttackSetup { FiringMode = FiringMode.Volley, AutofireCheckIndex = 1 },
            "каждая следующая проверка очереди; с третьей штрафной — сложность на уровень выше")
    ];

    public static IReadOnlyList<DiceModifierRow> Melee() =>
    [
        Probe(new AttackSetup { IsMelee = true, SurpriseMode = SurpriseMode.BonusDie },
            "цель не готова к атаке, но исход не предрешён", null, null),
        Probe(new AttackSetup { IsMelee = true }, "цель повалена на землю",
            null, new Combatant { IsProne = true }),
        Probe(new AttackSetup { IsMelee = true },
            "цель уже потратила все защиты раунда (численное превосходство)",
            null, new Combatant { DefenseCountThisRound = 1, AttacksPerRound = 1 })
    ];

    private static RangeRow Range(RangeLevel range, string distance, string note) =>
        new(distance, CombatService.GetRequiredLevelForRange(range), CombatService.GetDifficultyName(range), note);

    private static DiceModifierRow Ranged(
        AttackSetup setup, string condition, Combatant? attacker = null, Combatant? defender = null)
    {
        setup.IsMelee = false;
        return Probe(setup, condition, attacker, defender);
    }

    private static DiceModifierRow Probe(AttackSetup setup, string condition, Combatant? attacker, Combatant? defender)
    {
        var reasons = CombatService.CalculateAttackModifiers(setup, attacker, defender).Reasons;
        return new DiceModifierRow(reasons.Count > 0 ? string.Join("; ", reasons) : "—", condition);
    }
}
