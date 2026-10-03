using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.UI.Catalogs;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Возможные заклинания книги (F6a, K5): название — текстом, «Связать…» только у не связанных, выбор из справочника —
/// окном с поиском, а не списком в каждой строке; убрать — нейтральный крестик, не красная корзина.
/// </summary>
public sealed class BookSpellsEditorTests : KitContext
{
    private static readonly SpellDto Resurrection = new() { Id = Guid.NewGuid(), Name = "Воскрешение", SpellType = "Некромантия" };

    private IRenderedComponent<BookSpellsEditor> Open(List<BookSpellDto> spells, Action<List<BookSpellDto>>? changed = null) =>
        Render<BookSpellsEditor>(p => p
            .Add(e => e.Spells, spells)
            .Add(e => e.Catalog, [Resurrection])
            .Add(e => e.SpellsChanged, list => changed?.Invoke(list)));

    [Fact]
    public void Only_unlinked_rows_offer_to_link_and_a_found_name_needs_no_mark()
    {
        var cut = Open([new BookSpellDto("Воскрешение", null), new BookSpellDto("Знак Сулеймана", null)]);

        // «Воскрешение» сервер найдёт сам — пометки и кнопки нет; «Знак Сулеймана» — нет в справочнике.
        Assert.Single(cut.FindAll("button[aria-label^='Связать со справочником']"));
        Assert.Contains("нет в справочнике", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("select"));
        Assert.Empty(cut.FindAll("button[aria-label^='Удалить'].cm-btn-outline-error"));
    }

    [Fact]
    public void Removing_a_row_returns_a_new_list_and_the_draft_adds_a_raw_name()
    {
        List<BookSpellDto>? changed = null;
        var cut = Open([new BookSpellDto("Знак Сулеймана", null)], list => changed = list);

        cut.Find("button[aria-label='Удалить: Знак Сулеймана']").Click();
        Assert.Empty(changed!);

        // «Добавить» появляется, когда в поле есть текст.
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Trim() == "Добавить");
        cut.Find("input[aria-label='Новое заклинание книги']").Input("  Зов Ктулху ");
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Добавить").Click();

        Assert.Equal([new BookSpellDto("Зов Ктулху", null), new BookSpellDto("Знак Сулеймана", null)], changed!.OrderBy(s => s.RawName.Length));
    }
}
