using Bunit;
using CampaignManager.Contracts.Characters;
using CampaignManager.Contracts.Files;
using CampaignManager.Core;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.UI.Characters;
using CampaignManager.UI.Checks;
using CampaignManager.UI.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// UX-1 (группа U2): корзина строки листа убирает сразу, а тост «Отменить» возвращает строку на место; кнопка «Лечение»
/// не показывается, когда лечить нечего; диалог проверки несёт справку и не повторяет итог трижды.
/// </summary>
public sealed class SheetUx1Tests : KitContext
{
    private static readonly SkillDefinition Spot = new(Guid.NewGuid(), "Внимание")
    {
        Code = "skill.spot-hidden", BaseValue = 25, Category = SkillCategory.InformationGathering,
    };

    private static readonly SkillCatalog Catalog = new([Spot]);

    private int _changed;

    public SheetUx1Tests() => Services.AddSingleton(Fake.Of<IFilesApi>(new()));

    private IRenderedComponent<CascadingValue<SheetContext>> Render<TPanel>(CharacterSheet sheet) where TPanel : IComponent
    {
        var context = new SheetContext(new CharacterDto { Sheet = sheet, CanEdit = true }, SkillCatalogs.From([]), [], null!,
            () => _changed++, _ => { });

        return Render<CascadingValue<SheetContext>>(p => p.Add(c => c.Value, context).AddChildContent<TPanel>());
    }

    [Fact]
    public async Task Removed_weapon_returns_to_its_place_from_the_undo_toast()
    {
        var sheet = new CharacterSheet
        {
            Weapons = [new SheetWeapon { Name = "Кольт" }, new SheetWeapon { Name = "Нож" }, new SheetWeapon { Name = "Топор" }],
        };
        var cut = Render<WeaponsPanel>(sheet);
        var toasts = Services.GetRequiredService<ToastService>();

        cut.FindAll("button[aria-label^='Убрать']")[1].Click();

        Assert.Equal(["Кольт", "Топор"], sheet.Weapons.Select(w => w.Name));
        var toast = Assert.Single(toasts.Messages);
        Assert.Contains("Убрано", toast.Message);
        Assert.Contains("Нож", toast.Message);

        await toasts.RunActionAsync(toast.Id);

        Assert.Equal(["Кольт", "Нож", "Топор"], sheet.Weapons.Select(w => w.Name));
        Assert.Equal(2, _changed);
    }

    [Fact]
    public async Task Removed_equipment_item_can_be_undone()
    {
        var sheet = new CharacterSheet { Equipment = [new EquipmentItem { Name = "Свечи" }] };
        var cut = Render<EquipmentPanel>(sheet);
        var toasts = Services.GetRequiredService<ToastService>();

        cut.Find("button[aria-label^='Убрать']").Click();
        Assert.Empty(sheet.Equipment);

        await toasts.RunActionAsync(Assert.Single(toasts.Messages).Id);
        Assert.Equal("Свечи", Assert.Single(sheet.Equipment).Name);
    }

    [Fact]
    public void Healing_button_is_hidden_until_there_is_something_to_heal()
    {
        var healthy = new CharacterSheet { Current = new CurrentValues { HitPoints = 12 } };
        healthy.Overrides.MaxHitPoints = 12;
        Assert.Empty(Render<PersonalPanel>(healthy).FindAll("[data-testid='open-recovery']"));

        var hurt = new CharacterSheet { Current = new CurrentValues { HitPoints = 5 } };
        hurt.Overrides.MaxHitPoints = 12;
        Assert.Single(Render<PersonalPanel>(hurt).FindAll("[data-testid='open-recovery']"));
    }

    [Fact]
    public void Check_panel_names_the_skill_offers_help_and_says_the_result_once()
    {
        var sheet = new CharacterSheet
        {
            Characteristics = new Characteristics { Str = 50, Con = 50, Siz = 50, Dex = 50, App = 50, Int = 50, Pow = 50, Edu = 50 },
            Skills = [new SheetSkill { SkillId = Spot.Id, Value = 60 }],
        };
        var cut = Render<SkillCheckPanel>(p => p
            .Add(c => c.Sheet, sheet)
            .Add(c => c.Catalog, Catalog)
            .Add(c => c.InitialKey, $"skill:{Spot.Id}")
            .Add(c => c.Help, key => b => b.AddContent(0, $"справка {key[..5]}")));

        Assert.Contains("Внимание", cut.Find("[data-testid='check-subject-head']").TextContent);
        Assert.Contains("60%", cut.Find("[data-testid='check-subject-head']").TextContent);
        Assert.Contains("справка skill", cut.Find("[data-testid='check-help']").TextContent);

        cut.Find("[data-testid='check-roll-first'] input").Change("99"); // провал: значок уровня — единственный итог
        Assert.DoesNotContain("Проверка провалена", cut.Markup);
        Assert.Equal("Провал", cut.Find("[data-testid='check-level']").TextContent.Trim());
    }

    [Fact]
    public void Check_panel_without_a_skill_has_no_help_row()
    {
        var sheet = new CharacterSheet { Characteristics = new Characteristics { Str = 50 } };
        var cut = Render<SkillCheckPanel>(p => p
            .Add(c => c.Sheet, sheet)
            .Add(c => c.Catalog, Catalog)
            .Add(c => c.InitialKey, "char:STR")
            .Add(c => c.Help, _ => b => b.AddContent(0, "справка")));

        Assert.Empty(cut.FindAll("[data-testid='check-help']"));
    }
}
