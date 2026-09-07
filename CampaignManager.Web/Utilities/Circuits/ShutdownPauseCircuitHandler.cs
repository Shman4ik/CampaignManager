using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.JSInterop;

namespace CampaignManager.Web.Utilities.Circuits;

/// <summary>
/// Держит <see cref="ActiveCircuitTracker"/> в актуальном состоянии: регистрирует circuit,
/// пока он на связи, и снимает регистрацию при обрыве или закрытии.
/// </summary>
public sealed class ShutdownPauseCircuitHandler(
    ActiveCircuitTracker tracker,
    IJSRuntime jsRuntime) : CircuitHandler
{
    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        tracker.Connected(circuit.Id, jsRuntime);
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        tracker.Disconnected(circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        tracker.Disconnected(circuit.Id);
        return Task.CompletedTask;
    }
}
