using System.Globalization;
using System.Text.Json;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Platform;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.UI.Platform;

namespace CampaignManager.UI.Characters.Creation;

/// <summary>
/// Где живёт черновик помощника. Всегда — <c>localStorage</c> (ключ <see cref="CreationLinks.DraftKey"/>), сразу на каждую правку.
/// Сыщик игрока в кампании ещё и на сервере (<see cref="ICharacterDraftsApi"/>): запись с задержкой <see cref="ServerDelay"/>
/// после последней правки, с версией (<c>If-Match</c>), как автосохранение листа, — продолжить можно с другого устройства, а
/// Хранитель видит шаг. Без сети остаётся <c>localStorage</c>; при открытии берётся более свежий из двух
/// (<see cref="PreferLocal"/>). Чужая запись с другого устройства (409 <c>stale</c>) останавливает запись на сервер —
/// <see cref="Conflict"/>, решает игрок: взять оттуда или оставить свой.
/// <para>
/// Рядом с черновиком в <c>localStorage</c> — строка <c>{ключ}:sync</c>: когда черновик записан здесь и на какой версии сервера
/// он начат (<c>2026-10-04T12:00:00.0000000+00:00|123</c>). По ней видно, есть ли здесь правки, которые сервер ещё не получил.
/// </para>
/// </summary>
public sealed class WizardDraftStore(BrowserStorage storage, ICharacterDraftsApi api, TimeProvider time, string key, Guid? campaignId)
    : IDisposable
{
    /// <summary>Пауза после последней правки до записи на сервер: набор имени не шлёт запрос на каждую букву.</summary>
    public static readonly TimeSpan ServerDelay = TimeSpan.FromSeconds(2);

    private readonly string _syncKey = key + ":sync";
    private InvestigatorDraft? _latest;
    private uint? _version;
    private string? _serverJson;
    private DateTimeOffset? _savedAt;
    private bool _serverOff;
    private bool _saving;
    private bool _again;
    private CancellationTokenSource? _debounce;

    /// <summary>Черновик на сервере записали или стёрли с другого устройства: запись туда остановлена до выбора игрока.</summary>
    public bool Conflict { get; private set; }

    /// <summary>Сменилось <see cref="Conflict"/> — странице перерисоваться (событие приходит не из её обработчика).</summary>
    public event Action? Changed;

    /// <summary>Черновик хранится и на сервере (сыщик игрока в кампании, пока сервер его принимает).</summary>
    public bool UsesServer => campaignId is not null && !_serverOff;

    /// <summary>
    /// Черновик при открытии: из <c>localStorage</c> и с сервера — более свежий; здешний, которого сервер ещё не видел, уходит туда
    /// сразу. Без сети — здешний. <c>null</c> — черновика нет нигде.
    /// </summary>
    public async Task<InvestigatorDraft?> LoadAsync(CancellationToken cancellationToken)
    {
        var localJson = await storage.GetAsync(key);
        var local = Parse(localJson);
        if (local is null && localJson is not null)
            await storage.RemoveAsync(key);
        (_savedAt, _version) = ParseSync(await storage.GetAsync(_syncKey));

        if (campaignId is not { } campaign)
            return local;

        CharacterDraftDto? server;
        try
        {
            server = await api.GetMineAsync(campaign, cancellationToken);
        }
        catch (ApiException)
        {
            // 403 и прочие отказы по смыслу: сервер этот черновик не примет (например, администратор не участник) — только здесь.
            _serverOff = true;
            return local;
        }
        catch (HttpRequestException)
        {
            // Нет связи: продолжаем здешний, на сервер он уйдёт со следующей правкой.
            return local;
        }

        if (server is not null)
        {
            _serverJson = CmJson.Serialize(server.Draft);
        }

        if (!PreferLocal(local is null ? null : localJson, _savedAt, _version, server))
        {
            _version = server?.Version;
            if (server is null)
                return null;

            _latest = server.Draft;
            await WriteLocalAsync(_serverJson!, server.UpdatedAt);
            return server.Draft;
        }

        // Здешний свежее: сервер получает его поверх своей версии.
        _version = server?.Version;
        _latest = local;
        await PushAsync();
        return local;
    }

    /// <summary>
    /// Брать ли при открытии здешний черновик, а не серверный. Здешний — если на сервере нет; если в нём правки поверх той же
    /// версии сервера, что ещё не ушли; если записан позже серверного. Одинаковые — серверный (он уже всё знает).
    /// </summary>
    public static bool PreferLocal(string? localJson, DateTimeOffset? localSavedAt, uint? localBaseVersion, CharacterDraftDto? server)
    {
        if (localJson is null)
            return false;
        if (server is null)
            return true;
        if (CmJson.Serialize(server.Draft) == localJson)
            return false;
        if (localBaseVersion == server.Version)
            return true;
        return localSavedAt is { } savedAt && savedAt > server.UpdatedAt;
    }

    /// <summary>Правка: в <c>localStorage</c> сразу, на сервер — через <see cref="ServerDelay"/> после последней.</summary>
    public async Task SaveAsync(InvestigatorDraft draft)
    {
        _latest = draft;
        await WriteLocalAsync(CmJson.Serialize(draft), time.GetUtcNow());
        if (UsesServer && !Conflict)
            Schedule();
    }

    /// <summary>Уходя со страницы — дописать на сервер то, что ждёт паузы.</summary>
    public async Task FlushAsync()
    {
        if (_debounce is null)
            return;
        CancelSchedule();
        await PushAsync();
    }

    /// <summary>«Начать заново»: черновика нет ни здесь, ни на сервере (Хранитель больше не видит шаг).</summary>
    public async Task ClearAsync()
    {
        CancelSchedule();
        _latest = null;
        await storage.RemoveAsync(key);
        await storage.RemoveAsync(_syncKey);
        _savedAt = null;
        if (campaignId is { } campaign && !_serverOff)
        {
            try
            {
                await api.DeleteAsync(campaign);
                (_version, _serverJson) = (null, null);
                SetConflict(false);
            }
            catch (HttpRequestException)
            {
                // Без связи серверный останется; следующая запись отсюда — новее, её возьмёт любое устройство.
            }
        }
    }

    /// <summary>Лист создан — сервер стёр черновик сам, здесь он тоже не нужен; больше ничего не писать.</summary>
    public async Task ForgetAsync()
    {
        CancelSchedule();
        _serverOff = true;
        await storage.RemoveAsync(key);
        await storage.RemoveAsync(_syncKey);
    }

    /// <summary>Конфликт: взять черновик с сервера (его нет — лист уже создан или начат заново там — пустой).</summary>
    public async Task<InvestigatorDraft?> TakeServerAsync()
    {
        var server = await api.GetMineAsync(campaignId!.Value);
        _version = server?.Version;
        _serverJson = server is null ? null : CmJson.Serialize(server.Draft);
        _latest = server?.Draft;
        if (server is null)
        {
            await storage.RemoveAsync(key);
            await storage.RemoveAsync(_syncKey);
        }
        else
        {
            await WriteLocalAsync(_serverJson!, server.UpdatedAt);
        }

        SetConflict(false);
        return server?.Draft;
    }

    /// <summary>Конфликт: оставить здешний — записать его поверх свежей версии сервера.</summary>
    public async Task KeepMineAsync(InvestigatorDraft draft)
    {
        var server = await api.GetMineAsync(campaignId!.Value);
        _version = server?.Version;
        _serverJson = null;
        _latest = draft;
        SetConflict(false);
        await PushAsync();
    }

    public void Dispose()
    {
        // Ушли со страницы, не дождавшись паузы: дописать в фоне (не вышло — здешний новее, его возьмёт следующее открытие).
        if (_debounce is not null)
        {
            CancelSchedule();
            _ = PushAsync();
        }
    }

    private void Schedule()
    {
        CancelSchedule();
        var debounce = _debounce = new CancellationTokenSource();
        _ = DelayedPushAsync(debounce.Token);
    }

    private async Task DelayedPushAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(ServerDelay, time, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _debounce = null;
        await PushAsync();
    }

    private void CancelSchedule()
    {
        _debounce?.Cancel();
        _debounce?.Dispose();
        _debounce = null;
    }

    private async Task PushAsync()
    {
        if (!UsesServer || Conflict || _latest is null)
            return;
        if (_saving)
        {
            _again = true;
            return;
        }

        var json = CmJson.Serialize(_latest);
        if (json == _serverJson)
            return;

        _saving = true;
        try
        {
            // Копия, а не сам черновик: тело запроса сериализуется при отправке, а игрок тем временем правит дальше.
            var saved = await api.SaveAsync(campaignId!.Value, CmJson.DeserializeDraft(json)!, _version);
            (_version, _serverJson) = (saved.Version, json);
            await storage.SetAsync(_syncKey, SyncText(_savedAt ?? time.GetUtcNow(), _version));
        }
        catch (ApiException error) when (error.IsStale || error.Code == ApiProblemCodes.VersionRequired)
        {
            // Записали с другого устройства — или там завели черновик, пока здесь не было связи (версии мы не знаем).
            SetConflict(true);
        }
        catch (ApiException)
        {
            // 400/403: повтор не поможет — черновик остаётся только здесь.
            _serverOff = true;
        }
        catch (HttpRequestException)
        {
            // Нет связи: следующая правка повторит запись.
        }
        finally
        {
            _saving = false;
        }

        if (_again)
        {
            _again = false;
            await PushAsync();
        }
    }

    private async Task WriteLocalAsync(string json, DateTimeOffset savedAt)
    {
        _savedAt = savedAt;
        await storage.SetAsync(key, json);
        if (campaignId is not null)
            await storage.SetAsync(_syncKey, SyncText(savedAt, _version));
    }

    private void SetConflict(bool conflict)
    {
        if (Conflict == conflict)
            return;
        Conflict = conflict;
        Changed?.Invoke();
    }

    private static InvestigatorDraft? Parse(string? json)
    {
        if (json is null)
            return null;
        try
        {
            return CmJson.DeserializeDraft(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string SyncText(DateTimeOffset savedAt, uint? version) =>
        savedAt.ToString("O", CultureInfo.InvariantCulture) + "|" + version?.ToString(CultureInfo.InvariantCulture);

    private static (DateTimeOffset? SavedAt, uint? Version) ParseSync(string? text)
    {
        var parts = (text ?? "").Split('|');
        DateTimeOffset? savedAt = DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;
        uint? version = parts.Length > 1 && uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : null;
        return (savedAt, version);
    }
}
