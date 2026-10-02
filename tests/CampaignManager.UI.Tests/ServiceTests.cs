using CampaignManager.UI.Platform;
using CampaignManager.UI.Shared;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CampaignManager.UI.Tests;

public sealed class ServiceTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void Error_toast_stays_until_closed_others_leave_by_themselves()
    {
        var toasts = new ToastService(_time);
        toasts.Error("Не удалось удалить.");
        toasts.Success("Сохранено.");

        _time.Advance(ToastService.AutoDismissAfter);

        var left = Assert.Single(toasts.Messages);
        Assert.Equal(Tone.Error, left.Tone);

        toasts.Dismiss(left.Id);
        Assert.Empty(toasts.Messages);
    }

    [Fact]
    public void Toasts_keep_only_the_latest()
    {
        var toasts = new ToastService(_time);
        for (var i = 1; i <= ToastService.MaxVisible + 2; i++)
        {
            toasts.Error($"Ошибка {i}");
        }

        Assert.Equal(ToastService.MaxVisible, toasts.Messages.Count);
        Assert.Equal("Ошибка 3", toasts.Messages[0].Message);
    }

    [Fact]
    public void Write_shows_saving_then_saved_then_nothing()
    {
        var activity = new ApiActivity(_time);

        var write = activity.BeginWrite();
        Assert.Equal(ConnectionState.Saving, activity.State);

        activity.ReportReachable(saved: true);
        write.Dispose();
        Assert.Equal(ConnectionState.Saved, activity.State);

        _time.Advance(ApiActivity.SavedNoticeFor);
        Assert.Equal(ConnectionState.Idle, activity.State);
    }

    // Ответ с любым кодом — связь есть; «нет связи» — только когда ответа не было.
    [Fact]
    public void Unreachable_server_is_offline_until_it_answers_again()
    {
        var activity = new ApiActivity(_time);

        activity.ReportUnreachable();
        Assert.Equal(ConnectionState.Offline, activity.State);

        activity.ReportReachable(saved: false);
        Assert.Equal(ConnectionState.Idle, activity.State);
    }

    [Fact]
    public void Browser_offline_wins_over_pending_write()
    {
        var activity = new ApiActivity(_time);
        using var write = activity.BeginWrite();

        activity.SetBrowserOnline(false);
        Assert.Equal(ConnectionState.Offline, activity.State);

        activity.SetBrowserOnline(true);
        Assert.Equal(ConnectionState.Saving, activity.State);
    }
}
