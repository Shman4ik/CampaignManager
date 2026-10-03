using System.Globalization;
using System.Net;
using CampaignManager.Contracts.Encounters;
using CampaignManager.Contracts.Platform;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.Core.Encounters;
using CampaignManager.UI.Platform;

namespace CampaignManager.UI.Encounters;

public enum EncounterSaveState
{
    /// <summary>Сцена совпадает с базой.</summary>
    Saved,

    Saving,

    /// <summary>Запись не прошла (нет связи, ошибка сервера) — повторится сама.</summary>
    Failed,

    /// <summary>Сцену записали в другой вкладке: запись остановлена до «Перечитать» или «Записать мои».</summary>
    Conflict,

    /// <summary>Сервер отказал по смыслу или правам (400, 403, 404): повтор не поможет.</summary>
    Rejected,
}

/// <summary>
/// Открытая сцена на странице: документ, справочник навыков, автосохранение с <c>If-Match</c>, черновик вкладки и запись
/// итогов в листы. Каждое действие Хранителя — <see cref="ChangeAsync"/>: правка документа правилом Core, черновик в
/// <c>localStorage</c>, запись в базу сразу (а не тиком: перезагрузка вкладки посреди сцены ничего не теряет), затем —
/// очередь записей в листы. Тик страницы (<see cref="Interval"/>) повторяет то, что не прошло.
/// <para>
/// Листы пишутся, только когда сцена в базе совпадает с вкладкой: очередь записей живёт в документе, и запись в лист до
/// сохранения сцены при перезагрузке легла бы второй раз.
/// </para>
/// </summary>
public sealed class EncounterSession(IEncountersApi api, EncounterSheetSync sheets, BrowserStorage storage, TimeProvider time)
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private string _saved = "";

    public EncounterDto Encounter { get; private set; } = new();

    public EncounterState State => Encounter.State;

    public SkillCatalog Catalog { get; private set; } = new([]);

    public EncounterSaveState SaveState { get; private set; } = EncounterSaveState.Saved;

    public string? Error { get; private set; }

    public bool SheetsBusy { get; private set; }

    public bool HasChanges => CmJson.Serialize(State) != _saved;

    /// <summary>Что-то изменилось — страница перерисуется.</summary>
    public event Action? Changed;

    public DateTimeOffset Now => time.GetUtcNow();

    public static string DraftKey(Guid encounterId) => $"cm.encounter-draft:{encounterId}";

    /// <summary>Сцена прочитана из базы: она и есть сохранённое состояние.</summary>
    public void Start(EncounterDto encounter, SkillCatalog catalog)
    {
        Encounter = encounter;
        Catalog = catalog;
        _saved = CmJson.Serialize(encounter.State);
        SaveState = EncounterSaveState.Saved;
        Error = null;
    }

    /// <summary>Действие Хранителя: правка документа, черновик, запись, листы.</summary>
    public async Task ChangeAsync(Action<EncounterState> change)
    {
        change(State);
        await storage.SetAsync(DraftKey(Encounter.Id), Draft(Encounter.Version, CmJson.Serialize(State)));
        Changed?.Invoke();
        await SaveAsync();
        await FlushSheetsAsync();
    }

    /// <summary>Тик: недописанное — в базу, неписанное в листы — в листы.</summary>
    public async Task TickAsync()
    {
        await SaveAsync();
        await FlushSheetsAsync();
    }

    /// <summary>Запись документа, если он разошёлся с базой. Возвращает true, если состояние сменилось.</summary>
    public async Task<bool> SaveAsync()
    {
        if (SaveState is EncounterSaveState.Saving or EncounterSaveState.Conflict or EncounterSaveState.Rejected)
            return false;

        var snapshot = CmJson.Serialize(State);
        if (snapshot == _saved)
        {
            var changed = SaveState != EncounterSaveState.Saved;
            SaveState = EncounterSaveState.Saved;
            return changed;
        }

        SaveState = EncounterSaveState.Saving;
        Changed?.Invoke();
        try
        {
            var saved = await api.SaveStateAsync(Encounter.Id, State, Encounter.Version);
            Encounter.Version = saved.Version;
            Encounter.UpdatedAt = saved.UpdatedAt;
            _saved = snapshot;
            Error = null;
            if (CmJson.Serialize(State) == snapshot)
            {
                SaveState = EncounterSaveState.Saved;
                await storage.RemoveAsync(DraftKey(Encounter.Id));
            }
            else
            {
                // Пока шёл запрос, Хранитель сделал ещё ход — его унесёт следующая запись.
                SaveState = EncounterSaveState.Failed;
                return await SaveAsync() || true;
            }
        }
        catch (ApiException error) when (error.IsStale)
        {
            SaveState = EncounterSaveState.Conflict;
            Error = error.Message;
        }
        catch (HttpRequestException error)
        {
            SaveState = error.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.BadRequest
                        || (error.StatusCode == HttpStatusCode.Conflict && error is ApiException { Code: ApiProblemCodes.Conflict })
                ? EncounterSaveState.Rejected
                : EncounterSaveState.Failed;
            Error = ApiErrors.Describe(error);
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>Очередь записей в листы — только когда сцена в базе совпадает с вкладкой (см. описание класса).</summary>
    public async Task FlushSheetsAsync()
    {
        if (SheetsBusy || SaveState != EncounterSaveState.Saved || HasChanges || !State.SheetWrites.Any(w => !w.Blocked))
            return;

        SheetsBusy = true;
        Changed?.Invoke();
        try
        {
            if (await sheets.FlushAsync(State, Catalog, Now))
                await SaveAsync();
        }
        finally
        {
            SheetsBusy = false;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Правка листа участника из сцены (подобранное оружие сыщику — в снаряжение; «Отменить» — убрать) и снимок из
    /// записанного листа. Null — записано, иначе текст ошибки.
    /// </summary>
    public async Task<string?> EditSheetAsync(Guid participantId, Action<CharacterSheet> edit)
    {
        if (State.Find(participantId)?.SourceCharacterId is not { } characterId)
            return "У участника нет листа.";

        var (sheet, error) = await sheets.EditAsync(characterId, edit);
        if (sheet is null)
            return error ?? "Не удалось записать в лист.";

        await ChangeAsync(state =>
        {
            if (state.Find(participantId) is { } participant)
                EncounterParticipants.Refresh(participant, sheet, Catalog);
        });
        return null;
    }

    /// <summary>Числа участников из их листов (при открытии сцены): лист мог поправить игрок.</summary>
    public async Task RefreshFromSheetsAsync()
    {
        if (await sheets.RefreshAsync(State, Catalog))
        {
            Changed?.Invoke();
            await SaveAsync();
        }
    }

    /// <summary>
    /// Прохождение сценария у сцены (T2.6d). Строка меняет версию, поэтому несохранённый документ уходит первым, а новая
    /// версия возвращается в сцену — иначе следующая запись состояния получила бы 409 от самой себя.
    /// </summary>
    public async Task SetRunAsync(Guid? runId)
    {
        if (Encounter.RunId == runId)
            return;

        await SaveAsync();
        if (SaveState is not EncounterSaveState.Saved)
            return;

        try
        {
            var saved = await api.SetRunAsync(Encounter.Id, runId, Encounter.Version);
            Encounter.Version = saved.Version;
            Encounter.UpdatedAt = saved.UpdatedAt;
            Encounter.RunId = runId;
            Error = null;
        }
        catch (ApiException error) when (error.IsStale)
        {
            SaveState = EncounterSaveState.Conflict;
            Error = error.Message;
        }
        catch (HttpRequestException error)
        {
            Error = ApiErrors.Describe(error);
        }

        Changed?.Invoke();
    }

    /// <summary>Конфликт: записать вкладку поверх — с версией, прочитанной сейчас.</summary>
    public async Task OverwriteAsync()
    {
        var fresh = await api.GetAsync(Encounter.Id);
        Encounter.Version = fresh.Version;
        SaveState = EncounterSaveState.Failed;
        Error = null;
        await SaveAsync();
        await FlushSheetsAsync();
    }

    /// <summary>Несохранённый черновик этой сцены: на какой версии его начали и сам документ.</summary>
    public async Task<(uint Version, EncounterState State)?> ReadDraftAsync(Guid encounterId)
    {
        var text = await storage.GetAsync(DraftKey(encounterId));
        if (text is null)
            return null;

        var newline = text.IndexOf('\n', StringComparison.Ordinal);
        if (newline < 0 || !uint.TryParse(text.AsSpan(0, newline), NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            return null;

        try
        {
            return CmJson.DeserializeEncounterState(text[(newline + 1)..]) is { } state ? (version, state) : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public Task DiscardDraftAsync(Guid encounterId) => storage.RemoveAsync(DraftKey(encounterId));

    /// <summary>Черновик вкладки вместо прочитанного (начат на той же версии — значит, просто не дошёл до базы).</summary>
    public void Restore(EncounterState state) => Encounter.State = state;

    private static string Draft(uint version, string snapshot) =>
        $"{version.ToString(CultureInfo.InvariantCulture)}\n{snapshot}";
}
