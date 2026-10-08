using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Characters;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.UI.Characters;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Шапка листа сыщика (g2 0.1, 0.2, 0.6): «Сохранить» только пока есть несохранённое и не главная в «Игре», переключатель
/// режимов стоит последним в действиях (не двигается, когда появляется кнопка), заголовок для телефона — только имя.
/// </summary>
public sealed class CharacterPageTests : KitContext
{
    private static readonly Guid Id = Guid.Parse("0199b000-0000-7000-8000-0000000000aa");

    private int _saves;

    private CharacterDto Arrange(CharacterKind kind = CharacterKind.Player, bool canEdit = true)
    {
        var character = new CharacterDto
        {
            Id = Id,
            Kind = kind,
            CanEdit = canEdit,
            Version = 1,
            Sheet = new CharacterSheet
            {
                Personal = new PersonalInfo { Name = "Элизабет Миллер" },
                Characteristics = new Characteristics { Str = 50, Con = 50, Siz = 50, Dex = 50, App = 50, Int = 60, Pow = 50, Edu = 60 },
                Current = new CurrentValues { HitPoints = 10, MagicPoints = 10, Sanity = 50, Luck = 40 },
            },
        };
        Services.AddSingleton(Fake.Of<ICharactersApi>(new()
        {
            [nameof(ICharactersApi.GetAsync)] = _ => Task.FromResult(character),
            [nameof(ICharactersApi.GetPartyAsync)] = _ => Task.FromResult<IReadOnlyList<PartyMemberDto>>([]),
            [nameof(ICharactersApi.SaveSheetAsync)] = args =>
            {
                _saves++;
                return Task.FromResult(new CharacterSavedDto((uint)args![2]! + 1, DateTimeOffset.UnixEpoch));
            },
        }));
        Services.AddSingleton(Fake.Of<ICatalogApi<SkillDto>>(new()
        {
            [nameof(ICatalogApi<SkillDto>.ListAsync)] = _ => Task.FromResult(new CatalogList<SkillDto>([], false)),
        }));
        Services.AddSingleton(Fake.Of<ICatalogApi<WeaponDto>>(new()));
        Services.AddSingleton(Fake.Of<ICatalogApi<SpellDto>>(new()));
        Services.AddSingleton(Fake.Of<ICatalogApi<BookDto>>(new()));
        Services.AddSingleton(Fake.Of<ICatalogApi<CreatureDto>>(new()));
        Services.AddSingleton(Fake.Of<ICatalogApi<ItemDto>>(new()));
        Services.AddSingleton(Fake.Of<CampaignManager.Contracts.Files.IFilesApi>(new()));
        Services.AddSingleton(Fake.Of<CampaignManager.Contracts.Campaigns.ICampaignsApi>(new()));
        return character;
    }

    private IRenderedComponent<CharacterPage> Open(string query = "")
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/character/{Id}{query}");
        return Render<CharacterPage>(p => p.Add(c => c.CharacterId, Id));
    }

    [Fact]
    public void Save_button_appears_only_with_unsaved_changes_and_is_not_primary_in_play_mode()
    {
        Arrange();
        var cut = Open();
        cut.WaitForElement("[data-testid='play-sheet']");

        Assert.Empty(cut.FindAll("[data-testid='save-sheet']"));

        cut.Find("[data-testid='play-hp-minus']").Click();

        var save = cut.Find("[data-testid='save-sheet']");
        Assert.Contains("cm-btn-secondary", save.ClassName);
        Assert.Contains("Есть правки", cut.Find("[data-testid='autosave-status']").TextContent);

        save.Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='save-sheet']")));
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void Save_button_is_primary_in_sheet_mode()
    {
        Arrange();
        var cut = Open("?mode=sheet");
        cut.WaitForElement("[data-testid='nav-personal']");

        cut.Find("[data-testid='sheet-status']"); // секция «Личные данные» нарисована
        // change от любого поля листа всплывает до div.sheet — страница помечает правку несохранённой
        cut.Find("div.sheet").TriggerEvent("onchange", new ChangeEventArgs { Value = "1" });

        Assert.Contains("cm-btn-primary", cut.Find("[data-testid='save-sheet']").ClassName);
    }

    [Fact]
    public void Mode_switch_is_the_last_action_so_it_does_not_move_when_save_appears()
    {
        Arrange();
        var cut = Open();
        cut.WaitForElement("[data-testid='play-sheet']");
        cut.Find("[data-testid='play-hp-minus']").Click();

        var actions = cut.Find(".cm-topbar-actions");
        var children = actions.Children.Select(e => e.ClassName ?? "").ToList();
        var save = children.FindIndex(c => c.Contains("cm-btn"));
        var mode = children.FindIndex(c => c.Contains("sheet-mode"));
        Assert.True(save >= 0 && mode > save, $"«Сохранить» стоит левее переключателя: {string.Join(" | ", children)}");

        // подписи у обоих сегментов в разметке: на телефоне CSS оставляет видимой только активную
        Assert.Equal("ИграЛист", string.Concat(cut.FindAll("[data-testid='sheet-mode'] span").Select(s => s.TextContent)));
    }

    [Fact]
    public void Player_sheet_has_a_short_title_for_the_phone_but_an_npc_keeps_the_full_one()
    {
        Arrange();
        var player = Open();
        player.WaitForElement("[data-testid='play-sheet']");
        var title = player.Find(".cm-topbar-title");
        Assert.Equal("Лист сыщика: Элизабет Миллер", title.QuerySelector(".max-md\\:hidden")?.TextContent);
        Assert.Equal("Элизабет Миллер", title.QuerySelector(".md\\:hidden")?.TextContent);
    }

    [Fact]
    public void Npc_title_stays_full()
    {
        Arrange(CharacterKind.Npc);
        var cut = Open();
        cut.WaitForElement("[data-testid='play-sheet']");

        var title = cut.Find(".cm-topbar-title");
        Assert.Equal("НПС: Элизабет Миллер", title.TextContent);
        Assert.Empty(title.QuerySelectorAll("span"));
    }

    [Fact]
    public void Read_only_sheet_has_no_save_state_or_button()
    {
        Arrange(canEdit: false);
        var cut = Open();
        cut.WaitForElement("[data-testid='play-sheet']");

        Assert.Empty(cut.FindAll("[data-testid='save-sheet']"));
        Assert.Empty(cut.FindAll("[data-testid='autosave-status']"));
    }

    /// <summary>
    /// «Проверка ИНТ» из тревоги Рассудка: когда окно закрыли после броска, исход отмечается сам (g2 1.7) — успех даёт
    /// безумие и положенный приступ, провал — снимает тревогу.
    /// </summary>
    [Theory]
    [InlineData("80", false)] // ИНТ 60: 80 — провал
    [InlineData("20", true)]
    public void Int_check_from_the_prompt_marks_the_outcome_when_the_window_is_closed(string roll, bool passed)
    {
        var character = Arrange();
        character.Sheet.Condition.LastSanityLoss = 6;
        var cut = Open();
        cut.WaitForElement("[data-testid='int-check-prompt']");

        cut.Find("[data-testid='int-check']").Click();
        cut.Find("[data-testid='check-roll-first'] input").Change(roll);
        cut.FindAll("button").Last(b => b.TextContent.Trim() == "Закрыть").Click();

        Assert.Empty(cut.FindAll("[data-testid='int-check-prompt']"));
        Assert.Equal(passed, character.Sheet.Condition.TemporaryInsanity);
        Assert.Equal(passed ? 1 : 0, cut.FindAll("[data-testid='bout-due']").Count);
    }

    /// <summary>Обычная проверка ИНТ из плитки характеристики тревогу не закрывает: флаг ставит только кнопка тревоги.</summary>
    [Fact]
    public void A_plain_int_check_does_not_resolve_the_prompt()
    {
        var character = Arrange();
        character.Sheet.Condition.LastSanityLoss = 6;
        var cut = Open();
        cut.WaitForElement("[data-testid='int-check-prompt']");

        cut.Find("[data-testid='play-char-INT']").Click();
        cut.Find("[data-testid='check-roll-first'] input").Change("80");
        cut.FindAll("button").Last(b => b.TextContent.Trim() == "Закрыть").Click();

        Assert.Single(cut.FindAll("[data-testid='int-check-prompt']"));
        Assert.False(character.Sheet.Condition.TemporaryInsanity);
    }
}
