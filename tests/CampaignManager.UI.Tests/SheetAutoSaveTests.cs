using System.Net;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Platform;
using CampaignManager.Core.Characters;
using CampaignManager.UI.Characters;
using CampaignManager.UI.Platform;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Автосохранение листа (T2.3): пишет только разошедшийся слепок и с версией, которую правили; конфликт
/// версии останавливает запись (вторая вкладка не затирает первую), отказ сервера — тоже.
/// </summary>
public sealed class SheetAutoSaveTests : KitContext
{
    private sealed class FakeApi : ICharactersApi
    {
        public List<(uint Version, string Name)> Saves { get; } = [];

        public Exception? Fail { get; set; }

        /// <summary>Задержать ответ сервера: запись «идёт», пока ворота не открыты.</summary>
        public TaskCompletionSource? Gate { get; set; }

        public async Task<CharacterSavedDto> SaveSheetAsync(Guid characterId, CharacterSheet sheet, uint version, CancellationToken cancellationToken = default)
        {
            if (Fail is not null)
                throw Fail;

            Saves.Add((version, sheet.Personal.Name));
            if (Gate is { } gate)
            {
                Gate = null;
                await gate.Task;
            }

            return new CharacterSavedDto(version + 1, DateTimeOffset.UnixEpoch);
        }

        public Task<CharacterDto> GetAsync(Guid characterId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CharacterSavedDto> SetPortraitAsync(Guid characterId, Guid? fileId, uint version, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CharacterSavedDto> SetStatusAsync(Guid characterId, CharacterStatus status, uint version, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PartyMemberDto>> GetPartyAsync(Guid characterId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CharacterCreatedDto> CreateAsync(CreateCharacterRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CreationContextDto> GetCreationContextAsync(CharacterKind kind, Guid? campaignId, Guid? scenarioId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CharacterSummaryDto>> ListAsync(CharacterKind kind, bool archived, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<InvestigatorDto>> GetCampaignInvestigatorsAsync(Guid campaignId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private readonly FakeApi _api = new();

    private (SheetAutoSave Saver, CharacterDto Character) Start()
    {
        var saver = new SheetAutoSave(_api, new BrowserStorage(JSInterop.JSRuntime));
        var character = new CharacterDto { Id = Guid.NewGuid(), Version = 7, Sheet = new CharacterSheet { Personal = new PersonalInfo { Name = "Харви" } } };
        saver.Start(character);
        return (saver, character);
    }

    [Fact]
    public async Task Unchanged_sheet_is_not_written()
    {
        var (saver, character) = Start();

        await saver.SaveIfChangedAsync(character.Sheet, TestContext.Current.CancellationToken);

        Assert.Empty(_api.Saves);
        Assert.Equal(SheetSaveState.Saved, saver.State);
    }

    [Fact]
    public async Task Change_is_written_with_the_version_it_was_made_on()
    {
        var (saver, character) = Start();

        character.Sheet.Personal.Name = "Харви Уолтерс";
        await saver.SaveIfChangedAsync(character.Sheet, TestContext.Current.CancellationToken);
        character.Sheet.Current.Luck = 40;
        await saver.SaveIfChangedAsync(character.Sheet, TestContext.Current.CancellationToken);

        Assert.Equal([(7u, "Харви Уолтерс"), (8u, "Харви Уолтерс")], _api.Saves);
        Assert.Equal(9u, saver.Version);
        Assert.Equal(SheetSaveState.Saved, saver.State);
    }

    [Fact]
    public async Task Flush_waits_for_the_running_save_and_writes_what_changed_meanwhile()
    {
        var (saver, character) = Start();
        var gate = new TaskCompletionSource();
        _api.Gate = gate;

        character.Sheet.Personal.Name = "Убрано";
        var tick = saver.SaveIfChangedAsync(character.Sheet, TestContext.Current.CancellationToken); // запись идёт
        character.Sheet.Personal.Name = "Отменено";                                                    // «Отменить» во время записи
        var flush = saver.FlushAsync(character.Sheet);                                                 // уход со страницы

        gate.SetResult();
        await tick;
        await flush;

        // Без FlushAsync вторая запись не случалась: тик видел «уже сохраняю», а страница закрывалась раньше следующего.
        Assert.Equal([(7u, "Убрано"), (8u, "Отменено")], _api.Saves);
        Assert.Equal(SheetSaveState.Saved, saver.State);
    }

    [Fact]
    public async Task Draft_is_dropped_when_the_sheet_returns_to_the_saved_state()
    {
        var (saver, character) = Start();

        character.Sheet.Personal.Name = "Убрано";
        await saver.KeepDraftAsync(character.Sheet);                // «Убрать» — черновик без оружия
        character.Sheet.Personal.Name = "Харви";
        await saver.KeepDraftAsync(character.Sheet);                // «Отменить» — лист снова как в базе

        // Черновик удалён: иначе при возврате он восстановился бы сам и записал «Убрано».
        Assert.Contains(JSInterop.Invocations, i => i.Arguments.Any(a => a?.ToString() == SheetAutoSave.DraftKey(character.Id))
            && i.Identifier.Contains("remove", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Stale_version_stops_autosave_instead_of_overwriting()
    {
        var (saver, character) = Start();
        _api.Fail = new ApiException("Запись изменили на другом устройстве.", HttpStatusCode.Conflict, ApiProblemCodes.Stale);

        character.Sheet.Personal.Name = "Чужая правка";
        await saver.SaveIfChangedAsync(character.Sheet, TestContext.Current.CancellationToken);
        _api.Fail = null;
        await saver.SaveIfChangedAsync(character.Sheet, TestContext.Current.CancellationToken);

        Assert.Equal(SheetSaveState.Conflict, saver.State);
        Assert.Empty(_api.Saves);
    }

    [Fact]
    public async Task Lost_connection_is_retried_next_tick()
    {
        var (saver, character) = Start();
        _api.Fail = new HttpRequestException("нет связи");

        character.Sheet.Personal.Name = "Харви Уолтерс";
        await saver.SaveIfChangedAsync(character.Sheet, TestContext.Current.CancellationToken);
        Assert.Equal(SheetSaveState.Failed, saver.State);

        _api.Fail = null;
        await saver.SaveIfChangedAsync(character.Sheet, TestContext.Current.CancellationToken);

        Assert.Equal(SheetSaveState.Saved, saver.State);
        Assert.Single(_api.Saves);
    }

    [Fact]
    public async Task Rejected_by_server_is_not_retried()
    {
        var (saver, character) = Start();
        _api.Fail = new ApiException("В листе навык, которого нет в справочнике.", HttpStatusCode.BadRequest, ApiProblemCodes.Invalid);

        character.Sheet.Personal.Name = "Харви Уолтерс";
        await saver.SaveIfChangedAsync(character.Sheet, TestContext.Current.CancellationToken);
        _api.Fail = null;
        await saver.SaveIfChangedAsync(character.Sheet, TestContext.Current.CancellationToken);

        Assert.Equal(SheetSaveState.Rejected, saver.State);
        Assert.Empty(_api.Saves);
    }
}
