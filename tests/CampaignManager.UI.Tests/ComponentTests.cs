using System.Security.Claims;
using Bunit;
using CampaignManager.UI.Layout;
using CampaignManager.UI.Shared;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace CampaignManager.UI.Tests;

public sealed class ComponentTests : KitContext
{
    private sealed record Row(string Name, int Range);

    private static readonly Row[] Rows = [new("Нож", 0), new("Винтовка", 110), new("Кольт", 15)];

    [Fact]
    public void Icon_only_button_requires_aria_label()
    {
        Assert.Throws<InvalidOperationException>(() => Render<Button>(p => p.Add(b => b.Icon, "fa-pen")));
    }

    [Fact]
    public void Button_merges_class_instead_of_replacing_it()
    {
        var cut = Render<Button>(p => p
            .Add(b => b.Variant, ButtonVariant.OutlineError)
            .Add(b => b.Small, true)
            .AddUnmatched("class", "w-full")
            .AddChildContent("Удалить"));

        Assert.Equal("cm-btn cm-btn-outline-error cm-btn-sm w-full", cut.Find("button").ClassName);
    }

    /// <summary>
    /// На телефоне шапка сворачивает кнопку с иконкой до значка правилом shell.css
    /// (<c>.cm-topbar-actions .cm-btn:has(&gt; i) &gt; span</c> — в sr-only). Правило держится на этой разметке:
    /// иконка и подпись — прямые дети кнопки, подпись — текст без aria-hidden и без своего aria-label у кнопки,
    /// то есть она и есть имя кнопки для диктора. Сама вёрстка — в браузере на 390×844.
    /// </summary>
    [Fact]
    public void Header_action_renders_icon_and_label_the_phone_rule_collapses()
    {
        var cut = Render<PageHeader>(p => p
            .Add(h => h.Title, "Заклинания")
            .Add(h => h.Actions, builder =>
            {
                builder.OpenComponent<Button>(0);
                builder.AddAttribute(1, nameof(Button.Small), true);
                builder.AddAttribute(2, nameof(Button.Icon), "fa-plus");
                builder.AddAttribute(3, nameof(Button.ChildContent), (RenderFragment)(b => b.AddContent(0, "Добавить заклинание")));
                builder.CloseComponent();
            }));

        var button = cut.Find(".cm-topbar-actions > button.cm-btn.cm-btn-sm");
        Assert.NotNull(button.QuerySelector(":scope > i.fa-plus[aria-hidden=true]"));
        var label = button.QuerySelector(":scope > span");
        Assert.NotNull(label);
        Assert.Equal("Добавить заклинание", label.TextContent);
        Assert.Null(label.GetAttribute("aria-hidden"));
        Assert.Null(label.GetAttribute("class"));
        Assert.Null(button.GetAttribute("aria-label"));
    }

    [Fact]
    public void Data_table_sorts_by_clicked_column_and_flips_direction()
    {
        var cut = Render<DataTable<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.Columns, Columns));

        Assert.Equal(["Нож", "Винтовка", "Кольт"], NamesOf(cut));

        cut.FindAll("th button")[0].Click();
        Assert.Equal(["Нож", "Кольт", "Винтовка"], NamesOf(cut));
        Assert.Equal("ascending", cut.FindAll("th")[1].GetAttribute("aria-sort"));

        cut.FindAll("th button")[0].Click();
        Assert.Equal(["Винтовка", "Кольт", "Нож"], NamesOf(cut));
        Assert.Equal("descending", cut.FindAll("th")[1].GetAttribute("aria-sort"));
    }

    [Fact]
    public void Data_table_with_external_sort_reports_column_key_and_keeps_order()
    {
        DataSort? requested = null;
        var cut = Render<DataTable<Row>>(p => p
            .Add(t => t.Items, Rows)
            .Add(t => t.Columns, Columns)
            .Add(t => t.SortChanged, sort => requested = sort));

        cut.FindAll("th button")[0].Click();

        Assert.Equal(new DataSort("range", false), requested);
        Assert.Equal(["Нож", "Винтовка", "Кольт"], NamesOf(cut));
    }

    [Fact]
    public void String_list_editor_returns_a_new_trimmed_list()
    {
        IReadOnlyList<string> original = ["Мать"];
        IReadOnlyList<string>? changed = null;
        var cut = Render<StringListEditor>(p => p
            .Add(e => e.Values, original)
            .Add(e => e.ValuesChanged, values => changed = values));

        var inputs = cut.FindAll("input");
        inputs[^1].Input("  Мискатоник  ");
        cut.FindAll("button").Single(b => b.TextContent.Contains("Добавить")).Click();

        Assert.Equal(["Мать", "Мискатоник"], changed);
        Assert.Equal(["Мать"], original);
    }

    [Fact]
    public void String_list_editor_removes_by_row()
    {
        IReadOnlyList<string>? changed = null;
        var cut = Render<StringListEditor>(p => p
            .Add(e => e.Values, ["Мать", "Брат"])
            .Add(e => e.ValuesChanged, values => changed = values));

        cut.Find("button[aria-label='Удалить: Мать']").Click();

        Assert.Equal(["Брат"], changed);
    }

    [Fact]
    public void Async_content_shows_error_with_retry_then_data()
    {
        var fail = true;
        var cut = Render<AsyncContent<IReadOnlyList<string>>>(p => p
            .Add(a => a.Load, _ => fail
                ? throw new HttpRequestException("нет связи")
                : Task.FromResult<IReadOnlyList<string>>(["Харви Уолтерс"]))
            .Add(a => a.ChildContent, names => builder => builder.AddContent(0, string.Join(", ", names))));

        Assert.Contains("Нет связи с сервером", cut.Find(".cm-alert-error").TextContent);

        fail = false;
        cut.FindAll("button").Single(b => b.TextContent.Contains("Повторить")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Харви Уолтерс", cut.Markup));
    }

    [Fact]
    public void Async_content_shows_empty_for_empty_collection()
    {
        var cut = Render<AsyncContent<IReadOnlyList<string>>>(p => p
            .Add(a => a.Load, _ => Task.FromResult<IReadOnlyList<string>>([]))
            .Add(a => a.Empty, builder => builder.AddContent(0, "В кампании нет сыщиков."))
            .Add(a => a.ChildContent, _ => builder => builder.AddContent(0, "список")));

        Assert.Contains("В кампании нет сыщиков.", cut.Markup);
        Assert.DoesNotContain("список", cut.Markup);
    }

    // Подпись рельса = заголовок страницы, одна строка 11px в рельсе 90px: не длиннее «Пользователи» (12 знаков, влезает впритык).
    [Fact]
    public void Rail_labels_fit_one_line()
    {
        Assert.All(NavMenu.Items, item => Assert.True(item.RailLabel.Length <= "Пользователи".Length, item.RailLabel));
    }

    // Нижняя панель телефона: четыре пункта и «Ещё».
    [Fact]
    public void Phone_bar_has_room_for_four_items()
    {
        Assert.InRange(NavMenu.Items.Count(i => i.OnPhoneBar), 1, 4);
    }

    // Меню только прячет — защищает сервер; гостю не показывается ничего: «Главная» — страница под входом (S8), «Войти» — в подвале.
    // Хранителю — и «Ширма» (R3): закладка на планшете без кнопки в шапке.
    [Theory]
    [InlineData(null, new string[0])]
    [InlineData("Player", new[] { "", "campaigns", "weapons" })]
    [InlineData("Keeper", new[] { "", "campaigns", "scenarios", "weapons", "reference" })]
    public void Menu_shows_sections_by_role(string? role, string[] mustSee)
    {
        var user = role is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));

        var visible = NavMenu.VisibleTo(user).Select(i => i.Href).ToList();

        Assert.All(mustSee, href => Assert.Contains(href, visible));
        Assert.DoesNotContain("admin/users", visible);
        if (role is null)
        {
            Assert.Empty(visible);
        }
        else if (role == "Player")
        {
            Assert.DoesNotContain("scenarios", visible);
            Assert.DoesNotContain("reference", visible);
        }
    }

    [Fact]
    public void Admin_sees_every_section()
    {
        var admin = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "Admin"), new Claim(ClaimTypes.Role, "Keeper")], "test"));

        Assert.Equal(NavMenu.Items.Count, NavMenu.VisibleTo(admin).Count);
    }

    private static RenderFragment Columns => builder =>
    {
        builder.OpenComponent<DataColumn<Row>>(0);
        builder.AddComponentParameter(1, nameof(DataColumn<Row>.Title), "Название");
        builder.AddComponentParameter(2, nameof(DataColumn<Row>.ChildContent), (RenderFragment<Row>)(row => b => b.AddContent(0, row.Name)));
        builder.CloseComponent();

        builder.OpenComponent<DataColumn<Row>>(3);
        builder.AddComponentParameter(4, nameof(DataColumn<Row>.Title), "Дальность");
        builder.AddComponentParameter(5, nameof(DataColumn<Row>.SortBy), (Func<Row, object?>)(row => row.Range));
        builder.AddComponentParameter(6, nameof(DataColumn<Row>.SortKey), "range");
        builder.AddComponentParameter(7, nameof(DataColumn<Row>.ChildContent), (RenderFragment<Row>)(row => b => b.AddContent(0, row.Range)));
        builder.CloseComponent();

        builder.OpenComponent<DataColumn<Row>>(8);
        builder.AddComponentParameter(9, nameof(DataColumn<Row>.Title), "Имя");
        builder.AddComponentParameter(10, nameof(DataColumn<Row>.SortBy), (Func<Row, object?>)(row => row.Name));
        builder.CloseComponent();
    };

    private static string[] NamesOf(IRenderedComponent<DataTable<Row>> cut) =>
        cut.FindAll("tbody tr").Select(tr => tr.QuerySelector("td")!.TextContent).ToArray();
}
