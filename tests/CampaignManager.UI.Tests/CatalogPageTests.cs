using System.Net;
using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Platform;
using CampaignManager.Core;
using CampaignManager.UI.Catalogs.Pages;
using CampaignManager.UI.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Общая страница справочника (T2.1) на примере предметов: кнопки правки — только по CanEdit из API,
/// поиск и эпоха — на клиенте, страница — 25 строк, дубль имени — Alert в форме, а не молча закрытое окно.
/// </summary>
public sealed class CatalogPageTests : KitContext
{
    private readonly FakeItems _api = new();

    public CatalogPageTests()
    {
        Services.AddSingleton<ICatalogApi<ItemDto>>(_api);
        Services.AddSingleton<IFilesApi>(new NoFiles());
    }

    [Fact]
    public void Player_sees_no_edit_controls()
    {
        _api.CanEdit = false;
        _api.Items = [Item("Фонарь"), Item("Бинокль")];

        var page = Render<ItemsPage>();

        page.WaitForAssertion(() => Assert.Contains("Фонарь", page.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("Добавить предмет", page.Markup, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("[aria-label^='Изменить:']"));
        Assert.Empty(page.FindAll("[aria-label^='Удалить:']"));
        Assert.Empty(page.FindAll("[aria-label^='Ещё: импорт']"));
    }

    [Fact]
    public void Keeper_sees_add_import_and_row_buttons()
    {
        _api.CanEdit = true;
        _api.Items = [Item("Фонарь")];

        var page = Render<ItemsPage>();

        page.WaitForAssertion(() => Assert.Contains("Добавить предмет", page.Markup, StringComparison.Ordinal));
        Assert.NotEmpty(page.FindAll("[aria-label='Изменить: Фонарь']"));
        // Импорт, экспорт и «С правилами» — в одном меню «⋯», а не рядом кнопок
        page.Find("[aria-label^='Ещё: импорт']").Click();
        Assert.Contains("Импорт", page.Find("[role='menu']").TextContent, StringComparison.Ordinal);
        Assert.Contains("Экспорт", page.Find("[role='menu']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refused_delete_is_shown_in_the_edit_window_not_in_a_toast_under_it()
    {
        _api.CanEdit = true;
        _api.Items = [Item("Фонарь")];
        _api.DeleteFailure = new ApiException("Предмет используется сценариями — сначала уберите ссылки.", System.Net.HttpStatusCode.Conflict, "in-use");
        var dialogs = Services.GetRequiredService<DialogService>();
        var page = Render<ItemsPage>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[aria-label='Изменить: Фонарь']")));

        page.Find("table [aria-label='Изменить: Фонарь']").Click();
        page.FindAll("dialog button").Single(b => b.TextContent.Contains("Удалить предмет", StringComparison.Ordinal)).Click();
        await page.InvokeAsync(() => dialogs.Complete(true));

        page.WaitForAssertion(() => Assert.Contains("сначала уберите ссылки", page.Find("dialog").TextContent, StringComparison.Ordinal));
        Assert.Empty(Services.GetRequiredService<ToastService>().Messages);
    }

    [Fact]
    public void Search_ignores_case_and_yo_and_returns_to_first_page()
    {
        _api.Items = [.. Enumerable.Range(1, 30).Select(i => Item($"Ящик {i:00}")), Item("Чёрный фонарь")];
        var page = Render<ItemsPage>();
        page.WaitForAssertion(() => Assert.Contains("1–25 из 31", page.Markup, StringComparison.Ordinal));

        page.Find("input[type=search]").Input("ЧЕРНЫЙ");

        Assert.Contains("Чёрный фонарь", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Ящик 01", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Era_filter_hides_era_badges_it_fixes()
    {
        _api.Items = [Item("Граммофон", Era.Classic), Item("Смартфон", Era.Modern)];
        var page = Render<ItemsPage>();
        page.WaitForAssertion(() => Assert.Contains("Граммофон", page.Markup, StringComparison.Ordinal));
        Assert.Contains("1920-е", page.Find("table").TextContent, StringComparison.Ordinal);

        page.Find("select[aria-label='Эпоха']").Change(nameof(Era.Classic));

        Assert.DoesNotContain("Смартфон", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("1920-е", page.Find("table").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_name_keeps_form_open_with_server_text()
    {
        _api.CanEdit = true;
        _api.Items = [Item("Фонарь")];
        _api.Failure = new ApiException("Предмет «Фонарь» уже есть в справочнике.", HttpStatusCode.Conflict, ApiProblemCodes.Duplicate);
        var page = Render<ItemsPage>();
        page.WaitForAssertion(() => Assert.Contains("Добавить предмет", page.Markup, StringComparison.Ordinal));

        page.FindAll("button").First(b => b.TextContent.Contains("Добавить предмет", StringComparison.Ordinal)).Click();
        page.Find("dialog input").Change("фонарь");
        page.FindAll("dialog button").First(b => b.TextContent.Contains("Сохранить", StringComparison.Ordinal)).Click();

        page.WaitForAssertion(() => Assert.Contains("уже есть в справочнике", page.Find("dialog").TextContent, StringComparison.Ordinal));
    }

    // ── Общий каркас (UX-0b) ──

    private static ItemDto Described(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = "Длинное описание предмета, которое не помещается в колонку и поэтому раскрывается строкой.",
        Eras = [.. Enum.GetValues<Era>()],
    };

    [Fact]
    public void Open_row_comes_from_the_address_and_toggling_writes_it_back()
    {
        var lamp = Described("Лампа");
        _api.Items = [Described("Бинокль"), lamp];
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/items?open={lamp.Id}");

        var page = Render<ItemsPage>();

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".cm-row-detail")));
        // Свернуть — касанием раскрытой строки: адрес теряет open.
        page.FindAll("tbody tr:not(.cm-row-detail)")[1].Click();
        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".cm-row-detail")));
        Assert.DoesNotContain("open=", navigation.Uri, StringComparison.Ordinal);

        // Раскрыть другую — адрес получает её id.
        page.FindAll("tbody tr:not(.cm-row-detail)")[0].Click();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".cm-row-detail")));
        Assert.Contains("open=", navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_filters_is_one_button_and_inactive_until_there_is_something_to_reset()
    {
        _api.Items = [Item("Фонарь"), Item("Бинокль")];
        var page = Render<ItemsPage>();
        page.WaitForAssertion(() => Assert.Contains("Фонарь", page.Markup, StringComparison.Ordinal));

        var reset = page.FindAll("button").Single(b => b.TextContent.Contains("Сбросить фильтры", StringComparison.Ordinal));
        Assert.NotNull(reset.GetAttribute("disabled"));

        page.Find("input[type=search]").Input("фонарь");
        reset = page.FindAll("button").Single(b => b.TextContent.Contains("Сбросить фильтры", StringComparison.Ordinal));
        Assert.Null(reset.GetAttribute("disabled"));
        Assert.DoesNotContain("Бинокль", page.Markup, StringComparison.Ordinal);

        reset.Click();
        Assert.Contains("Бинокль", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_filter_result_is_one_line_with_the_reset_button()
    {
        _api.Items = [Item("Фонарь")];
        var page = Render<ItemsPage>();
        page.WaitForAssertion(() => Assert.Contains("Фонарь", page.Markup, StringComparison.Ordinal));

        page.Find("input[type=search]").Input("такого нет");

        var line = page.Find(".cm-empty-line");
        Assert.Contains("Нет предметов по этим условиям.", line.TextContent, StringComparison.Ordinal);
        Assert.Contains("Сбросить фильтры", line.TextContent, StringComparison.Ordinal);
        Assert.Empty(page.FindAll(".cm-empty"));
    }

    [Fact]
    public void Empty_catalog_for_the_keeper_is_one_line_because_the_header_has_the_add_button()
    {
        _api.CanEdit = true;
        _api.Items = [];

        var page = Render<ItemsPage>();

        page.WaitForAssertion(() => Assert.Equal("Нет предметов.", page.Find(".cm-empty-line").TextContent.Trim()));
    }

    [Fact]
    public void Era_filter_is_absent_while_every_record_is_from_one_era()
    {
        _api.Items = [Item("Фонарь", Era.Classic), Item("Бинокль", Era.Classic)];

        var page = Render<ItemsPage>();

        page.WaitForAssertion(() => Assert.Contains("Фонарь", page.Markup, StringComparison.Ordinal));
        Assert.Empty(page.FindAll("select[aria-label='Эпоха']"));
        Assert.DoesNotContain("1920-е", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Row_has_a_single_edit_icon_and_delete_lives_in_the_edit_window()
    {
        _api.CanEdit = true;
        _api.Items = [Item("Фонарь")];
        var dialogs = Services.GetRequiredService<DialogService>();
        var page = Render<ItemsPage>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[aria-label='Изменить: Фонарь']")));

        Assert.Empty(page.FindAll("[aria-label^='Удалить']"));
        Assert.Empty(page.FindAll("dialog"));

        page.Find("table [aria-label='Изменить: Фонарь']").Click();
        var delete = page.FindAll("dialog button").Single(b => b.TextContent.Contains("Удалить предмет", StringComparison.Ordinal));
        delete.Click();

        Assert.Equal("Удалить предмет?", dialogs.Current!.Title);
        Assert.DoesNotContain("«", dialogs.Current.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pagination_sits_under_the_card_not_in_it()
    {
        _api.Items = [.. Enumerable.Range(1, 30).Select(i => Item($"Ящик {i:00}"))];

        var page = Render<ItemsPage>();

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("nav.cm-pagination")));
        Assert.Empty(page.FindAll(".cm-card nav.cm-pagination"));
        Assert.Empty(page.FindAll(".cm-card-footer"));
    }

    [Fact]
    public async Task Closing_a_changed_form_asks_once_and_a_clean_form_closes_silently()
    {
        _api.CanEdit = true;
        _api.Items = [Item("Фонарь")];
        var dialogs = Services.GetRequiredService<DialogService>();
        var page = Render<ItemsPage>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[aria-label='Изменить: Фонарь']")));

        // Не тронули — закрывается без вопроса.
        page.Find("table [aria-label='Изменить: Фонарь']").Click();
        page.FindAll("dialog button").First(b => b.TextContent.Contains("Отмена", StringComparison.Ordinal)).Click();
        Assert.Null(dialogs.Current);
        Assert.Empty(page.FindAll("dialog"));

        // Тронули — спрашивает; «Остаться» оставляет окно.
        page.Find("table [aria-label='Изменить: Фонарь']").Click();
        page.Find("dialog input").Change("Фонарь-2");
        page.FindAll("dialog button").First(b => b.TextContent.Contains("Отмена", StringComparison.Ordinal)).Click();
        Assert.Equal("Есть несохранённые изменения. Выйти?", dialogs.Current!.Title);
        await page.InvokeAsync(() => dialogs.Complete(false));
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);
        Assert.NotEmpty(page.FindAll("dialog"));
    }

    // UX-1 U5 (#174): ошибка у поля и сводка у кнопки — а не алерт вверху окна, до которого не долистать.
    [Fact]
    public void Saving_an_empty_form_marks_the_field_and_counts_errors_by_the_button()
    {
        _api.CanEdit = true;
        _api.Items = [Item("Фонарь")];
        var page = Render<ItemsPage>();
        page.WaitForAssertion(() => Assert.Contains("Добавить предмет", page.Markup, StringComparison.Ordinal));

        page.FindAll("button").First(b => b.TextContent.Contains("Добавить предмет", StringComparison.Ordinal)).Click();
        page.FindAll("dialog button").First(b => b.TextContent.Contains("Сохранить", StringComparison.Ordinal)).Click();

        Assert.Equal("Нужно название.", page.Find("dialog .cm-field-error").TextContent);
        Assert.Equal("true", page.Find("dialog input[aria-invalid]").GetAttribute("aria-invalid"));
        Assert.Contains("Проверьте поля: 1", page.Find("dialog .cm-form-errors").TextContent, StringComparison.Ordinal);
        Assert.Empty(_api.Created);

        // Исправили — ошибка гаснет сама, не дожидаясь второго нажатия.
        page.Find("dialog input").Change("Бинокль");
        Assert.Empty(page.FindAll("dialog .cm-field-error"));
    }

    [Fact]
    public void Form_asks_for_the_era_only_when_the_catalog_has_more_than_one()
    {
        _api.CanEdit = true;
        _api.Items = [Item("Фонарь", Era.Classic), Item("Бинокль", Era.Classic)];
        var page = Render<ItemsPage>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[aria-label='Изменить: Фонарь']")));
        page.Find("table [aria-label='Изменить: Фонарь']").Click();
        Assert.DoesNotContain("Наши дни", page.Find("dialog").TextContent, StringComparison.Ordinal);

    }

    [Fact]
    public void Form_asks_for_the_era_when_the_catalog_has_both()
    {
        _api.CanEdit = true;
        _api.Items = [Item("Фонарь", Era.Classic), Item("Лазер", Era.Modern)];
        var varied = Render<ItemsPage>();
        varied.WaitForAssertion(() => Assert.NotEmpty(varied.FindAll("[aria-label='Изменить: Фонарь']")));
        varied.Find("table [aria-label='Изменить: Фонарь']").Click();
        Assert.Contains("Наши дни", varied.Find("dialog").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Item_price_has_its_own_column_and_a_form_field()
    {
        _api.CanEdit = true;
        _api.Items = [Priced(Item("Фонарь", Era.Classic), 1250m), Item("Бинокль", Era.Classic)];

        var page = Render<ItemsPage>();

        page.WaitForAssertion(() => Assert.Contains("Фонарь", page.Markup, StringComparison.Ordinal));
        Assert.Contains("Цена", page.Find("table thead").TextContent, StringComparison.Ordinal);
        Assert.Contains("$1 250", page.Find("table tbody").TextContent, StringComparison.Ordinal);
        page.Find("table [aria-label='Изменить: Фонарь']").Click();
        Assert.Equal("1250", page.Find("dialog input[type=number]").GetAttribute("value"));
    }

    [Fact]
    public void Thumbnails_ask_the_server_for_a_narrow_copy_of_our_own_files_only()
    {
        Assert.Equal("/api/v1/files/abc?w=480", Catalogs.ImageUrls.Thumb("/api/v1/files/abc"));
        Assert.Equal("/api/v1/files/abc?w=160", Catalogs.ImageUrls.Thumb("/api/v1/files/abc", 160));
        Assert.Equal("https://example.test/a.png", Catalogs.ImageUrls.Thumb("https://example.test/a.png"));
        Assert.Null(Catalogs.ImageUrls.Thumb(null));
    }

    private static ItemDto Item(string name, params Era[] eras) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Eras = eras.Length > 0 ? [.. eras] : [.. Enum.GetValues<Era>()],
    };

    private static ItemDto Priced(ItemDto item, decimal price)
    {
        item.Price = price;
        return item;
    }

    private sealed class NoFiles : IFilesApi
    {
        public Task<StoredFileDto> UploadAsync(Stream content, string fileName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StoredFileDto> AddExternalAsync(string url, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OrphanFilesReport> GetOrphansAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DeleteOrphansResponse> DeleteOrphansAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeItems : ICatalogApi<ItemDto>
    {
        public List<ItemDto> Items { get; set; } = [];

        public bool CanEdit { get; set; }

        public ApiException? Failure { get; set; }

        public CatalogRoute Route => CatalogsRoutes.Items;

        public Task<CatalogList<ItemDto>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CatalogList<ItemDto>(Items, CanEdit));

        public List<ItemDto> Created { get; } = [];

        public Task<ItemDto> CreateAsync(ItemDto item, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Created.Add(item);
            return Task.FromResult(item);
        }

        public Task<ItemDto> UpdateAsync(ItemDto item, CancellationToken cancellationToken = default) =>
            Failure is not null ? throw Failure : Task.FromResult(item);

        public ApiException? DeleteFailure { get; set; }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            DeleteFailure is not null ? throw DeleteFailure : Task.CompletedTask;

        public Task<CatalogImportReport> ImportAsync(Stream file, bool overwrite, bool dryRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CatalogImportReport> SyncAsync(bool dryRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
