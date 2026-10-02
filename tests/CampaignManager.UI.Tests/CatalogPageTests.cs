using System.Net;
using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Platform;
using CampaignManager.Core;
using CampaignManager.UI.Catalogs.Pages;
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
        Assert.DoesNotContain("Импорт", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeper_sees_add_import_and_row_buttons()
    {
        _api.CanEdit = true;
        _api.Items = [Item("Фонарь")];

        var page = Render<ItemsPage>();

        page.WaitForAssertion(() => Assert.Contains("Добавить предмет", page.Markup, StringComparison.Ordinal));
        Assert.NotEmpty(page.FindAll("[aria-label='Изменить: Фонарь']"));
        Assert.Contains("Импорт", page.Markup, StringComparison.Ordinal);
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

    private static ItemDto Item(string name, params Era[] eras) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Eras = eras.Length > 0 ? [.. eras] : [.. Enum.GetValues<Era>()],
    };

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

        public Task<ItemDto> CreateAsync(ItemDto item, CancellationToken cancellationToken = default) =>
            Failure is not null ? throw Failure : Task.FromResult(item);

        public Task<ItemDto> UpdateAsync(ItemDto item, CancellationToken cancellationToken = default) =>
            Failure is not null ? throw Failure : Task.FromResult(item);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<CatalogImportReport> ImportAsync(Stream file, bool overwrite, bool dryRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CatalogImportReport> SyncAsync(bool dryRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
