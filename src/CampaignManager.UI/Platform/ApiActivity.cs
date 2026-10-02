namespace CampaignManager.UI.Platform;

/// <summary>
/// Что сейчас со связью: идёт ли запись, дошёл ли последний запрос, есть ли сеть у браузера.
/// Наполняет его обработчик запросов <c>ApiClient</c> в <c>Web.Client</c> (UI про HTTP-клиент не знает),
/// показывает — <c>ConnectionIndicator</c> в оболочке. Заменяет диалог переподключения circuit v1:
/// в WebAssembly состояние экрана живёт в браузере, обрыв связи его не сбрасывает, и пользователю
/// нужно знать только одно — дошли ли его правки до сервера.
/// </summary>
public sealed class ApiActivity(TimeProvider time)
{
    /// <summary>Столько после записи держится «Сохранено».</summary>
    public static readonly TimeSpan SavedNoticeFor = TimeSpan.FromSeconds(2);

    private bool _browserOnline = true;
    private bool _serverUnreachable;

    /// <summary>Записей (POST/PUT/PATCH/DELETE) в пути.</summary>
    public int PendingWrites { get; private set; }

    /// <summary>Когда сервер последний раз принял запись.</summary>
    public DateTimeOffset? LastSavedAt { get; private set; }

    public event Action? Changed;

    public ConnectionState State
    {
        get
        {
            if (!_browserOnline || _serverUnreachable)
            {
                return ConnectionState.Offline;
            }

            if (PendingWrites > 0)
            {
                return ConnectionState.Saving;
            }

            return LastSavedAt is { } saved && time.GetUtcNow() - saved < SavedNoticeFor
                ? ConnectionState.Saved
                : ConnectionState.Idle;
        }
    }

    /// <summary>Запись ушла на сервер; Dispose — ответ пришёл (или не пришёл).</summary>
    public IDisposable BeginWrite()
    {
        PendingWrites++;
        Changed?.Invoke();
        return new WriteScope(this);
    }

    /// <summary>Сервер ответил (любым кодом — значит, связь есть). <paramref name="saved"/> — запись принята.</summary>
    public void ReportReachable(bool saved)
    {
        _serverUnreachable = false;
        if (saved)
        {
            LastSavedAt = time.GetUtcNow();
        }

        Changed?.Invoke();
    }

    /// <summary>Запрос не дошёл до сервера: сеть или сервер лежит.</summary>
    public void ReportUnreachable()
    {
        _serverUnreachable = true;
        Changed?.Invoke();
    }

    /// <summary>События online/offline браузера. Появилась сеть — снова пробуем сервер.</summary>
    public void SetBrowserOnline(bool online)
    {
        _browserOnline = online;
        if (online)
        {
            _serverUnreachable = false;
        }

        Changed?.Invoke();
    }

    private void EndWrite()
    {
        PendingWrites = Math.Max(0, PendingWrites - 1);
        Changed?.Invoke();
    }

    private sealed class WriteScope(ApiActivity owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                owner.EndWrite();
            }
        }
    }
}

public enum ConnectionState
{
    Idle,
    Saving,
    Saved,
    Offline,
}
