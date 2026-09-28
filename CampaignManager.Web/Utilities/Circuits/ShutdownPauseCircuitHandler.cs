using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace CampaignManager.Web.Utilities.Circuits;

/// <summary>
/// Держит <see cref="ActiveCircuitTracker"/> в актуальном состоянии: регистрирует circuit,
/// пока он на связи, и снимает регистрацию при обрыве или закрытии. Заодно сообщает трекеру
/// о событиях жизни circuit — тот пишет их в лог по строке на событие.
/// <para>
/// Обработчик scoped, то есть свой у каждого circuit: в нём и живёт то, что нужно помнить между
/// событиями одного circuit (был ли обрыв, ушёл ли он на паузу, когда открылся).
/// </para>
/// </summary>
public sealed class ShutdownPauseCircuitHandler(
    ActiveCircuitTracker tracker,
    IJSRuntime jsRuntime,
    PersistentComponentState persistentState) : CircuitHandler, IDisposable
{
    private DateTimeOffset _openedAt;
    private bool _connectionLost;
    private bool _paused;
    private PersistingComponentStateSubscription _pauseSubscription;

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _openedAt = DateTimeOffset.UtcNow;
        tracker.Opened(circuit.Id);

        // Отдельного события «пауза» у CircuitHandler нет. Зато пауза — это сохранение состояния
        // circuit: Blazor зовёт колбэки сохранения из его области ровно тогда (пререндер живёт в
        // области HTTP-запроса, сюда он не попадает). Это и ловим.
        _pauseSubscription = persistentState.RegisterOnPersisting(() =>
        {
            _paused = true;
            tracker.Paused(circuit.Id);
            return Task.CompletedTask;
        }, RenderMode.InteractiveServer);

        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        tracker.Connected(circuit.Id, jsRuntime, reconnected: _connectionLost);
        _connectionLost = false;
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        // Обрыв после паузы — её же следствие, отдельной строки в логе он не стоит.
        _connectionLost = true;
        tracker.Disconnected(circuit.Id, logLoss: !_paused);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        tracker.Closed(circuit.Id, DateTimeOffset.UtcNow - _openedAt, _paused);
        return Task.CompletedTask;
    }

    public void Dispose() => _pauseSubscription.Dispose();
}
