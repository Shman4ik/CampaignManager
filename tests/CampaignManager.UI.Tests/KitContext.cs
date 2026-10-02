using Bunit;
using CampaignManager.Contracts.Platform;
using CampaignManager.Core.Dice;
using CampaignManager.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Контекст bUnit со службами кита, как их регистрирует Web.Client. JS-модули (диалог, связь)
/// в bUnit не исполняются — интероп в свободном режиме отвечает на них пустыми значениями.
/// </summary>
public abstract class KitContext : BunitContext
{
    protected KitContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<TimeProvider>(Time);
        Services.AddSingleton<IDiceRoller>(Dice);
        Services.AddSingleton<IPingApi>(new UnreachablePing());
        Services.AddCampaignManagerUi(isDevelopment: true);
    }

    protected FakeTimeProvider Time { get; } = new();

    protected QueuedDice Dice { get; } = new();

    private sealed class UnreachablePing : IPingApi
    {
        public Task<PingResponse> PingAsync(CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Нет связи");
    }
}

/// <summary>Кости из очереди: сырые значения так, как их отдаёт генератор (для d100 — сначала единицы, потом десятки).</summary>
public sealed class QueuedDice : IDiceRoller
{
    private readonly Queue<int> _values = new();

    public void Enqueue(params int[] values)
    {
        foreach (var value in values)
        {
            _values.Enqueue(value);
        }
    }

    public int Next(int minInclusive, int maxExclusive) => _values.Dequeue();
}
