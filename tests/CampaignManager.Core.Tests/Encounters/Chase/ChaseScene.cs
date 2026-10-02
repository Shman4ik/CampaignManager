using CampaignManager.Core.Dice;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Encounters.Chase;
using CampaignManager.Core.KeeperScreen;
using CampaignManager.Core.Tests.Infrastructure;

namespace CampaignManager.Core.Tests.Encounters.Chase;

/// <summary>
/// Заготовки погони на ядре сцены — перенос <c>ChaseScene</c> тестов T0.2: участники только с нужными числами, без листа
/// (эффекты в листы не пишутся), трасса и роли.
/// </summary>
internal static class ChaseScene
{
    public static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Сыщик или тварь без листа: СКО, ЛВК, ВЫН, ПЗ, Комплекция.</summary>
    public static EncounterParticipant Runner(string name, ChaseRole role, int mov = 8, int dex = 50, int con = 50, int hp = 10,
        int build = 0) => new()
    {
        Kind = ParticipantKind.Npc,
        Name = name,
        SourceName = name,
        Side = role == ChaseRole.Pursuer ? EncounterSide.Enemies : EncounterSide.Investigators,
        Initiative = dex,
        Stats = new ParticipantStats { Move = mov, Dex = dex, Con = con, Build = build },
        HitPoints = hp,
        MaxHitPoints = hp,
    };

    /// <summary>Трасса из <paramref name="locations"/> локаций; роли — по стороне участника.</summary>
    public static EncounterState Track(int locations, params EncounterParticipant[] participants)
    {
        var state = new EncounterState();
        foreach (var participant in participants)
            EncounterEngine.Add(state, participant, Now);
        ChaseRules.Begin(state, locations);
        return state;
    }

    public static ChaseRunner R(this EncounterState state, EncounterParticipant participant) => state.Chase!.Runner(participant.Id)!;

    /// <summary>Посадить за руль (по умолчанию стандартный автомобиль) с заданной Комплекцией.</summary>
    public static EncounterState InVehicle(this EncounterState state, EncounterParticipant participant, double build, int speed = 14,
        int? skill = null)
    {
        var car = VehicleReference.Find("Стандартный автомобиль")! with { Build = build, Speed = speed };
        ChaseRules.SetVehicle(state, participant.Id, car, skill);
        return state;
    }

    /// <summary>Проверка скорости пройдена всеми (с поправкой 0) и погоня начата по книге.</summary>
    public static EncounterState Started(this EncounterState state)
    {
        Assert.True(ChaseRules.BeginSpeedChecks(state), ChaseRules.SpeedChecksRejection(state));
        foreach (var runner in state.Chase!.Runners)
            runner.SpeedChecked = true;
        Assert.NotNull(ChaseRules.Start(state, Now));
        return state;
    }

    public static void PutHazard(this EncounterState state, int location, Difficulty difficulty = Difficulty.Regular, string? damage = null,
        string name = "Лужа") =>
        state.Chase!.Location(location)!.Hazard = new ChaseHazard { Name = name, Difficulty = difficulty, Damage = damage };

    public static void PutBarrier(this EncounterState state, int location, int hitPoints = 0, Difficulty difficulty = Difficulty.Regular,
        string name = "Забор") =>
        state.Chase!.Location(location)!.Barrier = new ChaseBarrier { Name = name, Difficulty = difficulty, HitPoints = hitPoints, HitPointsLeft = hitPoints };

    public static void Apply(this EncounterState state, ChaseOutcome outcome) =>
        EncounterEngine.Apply(state, outcome.Resolution, Now);

    public static IEnumerable<EncounterEffect> Of(this ChaseOutcome outcome, EncounterEffectKind kind) =>
        outcome.Resolution.Effects.Where(e => e.Kind == kind);

    public static int? Amount(this ChaseOutcome outcome, EncounterEffectKind kind, Guid? participantId = null) =>
        outcome.Of(kind).Where(e => participantId is null || e.ParticipantId == participantId).Select(e => (int?)e.Amount).FirstOrDefault();

    public static ChaseCheck Skill(string name, int value, int? roll, int bonus = 0, int penalty = 0) =>
        new(name, value, roll, BonusDice: bonus, PenaltyDice: penalty);

    /// <summary>Кости, которых не должно понадобиться.</summary>
    public static IDiceRoller NoDice => ScriptedDice.Of();
}
