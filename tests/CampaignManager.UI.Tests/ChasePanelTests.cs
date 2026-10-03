using Bunit;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Encounters;
using CampaignManager.Core;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Encounters.Chase;
using CampaignManager.UI.Encounters;
using CampaignManager.UI.Encounters.Chase;
using CampaignManager.UI.KeeperScreen;
using CampaignManager.UI.Platform;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Погоня (T2.6c): кнопки хода — ровно <c>ChaseRules.AvailableActions</c>, действие уходит на предпросмотр и ничего не
/// меняет; трасса подписывает бегущих и препятствия словами; ширма показывает таблицы V и VI из Core.
/// </summary>
public sealed class ChasePanelTests : KitContext
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private static EncounterParticipant Runner(string name, EncounterSide side, int dex) => new()
    {
        Name = name,
        Kind = ParticipantKind.Creature,
        Side = side,
        Initiative = dex,
        HitPoints = 10,
        MaxHitPoints = 10,
        Stats = new ParticipantStats { Move = 8, Dex = dex, Con = 50 },
    };

    private (EncounterSession Session, EncounterParticipant Prey, EncounterParticipant Pursuer) Scene()
    {
        Services.AddSingleton<ICharactersApi>(new NoCharacters());
        var state = new EncounterState();
        var prey = Runner("Харви", EncounterSide.Investigators, 60);
        var pursuer = Runner("Упырь", EncounterSide.Enemies, 40);
        EncounterEngine.Add(state, prey, Now);
        EncounterEngine.Add(state, pursuer, Now);
        ChaseRules.Begin(state, 6);
        state.Chase!.Location(2)!.Hazard = new ChaseHazard { Name = "Лужа", Difficulty = Difficulty.Hard };
        state.Chase.Location(5)!.Barrier = new ChaseBarrier { Name = "Забор", HitPoints = 5, HitPointsLeft = 5 };
        ChaseRules.BeginSpeedChecks(state);
        foreach (var runner in state.Chase.Runners)
            runner.SpeedChecked = true;
        ChaseRules.Start(state, Now);

        var session = new EncounterSession(new SavingEncounters(), new EncounterSheetSync(new NoCharacters()),
            Services.GetRequiredService<BrowserStorage>(), Time);
        session.Start(new EncounterDto { Id = Guid.CreateVersion7(), State = state, Version = 1 }, new SkillCatalog([]));
        return (session, prey, pursuer);
    }

    [Fact]
    public void Turn_buttons_are_available_actions_and_move_goes_to_preview()
    {
        var (session, prey, _) = Scene();
        var cut = Render<CascadingValue<EncounterSession>>(p => p
            .Add(c => c.Value, session)
            .Add(c => c.IsFixed, true)
            .AddChildContent<ChaseActionPanel>(c => c.Add(x => x.Skills, new ChaseSkills(new NoCharacters()))));

        var buttons = cut.FindAll("[data-testid^=chase-action-]").Select(b => b.GetAttribute("data-testid") ?? "").ToList();
        var expected = ChaseRules.AvailableActions(session.State, prey.Id).Select(a => $"chase-action-{a}").ToList();
        Assert.Equal(expected, buttons);
        Assert.Contains("chase-action-Move", buttons); // Харви на 3, на 4 свободно

        cut.Find("[data-testid=chase-action-Move]").Click();

        Assert.Equal(EncounterLogKind.Move, session.State.Pending!.Kind);
        Assert.Equal(3, session.State.Chase!.Runner(prey.Id)!.Location); // меняет только «Применить»
    }

    /// <summary>
    /// Обломки (стр. 136, решение владельца 2026-10-02): выбор — только у результата, который разрушит преграду; по умолчанию
    /// «Проход свободен», нажатие меняет предложенный результат, а трассу — только «Применить».
    /// </summary>
    [Fact]
    public void Debris_choice_shows_on_breaking_result_and_changes_only_the_pending()
    {
        var (session, _, pursuer) = Scene();
        var location = session.State.Chase!.Location(5)!;
        IRenderedComponent<CascadingValue<EncounterSession>> Choice(EncounterResolution pending) =>
            Render<CascadingValue<EncounterSession>>(p => p
                .Add(c => c.Value, session)
                .Add(c => c.IsFixed, true)
                .AddChildContent<ChaseDebrisChoice>(c => c.Add(x => x.Resolution, pending)));

        var dent = ChaseActions.BreakBarrier(session.State, pursuer.Id, 5, 2, new Core.Dice.SeededDiceRoller(1)).Resolution;
        Assert.Empty(Choice(dent).FindAll("[data-testid=chase-debris]"));

        var smash = ChaseActions.BreakBarrier(session.State, pursuer.Id, 5, 9, new Core.Dice.SeededDiceRoller(1)).Resolution;
        EncounterEngine.Propose(session.State, smash);
        var cut = Choice(smash);
        Assert.Contains("cm-btn-primary", cut.Find("[data-testid=chase-debris-none]").ClassName, StringComparison.Ordinal);

        cut.Find("[data-testid=chase-debris-Hard]").Click();

        Assert.Equal(Difficulty.Hard, ChaseRules.DebrisOf(session.State.Pending!));
        Assert.NotNull(location.Barrier);
        Assert.Null(location.Hazard);
    }

    [Fact]
    public void Track_names_runners_and_obstacles_in_words()
    {
        var (session, _, _) = Scene();

        var cut = Render<ChaseTrack>(p => p.Add(c => c.State, session.State));

        var text = cut.Find("[data-testid=chase-track]").TextContent;
        Assert.Contains("Харви", text, StringComparison.Ordinal);
        Assert.Contains("Упырь", text, StringComparison.Ordinal);
        Assert.Contains("Лужа · трудная", text, StringComparison.Ordinal);
        Assert.Contains("Забор · обычная · ПЗ 5/5", text, StringComparison.Ordinal);
        Assert.Equal(6, cut.FindAll(".chase-location").Count);
    }

    [Fact]
    public void Keeper_screen_shows_vehicle_and_crash_tables()
    {
        var cut = Render<KeeperReference>(p => p.Add(c => c.ActiveBlock, KeeperScreenBlock.Chase));

        Assert.Equal(5, cut.FindAll("[data-testid=ks-crashes] tbody tr").Count);
        Assert.Equal(27, cut.FindAll("[data-testid=ks-vehicles] tbody tr").Count);
        Assert.Contains("1d3", cut.Find("[data-testid=ks-crashes]").TextContent, StringComparison.Ordinal);
    }

    private sealed class SavingEncounters : IEncountersApi
    {
        private uint _version = 1;

        public Task<IReadOnlyList<EncounterSummaryDto>> ListActiveAsync(EncounterKind? kind, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EncounterSummaryDto>>([]);

        public Task<EncounterDto> StartAsync(StartEncounterRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<EncounterDto> GetAsync(Guid encounterId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<EncounterSavedDto> SaveStateAsync(Guid encounterId, EncounterState state, uint version, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EncounterSavedDto(++_version, Now));

        public Task<EncounterSavedDto> SetRunAsync(Guid encounterId, Guid? runId, uint version, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<EncounterSavedDto> FinishAsync(Guid encounterId, uint version, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoCharacters : ICharactersApi
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
