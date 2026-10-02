using CampaignManager.Core.Dice;
using CampaignManager.Core.Encounters;

namespace CampaignManager.Core.Tests.Encounters.Combat;

/// <summary>Участники и сцена боя для тестов правил: только числа, без листов (как <c>Fighters</c> тестов T0.2).</summary>
internal static class Fighters
{
    public static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    public static EncounterParticipant Make(string name, int hp = 12, EncounterSide side = EncounterSide.Investigators,
        int? sanity = null, int con = 50, int @int = 60, int pow = 50, int dex = 50) => new()
    {
        Name = name,
        SourceName = name,
        Kind = ParticipantKind.Npc,
        Side = side,
        Initiative = dex,
        HitPoints = hp,
        MaxHitPoints = hp,
        MagicPoints = 10,
        MaxMagicPoints = 10,
        Sanity = sanity,
        MaxSanity = sanity is null ? null : 99,
        Luck = 50,
        Stats = new ParticipantStats { Con = con, Int = @int, Pow = pow, Dex = dex, Str = 50, Siz = 50, Dodge = 25, DamageBonus = "0" },
    };

    /// <summary>Сыщик со ссылкой на лист: у него есть рассудок, его эффекты пишутся в лист.</summary>
    public static EncounterParticipant Investigator(string name, int sanity = 50, int @int = 60)
    {
        var p = Make(name, sanity: sanity, @int: @int);
        p.Kind = ParticipantKind.Investigator;
        p.SourceCharacterId = Guid.CreateVersion7();
        return p;
    }

    /// <summary>Сцена в первом раунде.</summary>
    public static EncounterState Battle(params EncounterParticipant[] participants)
    {
        var state = new EncounterState();
        foreach (var p in participants)
            EncounterEngine.Add(state, p, Now);
        EncounterQueue.Start(state, Now);
        return state;
    }

    public static D100Roll Rolled(int value) => D100Roll.Entered(value);

    /// <summary>Применить результат, как «Применить» на странице.</summary>
    public static void Apply(EncounterState state, EncounterResolution resolution) => EncounterEngine.Apply(state, resolution, Now);

    public static void Apply(EncounterState state, AttackOutcome outcome) => Apply(state, outcome.Resolution);
}
