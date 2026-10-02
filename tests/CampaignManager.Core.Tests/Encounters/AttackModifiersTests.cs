using CampaignManager.Core.Encounters;

namespace CampaignManager.Core.Tests.Encounters;

/// <summary>
/// Бонусные и штрафные кости атаки — таблица <see cref="AttackModifiers"/>. Ожидания — из тестов T0.2 на
/// <c>CombatService.CalculateAttackModifiers</c> (<c>Rules.Tests/Combat/AttackModifiersTests</c>): каждое
/// условие отдельно, остальные выключены. Таблицу кладёт T2.7 (её читает ширма), бой на неё опирается в T2.6b.
/// </summary>
public sealed class AttackModifiersTests
{
    private static readonly AttackSituation Melee = new() { Kind = AttackKind.Melee };
    private static readonly AttackSituation Ranged = new() { Kind = AttackKind.Ranged };

    [Theory]
    [Trait("page", "106")]
    [InlineData(AttackKind.Melee)]
    [InlineData(AttackKind.Ranged)]
    public void Calculate_NoConditions_NoDice(AttackKind kind)
    {
        var dice = AttackModifiers.Calculate(new AttackSituation { Kind = kind });

        Assert.Equal(0, dice.BonusDice);
        Assert.Equal(0, dice.PenaltyDice);
        Assert.Empty(dice.Reasons);
    }

    [Fact]
    [Trait("page", "106")]
    public void Calculate_KeeperDice_AddedAsIs_NotCancelled()
    {
        var dice = AttackModifiers.Calculate(Melee with { KeeperBonusDice = 2, KeeperPenaltyDice = 1 });

        Assert.Equal(2, dice.BonusDice);
        Assert.Equal(1, dice.PenaltyDice);
        Assert.Equal(1, dice.Net);
        Assert.Equal(["+2 от Хранителя", "−1 от Хранителя"], dice.Reasons);
    }

    [Fact]
    [Trait("page", "106")]
    public void Calculate_NegativeKeeperDice_TreatedAsZero()
    {
        var dice = AttackModifiers.Calculate(Ranged with { KeeperBonusDice = -1, KeeperPenaltyDice = -2 });

        Assert.Equal(0, dice.BonusDice);
        Assert.Equal(0, dice.PenaltyDice);
        Assert.Empty(dice.Reasons);
    }

    [Theory]
    [Trait("page", "105")]
    [InlineData(AttackKind.Melee, SurpriseMode.BonusDie, 1)]
    [InlineData(AttackKind.Ranged, SurpriseMode.BonusDie, 1)]
    [InlineData(AttackKind.Melee, SurpriseMode.AutoHit, 0)] // в ближнем бою автопопадание костей не даёт
    [InlineData(AttackKind.Ranged, SurpriseMode.AutoHit, 1)] // в стрельбе понижено до бонусной кости
    [InlineData(AttackKind.Melee, SurpriseMode.TargetReady, 0)]
    public void Calculate_Surprise(AttackKind kind, SurpriseMode mode, int bonus)
    {
        var dice = AttackModifiers.Calculate(new AttackSituation { Kind = kind, Surprise = mode });

        Assert.Equal(bonus, dice.BonusDice);
        Assert.Equal(0, dice.PenaltyDice);
        if (bonus > 0)
            Assert.Equal(["+1 цель застигнута врасплох"], dice.Reasons);
    }

    /// <summary>Сценарий → (бонусные, штрафные, единственная причина).</summary>
    [Theory]
    [Trait("page", "106")]
    [Trait("page", "110-114")]
    [InlineData("melee-prone-target", 1, 0, "+1 цель повалена")]
    [InlineData("melee-superiority", 1, 0, "+1 численное превосходство")]
    [InlineData("point-blank", 1, 0, "+1 стрельба в упор")]
    [InlineData("aiming", 1, 0, "+1 прицеливание")]
    [InlineData("shooter-prone", 1, 0, "+1 стрельба лёжа")]
    [InlineData("big-target", 1, 0, "+1 крупная цель")]
    [InlineData("taking-cover", 0, 1, "−1 цель укрылась от огня")]
    [InlineData("behind-cover", 0, 1, "−1 частичное укрытие")]
    [InlineData("fast-moving", 0, 1, "−1 быстро движущаяся цель")]
    [InlineData("into-melee", 0, 1, "−1 стрельба в ближнем бою")]
    [InlineData("pistol-burst", 0, 1, "−1 серия выстрелов")]
    [InlineData("reload-and-fire", 0, 1, "−1 зарядка и выстрел")]
    [InlineData("small-target", 0, 1, "−1 мелкая цель")]
    [InlineData("ranged-prone-target", 0, 1, "−1 цель лежит")]
    [InlineData("point-blank-prone-target", 1, 0, "+1 стрельба в упор")]
    [InlineData("volley-second", 0, 1, "−1 проверка №2 при автоматической стрельбе")]
    [InlineData("volley-third", 0, 2, "−2 проверка №3 при автоматической стрельбе")]
    [InlineData("volley-sixth", 0, 2, "−2 проверка №6 при автоматической стрельбе")]
    public void Calculate_SingleCondition(string scenario, int bonus, int penalty, string reason)
    {
        var dice = AttackModifiers.Calculate(Scenario(scenario));

        Assert.Equal(bonus, dice.BonusDice);
        Assert.Equal(penalty, dice.PenaltyDice);
        Assert.Equal([reason], dice.Reasons);
    }

    [Theory]
    [Trait("page", "106")]
    [InlineData("melee-ignores-firearm-flags")]
    [InlineData("build-3-is-not-big")]
    [InlineData("build-minus-1-is-not-small")]
    [InlineData("volley-first-check")]
    [InlineData("single-with-autofire-index")]
    [InlineData("superiority-not-yet")]
    public void Calculate_ConditionNotApplicable_NoDice(string scenario)
    {
        var dice = AttackModifiers.Calculate(Scenario(scenario));

        Assert.Equal(0, dice.BonusDice);
        Assert.Equal(0, dice.PenaltyDice);
    }

    [Fact]
    public void Calculate_SeveralConditions_SumInTableOrder()
    {
        var dice = AttackModifiers.Calculate(Ranged with { Aiming = true, TargetBuild = 5, TargetBehindCover = true, KeeperPenaltyDice = 1 });

        Assert.Equal(2, dice.BonusDice);
        Assert.Equal(2, dice.PenaltyDice);
        Assert.Equal(0, dice.Net);
        Assert.Equal(["−1 от Хранителя", "+1 прицеливание", "+1 крупная цель", "−1 частичное укрытие"], dice.Reasons);
    }

    private static AttackSituation Scenario(string name) => name switch
    {
        "melee-prone-target" => Melee with { TargetProne = true },
        "melee-superiority" => Melee with { TargetDefensesThisRound = 1 },
        "point-blank" => Ranged with { PointBlank = true },
        "aiming" => Ranged with { Aiming = true },
        "shooter-prone" => Ranged with { ShooterProne = true },
        "big-target" => Ranged with { TargetBuild = 4 },
        "taking-cover" => Ranged with { TargetTakingCover = true },
        "behind-cover" => Ranged with { TargetBehindCover = true },
        "fast-moving" => Ranged with { TargetFastMoving = true },
        "into-melee" => Ranged with { FiringIntoMelee = true },
        "pistol-burst" => Ranged with { FiringMode = FiringMode.PistolBurst },
        "reload-and-fire" => Ranged with { ReloadAndFire = true },
        "small-target" => Ranged with { TargetBuild = -2 },
        "ranged-prone-target" => Ranged with { TargetProne = true },
        "point-blank-prone-target" => Ranged with { PointBlank = true, TargetProne = true },
        "volley-second" => Ranged with { FiringMode = FiringMode.Volley, AutofireCheckIndex = 1 },
        "volley-third" => Ranged with { FiringMode = FiringMode.Volley, AutofireCheckIndex = 2 },
        "volley-sixth" => Ranged with { FiringMode = FiringMode.Volley, AutofireCheckIndex = 5 },
        "melee-ignores-firearm-flags" => Melee with
        {
            PointBlank = true, Aiming = true, TargetBehindCover = true, TargetFastMoving = true, FiringIntoMelee = true,
            ReloadAndFire = true, FiringMode = FiringMode.Volley, AutofireCheckIndex = 3, ShooterProne = true, TargetBuild = 6,
        },
        "build-3-is-not-big" => Ranged with { TargetBuild = 3 },
        "build-minus-1-is-not-small" => Ranged with { TargetBuild = -1 },
        "volley-first-check" => Ranged with { FiringMode = FiringMode.Volley, AutofireCheckIndex = 0 },
        "single-with-autofire-index" => Ranged with { FiringMode = FiringMode.Single, AutofireCheckIndex = 3 },
        "superiority-not-yet" => Melee with { TargetDefensesThisRound = 1, TargetAttacksPerRound = 2 },
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    [Theory]
    [Trait("page", "105")]
    [InlineData(AttackKind.Melee, SurpriseMode.AutoHit, SurpriseMode.AutoHit)]
    [InlineData(AttackKind.Ranged, SurpriseMode.AutoHit, SurpriseMode.BonusDie)]
    [InlineData(AttackKind.Ranged, SurpriseMode.BonusDie, SurpriseMode.BonusDie)]
    [InlineData(AttackKind.Ranged, SurpriseMode.TargetReady, SurpriseMode.TargetReady)]
    [InlineData(AttackKind.Melee, SurpriseMode.BonusDie, SurpriseMode.BonusDie)]
    public void NormalizeSurprise_RangedAutoHitBecomesBonusDie(AttackKind kind, SurpriseMode mode, SurpriseMode expected) =>
        Assert.Equal(expected, AttackModifiers.NormalizeSurprise(kind, mode));

    [Theory]
    [Trait("page", "106")]
    [InlineData(0, 1, false)]
    [InlineData(1, 1, true)]
    [InlineData(2, 1, true)]
    [InlineData(1, 2, false)]
    [InlineData(2, 2, true)]
    [InlineData(1, 0, true)] // атак за раунд 0 считается как одна
    [InlineData(0, 0, false)]
    public void HasNumericalSuperiority_DefensesUsedUpToAttacksPerRound(int defenses, int attacksPerRound, bool expected) =>
        Assert.Equal(expected, AttackModifiers.HasNumericalSuperiority(defenses, attacksPerRound));

    [Theory]
    [Trait("page", "110")]
    [InlineData(RangeBand.Base, Difficulty.Regular)]
    [InlineData(RangeBand.Long, Difficulty.Hard)]
    [InlineData(RangeBand.Extreme, Difficulty.Extreme)]
    public void RequiredDifficulty_ByRange(RangeBand range, Difficulty expected) =>
        Assert.Equal(expected, AttackModifiers.RequiredDifficulty(range));
}

/// <summary>
/// Памятка ширмы строится из таблицы (снимок <c>Rules.Tests/Checks/CombatModifierReferenceTests</c> v1): у
/// примера каждой строки ровно одна причина — и это сама строка.
/// </summary>
[Trait("page", "105-114")]
public sealed class AttackModifierTableTests
{
    [Fact]
    public void Firearms_Snapshot() =>
        Assert.Equal(
        [
            "+1 стрельба в упор",
            "+1 прицеливание",
            "+1 стрельба лёжа",
            "+1 крупная цель",
            "−1 цель укрылась от огня",
            "−1 частичное укрытие",
            "−1 быстро движущаяся цель",
            "−1 стрельба в ближнем бою",
            "−1 серия выстрелов",
            "−1 зарядка и выстрел",
            "−1 мелкая цель",
            "−1 цель лежит",
            "−1 проверка №2 при автоматической стрельбе",
        ],
        AttackModifiers.Table.Where(r => r.Kind == AttackKind.Ranged).Select(r => r.Describe(r.Example)));

    [Fact]
    public void Melee_Snapshot() =>
        Assert.Equal(
            ["+1 цель застигнута врасплох", "+1 цель повалена", "+1 численное превосходство"],
            AttackModifiers.For(AttackKind.Melee).Select(r => r.Describe(r.Example)));

    [Fact]
    public void EveryRow_ExampleGivesExactlyThisRow()
    {
        Assert.All(AttackModifiers.Table, rule =>
        {
            var dice = AttackModifiers.Calculate(rule.Example);

            Assert.Equal([rule.Describe(rule.Example)], dice.Reasons);
            Assert.False(string.IsNullOrWhiteSpace(rule.When));
            Assert.False(string.IsNullOrWhiteSpace(rule.Page));
            Assert.True(rule.Kind is null || rule.Kind == rule.Example.Kind);
        });
    }

    [Fact]
    public void Ids_Unique() =>
        Assert.Equal(AttackModifiers.Table.Count, AttackModifiers.Table.Select(r => r.Id).Distinct().Count());
}
