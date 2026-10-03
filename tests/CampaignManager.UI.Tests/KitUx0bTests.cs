using Bunit;
using CampaignManager.Contracts.Music;
using CampaignManager.UI.Music;
using CampaignManager.UI.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Компоненты кита волны UX-0b: меню строки, ссылки на книгу, сокращения, выбор файла, заголовок секции, ошибки
/// формы, защита от ухода, липкая колонка, кости, имена, вкладки, страницы, таблица и полоса плеера. Вёрстку
/// (размеры, липкость, позиция меню) bUnit не проверит — её смотрят в браузере на /dev/ui.
/// </summary>
public sealed class KitUx0bTests : KitContext
{
    // ── RowMenu ──

    private IRenderedComponent<RowMenu> RenderMenu(List<string> log, bool confirm = false) => Render<RowMenu>(p => p
        .Add(m => m.AriaLabel, "Действия: Кольт")
        .Add(m => m.ChildContent, b =>
        {
            b.OpenComponent<RowMenuItem>(0);
            b.AddAttribute(1, nameof(RowMenuItem.OnClick), EventCallback.Factory.Create(this, () => log.Add("edit")));
            b.AddAttribute(2, nameof(RowMenuItem.ChildContent), (RenderFragment)(c => c.AddContent(0, "Изменить")));
            b.CloseComponent();
        })
        .Add(m => m.Danger, b =>
        {
            b.OpenComponent<RowMenuItem>(0);
            b.AddAttribute(1, nameof(RowMenuItem.Danger), true);
            b.AddAttribute(2, nameof(RowMenuItem.OnClick), EventCallback.Factory.Create(this, () => log.Add("delete")));
            b.AddAttribute(3, nameof(RowMenuItem.ChildContent), (RenderFragment)(c => c.AddContent(0, "Удалить")));
            if (confirm)
            {
                b.AddAttribute(4, nameof(RowMenuItem.ConfirmTitle), "Удалить оружие?");
                b.AddAttribute(5, nameof(RowMenuItem.ConfirmMessage), "Кольт исчезнет.");
                b.AddAttribute(6, nameof(RowMenuItem.ConfirmText), "Удалить оружие");
            }

            b.CloseComponent();
        }));

    [Fact]
    public void Row_menu_is_closed_until_touched_and_the_button_is_named()
    {
        var cut = RenderMenu([]);

        Assert.Empty(cut.FindAll("[role=menu]"));
        var button = cut.Find("button[aria-haspopup=menu]");
        Assert.Equal("Действия: Кольт", button.GetAttribute("aria-label"));
        Assert.Equal("false", button.GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Row_menu_puts_dangerous_items_last_under_a_rule()
    {
        var cut = RenderMenu([]);

        cut.Find("button[aria-haspopup=menu]").Click();

        var children = cut.Find("[role=menu]").Children.Select(c => c.GetAttribute("role")).ToArray();
        Assert.Equal(["menuitem", "separator", "menuitem"], children);
        Assert.Contains("cm-menu-item-danger", cut.FindAll("[role=menuitem]")[1].ClassName, StringComparison.Ordinal);
    }

    [Fact]
    public void Row_menu_item_runs_its_action_and_closes_the_menu()
    {
        var log = new List<string>();
        var cut = RenderMenu(log);
        cut.Find("button[aria-haspopup=menu]").Click();

        cut.FindAll("[role=menuitem]")[0].Click();

        Assert.Equal(["edit"], log);
        Assert.Empty(cut.FindAll("[role=menu]"));
    }

    [Fact]
    public void Row_menu_closes_on_escape_and_on_touch_outside()
    {
        var cut = RenderMenu([]);
        cut.Find("button[aria-haspopup=menu]").Click();
        cut.Find(".cm-menu").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Empty(cut.FindAll("[role=menu]"));

        cut.Find("button[aria-haspopup=menu]").Click();
        cut.Find(".cm-pop-backdrop").Click();
        Assert.Empty(cut.FindAll("[role=menu]"));
    }

    [Fact]
    public async Task Row_menu_confirms_a_dangerous_item_before_running_it()
    {
        var log = new List<string>();
        var dialogs = Services.GetRequiredService<DialogService>();
        var cut = RenderMenu(log, confirm: true);

        cut.Find("button[aria-haspopup=menu]").Click();
        cut.FindAll("[role=menuitem]")[1].Click();

        Assert.NotNull(dialogs.Current);
        Assert.Equal("Удалить оружие?", dialogs.Current!.Title);
        Assert.Equal("Удалить оружие", dialogs.Current.ConfirmText);
        Assert.Empty(log);

        // Ответ окна приходит в потоке рендерера, как в браузере: на пуле он гонялся бы с следующим касанием.
        await cut.InvokeAsync(() => dialogs.Complete(false));
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);
        Assert.Empty(log);

        var before = cut.Find("button[aria-haspopup=menu]").GetAttribute("aria-expanded");
        cut.Find("button[aria-haspopup=menu]").Click();
        Assert.True(cut.FindAll("[role=menuitem]").Count == 2, $"before={before}; " + cut.Markup);
        cut.FindAll("[role=menuitem]")[1].Click();
        await cut.InvokeAsync(() => dialogs.Complete(true));
        cut.WaitForAssertion(() => Assert.Equal(["delete"], log));
    }

    [Fact]
    public void Row_menu_can_be_opened_again_after_an_item_closed_it()
    {
        var log = new List<string>();
        var cut = RenderMenu(log);

        cut.Find("button[aria-haspopup=menu]").Click();
        cut.FindAll("[role=menuitem]")[0].Click();
        Assert.Empty(cut.FindAll("[role=menu]"));
        cut.Find("button[aria-haspopup=menu]").Click();

        Assert.Single(cut.FindAll("[role=menu]"));
    }

    // ── BookRef и Abbr ──

    [Fact]
    public void Book_ref_shows_the_page_and_opens_the_book_by_touch()
    {
        var cut = Render<BookRef>(p => p.Add(b => b.Page, "277–280"));

        Assert.Contains("стр. 277–280", cut.Find("button").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[role=dialog]"));

        cut.Find("button.cm-bookref").Click();
        Assert.Contains("Книга правил", cut.Find("[role=dialog]").TextContent, StringComparison.Ordinal);
        Assert.Equal("true", cut.Find("button.cm-bookref").GetAttribute("aria-expanded"));

        cut.Find("button.cm-bookref").Click();
        Assert.Empty(cut.FindAll("[role=dialog]"));
    }

    [Fact]
    public void Abbr_spells_out_by_touch_not_by_title()
    {
        var cut = Render<Abbr>(p => p.Add(a => a.Short, "МКН").Add(a => a.Full, "Мифы: начальное чтение"));

        Assert.Equal("МКН", cut.Find("abbr").TextContent);
        Assert.Null(cut.Find("abbr").GetAttribute("title"));
        Assert.Contains("МКН — Мифы: начальное чтение", cut.Find("button").GetAttribute("aria-label"), StringComparison.Ordinal);

        cut.Find("button").Click();

        Assert.Contains("Мифы: начальное чтение", cut.Find("[role=dialog]").TextContent, StringComparison.Ordinal);
    }

    // ── FilePicker ──

    [Fact]
    public void File_picker_speaks_russian_and_reports_the_chosen_name()
    {
        InputFileChangeEventArgs? received = null;
        var cut = Render<FilePicker>(p => p
            .Add(f => f.Accept, ".json")
            .Add(f => f.OnChange, e => received = e));

        Assert.Contains("Выбрать файл", cut.Find("label").TextContent, StringComparison.Ordinal);
        Assert.Contains("Файл не выбран", cut.Markup, StringComparison.Ordinal);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("{}", "export.json"));

        cut.WaitForAssertion(() => Assert.Contains("export.json", cut.Find(".cm-file-picker-name").TextContent, StringComparison.Ordinal));
        Assert.NotNull(received);
    }

    [Fact]
    public void File_picker_gives_the_reason_when_it_cannot_pick()
    {
        var cut = Render<FilePicker>(p => p
            .Add(f => f.Disabled, true)
            .Add(f => f.DisabledReason, "Сначала введите название."));

        Assert.Equal("Сначала введите название.", cut.Find(".cm-file-picker-name").TextContent);
        Assert.NotNull(cut.Find("input[type=file]").GetAttribute("disabled"));
        Assert.Equal(cut.Find(".cm-file-picker-name").Id, cut.Find("input[type=file]").GetAttribute("aria-describedby"));
    }

    // ── SectionHeader ──

    [Fact]
    public void Section_header_does_not_draw_a_zero_section()
    {
        var empty = Render<SectionHeader>(p => p.Add(h => h.Title, "Игроки").Add(h => h.Count, 0));
        Assert.Empty(empty.Markup.Trim());

        var withAction = Render<SectionHeader>(p => p
            .Add(h => h.Title, "Локации")
            .Add(h => h.Count, 0)
            .Add(h => h.Actions, b => b.AddContent(0, "Добавить локацию")));
        Assert.Equal("Локации", withAction.Find(".cm-card-title").TextContent);
        Assert.Empty(withAction.FindAll(".cm-card-count"));
        Assert.Contains("Добавить локацию", withAction.Markup, StringComparison.Ordinal);

        var counted = Render<SectionHeader>(p => p.Add(h => h.Title, "Игроки").Add(h => h.Count, 3));
        Assert.Equal("3", counted.Find(".cm-card-count").TextContent);
    }

    // ── FormErrors, UnsavedChangesGuard ──

    [Fact]
    public void Form_errors_summarise_all_fields_at_once_and_list_them_on_demand()
    {
        var none = Render<FormErrors>();
        Assert.Empty(none.Markup.Trim());

        var cut = Render<FormErrors>(p => p.Add(f => f.Errors, ["Введите название.", "Укажите тип."]));
        Assert.Contains("Проверьте поля: 2", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("li"));

        cut.Render(p => p.Add(f => f.Errors, ["Введите название.", "Укажите тип."]).Add(f => f.ShowList, true));
        Assert.Equal(2, cut.FindAll("li").Count);
    }

    [Fact]
    public void Form_errors_scroll_to_the_first_invalid_field_after_each_failed_attempt()
    {
        var module = JSInterop.SetupModule("./_content/CampaignManager.UI/Shared/ValidationScroll.razor.js");
        module.Mode = JSRuntimeMode.Loose;

        var cut = Render<FormErrors>(p => p.Add(f => f.Errors, ["Введите название."]).Add(f => f.Attempt, 1));
        cut.WaitForAssertion(() => module.VerifyInvoke("scrollToFirstInvalid", 1));

        cut.Render(p => p.Add(f => f.Errors, ["Введите название."]).Add(f => f.Attempt, 2));
        cut.WaitForAssertion(() => module.VerifyInvoke("scrollToFirstInvalid", 2));
    }

    [Fact]
    public async Task Unsaved_changes_guard_asks_once_and_only_when_there_is_something_to_lose()
    {
        var dialogs = Services.GetRequiredService<DialogService>();
        var clean = Render<UnsavedChangesGuard>(p => p.Add(g => g.Dirty, false));
        Assert.True(await clean.Instance.ConfirmLeaveAsync());
        Assert.Null(dialogs.Current);

        var dirty = Render<UnsavedChangesGuard>(p => p.Add(g => g.Dirty, true));
        var answer = dirty.Instance.ConfirmLeaveAsync();
        Assert.Equal("Есть несохранённые изменения. Выйти?", dialogs.Current!.Title);
        dialogs.Complete(false);
        Assert.False(await answer);

        answer = dirty.Instance.ConfirmLeaveAsync();
        dialogs.Complete(true);
        Assert.True(await answer);
    }

    // ── StickyPane, DiceText, Names, Tabs, Pagination ──

    [Fact]
    public void Sticky_pane_carries_the_sticky_class_and_merges_its_own()
    {
        var cut = Render<StickyPane>(p => p.AddUnmatched("class", "w-full").AddChildContent("<b>Действие</b>"));

        Assert.Equal("cm-sticky-pane w-full", cut.Find("div").ClassName);
    }

    [Theory]
    [InlineData("1D6", "1d6")]
    [InlineData("1d6+2", "1d6 + 2")]
    [InlineData("3D6*5", "3d6 × 5")]
    [InlineData("1D6+БкУ", "1d6 + бонус к урону")]
    [InlineData(null, "—")]
    [InlineData("  ", "—")]
    public void Dice_text_is_the_single_place_dice_are_written(string? value, string expected)
    {
        var cut = Render<DiceText>(p => p.Add(d => d.Value, value));

        Assert.Equal(expected, cut.Find("span").TextContent);
    }

    [Theory]
    [InlineData("Кольт", "Кольт")]
    [InlineData("«Кольт»", "Кольт")]
    [InlineData("«««Кольт»»»", "Кольт")]
    [InlineData("\"Кольт\"", "Кольт")]
    [InlineData("Кольт «Миротворец»", "Кольт «Миротворец»")]
    [InlineData("«Кольт» «Миротворец»", "«Кольт» «Миротворец»")]
    [InlineData("«Кольт «Миротворец»»", "Кольт «Миротворец»")]
    [InlineData("  ", "")]
    [InlineData(null, "")]
    public void Names_drop_only_the_quotes_that_wrap_the_whole_name(string? name, string expected) =>
        Assert.Equal(expected, Names.Clean(name));

    [Fact]
    public void Names_make_messages_without_declension()
    {
        Assert.Equal("Цель: Артур Нельсон — промах", Names.Labelled("Цель", "Артур Нельсон", "промах"));
        Assert.Equal("Цель: Артур Нельсон", Names.Labelled("Цель", "«Артур Нельсон»"));

        var cut = Render<Microsoft.AspNetCore.Components.DynamicComponent>(p => p
            .Add(c => c.Type, typeof(NameStrong)));
        Assert.Equal("Кольт", cut.Find("strong").TextContent);
    }

    private sealed class NameStrong : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) =>
            builder.AddContent(0, Names.Strong("«Кольт»"));
    }

    [Fact]
    public void Tabs_can_stick_under_the_header()
    {
        IReadOnlyList<Tabs.Item> items = [new("a", "Описание"), new("b", "Факты", Count: 0)];
        var plain = Render<Tabs>(p => p.Add(t => t.Items, items).Add(t => t.ActiveKey, "a"));
        var sticky = Render<Tabs>(p => p.Add(t => t.Items, items).Add(t => t.ActiveKey, "a").Add(t => t.Sticky, true));

        Assert.DoesNotContain("cm-tabs-sticky", plain.Find("[role=tablist]").ClassName, StringComparison.Ordinal);
        Assert.Contains("cm-tabs-sticky", sticky.Find("[role=tablist]").ClassName, StringComparison.Ordinal);
        Assert.Empty(sticky.FindAll(".cm-tab-count"));
    }

    [Fact]
    public void Pagination_is_its_own_strip_not_a_card_footer_and_never_for_one_page()
    {
        var one = Render<Pagination>(p => p.Add(x => x.TotalItems, 10).Add(x => x.PageSize, 20));
        Assert.Empty(one.Markup.Trim());

        var cut = Render<Pagination>(p => p.Add(x => x.TotalItems, 95).Add(x => x.PageSize, 20));
        Assert.Equal("nav", cut.Find(".cm-pagination").TagName.ToLowerInvariant());
        Assert.Empty(cut.FindAll(".cm-card-footer"));
        Assert.Empty(cut.FindAll(".cm-btn-sm"));
        Assert.Contains("1–20 из 95", cut.Markup, StringComparison.Ordinal);
    }

    // ── DataTable ──

    private sealed record Row(Guid Id, string Name, int Range);

    private static readonly Row[] Rows = [new(Guid.NewGuid(), "Нож", 0), new(Guid.NewGuid(), "Винтовка", 110), new(Guid.NewGuid(), "Кольт", 15)];

    private static readonly RenderFragment Columns = b =>
    {
        b.OpenComponent<DataColumn<Row>>(0);
        b.AddAttribute(1, nameof(DataColumn<Row>.Title), "Название");
        b.AddAttribute(2, nameof(DataColumn<Row>.SortBy), (Func<Row, object?>)(r => r.Name));
        b.AddAttribute(3, nameof(DataColumn<Row>.ChildContent), (RenderFragment<Row>)(r => c => c.AddContent(0, r.Name)));
        b.CloseComponent();
        b.OpenComponent<DataColumn<Row>>(4);
        b.AddAttribute(5, nameof(DataColumn<Row>.Title), "Дальность");
        b.AddAttribute(6, nameof(DataColumn<Row>.SortBy), (Func<Row, object?>)(r => r.Range));
        b.AddAttribute(7, nameof(DataColumn<Row>.ChildContent), (RenderFragment<Row>)(r => c => c.AddContent(0, r.Range.ToString())));
        b.CloseComponent();
    };

    [Fact]
    public void Data_table_with_controlled_open_key_reports_instead_of_toggling_itself()
    {
        object? reported = "unset";
        var cut = Render<DataTable<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.Columns, Columns)
            .Add(t => t.RowKey, r => r.Id)
            .Add(t => t.RowDetail, r => b => b.AddContent(0, $"Подробно: {r.Name}"))
            .Add(t => t.OpenKey, Rows[1].Id)
            .Add(t => t.OpenKeyChanged, key => reported = key));

        // Раскрыта ровно одна строка — та, что в адресе.
        Assert.Single(cut.FindAll(".cm-row-detail"));
        Assert.Contains("Подробно: Винтовка", cut.Find(".cm-row-detail").TextContent, StringComparison.Ordinal);

        // Касание той же строки просит свернуть (null), другой — раскрыть её.
        cut.FindAll("tbody tr:not(.cm-row-detail)")[1].Click();
        Assert.Null(reported);
        cut.FindAll("tbody tr:not(.cm-row-detail)")[0].Click();
        Assert.Equal(Rows[0].Id, reported);
        Assert.Single(cut.FindAll(".cm-row-detail"));
    }

    [Fact]
    public void Data_table_keeps_sorting_reachable_on_a_narrow_screen()
    {
        var cut = Render<DataTable<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.Columns, Columns)
            .Add(t => t.CardTemplate, r => b => b.AddContent(0, r.Name)));

        var select = cut.Find(".cm-sortbar select");
        Assert.Equal(2, select.QuerySelectorAll("option").Length);

        select.Change("1");
        var cards = cut.FindAll(".md\\:hidden > div.border-t").Select(c => c.TextContent.Trim()).ToArray();
        Assert.Equal(["Нож", "Кольт", "Винтовка"], cards);

        cut.Find(".cm-sortbar button").Click();
        cards = [.. cut.FindAll(".md\\:hidden > div.border-t").Select(c => c.TextContent.Trim())];
        Assert.Equal(["Винтовка", "Кольт", "Нож"], cards);
    }

    // ── StatblockView ──

    [Fact]
    public void Statblock_view_draws_nothing_for_an_empty_statblock_and_uses_dictionary_labels_otherwise()
    {
        var blank = Render<CampaignManager.UI.Catalogs.StatblockView>(p => p.Add(v => v.Statblock, new CampaignManager.Core.Catalogs.Statblock()));
        Assert.Empty(blank.Markup.Trim());

        var filled = Render<CampaignManager.UI.Catalogs.StatblockView>(p => p.Add(v => v.Statblock, new CampaignManager.Core.Catalogs.Statblock { HitPoints = 12, AttacksPerRound = 2 }));
        var text = filled.Markup;
        Assert.Contains("Атаки за раунд", text, StringComparison.Ordinal);
        Assert.Contains("Бонус к урону", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ср. бонус", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ср. комплекция", text, StringComparison.Ordinal);
    }

    // ── PlayerStrip ──

    private static MusicTrackDto Track(string name) => new() { Id = Guid.NewGuid(), Name = name, YoutubeId = "dQw4w9WgXcQ", Tags = ["бой"] };

    [Fact]
    public void Player_strip_at_rest_has_no_dead_buttons()
    {
        var cut = Render<PlayerStrip>();

        Assert.Contains("Выберите настроение", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Пауза", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Остановить", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("button[disabled]"));
        Assert.Contains("Настроения", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Player_strip_playing_has_two_labelled_main_buttons_and_the_rest_in_the_menu()
    {
        var player = Services.GetRequiredService<MusicPlayer>();
        player.SetLibrary([Track("Погоня"), Track("Бой")]);
        player.PlayTag("бой");
        var cut = Render<PlayerStrip>();

        var labelled = cut.FindAll(".cm-music-controls > button.cm-btn").Select(b => b.TextContent.Trim()).Where(t => t.Length > 0).ToArray();
        Assert.Equal(["Пауза", "Другой трек"], labelled);
        Assert.DoesNotContain("Остановить", cut.Markup, StringComparison.Ordinal);

        cut.Find("button[aria-haspopup=menu]").Click();
        Assert.Contains("Остановить", cut.Find("[role=menu]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Player_strip_collapses_after_stop()
    {
        var player = Services.GetRequiredService<MusicPlayer>();
        player.SetLibrary([Track("Бой")]);
        player.PlayTag("бой");
        bool? expanded = true;
        var cut = Render<PlayerStrip>(p => p
            .Add(s => s.Expanded, true)
            .Add(s => s.ExpandedChanged, value => expanded = value));

        cut.Find("button[aria-haspopup=menu]").Click();
        cut.FindAll("[role=menuitem]").Single(i => i.TextContent.Contains("Остановить", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.False(expanded));
        Assert.Null(player.Current);
    }
}
