namespace CampaignManager.Web.Components.Features.Scenarios.Model;

public sealed class ScenarioHandout
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public string? Description { get; set; }

    public string? FileUrl { get; set; }

    /// <summary>
    ///     Пометка только для Хранителя: когда выдавать, кому, чем грозит («этот сыщик узнает
    ///     хижину — проверка Рассудка 1/1d6»). Игрокам не уходит никуда — ни в показ на весь экран,
    ///     ни на второй экран. Раньше такие строки писали прямо в <see cref="Description" />, и
    ///     «Показать игрокам» зачитывало их вслух.
    /// </summary>
    public string? KeeperNote { get; set; }

    public int Order { get; set; }
}
