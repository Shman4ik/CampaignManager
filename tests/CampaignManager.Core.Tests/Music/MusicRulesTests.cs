using CampaignManager.Core.Music;

namespace CampaignManager.Core.Tests.Music;

/// <summary>Фонотека: теги, ссылки YouTube, пул сцены и выбор «случайно, но не одно и то же» (v1, перенос T2.8).</summary>
public sealed class MusicRulesTests
{
    private sealed record Track(Guid Id, string[] Tags);

    [Fact]
    public void Tags_AreTrimmedLoweredAndYoIsE()
    {
        Assert.Equal("бой", MusicTags.Normalize("  Бой "));
        Assert.Equal("темная улица", MusicTags.Normalize("Тёмная   улица"));
        Assert.Equal(["бой", "ужас"], MusicTags.Normalize(["Ужас", "бой ", "БОЙ", "", null]));
    }

    [Fact]
    public void Tags_KeepOrderWhenAsked()
    {
        Assert.Equal(["ужас", "бой"], MusicTags.NormalizeKeepingOrder(["Ужас", "бой", "ужас"]));
        Assert.Equal(["бой", "погоня", "сон"], MusicTags.Parse("бой, Погоня;; сон"));
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?list=PL1&v=dQw4w9WgXcQ&t=10")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=abc")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ")]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData(" dQw4w9WgXcQ ")]
    public void YouTube_IdFromAnyLinkForm(string input)
    {
        Assert.True(YouTubeVideo.TryParseId(input, out var id));
        Assert.Equal("dQw4w9WgXcQ", id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://vimeo.com/123456")]
    [InlineData("short")]
    [InlineData("https://www.youtube.com/watch?v=short")]
    public void YouTube_RejectsNotAVideo(string input) =>
        Assert.False(YouTubeVideo.TryParseId(input, out _));

    [Fact]
    public void Pool_TakesTaggedAndPinnedTracks()
    {
        Track battle = new(Guid.NewGuid(), ["бой"]), calm = new(Guid.NewGuid(), ["спокойствие"]), plain = new(Guid.NewGuid(), []);
        var pool = MusicPool.Of(["Бой"], [plain.Id]);

        var selected = pool.Select([battle, calm, plain], t => t.Id, t => t.Tags);

        Assert.Equal([battle, plain], selected);
        Assert.Empty(MusicPool.Empty.Select([battle, calm, plain], t => t.Id, t => t.Tags));
    }

    [Fact]
    public void Shuffle_DoesNotRepeatRecentTracks()
    {
        var tracks = Enumerable.Range(0, 5).Select(_ => new Track(Guid.NewGuid(), [])).ToList();
        var random = new Random(7);
        List<Guid> history = [];

        for (var i = 0; i < 200; i++)
        {
            var pick = MusicShuffle.Pick(tracks, t => t.Id, history, random)!;
            Assert.DoesNotContain(pick.Id, history.TakeLast(MusicShuffle.AntiRepeatWindow));
            history = MusicShuffle.Remember(history, pick.Id);
        }

        Assert.Equal(MusicShuffle.HistoryLimit, history.Count);
    }

    [Fact]
    public void Shuffle_ShortPoolStillPlays()
    {
        Track only = new(Guid.NewGuid(), []), other = new(Guid.NewGuid(), []);

        Assert.Same(only, MusicShuffle.Pick([only], t => t.Id, [only.Id], new Random(1)));
        // Пул из двух: окно — один трек, значит повтора подряд нет.
        Assert.Same(other, MusicShuffle.Pick([only, other], t => t.Id, [only.Id], new Random(1)));
        Assert.Null(MusicShuffle.Pick(Array.Empty<Track>(), t => t.Id, [], new Random(1)));
    }
}
