using System.Globalization;
using System.Net;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Platform;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.UI.Platform;

namespace CampaignManager.UI.Characters;

public enum SheetSaveState
{
    /// <summary>Лист совпадает с базой.</summary>
    Saved,

    /// <summary>Есть несохранённые правки — уйдут ближайшим тиком.</summary>
    Dirty,

    Saving,

    /// <summary>Запись не прошла (нет связи, ошибка сервера) — повторится сама.</summary>
    Failed,

    /// <summary>Лист изменили на другом устройстве: автосохранение остановлено до «Перечитать».</summary>
    Conflict,

    /// <summary>Сервер отказал по смыслу или правам (400, 403, 404): повтор не поможет, автосохранение остановлено.</summary>
    Rejected,
}

/// <summary>
/// Автосохранение листа (знание v1 «Несохранённые правки листа», без circuit): слепок документа сверяется
/// с сохранённым, разошёлся — запись с <c>If-Match</c>. Опрос, а не подписка: блоки правят документ обычным
/// <c>@bind</c>, и слепок ловит любую правку любого поля разом.
/// <list type="bullet">
/// <item>Ручное и автоматическое сохранение делят один флаг занятости — две записи одной строки внахлёст не идут.</item>
/// <item>Несохранённое — черновик в <c>localStorage</c> (<c>cm.sheet-draft:{id}</c>): переживает перезагрузку,
/// закрытую вкладку и обрыв связи. Стирается после записи и при «Перечитать».</item>
/// <item>409 <c>stale</c> — конфликт: дальше не пишем (иначе затёрли бы чужую правку), черновик остаётся.</item>
/// </list>
/// </summary>
public sealed class SheetAutoSave(ICharactersApi api, BrowserStorage storage)
{
    /// <summary>Как часто сверять слепок (v1 — те же 3 секунды).</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private string _saved = "";
    private Guid _id;

    public SheetSaveState State { get; private set; } = SheetSaveState.Saved;

    public uint Version { get; private set; }

    /// <summary>Текст последней ошибки записи (для индикатора и тоста).</summary>
    public string? Error { get; private set; }

    public bool IsBusy => State == SheetSaveState.Saving;

    public static string DraftKey(Guid characterId) => $"cm.sheet-draft:{characterId}";

    /// <summary>Лист прочитан из базы: он и есть сохранённое состояние.</summary>
    public void Start(CharacterDto character)
    {
        _id = character.Id;
        Version = character.Version;
        _saved = CmJson.Serialize(character.Sheet);
        State = SheetSaveState.Saved;
        Error = null;
    }

    public bool HasChanges(CharacterSheet sheet) => CmJson.Serialize(sheet) != _saved;

    /// <summary>Запись прошла мимо листа (портрет, статус): только новая версия.</summary>
    public void Accept(CharacterSavedDto saved) => Version = saved.Version;

    /// <summary>
    /// Тик или кнопка: если лист разошёлся с сохранённым — черновик и запись. Возвращает true, если состояние
    /// сменилось (страница перерисует индикатор).
    /// </summary>
    public async Task<bool> SaveIfChangedAsync(CharacterSheet sheet, CancellationToken cancellationToken = default)
    {
        if (State is SheetSaveState.Saving or SheetSaveState.Conflict or SheetSaveState.Rejected)
            return false;

        var snapshot = CmJson.Serialize(sheet);
        if (snapshot == _saved)
        {
            var changed = State != SheetSaveState.Saved;
            State = SheetSaveState.Saved;
            return changed;
        }

        await storage.SetAsync(DraftKey(_id), Draft(Version, snapshot));
        State = SheetSaveState.Saving;
        try
        {
            var saved = await api.SaveSheetAsync(_id, sheet, Version, cancellationToken);
            Version = saved.Version;
            _saved = snapshot;
            Error = null;
            // Пока шёл запрос, лист могли поправить ещё — тогда он снова «грязный», черновик не трогаем.
            if (CmJson.Serialize(sheet) == snapshot)
            {
                State = SheetSaveState.Saved;
                await storage.RemoveAsync(DraftKey(_id));
            }
            else
            {
                State = SheetSaveState.Dirty;
            }
        }
        catch (ApiException error) when (error.IsStale)
        {
            State = SheetSaveState.Conflict;
            Error = error.Message;
        }
        catch (HttpRequestException error)
        {
            State = error.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.BadRequest
                ? SheetSaveState.Rejected
                : SheetSaveState.Failed;
            Error = ApiErrors.Describe(error);
        }

        return true;
    }

    /// <summary>Несохранённый черновик этого листа: на какой версии его начали и сам лист.</summary>
    public async Task<(uint Version, CharacterSheet Sheet)?> ReadDraftAsync(Guid characterId)
    {
        var text = await storage.GetAsync(DraftKey(characterId));
        if (text is null)
            return null;

        var newline = text.IndexOf('\n', StringComparison.Ordinal);
        if (newline < 0 || !uint.TryParse(text.AsSpan(0, newline), NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            return null;

        try
        {
            return CmJson.DeserializeSheet(text[(newline + 1)..]) is { } sheet ? (version, sheet) : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public Task DiscardDraftAsync(Guid characterId) => storage.RemoveAsync(DraftKey(characterId));

    private static string Draft(uint version, string snapshot) =>
        $"{version.ToString(CultureInfo.InvariantCulture)}\n{snapshot}";
}
