using System.Text.RegularExpressions;
using Bunit;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Encounters;
using CampaignManager.Contracts.Files;
using CampaignManager.Contracts.Music;
using CampaignManager.Core;
using CampaignManager.Core.Characters;
using CampaignManager.Core.Encounters;
using CampaignManager.Core.Encounters.Chase;
using CampaignManager.UI.Encounters;
using CampaignManager.UI.Encounters.Chase;
using CampaignManager.UI.KeeperScreen;
using CampaignManager.UI.Music;
using CampaignManager.UI.Pages;
using CampaignManager.UI.Platform;
using CampaignManager.UI.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Обход погони, ширмы и фонотеки (F5b, отчёт g5): поимка через предпросмотр, журнал погони без повторов, окно участника
/// предупреждает о том, кто бежать не может, итог погони, ширма (вкладки в одну строку, колонки), групповая проверка, форма
/// трека, плеер, «Нет доступа».
/// </summary>
public sealed class EncounterF5bTests : KitContext
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private static string Flat(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    // ───────── журнал погони (H18) и итог (H17)

    [Fact]
    public void Chase_log_shows_a_move_once_without_the_restating_lines_and_action_counters()
    {
        var entry = new EncounterLogEntry
        {
            Kind = EncounterLogKind.Move,
            Text = "Шоггот: локация 5 → 6",
            Lines =
            [
                "Шоггот: Локация, 5 → 6",
                "Шоггот: Тратит 1 действие, 1 → 0",
            ],
            At = Now,
        };

        var cut = Render<EncounterLog>(p => p.Add(l => l.Entries, [entry]));

        var text = Flat(cut.Markup);
        Assert.Contains("Шоггот: локация 5 → 6", text);
        Assert.DoesNotContain("Тратит", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Локация, 5", text, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("details"));
    }

    [Fact]
    public void Chase_log_keeps_a_line_that_says_more_than_the_title()
    {
        var entry = new EncounterLogEntry
        {
            Kind = EncounterLogKind.Move,
            Text = "Шоггот: локация 5 → 6 (разгон)",
            Lines = ["Шоггот: Локация, 4 → 5"],
            At = Now,
        };

        var cut = Render<EncounterLog>(p => p.Add(l => l.Entries, [entry]));

        Assert.Contains("Локация, 4 → 5", Flat(cut.Markup));
    }

    private static EncounterParticipant Runner(string name, EncounterSide side, int dex) => new()
    {
        Name = name,
        Kind = ParticipantKind.Creature,
        Side = side,
        Initiative = dex,
        HitPoints = 10,
        MaxHitPoints = 10,
        Stats = new ParticipantStats { Move = 8, Dex = dex, Con = 50 },
    };

    // ───────── окно участника (H19)

    private sealed class DyingSource : IParticipantSource
    {
        public string Key => "dying";

        public string Label => "Сыщики";

        public string Icon => "fa-user";

        public Task<ParticipantSourceResult> LoadAsync(ParticipantPickerContext context, CancellationToken cancellationToken) =>
            Task.FromResult(new ParticipantSourceResult(
            [
                new ParticipantOption("a", "Адам", "ПЗ 0 · при смерти", ParticipantKind.Investigator, Guid.NewGuid(), false,
                    (_, _) => Task.FromResult<IReadOnlyList<EncounterParticipant>>([new EncounterParticipant { Name = "Адам", Dying = true }])),
            ]));
    }

    [Fact]
    public async Task Chase_picker_asks_before_adding_someone_who_cannot_run_and_combat_does_not()
    {
        Services.AddSingleton(Fake.Of<CampaignManager.Contracts.Scenarios.IScenariosApi>(new()));
        Services.AddSingleton(Fake.Of<CampaignManager.Contracts.Scenarios.IRunsApi>(new()));
        List<EncounterParticipant> added = [];
        var cut = Render<ParticipantPicker>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.Sources, [new DyingSource()])
            .Add(c => c.Context, new ParticipantPickerContext(null, new SkillCatalog([])))
            .Add(c => c.WarnDying, true)
            .Add(c => c.OnPicked, list => added.AddRange(list)));
        var dialogs = Services.GetRequiredService<DialogService>();

        cut.Find("[data-testid=picker-add]").Click();

        cut.WaitForAssertion(() => Assert.NotNull(dialogs.Current));
        Assert.Equal("Адам", dialogs.Current!.Subject);
        Assert.Contains("бежать не сможет", dialogs.Current.Message, StringComparison.Ordinal);
        Assert.Empty(added);

        dialogs.Complete(false);
        cut.WaitForAssertion(() => Assert.Empty(added));

        cut.Find("[data-testid=picker-add]").Click();
        cut.WaitForAssertion(() => Assert.NotNull(dialogs.Current));
        dialogs.Complete(true);
        cut.WaitForAssertion(() => Assert.Single(added));
        await Task.CompletedTask;

        // В бою раненый — обычное дело: вопроса нет.
        var combat = Render<ParticipantPicker>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.Sources, [new DyingSource()])
            .Add(c => c.Context, new ParticipantPickerContext(null, new SkillCatalog([])))
            .Add(c => c.OnPicked, list => added.AddRange(list)));
        combat.Find("[data-testid=picker-add]").Click();
        combat.WaitForAssertion(() => Assert.Equal(2, added.Count));
        Assert.Null(dialogs.Current);
    }

    // ───────── ширма (R1, R2, R4)

    [Fact]
    public void Keeper_reference_tabs_are_one_sticky_scrolling_row_and_blocks_flow_into_columns()
    {
        Services.AddSingleton<ICampaignsApi>(new TableFakes.Campaigns());
        Services.AddSingleton<ICharactersApi>(new TableFakes.Characters());
        Services.AddSingleton<ICatalogApi<SkillDto>>(new TableFakes.Skills());

        var cut = Render<KeeperReference>(p => p.Add(r => r.ActiveBlock, KeeperScreenBlock.Damage));

        Assert.Contains("cm-tabs-sticky", cut.Find("[role=tablist]").ClassName, StringComparison.Ordinal);
        Assert.NotNull(cut.Find(".keeper-cols"));
        // Таблицы III и ядов — на всю ширину; короткие разделы встают парой.
        Assert.Equal(2, cut.FindAll(".keeper-cols > .keeper-wide").Count);
        Assert.True(cut.FindAll(".keeper-cols > section").Count > 2);
    }

    [Fact]
    public void Group_check_picks_perception_with_segments_and_other_skill_by_search_and_hides_dead_buttons()
    {
        var draft = new CampaignManager.UI.Checks.GroupCheckDraft();
        var cut = Render<CampaignManager.UI.Checks.GroupCheckPanel>(p => p.Add(c => c.Draft, draft));

        // Нет состава — нечего «Бросить за всех» и «Очистить».
        Assert.Empty(cut.FindAll("[data-testid='group-check-roll']"));
        Assert.Empty(cut.FindAll("[data-testid='group-check-clear']"));
        Assert.Equal(["Внимание", "Слух", "Психология", "Другой навык"], cut.FindAll("[data-testid='group-check-skill'] [role=radio]").Select(b => b.TextContent.Trim()).ToArray());
        Assert.Empty(cut.FindAll("[data-testid='group-check-other']"));

        cut.FindAll("[data-testid='group-check-skill'] [role=radio]")[1].Click();
        Assert.Equal("Слух", draft.SkillName);

        cut.FindAll("[data-testid='group-check-skill'] [role=radio]")[3].Click();
        Assert.NotNull(cut.Find("[data-testid='group-check-other']"));
    }

    // ───────── фонотека и плеер (M6–M9), «Нет доступа» (P1)

    [Fact]
    public void Track_form_shows_the_full_link_hides_volume_for_a_video_and_folds_the_moods()
    {
        Services.AddSingleton(Fake.Of<IFilesApi>(new()));
        var track = new MusicTrackDto { Name = "Джаз", YoutubeId = "WF8iZWrOmb0", Tags = ["улица", "1920-е"] };

        var cut = Render<MusicTrackForm>(p => p.Add(f => f.Track, track).Add(f => f.KnownTags, ["бой", "улица", "1920-е"]));

        Assert.Contains("https://www.youtube.com/watch?v=WF8iZWrOmb0", cut.Find("input[placeholder^='https://www.youtube.com']").GetAttribute("value"), StringComparison.Ordinal);
        Assert.DoesNotContain("Ролик WF8iZWrOmb0", cut.Markup, StringComparison.Ordinal); // подсказка не повторяет идентификатор
        Assert.Empty(cut.FindAll("input[type=range]")); // громкость YouTube на iPad не меняется — поля нет
        Assert.Contains("улица, 1920-е", cut.Find("[data-testid=track-tags-summary]").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[aria-label='Есть в фонотеке']")); // чипы тегов свёрнуты

        cut.FindAll("button.cm-link-btn").Single(b => b.TextContent.Trim() == "Изменить").Click();

        Assert.NotEmpty(cut.FindAll("[aria-label='Есть в фонотеке']"));
    }

    [Fact]
    public void Track_form_keeps_volume_for_a_file()
    {
        Services.AddSingleton(Fake.Of<IFilesApi>(new()));

        var cut = Render<MusicTrackForm>(p => p.Add(f => f.Track, new MusicTrackDto { Name = "Файл" }));

        Assert.Single(cut.FindAll("input[type=range]"));
        Assert.Contains("Не выбраны", cut.Find("[data-testid=track-tags-summary]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Player_strip_has_no_continue_after_a_video_refused_to_play()
    {
        var player = Services.GetRequiredService<MusicPlayer>();
        var bad = new MusicTrackDto { Id = Guid.NewGuid(), Name = "Запрет", YoutubeId = "dQw4w9WgXcQ", Tags = ["бой"] };
        var other = new MusicTrackDto { Id = Guid.NewGuid(), Name = "Другой", YoutubeId = "9bZkp7q19f0", Tags = ["бой"] };
        player.SetLibrary([bad, other]);
        player.PlayTag("бой");
        player.ReportError("Этот ролик нельзя воспроизвести встроенным плеером.");

        var cut = Render<PlayerStrip>();

        Assert.DoesNotContain("Продолжить", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Другой трек", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Expanded_player_puts_volume_in_the_seek_row_for_files_only_and_hides_the_library_link_on_the_library_page()
    {
        Services.AddSingleton(Fake.Of<ICatalogApi<MusicTrackDto>>(new()
        {
            [nameof(ICatalogApi<MusicTrackDto>.ListAsync)] = _ => Task.FromResult(new CatalogList<MusicTrackDto>([], false)),
        }));
        Services.AddSingleton(Fake.Of<IMusicApi>(new()
        {
            [nameof(IMusicApi.GetPinnedTagsAsync)] = _ => Task.FromResult(new PinnedTagsDto(["бой"], true)),
        }));
        var player = Services.GetRequiredService<MusicPlayer>();
        var file = new MusicTrackDto { Id = Guid.NewGuid(), Name = "Файл", FileId = Guid.NewGuid(), FileUrl = "/api/v1/files/x", Tags = ["бой"] };
        var video = new MusicTrackDto { Id = Guid.NewGuid(), Name = "Ролик", YoutubeId = "dQw4w9WgXcQ", Tags = ["погоня"] };
        player.SetLibrary([file, video]);
        player.PlayTrack(file);
        Services.GetRequiredService<NavigationManager>().NavigateTo("chase");

        var cut = Render<MusicPlayerBar>();
        cut.Find("button[aria-haspopup=menu]").Click();
        cut.FindAll("[role=menuitem]").Single(i => i.TextContent.Contains("Громкость", StringComparison.Ordinal)).Click();

        Assert.NotEmpty(cut.FindAll(".cm-music-seek input[aria-label=Громкость]"));
        Assert.NotEmpty(cut.FindAll("a[href$='music']"));

        Services.GetRequiredService<NavigationManager>().NavigateTo("music");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("a[href$='music']")));

        player.PlayTrack(video);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".cm-music-seek input[aria-label=Громкость]")));
    }

    [Fact]
    public void No_access_is_one_phrase_and_a_button_with_the_application_as_a_link_in_the_row()
    {
        var cut = Render<NoAccess>();

        Assert.Single(cut.FindAll("p"));
        Assert.Contains("Хранителям", cut.Find("[data-testid=no-access-text]").TextContent, StringComparison.Ordinal);
        Assert.Contains("заявку", cut.Find(".cm-btn-row a.cm-link-btn").TextContent, StringComparison.Ordinal);
    }
}
