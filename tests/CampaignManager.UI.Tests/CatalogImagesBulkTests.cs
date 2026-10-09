using System.Net;
using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Platform;
using CampaignManager.UI.Catalogs;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// «Загрузить картинки» — пачка рисунков справочника: файл находит запись по коду без префикса, иначе по названию; запись с
/// картинкой пропускается без «Заменить»; ошибка одной строки не останавливает остальные; PNG уходит на перевод в WebP.
/// </summary>
public sealed class CatalogImagesBulkTests : KitContext
{
    private static readonly string OldCover = $"/api/v1/files/{Guid.NewGuid()}";

    private readonly ItemDto _padlock = new() { Id = Guid.NewGuid(), Code = "item.padlock", Name = "Замок навесной" };
    private readonly ItemDto _umbrella = new() { Id = Guid.NewGuid(), Code = "item.umbrella", Name = "Зонтик" };
    private readonly ItemDto _knife = new() { Id = Guid.NewGuid(), Name = "Карманный нож" };
    private readonly ItemDto _telescope = new() { Id = Guid.NewGuid(), Code = "item.telescope", Name = "Телескоп", ImageUrl = OldCover };
    private readonly FakeItems _api = new();
    private readonly FakeFiles _files = new();

    public CatalogImagesBulkTests()
    {
        Services.AddSingleton<ICatalogApi<ItemDto>>(_api);
        Services.AddSingleton<IFilesApi>(_files);
    }

    private ItemDto[] Items => [_padlock, _umbrella, _knife, _telescope];

    [Fact]
    public void File_finds_record_by_code_then_by_name()
    {
        var matcher = new CatalogImageMatcher<ItemDto>(Items);

        Assert.Equal("item.", matcher.Prefix);
        Assert.Same(_padlock, matcher.Find("padlock.png"));
        Assert.Same(_padlock, matcher.Find("PADLOCK.webp"));
        Assert.Same(_knife, matcher.Find("карманный_нож.png"));
        Assert.Same(_umbrella, matcher.Find("Зонтик.JPG"));
        Assert.Null(matcher.Find("lantern.png"));
        Assert.True(CatalogImageMatcher<ItemDto>.IsImage("a.avif"));
        Assert.False(CatalogImageMatcher<ItemDto>.IsImage("art-prompts.json"));
    }

    [Fact]
    public void Record_with_cover_is_skipped_unless_replace_is_checked()
    {
        var saved = new List<ItemDto>();
        var cut = Open(saved);

        Pick(cut, "padlock.png", "Зонтик.png", "lantern.png", "telescope.png", "notes.txt");

        var summary = cut.Find("[data-testid=covers-summary]").TextContent;
        Assert.Contains("Файлов: 4, записи нашлись у 3, не нашлись у 1. Не картинки (1) пропущены.", summary, StringComparison.Ordinal);
        // Сначала то, что требует внимания: файл без записи, потом запись с картинкой — с её миниатюрой.
        var rows = cut.FindAll("[data-testid=covers-rows] li");
        Assert.Contains("lantern.png: нет записи", rows[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("картинка уже есть — останется", rows[1].TextContent, StringComparison.Ordinal);
        Assert.Equal($"{OldCover}?w=120", rows[1].QuerySelector("img")?.GetAttribute("src"));
        Assert.Contains("Загрузить: 2", cut.Find("[data-testid=covers-start]").TextContent, StringComparison.Ordinal);

        cut.Find("[data-testid=covers-start]").Click();

        cut.WaitForAssertion(() => Assert.StartsWith("Готово: загружено 2, пропущено 2.", cut.Find("[data-testid=covers-summary]").TextContent, StringComparison.Ordinal));
        Assert.Equal(new[] { _padlock.Id, _umbrella.Id }.Order(), _api.Covers.Select(c => c.Id).Order());
        Assert.All(_api.Covers, c => Assert.False(c.Replace));
        Assert.Equal(["padlock.png", "Зонтик.png"], _files.WebpUploads.Order(StringComparer.Ordinal));
        Assert.Equal(2, saved.Count);
    }

    [Fact]
    public void Replace_includes_records_with_cover_and_one_failure_does_not_stop_the_rest()
    {
        _api.FailFor = _padlock.Id;
        var cut = Open([]);
        Pick(cut, "padlock.png", "umbrella.png", "telescope.png");

        cut.Find("input[type=checkbox]").Change(true);
        Assert.Contains("Загрузить: 3", cut.Find("[data-testid=covers-start]").TextContent, StringComparison.Ordinal);
        cut.Find("[data-testid=covers-start]").Click();

        cut.WaitForAssertion(() => Assert.Contains("ошибок 1", cut.Find("[data-testid=covers-summary]").TextContent, StringComparison.Ordinal));
        Assert.Contains((_telescope.Id, true), _api.Covers.Select(c => (c.Id, c.Replace)));
        Assert.Contains(_umbrella.Id, _api.Covers.Select(c => c.Id));
        // Ошибка — первой строкой, с причиной от сервера.
        Assert.Contains("padlock.png: Запись изменили", cut.FindAll("[data-testid=covers-rows] li")[0].TextContent, StringComparison.Ordinal);
    }

    private IRenderedComponent<CatalogImagesBulkModal<ItemDto>> Open(List<ItemDto> saved) =>
        Render<CatalogImagesBulkModal<ItemDto>>(p => p
            .Add(m => m.Open, true)
            .Add(m => m.Items, Items)
            .Add(m => m.CoverOf, i => i.ImageUrl)
            .Add(m => m.OnSaved, item => saved.Add(item)));

    private static void Pick(IRenderedComponent<CatalogImagesBulkModal<ItemDto>> cut, params string[] names) =>
        cut.FindComponents<InputFile>()[0].UploadFiles([.. names.Select(n => InputFileContent.CreateFromBinary([1, 2, 3], n))]);

    private sealed class FakeFiles : IFilesApi
    {
        public List<string> WebpUploads { get; } = [];

        public Task<StoredFileDto> UploadAsync(Stream content, string fileName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Картинки справочника уходят на перевод в WebP.");

        public Task<StoredFileDto> UploadWebpAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
        {
            WebpUploads.Add(fileName);
            var id = Guid.NewGuid();
            return Task.FromResult(new StoredFileDto(id, $"/api/v1/files/{id}", null, "image/webp", 3, fileName, DateTimeOffset.UnixEpoch));
        }

        public Task<StoredFileDto> AddExternalAsync(string url, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OrphanFilesReport> GetOrphansAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DeleteOrphansResponse> DeleteOrphansAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeItems : ICatalogApi<ItemDto>
    {
        public List<(Guid Id, Guid FileId, bool Replace)> Covers { get; } = [];

        public Guid? FailFor { get; set; }

        public CatalogRoute Route => CatalogsRoutes.Items;

        public Task<ItemDto> SetCoverAsync(Guid id, Guid fileId, bool replace, CancellationToken cancellationToken = default)
        {
            if (id == FailFor)
            {
                throw new ApiException("Запись изменили на другом устройстве.", HttpStatusCode.Conflict, ApiProblemCodes.Stale);
            }

            Covers.Add((id, fileId, replace));
            return Task.FromResult(new ItemDto { Id = id, Name = "?", ImageFileId = fileId, ImageUrl = $"/api/v1/files/{fileId}" });
        }

        public Task<CatalogList<ItemDto>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ItemDto> CreateAsync(ItemDto item, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ItemDto> UpdateAsync(ItemDto item, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CatalogImportReport> ImportAsync(Stream file, bool overwrite, bool dryRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CatalogImportReport> SyncAsync(bool dryRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
