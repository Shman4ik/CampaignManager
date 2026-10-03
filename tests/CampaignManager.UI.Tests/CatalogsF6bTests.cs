using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Files;
using CampaignManager.Core.Catalogs;
using CampaignManager.UI.Catalogs;
using CampaignManager.UI.Catalogs.Pages;
using CampaignManager.UI.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Справочники, вторая волна ревью g6 (F6b): страница существа (E1 — «Сохранить» не спрашивает «Выйти?»; E3, E5, E6),
/// бестиарий (B2, B4, B7), профессии (O5, O6), навыки (S2, S5).
/// </summary>
public sealed class CatalogsF6bTests : KitContext
{
    private readonly FakeCatalog<CreatureDto> _creatures = new(CatalogsRoutes.Creatures);

    public CatalogsF6bTests()
    {
        Services.AddSingleton<ICatalogApi<CreatureDto>>(_creatures);
        Services.AddSingleton<ICatalogApi<SkillDto>>(new FakeCatalog<SkillDto>(CatalogsRoutes.Skills));
        Services.AddSingleton<IFilesApi>(new NoFiles());
    }

    // ── Страница существа ──

    private IRenderedComponent<CreatureEditPage> OpenCreature(CreatureDto creature)
    {
        _creatures.Items = [creature];
        _creatures.CanEdit = true;
        var page = Render<CreatureEditPage>(p => p.Add(x => x.Id, creature.Id));
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("input")));
        return page;
    }

    private static CreatureDto Creature(string name = "Глубоководный") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Statblock = new Statblock { HitPoints = 11, Str = new StatValue { Value = 70 } },
    };

    [Fact]
    public async Task Saving_leaves_the_page_without_asking_to_leave()
    {
        var dialogs = Services.GetRequiredService<DialogService>();
        var creature = Creature();
        var page = OpenCreature(creature);

        page.Find("input[aria-label='ПЗ']").Change("12"); // форма изменена: Dirty = true на момент нажатия
        page.FindAll("button").Single(b => b.TextContent.Contains("Сохранить", StringComparison.Ordinal)).Click();

        await page.WaitForStateAsync(() => _creatures.Updated.Count == 1);
        Assert.Null(dialogs.Current); // E1: после записи вопроса «Выйти?» нет
        Assert.EndsWith($"bestiary?open={creature.Id}", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelling_a_changed_page_asks_once_and_leaves_after_the_answer()
    {
        var dialogs = Services.GetRequiredService<DialogService>();
        var creature = Creature();
        var page = OpenCreature(creature);
        page.Find("input[aria-label='ПЗ']").Change("12");

        page.FindAll("button").First(b => b.TextContent.Trim() == "Отмена").Click();
        Assert.Equal("Есть несохранённые изменения. Выйти?", dialogs.Current!.Title);
        await page.InvokeAsync(() => dialogs.Complete(true));

        // E1: второго вопроса нет — на ответ «Выйти» страница уходит.
        await page.WaitForStateAsync(() => Services.GetRequiredService<NavigationManager>().Uri.EndsWith($"bestiary?open={creature.Id}", StringComparison.Ordinal));
        Assert.Null(dialogs.Current);
    }

    [Fact]
    public void Title_is_the_name_at_opening_and_does_not_follow_the_field()
    {
        var page = OpenCreature(Creature("Глубоководный"));

        Assert.Equal("Существо: Глубоководный", page.Find("h1").TextContent.Trim());
        page.Find("input[aria-invalid], input.cm-input").Change("");
        Assert.Equal("Существо: Глубоководный", page.Find("h1").TextContent.Trim());
    }

    [Fact]
    public void Stats_have_visible_captions_and_new_creature_has_no_prefilled_zeros()
    {
        _creatures.CanEdit = true;
        var page = Render<CreatureEditPage>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("input")));

        // E3: «Среднее» и «Кости» — видимым словом у каждой характеристики (6 + 6).
        Assert.Equal(6, page.FindAll(".cr-sub > span").Count(s => s.TextContent == "Среднее"));
        Assert.Equal(6, page.FindAll(".cr-sub > span").Count(s => s.TextContent == "Кости"));
        // E5: «Бонус к урону» и «Комплекция» в новой форме пусты, а не «0».
        Assert.Equal("", page.Find("input[aria-label='Бонус к урону']").GetAttribute("value") ?? "");
        Assert.Equal("", page.Find("input[aria-label='Комплекция']").GetAttribute("value") ?? "");
        // E4: подсказки — строчная d.
        Assert.DoesNotContain("1D", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("3D6", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Clearing_the_damage_bonus_stores_zero_again()
    {
        var creature = Creature();
        creature.Statblock.DamageBonus = "+1d4";
        var page = OpenCreature(creature);

        Assert.Equal("+1d4", page.Find("input[aria-label='Бонус к урону']").GetAttribute("value"));
        page.Find("input[aria-label='Бонус к урону']").Change("");

        Assert.Equal("0", creature.Statblock.DamageBonus);
    }

    // ── Статблок и бестиарий ──

    [Fact]
    public void Statblock_hides_zero_damage_and_zero_sanity_loss()
    {
        var statblock = new Statblock
        {
            HitPoints = 18,
            SanityLoss = "0/0",
            Attacks =
            [
                new CreatureAttack { Name = "Хватание", SkillValue = 60, Damage = "0", Description = "Жертва схвачена." },
                new CreatureAttack { Name = "Укус", SkillValue = 50, Damage = "1d6" },
            ],
        };

        var cut = Render<StatblockView>(p => p.Add(s => s.Statblock, statblock));

        var text = cut.Markup;
        Assert.DoesNotContain("урон 0", text, StringComparison.Ordinal);
        Assert.Contains("60% (30/12)", text, StringComparison.Ordinal);
        Assert.Contains("— см. ниже", text, StringComparison.Ordinal);
        Assert.Contains(", урон", text, StringComparison.Ordinal); // у «Укуса» урон свой
        Assert.DoesNotContain("0/0", text, StringComparison.Ordinal); // B2: «0/0» — не потеря нуля, а отсутствие
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("0/0", null)]
    [InlineData("0 / 0", null)]
    [InlineData("1d3/1d20", "1d3/1d20")]
    public void Sanity_loss_zero_zero_is_absence(string stored, string? shown) =>
        Assert.Equal(shown, StatblockView.SanityOf(new Statblock { SanityLoss = stored }));

    [Fact]
    public void Open_creature_has_one_edit_icon_and_no_collapse_button()
    {
        var creature = Creature();
        creature.Statblock.SanityLoss = "0/0";
        _creatures.Items = [creature];
        _creatures.CanEdit = true;
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/bestiary?open={creature.Id}");

        var page = Render<BestiaryPage>();

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("article.cr-open")));
        Assert.NotEmpty(page.FindAll("[aria-label='Изменить: Глубоководный']"));
        Assert.DoesNotContain("Свернуть", page.Find("article.cr-open").TextContent, StringComparison.Ordinal); // B7
    }

    // ── Профессии: слоты ──

    [Fact]
    public void Slot_order_and_removal_live_in_one_menu_per_row()
    {
        List<OccupationSlotDto>? saved = null;
        var first = new OccupationSlotDto { Kind = OccupationSlotKind.Social };
        var second = new OccupationSlotDto { Kind = OccupationSlotKind.Free };
        var editor = Render<OccupationSlotsEditor>(p => p
            .Add(c => c.Slots, [first, second])
            .Add(c => c.SlotsChanged, list => saved = list));

        // Три отдельные кнопки в строке (вверх, вниз, корзина) — одно «⋯»: восемь целей вместо двадцати четырёх (O5).
        Assert.Empty(editor.FindAll("[aria-label$=' выше']"));
        Assert.Empty(editor.FindAll(".fa-trash-can"));
        Assert.Equal(2, editor.FindAll("button[aria-label^='Действия: слот']").Count);

        editor.Find("button[aria-label='Действия: слот 1']").Click();
        var items = editor.FindAll("[role=menuitem]").Select(i => i.TextContent.Trim()).ToList();
        Assert.Equal(["Ниже", "Убрать навык"], items); // у первого слота «Выше» нет

        editor.FindAll("[role=menuitem]").First(i => i.TextContent.Contains("Ниже", StringComparison.Ordinal)).Click();
        Assert.Equal([OccupationSlotKind.Free, OccupationSlotKind.Social], saved!.Select(s => s.Kind));
    }

    // ── Список строк чипами ──

    [Fact]
    public void Chips_remove_by_tap_and_add_by_enter_in_the_same_row()
    {
        IReadOnlyList<string>? changed = null;
        var cut = Render<StringListEditor>(p => p
            .Add(e => e.Values, ["Научная", "Боевая"])
            .Add(e => e.Chips, true)
            .Add(e => e.ItemLabel, "Тег")
            .Add(e => e.ValuesChanged, v => changed = v));

        Assert.Equal(2, cut.FindAll("button.list-chip").Count);
        Assert.Empty(cut.FindAll("textarea")); // строк-полей нет
        Assert.Empty(cut.FindAll("button.cm-btn")); // «Добавить» не рисуется, пока в поле пусто

        cut.Find("input[aria-label='Новый: Тег']").Input("Тихая");
        cut.Find("input[aria-label='Новый: Тег']").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });
        Assert.Equal(["Научная", "Боевая", "Тихая"], changed);

        cut.Find("button[aria-label='Убрать: Научная']").Click();
        Assert.Equal(["Боевая"], changed);
    }

    // ── Навыки ──

    [Fact]
    public void Skill_details_put_the_opposing_check_and_time_right_under_the_description()
    {
        var skill = new SkillDto
        {
            Id = Guid.NewGuid(),
            Name = "Внимание",
            Description = "Заметить скрытое.",
            UsageExamples = ["Услышать шаги"],
            FailureConsequences = ["Пропустили засаду"],
            OpposingSkills = ["Скрытность"],
            TimeRequired = "1 раунд",
            CanRetry = true,
        };

        var cut = Render<SkillDetails>(p => p.Add(d => d.Skill, skill).Add(d => d.InRow, true));

        var text = cut.Markup;
        var meta = text.IndexOf("Встречная проверка", StringComparison.Ordinal);
        Assert.True(meta > text.IndexOf("Заметить скрытое.", StringComparison.Ordinal));
        Assert.True(meta < text.IndexOf("Примеры применения", StringComparison.Ordinal));
        Assert.True(meta < text.IndexOf("Последствия провала", StringComparison.Ordinal));
    }

    [Fact]
    public void Usage_names_are_bold_without_quotes_and_the_rest_links_to_the_holders()
    {
        var usage = new CatalogUsage(
            6,
            ["профессия Антиквар", "профессия Археолог", "профессия Детектив", "профессия Журналист", "профессия Учёный"],
            true);

        var cut = Render<CatalogUsageText>(p => p.Add(u => u.Usage, usage).Add(u => u.Query, "Внимание"));

        Assert.Equal("Используется: профессия Антиквар, профессия Археолог, профессия Детектив и ещё 3.", cut.Find("span").TextContent);
        Assert.Equal(["Антиквар", "Археолог", "Детектив"], cut.FindAll("strong").Select(s => s.TextContent));
        Assert.DoesNotContain("«", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("occupations?q=%D0%92%D0%BD%D0%B8%D0%BC%D0%B0%D0%BD%D0%B8%D0%B5", cut.Find("a").GetAttribute("href"));
    }

    [Fact]
    public void Usage_in_character_sheets_has_no_link()
    {
        var usage = new CatalogUsage(4, ["лист сыщика Артур Нельсон"], false);

        var cut = Render<CatalogUsageText>(p => p.Add(u => u.Usage, usage).Add(u => u.Query, "Кольт"));

        Assert.Empty(cut.FindAll("a"));
        Assert.Equal("Используется: лист сыщика Артур Нельсон и ещё 3.", cut.Find("span").TextContent);
    }

    // ── Охранник ухода ──

    [Fact]
    public async Task Guard_allow_lets_the_next_navigation_through_even_while_dirty()
    {
        var dialogs = Services.GetRequiredService<DialogService>();
        var guard = Render<UnsavedChangesGuard>(p => p.Add(g => g.Dirty, true));

        guard.Instance.Allow();

        Assert.True(await guard.Instance.ConfirmLeaveAsync());
        Assert.Null(dialogs.Current);
    }

    private sealed class FakeCatalog<T>(CatalogRoute route) : ICatalogApi<T>
        where T : CatalogItemDto
    {
        public List<T> Items { get; set; } = [];

        public bool CanEdit { get; set; }

        public List<T> Updated { get; } = [];

        public CatalogRoute Route => route;

        public Task<CatalogList<T>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CatalogList<T>(Items, CanEdit));

        public Task<T> CreateAsync(T item, CancellationToken cancellationToken = default) => Task.FromResult(item);

        public Task<T> UpdateAsync(T item, CancellationToken cancellationToken = default)
        {
            Updated.Add(item);
            return Task.FromResult(item);
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<CatalogImportReport> ImportAsync(Stream file, bool overwrite, bool dryRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CatalogImportReport> SyncAsync(bool dryRun, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
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
}
