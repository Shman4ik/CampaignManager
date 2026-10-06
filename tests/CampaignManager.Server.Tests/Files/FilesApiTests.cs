using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using CampaignManager.Contracts.Files;
using CampaignManager.Data.Music;
using CampaignManager.Server.Files.Storage;
using Xunit;

namespace CampaignManager.Server.Tests.Files;

/// <summary>
/// Модуль файлов через ApiClient и сырой HTTP (Range, ETag): тесты T1.5. Хранилище — в памяти.
/// </summary>
public sealed class FilesApiTests(FilesApp app) : IClassFixture<FilesApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // Права (T1.4): читать и загружать — вошедший, сироты — админ; загрузивший записывается.
    [Fact]
    public async Task Upload_records_who_uploaded()
    {
        TestDatabase.SkipIfMissing();

        var file = await new ApiClient.Files.FilesApiClient(app.CreateClient(app.Player))
            .UploadAsync(new MemoryStream(RandomBytes(900)), "Улика.jpg", Cancellation);

        await using var db = app.Database.CreateContext();
        Assert.Equal(app.Player.Id, db.Files.Single(f => f.Id == file.Id).UploadedById);
    }

    [Fact]
    public async Task Files_are_closed_to_anonymous()
    {
        TestDatabase.SkipIfMissing();
        var file = await app.Api().UploadAsync(new MemoryStream(RandomBytes(600)), "Тайна.png", Cancellation);
        var anonymous = app.CreateClient(anonymous: true);

        using var content = await anonymous.GetAsync(file.Url, Cancellation);
        using var upload = await anonymous.PostAsync(FilesRoutes.Upload, new MultipartFormDataContent
        {
            { new ByteArrayContent(RandomBytes(10)), FilesRoutes.UploadField, "a.png" },
        }, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, content.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, upload.StatusCode);
    }

    [Fact]
    public async Task Orphans_are_admin_only()
    {
        TestDatabase.SkipIfMissing();

        using var report = await app.CreateClient(app.Player).GetAsync(FilesRoutes.Orphans, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, report.StatusCode);
    }

    [Fact]
    public async Task Uploaded_image_is_served_with_type_and_immutable_cache()
    {
        TestDatabase.SkipIfMissing();
        var bytes = RandomBytes(3000);

        var file = await app.Api().UploadAsync(new MemoryStream(bytes), "Портрет сыщика.PNG", Cancellation);

        Assert.Equal("image/png", file.ContentType);
        Assert.Equal(bytes.Length, file.SizeBytes);
        Assert.Equal("Портрет сыщика.PNG", file.OriginalName);
        Assert.Equal(FilesRoutes.Content(file.Id), file.Url);

        using var response = await app.CreateClient().GetAsync(file.Url, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync(Cancellation));
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("bytes", Assert.Single(response.Headers.AcceptRanges));
        Assert.True(response.Headers.CacheControl?.Extensions.Any(e => e.Name == "immutable"));
        Assert.Equal($"\"{file.Id:N}\"", response.Headers.ETag?.Tag);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task Same_file_uploaded_twice_is_one_row_and_one_object()
    {
        TestDatabase.SkipIfMissing();
        var bytes = RandomBytes(2000);

        var first = await app.Api().UploadAsync(new MemoryStream(bytes), "Тема.mp3", Cancellation);
        var second = await app.Api().UploadAsync(new MemoryStream(bytes), "Тема (копия).mp3", Cancellation);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("audio/mpeg", first.ContentType);
        Assert.Matches(@"^music/[0-9a-f]{64}\.mp3$", KeyOf(bytes));
    }

    [Theory]
    [InlineData("Карта.svg")]
    [InlineData("страница.html")]
    [InlineData("без расширения")]
    public async Task Unsupported_type_is_rejected_with_reason(string fileName)
    {
        TestDatabase.SkipIfMissing();

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            app.Api().UploadAsync(new MemoryStream(RandomBytes(100)), fileName, Cancellation));

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Contains("Можно:", error.Message);
    }

    [Fact]
    public async Task File_over_limit_is_rejected()
    {
        TestDatabase.SkipIfMissing();

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            app.Api().UploadAsync(new MemoryStream(RandomBytes(FilesApp.MaxUploadBytes + 1)), "Большой.jpg", Cancellation));

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
    }

    // Перемотка трека: браузер просит отрезок, сервер отдаёт ровно его и не тянет объект целиком.
    [Theory]
    [InlineData("bytes=100-199", 100, 199)]
    [InlineData("bytes=4000-", 4000, 4095)]
    [InlineData("bytes=0-1", 0, 1)]
    [InlineData("bytes=-10", 4086, 4095)]
    [InlineData("bytes=4090-99999", 4090, 4095)]
    public async Task Range_returns_partial_content(string range, long from, long to)
    {
        TestDatabase.SkipIfMissing();
        var (file, bytes) = await UploadTrackAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, file.Url);
        request.Headers.TryAddWithoutValidation("Range", range);
        using var response = await app.CreateClient().SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal($"bytes {from}-{to}/{bytes.Length}", response.Content.Headers.ContentRange?.ToString());
        Assert.Equal(to - from + 1, response.Content.Headers.ContentLength);
        Assert.Equal(bytes[(int)from..(int)(to + 1)], await response.Content.ReadAsByteArrayAsync(Cancellation));
        Assert.Contains(new ByteRange(from, to), app.Storage.Reads);
    }

    [Fact]
    public async Task Range_past_the_end_is_416_with_size()
    {
        TestDatabase.SkipIfMissing();
        var (file, bytes) = await UploadTrackAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, file.Url);
        request.Headers.Range = new RangeHeaderValue(bytes.Length, null);
        using var response = await app.CreateClient().SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, response.StatusCode);
        Assert.Equal($"bytes */{bytes.Length}", response.Content.Headers.ContentRange?.ToString());
    }

    [Theory]
    [InlineData("bytes=0-1, 5-9")] // несколько отрезков — целиком, это разрешено RFC 9110
    [InlineData("items=0-1")]
    public async Task Unsupported_range_returns_whole_file(string range)
    {
        TestDatabase.SkipIfMissing();
        var (file, bytes) = await UploadTrackAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, file.Url);
        request.Headers.TryAddWithoutValidation("Range", range);
        using var response = await app.CreateClient().SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync(Cancellation));
    }

    [Fact]
    public async Task If_range_with_other_etag_returns_whole_file()
    {
        TestDatabase.SkipIfMissing();
        var (file, bytes) = await UploadTrackAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, file.Url);
        request.Headers.Range = new RangeHeaderValue(0, 9);
        request.Headers.IfRange = new RangeConditionHeaderValue(new EntityTagHeaderValue("\"другой\""));
        using var response = await app.CreateClient().SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(bytes.Length, response.Content.Headers.ContentLength);
    }

    [Fact]
    public async Task Matching_etag_is_304()
    {
        TestDatabase.SkipIfMissing();
        var (file, _) = await UploadTrackAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, file.Url);
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{file.Id:N}\""));
        using var response = await app.CreateClient().SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    [Fact]
    public async Task Head_answers_size_without_body()
    {
        TestDatabase.SkipIfMissing();
        var (file, bytes) = await UploadTrackAsync();

        using var request = new HttpRequestMessage(HttpMethod.Head, file.Url);
        using var response = await app.CreateClient().SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(bytes.Length, response.Content.Headers.ContentLength);
        Assert.Equal("audio/mpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Unknown_file_is_404_problem()
    {
        TestDatabase.SkipIfMissing();

        using var response = await app.CreateClient().GetAsync(FilesRoutes.Content(Guid.NewGuid()), Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // Строка есть, а объект пропал из бакета: 404, и такой ответ браузер не должен запомнить на год.
    [Fact]
    public async Task Missing_object_is_404_without_immutable_cache()
    {
        TestDatabase.SkipIfMissing();
        var bytes = RandomBytes(500);
        var file = await app.Api().UploadAsync(new MemoryStream(bytes), "Пропавший.jpg", Cancellation);
        app.Storage.Remove(KeyOf(bytes));

        using var response = await app.CreateClient().GetAsync(file.Url, Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(response.Headers.CacheControl?.Extensions.Any(e => e.Name == "immutable") ?? false);
    }

    [Fact]
    public async Task External_url_is_a_row_and_redirects()
    {
        TestDatabase.SkipIfMissing();
        const string url = "https://example.org/cthulhu.jpg";

        var file = await app.Api().AddExternalAsync(url, Cancellation);
        var again = await app.Api().AddExternalAsync(url, Cancellation);

        Assert.Equal(file.Id, again.Id);
        Assert.Equal(url, file.ExternalUrl);
        using var response = await app.CreateClient().GetAsync(file.Url, Cancellation);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(url, response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData("http://example.org/a.jpg")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/api/v1/files")]
    public async Task External_url_must_be_https(string url)
    {
        TestDatabase.SkipIfMissing();

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => app.Api().AddExternalAsync(url, Cancellation));

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
    }

    // Сирота — старше суток и ни на кого не ссылается. Удаление — только из отчёта и только если
    // файл всё ещё сирота.
    [Fact]
    public async Task Orphans_are_reported_and_deleted_on_request()
    {
        TestDatabase.SkipIfMissing();
        var api = app.Api();

        app.Time.Offset = -TimeSpan.FromDays(2);
        StoredFileDto orphan, used;
        try
        {
            orphan = await api.UploadAsync(new MemoryStream(RandomBytes(700)), "Забытая.webp", Cancellation);
            used = await api.UploadAsync(new MemoryStream(RandomBytes(800)), "Дождь.mp3", Cancellation);
        }
        finally
        {
            app.Time.Offset = TimeSpan.Zero;
        }

        var fresh = await api.UploadAsync(new MemoryStream(RandomBytes(900)), "Только что.webp", Cancellation);
        await using (var context = app.Database.CreateContext())
        {
            context.MusicTracks.Add(new MusicTrack { Name = $"Дождь {Guid.NewGuid():N}", FileId = used.Id });
            await context.SaveChangesAsync(Cancellation);
        }

        var report = await api.GetOrphansAsync(Cancellation);

        var reported = report.Files.Select(f => f.Id).ToList();
        Assert.Contains(orphan.Id, reported);
        Assert.DoesNotContain(used.Id, reported);
        Assert.DoesNotContain(fresh.Id, reported);
        Assert.True(report.TotalBytes >= 700);

        var orphanKeys = app.Storage.Keys.Count;
        var result = await api.DeleteOrphansAsync([orphan.Id, used.Id, fresh.Id], Cancellation);

        Assert.Equal([orphan.Id], result.Deleted);
        Assert.Equal(new HashSet<Guid> { used.Id, fresh.Id }, result.Skipped.ToHashSet());
        Assert.Empty(result.ObjectsNotDeleted);
        Assert.Equal(orphanKeys - 1, app.Storage.Keys.Count);
        using var gone = await app.CreateClient().GetAsync(orphan.Url, Cancellation);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        using var kept = await app.CreateClient().GetAsync(used.Url, Cancellation);
        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
    }

    private async Task<(StoredFileDto File, byte[] Bytes)> UploadTrackAsync()
    {
        var bytes = RandomBytes(4096);
        return (await app.Api().UploadAsync(new MemoryStream(bytes), "Трек.mp3", Cancellation), bytes);
    }

    /// <summary>Ключ объекта по содержимому; падает, если объекта нет или их несколько.</summary>
    private string KeyOf(byte[] bytes)
    {
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return Assert.Single(app.Storage.Keys, key => key.Contains(sha256, StringComparison.Ordinal));
    }

    // UX-1 U5: ?w=480 — миниатюра плитки (оригинал ~300 КБ, 20 плиток — 6 МБ).
    [Fact]
    public async Task Thumbnail_is_narrower_with_its_own_etag_and_small_images_come_as_is()
    {
        TestDatabase.SkipIfMissing();
        var wide = Png(1000, 800);
        var small = Png(100, 80);
        var wideFile = await app.Api().UploadAsync(new MemoryStream(wide), "Тварь.png", Cancellation);
        var smallFile = await app.Api().UploadAsync(new MemoryStream(small), "Мелочь.png", Cancellation);
        var client = app.CreateClient();

        using var thumb = await client.GetAsync($"{wideFile.Url}?w=200", Cancellation);
        var thumbBytes = await thumb.Content.ReadAsByteArrayAsync(Cancellation);
        using var image = SkiaSharp.SKBitmap.Decode(thumbBytes);
        Assert.Equal(200, image.Width);
        Assert.Equal(160, image.Height);
        Assert.Equal("image/png", thumb.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"\"{wideFile.Id:N}-w200\"", thumb.Headers.ETag?.Tag);

        // Тот же ETag — 304, оригинальный ETag миниатюру не подтверждает.
        using var again = new HttpRequestMessage(HttpMethod.Get, $"{wideFile.Url}?w=200");
        again.Headers.IfNoneMatch.Add(thumb.Headers.ETag!);
        using var notModified = await client.SendAsync(again, Cancellation);
        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);

        // Оригинал не шире просимого — он сам, с ETag оригинала; вне допустимого диапазона ширина игнорируется.
        using var asIs = await client.GetAsync($"{smallFile.Url}?w=480", Cancellation);
        Assert.Equal(small, await asIs.Content.ReadAsByteArrayAsync(Cancellation));
        Assert.Equal($"\"{smallFile.Id:N}\"", asIs.Headers.ETag?.Tag);
        using var tooWide = await client.GetAsync($"{wideFile.Url}?w=100000", Cancellation);
        Assert.Equal(wide, await tooWide.Content.ReadAsByteArrayAsync(Cancellation));

        // Битая картинка (заголовок PNG цел, данных нет) — тоже оригинал, а не 500.
        var broken = wide[..64];
        var brokenFile = await app.Api().UploadAsync(new MemoryStream(broken), "Битая.png", Cancellation);
        using var brokenThumb = await client.GetAsync($"{brokenFile.Url}?w=200", Cancellation);
        Assert.Equal(broken, await brokenThumb.Content.ReadAsByteArrayAsync(Cancellation));
    }

    private static byte[] Png(int width, int height)
    {
        using var image = new SkiaSharp.SKBitmap(width, height);
        image.Erase(SkiaSharp.SKColors.DarkSlateGray);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] RandomBytes(int length)
    {
        var bytes = new byte[length];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }
}
