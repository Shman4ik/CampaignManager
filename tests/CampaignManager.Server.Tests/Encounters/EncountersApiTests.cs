using System.Net;
using CampaignManager.ApiClient.Characters;
using CampaignManager.ApiClient.Encounters;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Encounters;
using CampaignManager.Contracts.Platform;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Dice;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Encounters.Chase;
using CampaignManager.Core.Identity;
using CampaignManager.Data.Identity;
using CampaignManager.Server.Tests.Campaigns;
using CampaignManager.UI.Encounters;
using Xunit;

namespace CampaignManager.Server.Tests.Encounters;

/// <summary>
/// Сцены через API (T2.6a): начать (одна активная на вид и кампанию), права ведущего, состояние с <c>If-Match</c> и
/// конфликт двух вкладок, перезагрузка посреди сцены, запись итогов в лист через API листа — в том числе с конфликтом
/// версии листа.
/// </summary>
public sealed class EncountersApiTests(CampaignsApp app) : IClassFixture<CampaignsApp>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>Справочник без Мифов — максимум Рассудка 99; листам тестов его хватает.</summary>
    private static readonly SkillCatalog Catalog = new([]);

    private EncountersApiClient Encounters(User user) => new(app.Http(user));

    private CharactersApiClient Characters(User user) => new(app.Http(user));

    private static async Task<ApiException> Fails(HttpStatusCode status, Func<Task> call)
    {
        var error = await Assert.ThrowsAsync<ApiException>(call);
        Assert.Equal(status, error.StatusCode);
        return error;
    }

    /// <summary>Хранитель, игрок кампании с листом (ПЗ 12, Рассудок 60) и посторонний Хранитель.</summary>
    private async Task<(User Keeper, User Player, User OtherKeeper, Guid CampaignId, Guid SheetId)> TableAsync()
    {
        var keeper = await app.AddUserAsync(UserRole.Keeper, "Хранитель");
        var player = await app.AddUserAsync(UserRole.Player, "Аня");
        var otherKeeper = await app.AddUserAsync(UserRole.Keeper, "Чужой");
        var campaign = await app.AddCampaignAsync(keeper, player);
        var character = await app.AddCharacterAsync(CharacterKind.Player, "Харви Уолтерс", player.Id, campaign.Id);

        var sheet = await Characters(player).GetAsync(character.Id, Cancellation);
        sheet.Sheet.Characteristics = new Characteristics { Str = 50, Con = 60, Siz = 60, Dex = 55, Int = 70, Pow = 60, Edu = 70, App = 50 };
        sheet.Sheet.Current = new CurrentValues { HitPoints = 12, MagicPoints = 12, Sanity = 60, Luck = 50 };
        await Characters(player).SaveSheetAsync(character.Id, sheet.Sheet, sheet.Version, Cancellation);
        return (keeper, player, otherKeeper, campaign.Id, character.Id);
    }

    [Fact]
    public async Task Keeper_starts_one_active_scene_per_kind_and_campaign()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, player, otherKeeper, campaignId, _) = await TableAsync();

        var combat = await Encounters(keeper).StartAsync(new StartEncounterRequest(EncounterKind.Combat, campaignId), Cancellation);
        var conflict = await Fails(HttpStatusCode.Conflict, () =>
            Encounters(keeper).StartAsync(new StartEncounterRequest(EncounterKind.Combat, campaignId), Cancellation));
        var chase = await Encounters(keeper).StartAsync(new StartEncounterRequest(EncounterKind.Chase, campaignId), Cancellation);
        var outside = await Encounters(keeper).StartAsync(new StartEncounterRequest(EncounterKind.Combat, null), Cancellation);

        Assert.Equal(ApiProblemCodes.Conflict, conflict.Code);
        Assert.Contains("уже идёт", conflict.Message, StringComparison.Ordinal);
        Assert.Equal(EncounterStatus.Active, combat.Status);
        Assert.Equal(0, combat.State.Round);
        Assert.NotEqual(0u, combat.Version);

        var active = await Encounters(keeper).ListActiveAsync(EncounterKind.Combat, Cancellation);
        Assert.Equal([combat.Id, outside.Id], active.Select(a => a.Id).Order());
        Assert.Contains(active, a => a.CampaignId == campaignId && a.CampaignName != null);
        Assert.Single(await Encounters(keeper).ListActiveAsync(EncounterKind.Chase, Cancellation), a => a.Id == chase.Id);

        // Игрок сцену не начинает, чужой Хранитель — не в чужой кампании и не видит чужую сцену.
        await Fails(HttpStatusCode.Forbidden, () => Encounters(player).StartAsync(new StartEncounterRequest(EncounterKind.Combat, null), Cancellation));
        await Fails(HttpStatusCode.Forbidden, () =>
            Encounters(otherKeeper).StartAsync(new StartEncounterRequest(EncounterKind.Combat, campaignId), Cancellation));
        await Fails(HttpStatusCode.NotFound, () => Encounters(otherKeeper).GetAsync(combat.Id, Cancellation));
        Assert.Empty(await Encounters(otherKeeper).ListActiveAsync(null, Cancellation));
        Assert.Empty(await Encounters(player).ListActiveAsync(null, Cancellation));

        // Завершённая освобождает место под новую.
        await Encounters(keeper).FinishAsync(combat.Id, combat.Version, Cancellation);
        var next = await Encounters(keeper).StartAsync(new StartEncounterRequest(EncounterKind.Combat, campaignId), Cancellation);
        Assert.NotEqual(combat.Id, next.Id);
    }

    [Fact]
    public async Task State_is_saved_with_version_and_a_stale_tab_gets_409()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, _, _, campaignId, _) = await TableAsync();
        var api = Encounters(keeper);
        var started = await api.StartAsync(new StartEncounterRequest(EncounterKind.Combat, campaignId), Cancellation);

        // Две вкладки открыли одну сцену; первая пишет, вторая — со старой версией.
        var first = started.State;
        EncounterEngine.Note(first, "Первая вкладка", Now);
        var saved = await api.SaveStateAsync(started.Id, first, started.Version, Cancellation);

        var second = (await api.GetAsync(started.Id, Cancellation)).State;
        EncounterEngine.Note(second, "Вторая вкладка", Now);
        var stale = await Fails(HttpStatusCode.Conflict, () => api.SaveStateAsync(started.Id, second, started.Version, Cancellation));
        Assert.True(stale.IsStale);

        var reread = await api.GetAsync(started.Id, Cancellation);
        Assert.Equal(saved.Version, reread.Version);
        Assert.Equal("Первая вкладка", Assert.Single(reread.State.Log).Text);

        // Без версии — 428; завершённую не пишут.
        using var noVersion = await app.Http(keeper).PutAsync(EncountersRoutes.State(started.Id),
            System.Net.Http.Json.JsonContent.Create(first, CampaignManager.Contracts.ContractsJsonContext.Default.EncounterState), Cancellation);
        Assert.Equal(HttpStatusCode.PreconditionRequired, noVersion.StatusCode);

        var finished = await api.FinishAsync(started.Id, reread.Version, Cancellation);
        var closed = await Fails(HttpStatusCode.Conflict, () => api.SaveStateAsync(started.Id, first, finished.Version, Cancellation));
        Assert.Equal(ApiProblemCodes.Conflict, closed.Code);
    }

    [Fact]
    public async Task Reload_mid_scene_returns_participants_queue_preview_and_pending_sheet_writes()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, _, _, campaignId, sheetId) = await TableAsync();
        var api = Encounters(keeper);
        var started = await api.StartAsync(new StartEncounterRequest(EncounterKind.Combat, campaignId), Cancellation);
        var state = started.State;

        var investigator = (await Characters(keeper).GetAsync(sheetId, Cancellation)).Sheet;
        var harvey = EncounterParticipants.FromSheet(sheetId, CharacterKind.Player, investigator, Catalog);
        var ghoul = EncounterParticipants.FromStatblock(null, "Гуль", new CampaignManager.Core.Catalogs.Statblock { HitPoints = 13, Dex = new() { Value = 65 } });
        EncounterEngine.Add(state, harvey, Now);
        EncounterEngine.Add(state, ghoul, Now);
        EncounterQueue.Start(state, Now);
        EncounterEngine.Apply(state, new EncounterResolution
        {
            Title = "Когти гуля",
            Effects = [new EncounterEffect { Kind = EncounterEffectKind.Damage, ParticipantId = harvey.Id, Amount = 4 }],
        }, Now);
        EncounterEngine.Propose(state, new EncounterResolution
        {
            Title = "Вид гуля",
            Effects = [new EncounterEffect { Kind = EncounterEffectKind.SanityLoss, ParticipantId = harvey.Id, Amount = 3, CreatureName = "Гуль" }],
        });
        await api.SaveStateAsync(started.Id, state, started.Version, Cancellation);

        // «Перезагрузка вкладки»: всё из базы.
        var reloaded = (await api.GetAsync(started.Id, Cancellation)).State;

        Assert.Equal(1, reloaded.Round);
        Assert.Equal(ghoul.Id, reloaded.ActiveParticipantId); // ЛВК 65 > 55
        Assert.Equal([ghoul.Id, harvey.Id], reloaded.TurnOrder);
        Assert.Equal(8, reloaded.Find(harvey.Id)!.HitPoints);
        Assert.Equal("Вид гуля", reloaded.Pending!.Title);
        Assert.Equal(4, Assert.Single(Assert.Single(reloaded.SheetWrites).Effects).Amount);
        Assert.Equal(sheetId, reloaded.Find(harvey.Id)!.SourceCharacterId);
    }

    [Fact]
    public async Task Applied_effects_reach_the_sheet_through_the_sheet_api()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, _, _, _, sheetId) = await TableAsync();
        var state = new EncounterState();
        var sheet = (await Characters(keeper).GetAsync(sheetId, Cancellation)).Sheet;
        var harvey = EncounterParticipants.FromSheet(sheetId, CharacterKind.Player, sheet, Catalog);
        EncounterEngine.Add(state, harvey, Now);
        EncounterEngine.Apply(state, new EncounterResolution
        {
            Title = "Ночь в склепе",
            Effects =
            [
                new EncounterEffect { Kind = EncounterEffectKind.Damage, ParticipantId = harvey.Id, Amount = 6 },
                new EncounterEffect { Kind = EncounterEffectKind.SanityLoss, ParticipantId = harvey.Id, Amount = 4, CreatureName = "Гуль", SanityLossFormula = "0/1D6" },
                new EncounterEffect { Kind = EncounterEffectKind.MagicPoints, ParticipantId = harvey.Id, Amount = -5 },
                new EncounterEffect { Kind = EncounterEffectKind.Power, ParticipantId = harvey.Id, Amount = -5 },
            ],
        }, Now);

        var changed = await new EncounterSheetSync(Characters(keeper)).FlushAsync(state, Catalog, Now, Cancellation);

        Assert.True(changed);
        Assert.Empty(state.SheetWrites);
        var written = (await Characters(keeper).GetAsync(sheetId, Cancellation)).Sheet;
        Assert.Equal(6, written.Current.HitPoints);
        Assert.True(written.Condition.MajorWound); // 6 ≥ ⌈12 / 2⌉
        Assert.Equal(56, written.Current.Sanity);
        Assert.Equal(4, Assert.Single(written.Condition.Habituations).LostSanity);
        Assert.Equal(55, written.Characteristics.Pow);
        Assert.Equal(7, written.Current.MagicPoints);
        Assert.Equal(6, state.Find(harvey.Id)!.HitPoints); // снимок — из записанного листа
        Assert.Equal(EncounterLogKind.Sheet, state.Log[^1].Kind);
    }

    [Fact]
    public async Task Sheet_changed_on_another_device_between_read_and_write_keeps_both_changes()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, player, _, _, sheetId) = await TableAsync();
        var state = new EncounterState();
        var harvey = EncounterParticipants.FromSheet(sheetId, CharacterKind.Player, (await Characters(keeper).GetAsync(sheetId, Cancellation)).Sheet, Catalog);
        EncounterEngine.Add(state, harvey, Now);
        EncounterEngine.Apply(state, new EncounterResolution
        {
            Effects = [new EncounterEffect { Kind = EncounterEffectKind.Damage, ParticipantId = harvey.Id, Amount = 3 }],
        }, Now);

        var playerTab = await Characters(player).GetAsync(sheetId, Cancellation);

        // Игрок тратит Удачу на своём листе (другое устройство) ровно между чтением и записью Хранителя.
        var racing = new RacingCharacters(Characters(keeper), async () =>
        {
            var mine = await Characters(player).GetAsync(sheetId, Cancellation);
            mine.Sheet.Current.Luck = 35;
            await Characters(player).SaveSheetAsync(sheetId, mine.Sheet, mine.Version, Cancellation);
        });

        var result = await new EncounterSheetSync(racing).WriteAsync(state.SheetWrites[0], Catalog, Cancellation);

        Assert.Null(result.Error);
        Assert.Equal(2, racing.Saves); // первая — 409 stale, вторая — поверх свежей версии
        var written = (await Characters(keeper).GetAsync(sheetId, Cancellation)).Sheet;
        Assert.Equal(9, written.Current.HitPoints); // урон Хранителя
        Assert.Equal(35, written.Current.Luck);     // и правка игрока

        // Открытая у игрока с утра вкладка (старая версия) урон молча не затрёт — её автосохранение получит 409.
        var stalePlayer = await Fails(HttpStatusCode.Conflict, () =>
            Characters(player).SaveSheetAsync(sheetId, playerTab.Sheet, playerTab.Version, Cancellation));
        Assert.True(stalePlayer.IsStale);
    }

    [Fact]
    public async Task Sheet_write_without_rights_stays_queued_as_permanent_failure()
    {
        TestDatabase.SkipIfMissing();
        var (_, _, otherKeeper, _, sheetId) = await TableAsync();
        var state = new EncounterState();
        var stolen = new EncounterParticipant { Kind = ParticipantKind.Investigator, SourceCharacterId = sheetId, Name = "Чужой лист", MaxHitPoints = 12, HitPoints = 12 };
        EncounterEngine.Add(state, stolen, Now);
        EncounterEngine.Apply(state, new EncounterResolution
        {
            Effects = [new EncounterEffect { Kind = EncounterEffectKind.Damage, ParticipantId = stolen.Id, Amount = 3 }],
        }, Now);

        var result = await new EncounterSheetSync(Characters(otherKeeper)).WriteAsync(state.SheetWrites[0], Catalog, Cancellation);

        Assert.True(result.Permanent);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Invalid_state_is_rejected()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, _, _, _, sheetId) = await TableAsync();
        var api = Encounters(keeper);
        var started = await api.StartAsync(new StartEncounterRequest(EncounterKind.Chase, null), Cancellation);

        var twice = new EncounterState
        {
            Participants =
            [
                new EncounterParticipant { SourceCharacterId = sheetId, Name = "А" },
                new EncounterParticipant { SourceCharacterId = sheetId, Name = "Б" },
            ],
        };
        var crowd = new EncounterState { Participants = [.. Enumerable.Range(0, EncounterEngine.MaxParticipants + 1).Select(i => new EncounterParticipant { Name = $"Культист {i}" })] };

        var stray = new EncounterState { Chase = new ChaseState { Runners = [new ChaseRunner { ParticipantId = Guid.NewGuid() }] } };

        await Fails(HttpStatusCode.BadRequest, () => api.SaveStateAsync(started.Id, twice, started.Version, Cancellation));
        await Fails(HttpStatusCode.BadRequest, () => api.SaveStateAsync(started.Id, crowd, started.Version, Cancellation));
        await Fails(HttpStatusCode.BadRequest, () => api.SaveStateAsync(started.Id, stray, started.Version, Cancellation));
    }

    /// <summary>
    /// Погоня (T2.6c) — свойство документа сцены: трасса, бегущие, разгон и предложенный результат переживают перезагрузку
    /// (читаются из базы), урон помехи уходит в лист через API листа.
    /// </summary>
    [Fact]
    public async Task Chase_survives_reload_and_hazard_damage_reaches_the_sheet()
    {
        TestDatabase.SkipIfMissing();
        var (keeper, _, _, campaignId, sheetId) = await TableAsync();
        var api = Encounters(keeper);
        var started = await api.StartAsync(new StartEncounterRequest(EncounterKind.Chase, campaignId), Cancellation);
        var sheet = (await Characters(keeper).GetAsync(sheetId, Cancellation)).Sheet;

        var state = started.State;
        var harvey = EncounterParticipants.FromSheet(sheetId, CharacterKind.Player, sheet, Catalog);
        var ghoul = new EncounterParticipant { Kind = ParticipantKind.Creature, Name = "Упырь", Side = EncounterSide.Enemies, HitPoints = 13, MaxHitPoints = 13, Stats = new ParticipantStats { Move = 9, Dex = 65, Con = 65 } };
        EncounterEngine.Add(state, harvey, Now);
        EncounterEngine.Add(state, ghoul, Now);
        ChaseRules.Begin(state, 6);
        ChaseRules.BeginSpeedChecks(state);
        foreach (var runner in state.Chase!.Runners)
            runner.SpeedChecked = true;
        ChaseRules.Start(state, Now);
        var hazardAt = state.Chase.Runner(harvey.Id)!.Location + 1;
        state.Chase.Location(hazardAt)!.Hazard = new ChaseHazard { Name = "Забор", Damage = "1D6" };
        var fall = ChaseActions.Hazard(state, harvey.Id, hazardAt, new ChaseCheck("Лазание", 40, 90),
            new ChaseMishap(new ChaseHarm(Roll: 4), LostActionsRoll: 1), DiceRoller.Shared);
        EncounterEngine.Propose(state, fall.Resolution);
        var saved = await api.SaveStateAsync(started.Id, state, started.Version, Cancellation);

        var reloaded = (await api.GetAsync(started.Id, Cancellation)).State;
        Assert.Equal(ChasePhase.Active, reloaded.Chase!.Phase);
        Assert.Equal("Забор", reloaded.Chase.Location(hazardAt)!.Hazard!.Name);
        Assert.Equal(state.Chase.Runner(ghoul.Id)!.Location, reloaded.Chase.Runner(ghoul.Id)!.Location);
        Assert.Equal(fall.Resolution.Id, reloaded.Pending!.Id);

        EncounterEngine.Apply(reloaded, Now);
        var flushed = await new EncounterSheetSync(Characters(keeper)).FlushAsync(reloaded, Catalog, Now, Cancellation);
        await api.SaveStateAsync(started.Id, reloaded, saved.Version, Cancellation);

        Assert.True(flushed);
        Assert.Equal(8, (await Characters(keeper).GetAsync(sheetId, Cancellation)).Sheet.Current.HitPoints);
        Assert.Equal(hazardAt, reloaded.Chase.Runner(harvey.Id)!.Location);
    }

    /// <summary>Клиент листа, перед первой записью которого «другое устройство» успевает записать лист.</summary>
    private sealed class RacingCharacters(ICharactersApi inner, Func<Task> race) : ICharactersApi
    {
        public int Saves { get; private set; }

        public async Task<CharacterSavedDto> SaveSheetAsync(Guid characterId, CharacterSheet sheet, uint version, CancellationToken cancellationToken = default)
        {
            if (Saves++ == 0)
                await race();
            return await inner.SaveSheetAsync(characterId, sheet, version, cancellationToken);
        }

        public Task<CharacterDto> GetAsync(Guid characterId, CancellationToken cancellationToken = default) => inner.GetAsync(characterId, cancellationToken);

        public Task<CharacterCreatedDto> CreateAsync(CreateCharacterRequest request, CancellationToken cancellationToken = default) =>
            inner.CreateAsync(request, cancellationToken);

        public Task<CreationContextDto> GetCreationContextAsync(CharacterKind kind, Guid? campaignId, Guid? scenarioId,
            CancellationToken cancellationToken = default) => inner.GetCreationContextAsync(kind, campaignId, scenarioId, cancellationToken);

        public Task<IReadOnlyList<CharacterSummaryDto>> ListAsync(CharacterKind kind, bool archived, CancellationToken cancellationToken = default) =>
            inner.ListAsync(kind, archived, cancellationToken);

        public Task<CharacterSavedDto> SetPortraitAsync(Guid characterId, Guid? fileId, uint version, CancellationToken cancellationToken = default) =>
            inner.SetPortraitAsync(characterId, fileId, version, cancellationToken);

        public Task<CharacterSavedDto> SetStatusAsync(Guid characterId, CharacterStatus status, uint version, CancellationToken cancellationToken = default) =>
            inner.SetStatusAsync(characterId, status, version, cancellationToken);

        public Task<IReadOnlyList<PartyMemberDto>> GetPartyAsync(Guid characterId, CancellationToken cancellationToken = default) =>
            inner.GetPartyAsync(characterId, cancellationToken);

        public Task<IReadOnlyList<InvestigatorDto>> GetCampaignInvestigatorsAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
            inner.GetCampaignInvestigatorsAsync(campaignId, cancellationToken);
    }
}
