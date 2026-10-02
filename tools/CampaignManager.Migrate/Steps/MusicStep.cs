using CampaignManager.Data.Music;
using CampaignManager.Migrate.V1;

namespace CampaignManager.Migrate.Steps;

/// <summary>Фонотека: <c>Storage</c> → строка <c>files</c>, <c>YouTube</c> → <c>youtube_id</c>.</summary>
public static class MusicStep
{
    public static async Task RunAsync(MigrationState s, CancellationToken cancellationToken)
    {
        var (stored, youtube) = (0, 0);
        foreach (var row in s.V1.MusicTracks)
        {
            var name = row.Text("Name")!;
            var source = row.Text("Source");
            var track = new MusicTrack
            {
                Id = row.Guid("Id")!.Value,
                Name = name,
                StartSeconds = Math.Max(0, row.Int("StartSeconds") ?? 0),
                Loop = row.Bool("Loop"),
                Volume = Math.Clamp(row.Int("Volume") ?? 100, 0, 100),
                Tags = row.Strings("Tags").Distinct(StringComparer.Ordinal).ToList(),
                Notes = row.Text("Notes"),
                CreatedAt = row.Time("CreatedAt") ?? default,
                UpdatedAt = row.Time("LastUpdated") ?? default,
            };

            switch (row.Str("SourceType"))
            {
                case "YouTube" when source is not null:
                    track.YoutubeId = source;
                    youtube++;
                    break;
                case "Storage" when source is not null:
                    if (await s.StoredFileAsync(source, $"трек «{name}»", cancellationToken) is not { } file)
                    {
                        s.Report.Add(ReportSections.DroppedJunk, $"трек «{name}»: файла нет в хранилище — трек не перенесён");
                        continue;
                    }

                    track.FileId = file.Id;
                    stored++;
                    break;
                default:
                    s.Report.Add(ReportSections.DroppedJunk, $"трек «{name}»: без источника ({row.Str("SourceType")})");
                    continue;
            }

            s.Tracks.Add(track.Id);
            s.Db.MusicTracks.Add(track);
        }

        s.Report.Count("games.MusicTracks", s.V1.MusicTracks.Count, "music_tracks", s.Tracks.Count, $"файлом — {stored}, YouTube — {youtube}");
    }
}
