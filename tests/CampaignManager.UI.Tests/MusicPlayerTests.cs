using CampaignManager.Contracts.Music;
using CampaignManager.Core.Music;
using CampaignManager.UI.Music;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Плеер (T2.8): пул и «Другой», конец трека, ошибки и жест, громкость. Звук (JS) bUnit не проверит — его
/// смотрят в браузере; здесь решения, которые плеер передаёт модулю.
/// </summary>
public sealed class MusicPlayerTests
{
    private static MusicTrackDto Track(string name, bool loop = true, params string[] tags) =>
        new() { Id = Guid.NewGuid(), Name = name, YoutubeId = "dQw4w9WgXcQ", Loop = loop, Tags = [.. tags] };

    private static MusicPlayer Player(params MusicTrackDto[] tracks)
    {
        var player = new MusicPlayer(new Random(3));
        player.SetLibrary(tracks);
        return player;
    }

    [Fact]
    public void Mood_plays_from_its_pool_and_opens_panel()
    {
        var battle = Track("Бой", tags: "бой");
        var player = Player(battle, Track("Тишина", tags: "спокойствие"));

        Assert.True(player.PlayTag("Бой"));

        Assert.Same(battle, player.Current);
        Assert.True(player.IsPlaying);
        Assert.True(player.IsPanelOpen);
        Assert.Equal("бой", player.PoolLabel);
        Assert.False(player.CanRoll);
    }

    [Fact]
    public void Empty_pool_says_so_instead_of_playing()
    {
        var player = Player(Track("Тишина", tags: "спокойствие"));

        Assert.False(player.PlayTag("бой"));

        Assert.Null(player.Current);
        Assert.NotNull(player.Error);
        Assert.True(player.IsPanelOpen);
    }

    [Fact]
    public void Roll_never_repeats_the_track_right_away()
    {
        var player = Player(Track("А", tags: "бой"), Track("Б", tags: "бой"), Track("В", tags: "бой"));
        player.PlayTag("бой");

        for (var i = 0; i < 30; i++)
        {
            var before = player.Current!.Id;
            var version = player.CommandVersion;
            Assert.True(player.Roll());
            Assert.NotEqual(before, player.Current!.Id);
            Assert.Equal(version + 1, player.CommandVersion);
        }
    }

    [Fact]
    public void Single_track_stops_at_end_but_pool_track_moves_on()
    {
        var once = Track("Один раз", loop: false, "бой");
        var player = Player(once, Track("Ещё", tags: "бой"));

        player.PlayTrack(once);
        player.TrackEnded();
        Assert.False(player.IsPlaying);
        Assert.Same(once, player.Current);

        player.PlayPool(MusicPool.Of(["бой"]), "бой");
        while (player.Current != once)
        {
            player.Roll();
        }

        player.TrackEnded();
        Assert.True(player.IsPlaying);
        Assert.NotSame(once, player.Current);
    }

    [Fact]
    public void Edited_library_renames_current_track_without_reloading_sound()
    {
        var track = Track("Старое имя");
        var player = Player(track);
        player.PlayTrack(track);
        var version = player.CommandVersion;

        var renamed = Track("Новое имя");
        renamed.Id = track.Id;
        player.SetLibrary([renamed]);

        Assert.Equal("Новое имя", player.Current!.Name);
        Assert.Equal(version, player.CommandVersion);
    }

    [Fact]
    public void Gesture_request_opens_panel_and_error_pauses()
    {
        var track = Track("Ролик");
        var player = Player(track);
        player.PlayTrack(track);
        player.ClosePanel();

        player.SetNeedsGesture(true);
        Assert.True(player.IsPanelOpen);

        player.ReportError("Встраивание запрещено.");
        Assert.False(player.IsPlaying);
        Assert.Equal("Встраивание запрещено.", player.Error);

        player.Stop();
        Assert.Null(player.Current);
        Assert.Null(player.Error);
        Assert.False(player.NeedsGesture);
    }

    [Fact]
    public void Volume_is_clamped_and_unmutes()
    {
        var player = Player();
        player.ToggleMuted();

        player.SetVolume(140);

        Assert.Equal(100, player.Volume);
        Assert.False(player.Muted);
        Assert.Equal(MusicDefaults.PinnedTags, player.PinnedTags);
    }
}
