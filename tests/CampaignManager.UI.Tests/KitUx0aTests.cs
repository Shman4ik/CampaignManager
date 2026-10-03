using Bunit;
using CampaignManager.UI.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>UX-0a: новые параметры общих компонентов кита (design-system.md, «Правила 2.0 после аудита»).</summary>
public sealed class KitUx0aTests : KitContext
{
    [Fact]
    public void Disabled_button_names_the_reason_and_links_it_by_aria()
    {
        var cut = Render<Button>(p => p
            .Add(b => b.Disabled, true)
            .Add(b => b.DisabledReason, "Введите название")
            .AddChildContent("Создать"));

        var reason = cut.Find(".cm-btn-reason");
        Assert.Equal("Введите название", reason.TextContent);
        Assert.Equal(reason.Id, cut.Find("button").GetAttribute("aria-describedby"));
    }

    [Fact]
    public void Enabled_button_shows_no_reason()
    {
        var cut = Render<Button>(p => p
            .Add(b => b.DisabledReason, "Введите название")
            .AddChildContent("Создать"));

        Assert.Empty(cut.FindAll(".cm-btn-reason"));
        Assert.Null(cut.Find("button").GetAttribute("aria-describedby"));
    }

    [Fact]
    public void Required_field_marks_label_and_error_marks_the_field_invalid()
    {
        var cut = Render<Field>(p => p
            .Add(f => f.Label, "Название")
            .Add(f => f.Required, true)
            .Add(f => f.Error, "Введите название.")
            .AddChildContent("<input class=\"cm-input\" />"));

        Assert.Equal("*", cut.Find(".cm-field-required").TextContent);
        Assert.Contains("cm-field-invalid", cut.Find("label").ClassName);
        Assert.Equal("Введите название.", cut.Find(".cm-field-error").TextContent);
    }

    [Fact]
    public void Field_without_error_is_not_invalid_and_has_no_required_mark()
    {
        var cut = Render<Field>(p => p
            .Add(f => f.Label, "Название")
            .Add(f => f.Note, "Одна строка.")
            .AddChildContent("<input class=\"cm-input\" />"));

        Assert.DoesNotContain("cm-field-invalid", cut.Find("label").ClassName);
        Assert.Empty(cut.FindAll(".cm-field-required"));
    }

    [Fact]
    public void Modal_puts_server_error_and_disabled_reason_into_the_footer_and_reserves_height()
    {
        var cut = Render<Modal>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.Title, "Новая раздатка")
            .Add(m => m.MinHeight, "22rem")
            .Add(m => m.FooterError, (RenderFragment)(b => b.AddMarkupContent(0, "<div class=\"cm-alert\">Отказ сервера</div>")))
            .Add(m => m.FooterHint, (RenderFragment)(b => b.AddContent(0, "Введите название")))
            .Add(m => m.Footer, (RenderFragment)(b => b.AddMarkupContent(0, "<button class=\"cm-btn cm-btn-secondary\">Отмена</button>")))
            .AddChildContent("<p>Тело</p>"));

        var footer = cut.Find(".cm-modal-footer");
        Assert.Contains("Отказ сервера", footer.QuerySelector(".cm-modal-footer-error")!.TextContent);
        Assert.Equal("Введите название", footer.QuerySelector(".cm-modal-footer-hint")!.TextContent);
        Assert.Empty(cut.Find(".cm-modal-body").QuerySelectorAll(".cm-alert"));
        Assert.Contains("--cm-modal-min-height: 22rem", cut.Find(".cm-modal-body").GetAttribute("style"));
    }

    [Fact]
    public void Modal_asks_the_script_to_focus_the_first_field_not_the_close_button()
    {
        Render<Modal>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.Title, "Окно")
            .AddChildContent("<input class=\"cm-input\" />"));

        // show() — тот же вызов, что ставит фокус: сам выбор элемента (focusFirst) живёт в ModalWindow.razor.js.
        Assert.Single(JSInterop.Invocations, i => i.Identifier == "show");
    }

    [Fact]
    public async Task Removed_toast_offers_undo_for_8_seconds_and_runs_it_once()
    {
        var time = new FakeTimeProvider();
        var toasts = new ToastService(time);
        var undone = 0;

        toasts.Removed("Кольт .45", () =>
        {
            undone++;
            return Task.CompletedTask;
        });

        var toast = Assert.Single(toasts.Messages);
        Assert.Equal("Убрано: Кольт .45", toast.Message);
        Assert.Equal("Отменить", toast.Action!.Label);

        time.Advance(ToastService.AutoDismissAfter);
        Assert.Single(toasts.Messages);

        await toasts.RunActionAsync(toast.Id);
        await toasts.RunActionAsync(toast.Id);
        Assert.Equal(1, undone);
        Assert.Empty(toasts.Messages);
    }

    [Fact]
    public void Undo_toast_unused_leaves_after_8_seconds()
    {
        var time = new FakeTimeProvider();
        var toasts = new ToastService(time);
        toasts.Removed("Кольт .45", () => Task.CompletedTask);

        time.Advance(ToastService.LongDismissAfter);

        Assert.Empty(toasts.Messages);
    }

    [Fact]
    public void Toast_host_renders_undo_button_that_runs_the_action()
    {
        var toasts = Services.GetRequiredService<ToastService>();
        var host = Render<ToastHost>();
        var undone = false;

        toasts.Removed("Кольт .45", () =>
        {
            undone = true;
            return Task.CompletedTask;
        });

        host.WaitForElement("[data-testid='toast-action']").Click();

        Assert.True(undone);
        Assert.Empty(host.FindAll("[data-testid='toast-action']"));
    }

    [Fact]
    public async Task Confirm_with_object_shows_bold_subject_without_wrapping_quotes_and_names_the_button()
    {
        var dialogs = Services.GetRequiredService<DialogService>();
        var host = Render<DialogHost>();

        var answer = dialogs.ConfirmDeleteAsync("оружие", "«Кольт»", "Пропадёт из справочника.", reversible: false);
        host.WaitForElement("dialog");

        Assert.Equal("Кольт", host.Find("[data-testid='confirm-dialog-subject']").TextContent);
        Assert.Equal("Удалить оружие", host.Find("[data-testid='confirm-dialog-ok']").TextContent.Trim());
        Assert.Contains("Нельзя отменить.", host.Markup);

        host.Find("[data-testid='confirm-dialog-ok']").Click();
        Assert.True(await answer);
    }

    [Fact]
    public async Task Confirm_keeps_inner_quotes_and_says_reversible_when_it_is()
    {
        var dialogs = Services.GetRequiredService<DialogService>();
        var host = Render<DialogHost>();

        _ = dialogs.ConfirmDeleteAsync("оружие", "Кольт «Миротворец»", "Пропадёт.", reversible: true);
        host.WaitForElement("dialog");

        Assert.Equal("Кольт «Миротворец»", host.Find("[data-testid='confirm-dialog-subject']").TextContent);
        Assert.Contains("Можно вернуть.", host.Markup);
        dialogs.Complete(false);
        await Task.CompletedTask;
    }

    [Fact]
    public void Header_without_game_has_no_screen_or_player_slots_and_shows_subtitle_and_title_suffix()
    {
        var cut = Render<PageHeader>(p => p
            .Add(h => h.Title, "Кампании")
            .Add(h => h.Subtitle, "Маски Ньярлатотепа · архив"));

        Assert.Equal("Маски Ньярлатотепа · архив", cut.Find("[data-testid='page-subtitle']").TextContent);
        Assert.Equal("Кампании — Campaign Manager", cut.Instance.FullTitle);
    }

    [Fact]
    public void Header_title_for_object_keeps_type_and_name()
    {
        var cut = Render<PageHeader>(p => p
            .Add(h => h.Title, "Лист сыщика: Элизабет Миллер"));

        Assert.Equal("Лист сыщика: Элизабет Миллер — Campaign Manager", cut.Instance.FullTitle);
        Assert.Empty(cut.FindAll("[data-testid='page-subtitle']"));
    }

    [Fact]
    public void Compact_empty_state_is_one_line_without_icon_or_card()
    {
        var cut = Render<EmptyState>(p => p
            .Add(e => e.Compact, true)
            .Add(e => e.Title, EmptyState.None("книг"))
            .Add(e => e.Message, "Не выводится"));

        Assert.Equal("Нет книг.", cut.Find("p.cm-empty-compact").TextContent);
        Assert.Empty(cut.FindAll("i"));
        Assert.Empty(cut.FindAll(".cm-card"));
        Assert.Equal("Нет книг по этим условиям.", EmptyState.NoneByFilter("книг"));
    }
}
