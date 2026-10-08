using Bunit;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Encounters;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Encounters;
using CampaignManager.UI.Encounters;
using CampaignManager.UI.Encounters.Combat;
using CampaignManager.UI.Platform;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Панель боя (T2.6b): правило Core разрешает действие и кладёт результат на предпросмотр, ничего не меняя у участников;
/// проверка ВЫН умирающих — плашкой над вкладками; отметки состояния — в строке участника.
/// </summary>
public sealed class CombatPanelTests : KitContext
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private (EncounterSession Session, EncounterParticipant Investigator, EncounterParticipant Ghoul) Scene()
    {
        var state = new EncounterState();
        var investigator = new EncounterParticipant
        {
            Name = "Харви", Kind = ParticipantKind.Npc, Initiative = 60, Side = EncounterSide.Investigators, HitPoints = 12, MaxHitPoints = 12,
            Stats = new ParticipantStats { Con = 50, Dex = 60, Dodge = 30 },
            Profile = new CombatProfile { Attacks = [new CombatAttack { Key = "brawl", Name = "Драка", Skill = 50, Damage = "1D3" }] },
        };
        var ghoul = EncounterParticipants.FromStatblock(null, "Гуль", new Statblock { HitPoints = 13, Dex = new StatValue { Value = 40 }, Con = new StatValue { Value = 50 } });
        EncounterEngine.Add(state, investigator, Now);
        EncounterEngine.Add(state, ghoul, Now);
        EncounterQueue.Start(state, Now);

        var session = new EncounterSession(new SavingEncounters(), new EncounterSheetSync(new NoCharacters()),
            Services.GetRequiredService<BrowserStorage>(), Time);
        session.Start(new EncounterDto { Id = Guid.CreateVersion7(), State = state, Version = 1 }, new SkillCatalog([]));
        return (session, investigator, ghoul);
    }

    private IRenderedComponent<CascadingValue<EncounterSession>> RenderPanel(EncounterSession session, string? tab = null) =>
        Render<CascadingValue<EncounterSession>>(p => p
            .Add(c => c.Value, session)
            .Add(c => c.IsFixed, true)
            .AddChildContent<CombatPanel>(c => c.Add(x => x.Tab, tab)));

    [Fact]
    public void Attack_goes_to_preview_and_changes_nobody()
    {
        var (session, investigator, ghoul) = Scene();
        var cut = RenderPanel(session);

        Dice.Enqueue(0, 2, 5, 9, 2, 7, 7); // атака 20, защита гуля 95, урон 2 (1d3), ВЫН не нужна; лишнее не тронется
        cut.Find("[data-testid=attack-resolve]").Click();

        var pending = Assert.IsType<EncounterResolution>(session.State.Pending);
        Assert.Equal(EncounterLogKind.Attack, pending.Kind);
        Assert.StartsWith("Харви попадает. Цель: Гуль", pending.Title, StringComparison.Ordinal);
        Assert.Equal(13, ghoul.HitPoints); // меняет только «Применить»
        Assert.Equal(0, investigator.Combat.AttacksIn(session.State.Round));
    }

    /// <summary>Залп очереди (стр. 115): «Пуль в залпе» и урон каждой пули — со стола по порядку; попадает половина залпа.</summary>
    [Fact]
    public void Volley_takes_bullet_damage_from_the_table()
    {
        var (session, investigator, _) = Scene();
        investigator.Profile.Attacks.Add(new CombatAttack
        {
            Key = "smg", Name = "Томпсон", Skill = 50, Damage = "1D10+2", Kind = CombatAttackKind.Ranged, Automatic = true, Impaling = true,
            AmmoCapacity = 20, Malfunction = 96,
        });
        var cut = RenderPanel(session);

        cut.Find("[data-testid=attack-weapon]").Change("smg");
        Assert.Empty(cut.FindAll("[data-testid=attack-volley-size]")); // одиночный выстрел — пуль не спрашивают
        cut.FindAll("button.cm-segment").Single(b => b.TextContent.Trim() == "Очередь").Click();
        cut.Find("[data-testid=attack-volley-size]").Change("4");
        cut.Find("[data-testid=attack-bullet-damage]").Change("5, 6");
        Dice.Enqueue(0, 3, 0, 1); // атака 30 против 50 — обычный успех; 11 урона — серьёзная рана, ВЫН гуля 10
        cut.Find("[data-testid=attack-resolve]").Click();

        var pending = Assert.IsType<EncounterResolution>(session.State.Pending);
        Assert.StartsWith("Харви попадает: 2 из 4.", pending.Title, StringComparison.Ordinal);
        Assert.Contains(pending.Lines, l => l.StartsWith("Пуля 1. Урон: 1d10 + 2 = 5", StringComparison.Ordinal));
        Assert.Contains(pending.Lines, l => l.StartsWith("Пуля 2. Урон: 1d10 + 2 = 6", StringComparison.Ordinal));
    }

    /// <summary>От метательного уклоняются (стр. 106): цель начеку бросает Уклонение.</summary>
    [Fact]
    public void Thrown_weapon_is_dodged_by_a_ready_target()
    {
        var (session, investigator, ghoul) = Scene();
        ghoul.Stats.Dodge = 30;
        investigator.Profile.Attacks.Add(new CombatAttack
        {
            Key = "knife", Name = "Метательный нож", Skill = 50, Damage = "1D4+½БкУ", Kind = CombatAttackKind.Ranged, Thrown = true,
            DamageBonus = CreatureDamageBonusMode.Half,
        });
        var cut = RenderPanel(session);

        cut.Find("[data-testid=attack-weapon]").Change("knife");
        Assert.Contains("Гуль начеку — уклоняется", cut.Find("[data-testid=attack-thrown-dodge]").TextContent, StringComparison.Ordinal);
        Dice.Enqueue(0, 2, 0, 1); // атака 20 — трудный успех; уклонение 10 — тоже трудный: ничья — уклонившемуся
        cut.Find("[data-testid=attack-resolve]").Click();

        var pending = Assert.IsType<EncounterResolution>(session.State.Pending);
        Assert.Contains("уклонился", pending.Title, StringComparison.Ordinal);
        Assert.Contains(pending.Lines, l => l.StartsWith("Гуль (уклонение)", StringComparison.Ordinal));
    }

    [Fact]
    public void Dying_participant_gets_con_check_plaque_next_round()
    {
        var (session, investigator, _) = Scene();
        investigator.HitPoints = 0;
        investigator.MajorWound = true;
        investigator.Dying = true;
        investigator.Unconscious = true;

        var cut = RenderPanel(session);

        Assert.Contains("Харви", cut.Find("[data-testid=dying-checks]").TextContent, StringComparison.Ordinal);
        Dice.Enqueue(0, 9); // 90 против ВЫН 50 — провал
        cut.Find("[data-testid=dying-check]").Click();
        Assert.Contains("смерть", session.State.Pending!.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Row_shows_death_and_combat_marks()
    {
        var (session, investigator, _) = Scene();
        investigator.Dead = true;
        investigator.Combat.Prone = true;
        investigator.Combat.Aiming = true;

        var cut = Render<ParticipantRow>(p => p.Add(c => c.Participant, investigator).Add(c => c.Round, session.State.Round));

        Assert.Contains("мёртв", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("лежит", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("целится", cut.Markup, StringComparison.Ordinal);
    }

    internal sealed class SavingEncounters : IEncountersApi
    {
        private uint _version = 1;

        public Task<IReadOnlyList<EncounterSummaryDto>> ListActiveAsync(EncounterKind? kind, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EncounterSummaryDto>>([]);

        public Task<IReadOnlyList<EncounterSummaryDto>> ListFinishedAsync(EncounterKind? kind, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EncounterSummaryDto>>([]);

        public Task<EncounterDto> StartAsync(StartEncounterRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<EncounterDto> GetAsync(Guid encounterId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<EncounterSavedDto> SaveStateAsync(Guid encounterId, EncounterState state, uint version, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EncounterSavedDto(++_version, Now));

        public Task<EncounterSavedDto> SetRunAsync(Guid encounterId, Guid? runId, uint version, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<EncounterSavedDto> FinishAsync(Guid encounterId, uint version, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>Листов в сцене нет — API листа звать некому.</summary>
    internal sealed class NoCharacters : ICharactersApi
    {
        public Task<CharacterCreatedDto> CreateAsync(CreateCharacterRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CreationContextDto> GetCreationContextAsync(CharacterKind kind, Guid? campaignId, Guid? scenarioId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CharacterSummaryDto>> ListAsync(CharacterKind kind, bool archived, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CharacterDto> GetAsync(Guid characterId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CharacterSavedDto> SaveSheetAsync(Guid characterId, CharacterSheet sheet, uint version, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CharacterSavedDto> SetPortraitAsync(Guid characterId, Guid? fileId, uint version, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CharacterSavedDto> SetStatusAsync(Guid characterId, CharacterStatus status, uint version, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PartyMemberDto>> GetPartyAsync(Guid characterId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<InvestigatorDto>> GetCampaignInvestigatorsAsync(Guid campaignId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
