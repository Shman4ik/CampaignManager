using Bunit;
using CampaignManager.UI.Shared;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

public sealed class DialogTests : KitContext
{
    // В v1 закрытый Modal оставался в дереве скрытым, и id="modal-title" повторялся.
    [Fact]
    public void Closed_modal_renders_nothing()
    {
        var cut = Render<Modal>(p => p
            .Add(m => m.Open, false)
            .Add(m => m.Title, "Изменить оружие")
            .AddChildContent("<p>Тело</p>"));

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void Open_modal_renders_dialog_titled_by_its_heading()
    {
        var cut = Render<Modal>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.Title, "Изменить оружие")
            .AddChildContent("<p>Тело</p>"));

        var dialog = cut.Find("dialog");
        var title = cut.Find("h2");
        Assert.Equal(title.Id, dialog.GetAttribute("aria-labelledby"));
        Assert.Equal("Изменить оружие", title.TextContent);
    }

    [Fact]
    public void Close_button_asks_page_to_close()
    {
        var closed = false;
        var cut = Render<Modal>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.Title, "Окно")
            .Add(m => m.OnClose, () => closed = true));

        cut.Find("button[aria-label='Закрыть']").Click();

        Assert.True(closed);
    }

    [Theory]
    [InlineData("confirm-dialog-ok", true)]
    [InlineData("cancel", false)]
    public async Task Confirm_resolves_with_the_answer_and_closes(string button, bool expected)
    {
        var dialogs = Services.GetRequiredService<DialogService>();
        var host = Render<DialogHost>();

        var answer = dialogs.ConfirmDeleteAsync("Удалить оружие?", "«Кольт» будет удалён.");
        host.WaitForElement("dialog");
        Assert.Contains("«Кольт» будет удалён.", host.Markup);

        if (button == "cancel")
        {
            host.FindAll(".cm-modal-footer button")[0].Click();
        }
        else
        {
            host.Find($"[data-testid='{button}']").Click();
        }

        Assert.Equal(expected, await answer);
        Assert.Empty(host.FindAll("dialog"));
    }

    [Fact]
    public async Task New_request_answers_the_open_one_no()
    {
        var dialogs = Services.GetRequiredService<DialogService>();

        var first = dialogs.ConfirmDeleteAsync("Первый", "…");
        var second = dialogs.ConfirmDeleteAsync("Второй", "…");
        dialogs.Complete(true);

        Assert.False(await first);
        Assert.True(await second);
    }
}
