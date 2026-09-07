namespace CampaignManager.Web.Utilities.Circuits;

/// <summary>
/// При остановке приложения просит подключённые вкладки поставить circuit на паузу,
/// чтобы состояние уехало в браузер и вернулось на новый экземпляр после деплоя.
/// <para>
/// Регистрируется в <c>Program.cs</c> после веб-хоста: сервисы останавливаются в порядке,
/// обратном регистрации, поэтому на момент вызова SignalR ещё принимает сообщения.
/// </para>
/// </summary>
public sealed class CircuitShutdownPauseService(ActiveCircuitTracker tracker) : IHostedService
{
    /// <summary>
    /// Сколько ждём вкладки. Меньше и стандартного докеровского SIGTERM-грейса (10 с),
    /// и <c>HostOptions.ShutdownTimeout</c>.
    /// </summary>
    private static readonly TimeSpan PauseTimeout = TimeSpan.FromSeconds(4);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => tracker.PauseAllAsync(PauseTimeout);
}
