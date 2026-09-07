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
public sealed class ActiveCircuitTracker(ILogger<ActiveCircuitTracker> logger)
{
    private readonly ConcurrentDictionary<string, IJSRuntime> _connected = new();

    public void Connected(string circuitId, IJSRuntime jsRuntime) => _connected[circuitId] = jsRuntime;

    public void Disconnected(string circuitId) => _connected.TryRemove(circuitId, out _);

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
