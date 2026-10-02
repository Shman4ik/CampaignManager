using Bunit;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Contracts.Scenarios;
using CampaignManager.UI.Campaigns;
using CampaignManager.UI.Platform;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Кнопки брони на карточке ваншота главной (T2.5c) — по флагам сервера: свободного прегена бронирует тот, кому можно
/// (<see cref="HomeOneShotDto.CanReserve"/>), у своего — «Лист» и «Снять бронь», ведущий снимает любую.
/// </summary>
public sealed class OneShotCardTests : KitContext
{
    private static readonly Guid RunId = Guid.Parse("0199b000-0000-7000-8000-000000000201");
    private static readonly Guid Free = Guid.Parse("0199b000-0000-7000-8000-000000000202");
    private static readonly Guid Taken = Guid.Parse("0199b000-0000-7000-8000-000000000203");
    private static readonly Guid Sheet = Guid.Parse("0199b000-0000-7000-8000-000000000204");

    private readonly List<Guid> _reserved = [];

    public OneShotCardTests()
    {
        Services.AddSingleton(Fake.Of<IRunsApi>(new()
        {
            [nameof(IRunsApi.ReserveAsync)] = args =>
            {
                _reserved.Add((Guid)args![1]!);
                return Task.FromResult(new ReservationDto(RunId, (Guid)args[1]!, Sheet, Guid.Empty));
            },
        }));
    }

    private static HomeOneShotDto Run(bool canReserve, params HomePregenDto[] pregens) =>
        new(RunId, Guid.Empty, Guid.Empty, "Эликсир жизни", null, null, "Хранитель", IsMine: false, pregens, canReserve);

    [Fact]
    public async Task Player_without_reservation_reserves_a_free_pregen_and_the_page_reloads()
    {
        var reloaded = 0;
        var card = Render<OneShotCard>(p => p
            .Add(c => c.Run, Run(true,
                new HomePregenDto(Free, "Доктор", null, false, null, false, null, false),
                new HomePregenDto(Taken, "Букинист", null, true, "Анна", false, null, false)))
            .Add(c => c.OnChanged, () => reloaded++));

        Assert.Single(card.FindAll("[data-testid=reserve]"));
        Assert.Empty(card.FindAll("[data-testid=release]"));
        Assert.Contains("Занят: Анна", card.Markup, StringComparison.Ordinal);

        await card.Find("[data-testid=reserve]").ClickAsync(new());
        Assert.Equal([Free], _reserved);
        Assert.Equal(1, reloaded);
    }

    [Fact]
    public void Own_reservation_links_the_copy_and_can_be_released()
    {
        var card = Render<OneShotCard>(p => p.Add(c => c.Run, Run(false,
            new HomePregenDto(Free, "Доктор", null, false, null, false, null, false),
            new HomePregenDto(Taken, "Букинист", null, true, "Анна", true, Sheet, true))));

        Assert.Empty(card.FindAll("[data-testid=reserve]"));
        Assert.Single(card.FindAll("[data-testid=release]"));
        Assert.Single(card.FindAll($"a[href='character/{Sheet}']"));
        Assert.Contains("Свободен", card.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Datetime_input_round_trips_through_utc()
    {
        var moment = new DateTimeOffset(2026, 10, 10, 17, 0, 0, TimeSpan.Zero);
        var input = LocalTime.ToInput(moment);
        Assert.Equal(moment, LocalTime.FromInput(input));
        Assert.Equal(TimeSpan.Zero, LocalTime.FromInput(input)!.Value.Offset);
        Assert.Null(LocalTime.FromInput(null));
    }
}
