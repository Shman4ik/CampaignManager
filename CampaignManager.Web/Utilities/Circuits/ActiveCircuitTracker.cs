using System.Collections.Concurrent;
using Microsoft.JSInterop;

namespace CampaignManager.Web.Utilities.Circuits;

/// <summary>
/// Список подключённых circuit'ов и их <see cref="IJSRuntime"/>.
/// <para>
/// Нужен только для одного сценария — планового выключения сервера (деплой, перезапуск
/// контейнера). Blazor .NET 10 сохраняет состояние circuit либо на сервере (при вытеснении
/// отключённого circuit), либо в браузере (при явной паузе). Первое не переживает перезапуск
/// процесса, поэтому перед остановкой мы просим каждую вкладку встать на паузу: состояние
/// уезжает в браузер и возвращается на новый экземпляр приложения.
/// </para>
/// <para>
/// В .NET 11 для этого появится штатный <c>Circuit.RequestCircuitPauseAsync</c> — тогда этот
/// класс можно будет выбросить.
/// </para>
/// </summary>
public sealed class ActiveCircuitTracker(
    IHostApplicationLifetime lifetime,
    ILogger<ActiveCircuitTracker> logger)
{
    /// <summary>
    /// Сколько ждём вкладки. Меньше и стандартного докеровского SIGTERM-грейса (10 с),
    /// и <c>HostOptions.ShutdownTimeout</c>.
    /// </summary>
    private static readonly TimeSpan PauseTimeout = TimeSpan.FromSeconds(4);

    private readonly ConcurrentDictionary<string, IJSRuntime> _connected = new();
    private int _shutdownHookRegistered;

    public void Connected(string circuitId, IJSRuntime jsRuntime)
    {
        _connected[circuitId] = jsRuntime;
        EnsureShutdownHook();
    }

    public void Disconnected(string circuitId) => _connected.TryRemove(circuitId, out _);

    /// <summary>
    ///     Вешает паузу на <see cref="IHostApplicationLifetime.ApplicationStopping"/> — и делает это
    ///     лениво, при первом подключившемся circuit. И то, и другое обязательно.
    ///     <para>
    ///     Из <c>IHostedService.StopAsync</c> просить вкладки о паузе бесполезно: SignalR закрывает
    ///     все соединения в своём обработчике <c>ApplicationStopping</c>
    ///     (<c>HttpConnectionManager.CloseAllConnections</c>), а он отрабатывает раньше любого
    ///     <c>StopAsync</c>. К моменту вызова говорить уже не с кем — трекер пуст, JS-вызов уходить
    ///     некуда. Ровно так этот механизм и простоял мёртвым: в логе не появлялось ни строчки.
    ///     </para>
    ///     <para>
    ///     Обработчики <see cref="CancellationToken"/> вызываются в порядке, обратном регистрации,
    ///     поэтому наш должен быть зарегистрирован <b>позже</b> сигналровского. Отсюда лень:
    ///     <c>HttpConnectionManager</c> создаётся при первом соединении, значит на момент первого
    ///     <c>OnConnectionUpAsync</c> он уже подписан, и наш обработчик встаёт перед ним в очередь.
    ///     Регистрация на старте приложения этой гарантии не даёт.
    ///     </para>
    ///     <para>
    ///     Обработчик токена синхронный, поэтому паузу приходится дожидаться блокирующе: иначе
    ///     остановка уйдёт дальше и оборвёт соединения на полуслове. Ожидание ограничено
    ///     <see cref="PauseTimeout"/>.
    ///     </para>
    /// </summary>
    private void EnsureShutdownHook()
    {
        if (Interlocked.Exchange(ref _shutdownHookRegistered, 1) == 1)
            return;

        lifetime.ApplicationStopping.Register(() =>
        {
            try
            {
                PauseAllAsync(PauseTimeout).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Не удалось приостановить circuit'ы перед остановкой сервера.");
            }
        });
    }

    /// <summary>
    /// Просит все подключённые вкладки поставить свой circuit на паузу и ждёт, пока они
    /// отключатся, но не дольше <paramref name="timeout"/> — остановку сервера задерживать нельзя.
    /// </summary>
    public async Task PauseAllAsync(TimeSpan timeout)
    {
        var circuits = _connected.ToArray();
        if (circuits.Length == 0)
            return;

        logger.LogInformation(
            "Остановка сервера: просим {Count} вкладок сохранить состояние circuit в браузере.",
            circuits.Length);

        var deadline = DateTimeOffset.UtcNow + timeout;

        foreach (var (circuitId, jsRuntime) in circuits)
        {
            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            try
            {
                using var cts = new CancellationTokenSource(remaining);
                // Вызов возвращается сразу: сама пауза закрывает соединение, по которому пришла,
                // поэтому ждать её завершения в этом вызове нельзя.
                await jsRuntime.InvokeVoidAsync("campaignManagerCircuit.pauseForShutdown", cts.Token);
            }
            catch (Exception ex)
            {
                // Вкладка могла отвалиться сама — это не повод задерживать остановку.
                logger.LogDebug(ex, "Не удалось попросить circuit {CircuitId} встать на паузу.", circuitId);
            }
        }

        // Пауза завершается отключением, поэтому счётчик подключённых circuit'ов и есть индикатор.
        while (_connected.Count > 0 && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(100);

        logger.LogInformation(
            "Остановка сервера: осталось {Count} неприостановленных вкладок.", _connected.Count);
    }
}
