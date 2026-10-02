using System.Net;
using System.Text;
using System.Text.Json;
using CampaignManager.ApiClient.Music;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Music;
using CampaignManager.Contracts.Platform;
using CampaignManager.Contracts.Profile;
using CampaignManager.Core.Identity;
using CampaignManager.Core.Music;
using CampaignManager.Data;
using CampaignManager.Data.Files;
using CampaignManager.Data.Identity;
using CampaignManager.Server.Tests.Schema;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampaignManager.Server.Tests.Music;

/// <summary>Сервер со своей базой, Хранителем, игроком и тремя файлами: звук, картинка и внешний адрес.</summary>
public sealed class MusicApp : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;

    public SchemaDatabase Database { get; } = new();

    public User Keeper { get; } = new() { Email = $"keeper-{Guid.NewGuid():N}@example.test", DisplayName = "Хранитель", Role = UserRole.Keeper };

    public User Player { get; } = new() { Email = $"player-{Guid.NewGuid():N}@example.test", DisplayName = "Игрок" };

    /// <summary>Хранитель, перенесённый из v1: его настройка — строка через запятую.</summary>
    public User Veteran { get; } = new() { Email = $"veteran-{Guid.NewGuid():N}@example.test", DisplayName = "Ветеран", Role = UserRole.Keeper };

    public StoredFile Audio { get; } = new() { StorageKey = $"music/{Guid.NewGuid():N}.mp3", ContentType = "audio/mpeg", OriginalName = "Гроза.mp3", SizeBytes = 10 };

    public StoredFile Image { get; } = new() { StorageKey = $"images/{Guid.NewGuid():N}.png", ContentType = "image/png", SizeBytes = 10 };

    public StoredFile External { get; } = new() { ExternalUrl = "https://example.test/track.mp3", ContentType = "audio/mpeg" };

    public async ValueTask InitializeAsync()
    {
        await Database.InitializeAsync();
        if (TestDatabase.ConnectionString is null)
        {
            return;
        }

        await using (var db = Database.CreateContext())
        {
            db.Users.AddRange(Keeper, Player, Veteran);
            db.Files.AddRange(Audio, Image, External);
            await db.SaveChangesAsync();
        }

        _factory = new CmApp().WithWebHostBuilder(builder =>
            builder.UseSetting($"ConnectionStrings:{CmDatabase.ConnectionStringName}", Database.ConnectionString));
    }

    public HttpClient CreateClient(User? user = null, bool anonymous = false)
    {
        var client = (_factory ?? throw new InvalidOperationException(
            "Сервер не поднят: тест должен начинаться с TestDatabase.SkipIfMissing().")).CreateClient();
        return anonymous ? client : client.As((user ?? Keeper).Id);
    }

    public MusicTracksApiClient Tracks(User? user = null) => new(CreateClient(user));

    public MusicApiClient Music(User? user = null) => new(CreateClient(user));

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await Database.DisposeAsync();
    }
}

/// <summary>
/// Фонотека (T2.8): треки — справочник на общем сервисе (права, дубль, версия, импорт, журнал), источник —
/// ровно одно из двух и только звук со своего origin, теги нормализуются; закреплённые настроения — своя
/// настройка в обеих формах значения; пул сцены — правило Core.
/// </summary>
public sealed class MusicApiTests(MusicApp app) : IClassFixture<MusicApp>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static string Unique(string name) => $"{name} {Guid.NewGuid():N}"[..(name.Length + 9)];

    private static MusicTrackDto Video(string name, string link = "https://youtu.be/dQw4w9WgXcQ", params string[] tags) =>
        new() { Name = name, YoutubeId = link, Tags = [.. tags] };

    [Fact]
    public async Task Keeper_adds_youtube_track_by_link_and_tags_are_normalized()
    {
        TestDatabase.SkipIfMissing();

        var track = await app.Tracks().CreateAsync(
            Video(Unique("Дождь"), "https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=5", "Тёмная улица", " БОЙ", "бой"), Cancellation);

        Assert.Equal("dQw4w9WgXcQ", track.YoutubeId);
        Assert.Null(track.FileId);
        Assert.Equal(["бой", "темная улица"], track.Tags);
        Assert.True(track.Loop);
        Assert.Equal(100, track.Volume);
        Assert.Null(track.Code);
    }

    [Fact]
    public async Task File_track_gets_url_on_own_origin_and_file_name()
    {
        TestDatabase.SkipIfMissing();

        var track = await app.Tracks().CreateAsync(
            new MusicTrackDto { Name = Unique("Гроза"), FileId = app.Audio.Id, StartSeconds = 12, Loop = false, Volume = 70 }, Cancellation);

        Assert.Equal(FilesRoutes.Content(app.Audio.Id), track.FileUrl);
        Assert.Equal("Гроза.mp3", track.FileName);
        Assert.Equal(12, track.StartSeconds);
        Assert.False(track.Loop);
        Assert.Equal(70, track.Volume);
    }

    [Fact]
    public async Task Source_must_be_exactly_one_playable_thing()
    {
        TestDatabase.SkipIfMissing();
        var tracks = app.Tracks();

        Task<MusicTrackDto> Create(MusicTrackDto dto) => tracks.CreateAsync(dto, Cancellation);

        var neither = await Assert.ThrowsAsync<ApiException>(() => Create(new MusicTrackDto { Name = Unique("Пусто") }));
        var bothDto = Video(Unique("Оба"));
        bothDto.FileId = app.Audio.Id;
        var both = await Assert.ThrowsAsync<ApiException>(() => Create(bothDto));
        var notVideo = await Assert.ThrowsAsync<ApiException>(() => Create(Video(Unique("Вимео"), "https://vimeo.com/1")));
        var image = await Assert.ThrowsAsync<ApiException>(() => Create(new MusicTrackDto { Name = Unique("Картинка"), FileId = app.Image.Id }));
        var external = await Assert.ThrowsAsync<ApiException>(() => Create(new MusicTrackDto { Name = Unique("Чужой"), FileId = app.External.Id }));
        var missing = await Assert.ThrowsAsync<ApiException>(() => Create(new MusicTrackDto { Name = Unique("Нет"), FileId = Guid.NewGuid() }));
        var loud = Video(Unique("Громко"));
        loud.Volume = 101;
        var volume = await Assert.ThrowsAsync<ApiException>(() => Create(loud));

        Assert.All([neither, both, notVideo, image, external, missing, volume], ex =>
        {
            Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
            Assert.Equal(ApiProblemCodes.Invalid, ex.Code);
        });
        Assert.Contains("звуковой файл", image.Message);
    }

    [Fact]
    public async Task Player_reads_but_cannot_write_and_anonymous_is_rejected()
    {
        TestDatabase.SkipIfMissing();
        var existing = await app.Tracks().CreateAsync(Video(Unique("Ветер")), Cancellation);
        var player = app.Tracks(app.Player);

        var list = await player.ListAsync(Cancellation);
        var create = await Assert.ThrowsAsync<ApiException>(() => player.CreateAsync(Video(Unique("Шторм")), Cancellation));
        var update = await Assert.ThrowsAsync<ApiException>(() => player.UpdateAsync(existing, Cancellation));
        var delete = await Assert.ThrowsAsync<ApiException>(() => player.DeleteAsync(existing.Id, Cancellation));
        using var anonymous = await app.CreateClient(anonymous: true).GetAsync(MusicRoutes.PinnedTags, Cancellation);

        Assert.False(list.CanEdit);
        Assert.Contains(list.Items, t => t.Id == existing.Id);
        Assert.All([create, update, delete], ex => Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Duplicate_name_and_stale_version_are_conflicts()
    {
        TestDatabase.SkipIfMissing();
        var name = Unique("Туман");
        var track = await app.Tracks().CreateAsync(Video(name), Cancellation);

        var duplicate = await Assert.ThrowsAsync<ApiException>(() => app.Tracks().CreateAsync(Video(name.ToUpperInvariant()), Cancellation));
        track.Notes = "первая правка";
        var updated = await app.Tracks().UpdateAsync(track, Cancellation);
        track.Notes = "правка со старой версией";
        var stale = await Assert.ThrowsAsync<ApiException>(() => app.Tracks().UpdateAsync(track, Cancellation));

        Assert.Equal(ApiProblemCodes.Duplicate, duplicate.Code);
        Assert.Contains("Трек", duplicate.Message);
        Assert.Equal("первая правка", updated.Notes);
        Assert.True(stale.IsStale);
    }

    [Fact]
    public async Task Edits_go_to_audit_log_and_switching_source_clears_the_other()
    {
        TestDatabase.SkipIfMissing();
        var track = await app.Tracks().CreateAsync(Video(Unique("Смена")), Cancellation);

        track.YoutubeId = null;
        track.FileId = app.Audio.Id;
        var updated = await app.Tracks().UpdateAsync(track, Cancellation);

        Assert.Null(updated.YoutubeId);
        Assert.Equal(app.Audio.Id, updated.FileId);
        await using var db = app.Database.CreateContext();
        var log = await db.AuditLog.Where(e => e.EntityId == track.Id).ToListAsync(Cancellation);
        Assert.Equal(2, log.Count);
        Assert.All(log, e => Assert.Equal("MusicTrack", e.EntityType));
    }

    [Fact]
    public async Task Export_then_import_skips_existing_and_adds_new()
    {
        TestDatabase.SkipIfMissing();
        var existing = await app.Tracks().CreateAsync(Video(Unique("Экспорт")), Cancellation);
        var fresh = Unique("Новый");
        var file = $$"""
            {"catalog":"music","items":[
              {"name":"{{existing.Name}}","youtubeId":"dQw4w9WgXcQ"},
              {"name":"{{fresh}}","youtubeId":"https://youtu.be/dQw4w9WgXcQ","tags":["Погоня"],"code":"music.ignored"},
              {"name":"{{Unique("Чужой файл")}}","fileId":"{{Guid.NewGuid()}}"}
            ]}
            """;

        var report = await app.Tracks().ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(file)), overwrite: false, dryRun: false, Cancellation);
        var list = await app.Tracks().ListAsync(Cancellation);

        Assert.Equal((1, 1, 1), (report.Created, report.Skipped, report.Failed));
        Assert.Contains("другую", report.Lines.Single(l => l.Outcome == CatalogImportOutcome.Failed).Message);
        var imported = list.Items.Single(t => t.Name == fresh);
        Assert.Equal(["погоня"], imported.Tags);
        Assert.Null(imported.Code);

        using var export = await app.CreateClient().GetAsync(MusicRoutes.Tracks.Export, Cancellation);
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Contains("\"catalog\":\"music\"", await export.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact]
    public async Task Pool_takes_tagged_and_pinned_tracks()
    {
        TestDatabase.SkipIfMissing();
        var tag = $"тег{Guid.NewGuid():N}"[..12];
        var tagged = await app.Tracks().CreateAsync(Video(Unique("С тегом"), tags: tag.ToUpperInvariant()), Cancellation);
        var pinned = await app.Tracks().CreateAsync(Video(Unique("Прибитый")), Cancellation);
        await app.Tracks().CreateAsync(Video(Unique("Чужой"), tags: "другое"), Cancellation);

        var pool = await app.Music(app.Player).GetPoolAsync(MusicPool.Of([tag], [pinned.Id]), Cancellation);
        var empty = await app.Music().GetPoolAsync(MusicPool.Empty, Cancellation);

        Assert.Equal([tagged.Id, pinned.Id], pool.Tracks.Select(t => t.Id).Order());
        Assert.Empty(empty.Tracks);
    }

    [Fact]
    public async Task Pinned_tags_default_then_own_then_back_to_default()
    {
        TestDatabase.SkipIfMissing();
        var music = app.Music(app.Player);

        var initial = await music.GetPinnedTagsAsync(Cancellation);
        var saved = await music.SetPinnedTagsAsync(["Ужас", "бой", "ужас", " "], Cancellation);
        var reread = await music.GetPinnedTagsAsync(Cancellation);
        var keeperOwn = await app.Music(app.Keeper).GetPinnedTagsAsync(Cancellation);
        var reset = await music.SetPinnedTagsAsync([], Cancellation);

        Assert.True(initial.IsDefault);
        Assert.Equal(MusicDefaults.PinnedTags, initial.Tags);
        Assert.Equal(["ужас", "бой"], saved.Tags);
        Assert.Equal(["ужас", "бой"], reread.Tags);
        Assert.False(reread.IsDefault);
        Assert.True(keeperOwn.IsDefault);
        Assert.True(reset.IsDefault);
        Assert.True((await music.GetPinnedTagsAsync(Cancellation)).IsDefault);
    }

    [Fact]
    public async Task Pinned_tags_read_v1_comma_string_and_reject_too_many()
    {
        TestDatabase.SkipIfMissing();
        await using (var db = app.Database.CreateContext())
        {
            db.UserPreferences.Add(new UserPreference
            {
                UserId = app.Veteran.Id,
                Key = PreferenceKeys.MusicPinnedTags,
                Value = JsonDocument.Parse("\"Погоня,сон, Бой\""),
            });
            await db.SaveChangesAsync(Cancellation);
        }

        var v1 = await app.Music(app.Veteran).GetPinnedTagsAsync(Cancellation);
        var tooMany = await Assert.ThrowsAsync<ApiException>(() =>
            app.Music(app.Veteran).SetPinnedTagsAsync([.. Enumerable.Range(0, MusicTags.MaxCount + 1).Select(i => $"тег {i}")], Cancellation));

        Assert.Equal(["погоня", "сон", "бой"], v1.Tags);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
    }
}
