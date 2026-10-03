using Bunit;
using CampaignManager.Contracts.Catalogs;
using CampaignManager.Contracts.Characters;
using CampaignManager.Core.Documents;
using CampaignManager.UI.Shared;
using CampaignManager.Core;
using CampaignManager.Core.Catalogs;
using CampaignManager.Core.Characters;
using CampaignManager.UI.Characters.Creation;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CampaignManager.UI.Tests;

/// <summary>
/// Помощник создания сыщика (ревью g3, F3a): значения уходят в черновик на каждое нажатие, а не при потере фокуса
/// (на iPad касание кнопки фокус с поля не снимает); имя — на первом шаге; аккордеон биографии; специализации свёрнуты
/// под родителем на шаге «Навыки»; «Бросить заново» только у случайного сыщика; производные — тире, пока нет характеристик.
/// </summary>
public sealed class WizardTests : KitContext
{
    private static SkillDefinition Skill(string name, string code, int baseValue = 10, Guid? parent = null) =>
        new(Guid.NewGuid(), name) { Code = code, BaseValue = baseValue, ParentId = parent, Category = SkillCategory.Knowledge };

    private static readonly SkillDefinition Science = Skill("Наука", "skill.science", 1);
    private static readonly SkillDefinition Biology = Skill("Наука (биология)", "skill.science.biology", 1, Science.Id);
    private static readonly SkillDefinition Chemistry = Skill("Наука (химия)", "skill.science.chemistry", 1, Science.Id);
    private static readonly SkillDefinition Listen = Skill("Слух", "skill.listen", 20);
    private static readonly SkillDefinition Library = Skill("Работа в библиотеке", "skill.library-use", 20);
    private static readonly SkillDefinition Credit = Skill("Средства", SkillCodes.CreditRating, 0);

    private static readonly SkillCatalog Catalog = new([Science, Biology, Chemistry, Listen, Library, Credit]);

    private static InvestigatorDraft Filled()
    {
        var draft = new InvestigatorDraft { Age = 25 };
        foreach (var key in Enum.GetValues<Characteristic>())
            draft.SetCharacteristic(key, 60);
        draft.SetLuckRolls([50]);
        draft.EducationChecks.Add(new EducationCheck(90, 60, 4));
        return draft;
    }

    private static CreationPlan PlanOf(InvestigatorDraft draft, OccupationDefinition? occupation = null) => new(draft, Catalog, occupation);

    // ── NumberInput ──────────────────────────────────────────────────────────

    [Fact]
    public void NumberInput_reports_each_typed_number_without_waiting_for_blur()
    {
        var values = new List<int>();
        var cut = Render<NumberInput>(p => p
            .Add(c => c.Value, 25)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<int>(this, v => values.Add(v))));

        cut.Find("input").Input("3");
        cut.Find("input").Input("35");
        cut.Find("input").Input("");
        cut.Find("input").Input("abc");

        Assert.Equal([3, 35], values);
    }

    // ── Шаг «Способ» ─────────────────────────────────────────────────────────

    [Fact]
    public void Method_step_name_goes_to_the_draft_on_every_keystroke_and_unlocks_the_step()
    {
        var draft = new InvestigatorDraft();
        var changed = 0;
        var cut = Render<WizardMethodStep>(p => p
            .Add(c => c.Plan, PlanOf(draft))
            .Add(c => c.OnChanged, EventCallback.Factory.Create(this, () => changed++)));

        Assert.Equal("Впишите имя сыщика", PlanOf(draft).Validate(CreationStep.Method));

        cut.Find("[data-testid=investigator-name]").Input("Артур");

        Assert.Equal("Артур", draft.Personal.Name);
        Assert.Null(PlanOf(draft).Validate(CreationStep.Method));
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Method_step_age_outside_the_book_range_is_not_applied_while_typing()
    {
        var draft = new InvestigatorDraft { Age = 25 };
        var cut = Render<WizardMethodStep>(p => p.Add(c => c.Plan, PlanOf(draft)));

        cut.Find("[data-testid=wizard-age]").Input("3");
        Assert.Equal(25, draft.Age);

        cut.Find("[data-testid=wizard-age]").Input("35");
        Assert.Equal(35, draft.Age);
    }

    [Fact]
    public void Method_step_offers_random_investigator_and_uses_the_dictionary_words()
    {
        var randoms = 0;
        var cut = Render<WizardMethodStep>(p => p
            .Add(c => c.Plan, PlanOf(new InvestigatorDraft()))
            .Add(c => c.Kind, CharacterKind.Pregen)
            .Add(c => c.OnRandom, EventCallback.Factory.Create(this, () => randoms++)));

        cut.Find("[data-testid=create-random]").Click();

        Assert.Equal(1, randoms);
        Assert.Contains("Случайный готовый сыщик", cut.Find("[data-testid=create-random]").TextContent, StringComparison.Ordinal);
        Assert.Contains("Готовый набор", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Блиц", cut.Markup, StringComparison.Ordinal);
    }

    // ── Шаг «Характеристики» ─────────────────────────────────────────────────

    [Fact]
    public void Derived_tiles_show_dashes_until_all_characteristics_are_defined()
    {
        var sheet = new CharacterSheet { Characteristics = Filled().BuildCharacteristics() };
        var derived = DerivedAttributeRules.Compute(sheet, Catalog);

        var empty = Render<DerivedTiles>(p => p.Add(c => c.Derived, derived).Add(c => c.Ready, false));
        Assert.All(empty.FindAll("dd"), dd => Assert.Equal(Terms.None, dd.TextContent.Trim()));

        var ready = Render<DerivedTiles>(p => p.Add(c => c.Derived, derived).Add(c => c.Ready, true));
        Assert.Contains(ready.FindAll("dd"), dd => dd.TextContent.Trim() != Terms.None);
    }

    [Fact]
    public void Derived_tiles_write_the_damage_bonus_dice_in_lowercase()
    {
        var characteristics = new Characteristics { Str = 80, Siz = 80 }; // сумма 160 → +1D4 в таблице I
        var derived = DerivedAttributeRules.Compute(new CharacterSheet { Characteristics = characteristics }, Catalog);

        var cut = Render<DerivedTiles>(p => p.Add(c => c.Derived, derived));

        Assert.Contains("+1d4", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("1D4", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Characteristics_step_education_check_rolls_both_dice_itself()
    {
        // 1d100 = 90 (единицы 0, десятки 9) больше ОБР 60, прибавка 1d10 = 4.
        Dice.Enqueue(0, 9, 4);
        var draft = Filled();
        draft.EducationChecks.Clear();
        var cut = Render<WizardCharacteristicsStep>(p => p.Add(c => c.Plan, PlanOf(draft)));

        cut.Find("[data-testid=edu-check-roll]").Click();

        var check = Assert.Single(draft.EducationChecks);
        Assert.Equal(4, check.Gain);
    }

    // ── Шаг «Навыки» ─────────────────────────────────────────────────────────

    private static OccupationDefinition Scholar() => new(Guid.NewGuid(), "Учёный", SkillPointsFormula.Edu4)
    {
        CreditRatingMin = 9,
        CreditRatingMax = 30,
        Slots = [new OccupationSlotDefinition(OccupationSlotKind.Skill) { SkillId = Library.Id }],
    };

    private IRenderedComponent<WizardSkillsStep> RenderSkills(InvestigatorDraft draft, OccupationDefinition occupation)
    {
        CreationPlan.SelectOccupation(draft, occupation);
        var plan = PlanOf(draft, occupation);
        plan.SyncSlots();
        return Render<WizardSkillsStep>(p => p.Add(c => c.Plan, plan));
    }

    [Fact]
    public void Skills_step_collapses_specializations_under_the_parent_in_the_all_skills_list()
    {
        var cut = RenderSkills(Filled(), Scholar());

        cut.Find("[data-testid=show-all-skills]").Change(true);

        var group = cut.Find("[data-testid=skill-group]");
        Assert.Contains("Наука", group.TextContent, StringComparison.Ordinal);
        Assert.Contains("специализаций: 2", group.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Наука (биология)", cut.Markup, StringComparison.Ordinal);

        group.Click();

        Assert.Contains("Наука (биология)", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Наука (химия)", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Skills_step_credit_rating_is_the_first_row_and_is_taken_from_occupation_points()
    {
        var draft = Filled();
        var cut = RenderSkills(draft, Scholar());

        var first = cut.Find("[data-testid=skills-table] tbody tr[data-testid=credit-row]");
        Assert.Contains("Средства", first.TextContent, StringComparison.Ordinal);
        Assert.Contains("(9–30%)", first.TextContent, StringComparison.Ordinal);

        cut.Find("[data-testid=credit-rating]").Input("20");
        Assert.Equal(20, draft.CreditRating);
        Assert.Equal(20, draft.SpentOccupationPoints);

        // 5 не лежит в пределах профессии 9–30: при наборе (на пути к «25») пропускается.
        cut.Find("[data-testid=credit-rating]").Input("5");
        Assert.Equal(20, draft.CreditRating);
    }

    [Fact]
    public void Skills_step_says_when_a_skill_hits_the_ceiling()
    {
        var draft = Filled();
        var cut = RenderSkills(draft, Scholar());
        var plan = PlanOf(draft, Scholar());
        Assert.NotNull(plan);

        cut.Find("[data-testid=occupation-points]").Input("500");

        Assert.NotNull(cut.Find("[data-testid=skill-ceiling]"));
    }

    [Fact]
    public async Task Skills_step_reset_asks_first_and_only_exists_when_something_is_spent()
    {
        var draft = Filled();
        var cut = RenderSkills(draft, Scholar());
        Assert.Empty(cut.FindAll("[data-testid=reset-points]"));

        cut.Find("[data-testid=occupation-points]").Input("10");
        var dialogs = Services.GetRequiredService<UI.Shared.DialogService>();
        cut.Find("[data-testid=reset-points]").Click();

        Assert.NotNull(dialogs.Current);
        Assert.Equal("Сбросить очки?", dialogs.Current!.Title);
        dialogs.Complete(true);
        await cut.WaitForAssertionAsync(() => Assert.Empty(draft.OccupationPoints));
    }

    // ── Шаг «Биография» ──────────────────────────────────────────────────────

    [Fact]
    public void Biography_step_opens_the_first_empty_section_and_keeps_the_rest_as_one_line()
    {
        var draft = Filled();
        BiographyTables.Find("appearance")!.Write(draft.Biography, "Хмурый");
        var cut = Render<WizardBiographyStep>(p => p.Add(c => c.Plan, PlanOf(draft)));

        Assert.Equal("false", cut.Find("[data-testid=bio-toggle-appearance]").GetAttribute("aria-expanded"));
        Assert.Equal("true", cut.Find("[data-testid=bio-toggle-ideals]").GetAttribute("aria-expanded"));
        Assert.Single(cut.FindAll("textarea"));
        Assert.Contains("Хмурый", cut.Find("[data-testid=bio-section-appearance]").TextContent, StringComparison.Ordinal);

        cut.Find("[data-testid=bio-toggle-traits]").Click();

        Assert.Equal("true", cut.Find("[data-testid=bio-toggle-traits]").GetAttribute("aria-expanded"));
        Assert.Equal("false", cut.Find("[data-testid=bio-toggle-ideals]").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Biography_step_typed_text_and_key_connection_go_to_the_draft_at_once()
    {
        var draft = Filled();
        var cut = Render<WizardBiographyStep>(p => p.Add(c => c.Plan, PlanOf(draft)));

        cut.Find("textarea").Input("Верит в судьбу");
        cut.Find("[data-testid=key-connection-ideals]").Change(true);

        Assert.Equal("Верит в судьбу", draft.Biography.Appearance);
        Assert.Equal("ideals", draft.KeyConnectionSection);
    }

    [Fact]
    public void Biography_step_age_is_declined()
    {
        var draft = Filled();
        draft.SetAge(32);
        var cut = Render<WizardBiographyStep>(p => p.Add(c => c.Plan, PlanOf(draft)));

        Assert.Contains("32 года", cut.Markup, StringComparison.Ordinal);
    }

    // ── Итог ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Summary_offers_reroll_only_for_a_random_draft_and_writes_assets_with_a_dollar_sign()
    {
        var draft = Filled();
        draft.Personal.Name = "Артур Морган";
        draft.CreditRating = 40;

        var manual = Render<WizardSummaryStep>(p => p.Add(c => c.Plan, PlanOf(draft)));
        Assert.Empty(manual.FindAll("[data-testid=summary-reroll]"));
        Assert.DoesNotMatch(@"Активы:\s*\d", manual.Markup);
        Assert.Contains("Активы:", manual.Markup, StringComparison.Ordinal);
        Assert.Matches(@"Активы:</span>\s*\$\d", manual.Markup);

        draft.IsRandom = true;
        var rerolls = 0;
        var random = Render<WizardSummaryStep>(p => p
            .Add(c => c.Plan, PlanOf(draft))
            .Add(c => c.OnRandom, EventCallback.Factory.Create(this, () => rerolls++)));
        random.Find("[data-testid=summary-reroll]").Click();
        Assert.Equal(1, rerolls);
    }

    // ── Снаряжение ───────────────────────────────────────────────────────────

    [Fact]
    public void Gear_step_adds_a_catalog_weapon_to_the_draft_and_removes_it()
    {
        var weapon = new WeaponDto { Id = Guid.NewGuid(), Name = "Автоматический пистолет 45-го калибра", Damage = "1D10+2", Eras = [Era.Classic] };
        Services.AddSingleton(Fake.Of<ICatalogApi<WeaponDto>>(new()
        {
            [nameof(ICatalogApi<WeaponDto>.ListAsync)] = _ => Task.FromResult(new CatalogList<WeaponDto>([weapon], true)),
        }));
        var draft = Filled();
        var cut = Render<WizardGearStep>(p => p.Add(c => c.Plan, PlanOf(draft)));
        Assert.Contains("Нет оружия.", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid=add-weapon]").Click();
        cut.Find("[data-testid=catalog-search-input]").Input("пистолет");
        cut.Find(".catalog-search-row").Click();

        var added = Assert.Single(draft.Weapons);
        Assert.Equal(weapon.Id, added.CatalogWeaponId);
        Assert.Equal("Автоматический пистолет 45-го калибра", added.Name);
        Assert.Contains("1d10 + 2", cut.Markup, StringComparison.Ordinal);

        cut.Find("button[aria-label^='Убрать оружие']").Click();
        Assert.Empty(draft.Weapons);
    }

    // ── Страница помощника ───────────────────────────────────────────────────

    private const string DraftKey = "cm.investigator-draft:Player:library";

    private void SetUpPage(InvestigatorDraft? stored = null, OccupationDefinition? occupation = null)
    {
        var skills = Catalog.Skills.Select(s => new SkillDto
        {
            Id = s.Id, Name = s.Name, ParentId = s.ParentId, BaseValue = s.BaseValue, Code = s.Code, Category = s.Category,
        }).ToList();
        var occupations = occupation is null
            ? []
            : new List<OccupationDto>
            {
                new()
                {
                    Id = occupation.Id, Name = occupation.Name, SkillPointsFormula = occupation.Formula,
                    CreditRatingMin = occupation.CreditRatingMin, CreditRatingMax = occupation.CreditRatingMax,
                    Slots = [.. occupation.Slots.Select(sl => new OccupationSlotDto { Kind = sl.Kind, SkillId = sl.SkillId })],
                },
            };
        Services.AddSingleton(Fake.Of<ICharactersApi>(new()
        {
            [nameof(ICharactersApi.GetCreationContextAsync)] = _ => Task.FromResult(new CreationContextDto { Kind = CharacterKind.Player, CanCreate = true, CampaignName = "Тест" }),
        }));
        Services.AddSingleton(Fake.Of<ICatalogApi<SkillDto>>(new()
        {
            [nameof(ICatalogApi<SkillDto>.ListAsync)] = _ => Task.FromResult(new CatalogList<SkillDto>(skills, false)),
        }));
        Services.AddSingleton(Fake.Of<ICatalogApi<OccupationDto>>(new()
        {
            [nameof(ICatalogApi<OccupationDto>.ListAsync)] = _ => Task.FromResult(new CatalogList<OccupationDto>(occupations, false)),
        }));
        if (stored is not null)
            JSInterop.Setup<string?>("localStorage.getItem", DraftKey).SetResult(CmJson.Serialize(stored));
    }

    [Fact]
    public async Task Exit_with_a_started_draft_says_it_was_saved_and_without_one_is_silent()
    {
        SetUpPage();
        var toasts = Services.GetRequiredService<ToastService>();
        var cut = Render<InvestigatorWizardPage>();
        await cut.WaitForStateAsync(() => cut.FindAll("[data-testid=wizard-cancel]").Count > 0);

        cut.Find("[data-testid=wizard-cancel]").Click();
        Assert.Empty(toasts.Messages);

        cut.Find("[data-testid=investigator-name]").Input("Артур");
        cut.Find("[data-testid=wizard-cancel]").Click();

        Assert.Equal("Черновик сохранён — продолжить можно с главной.", Assert.Single(toasts.Messages).Message);
    }

    [Fact]
    public async Task Unspent_points_ask_before_leaving_the_skills_step_and_the_main_answer_is_to_go_back()
    {
        var occupation = Scholar();
        var draft = Filled();
        draft.Personal.Name = "Артур";
        CreationPlan.SelectOccupation(draft, occupation);
        draft.StepIndex = (int)CreationStep.Skills;
        SetUpPage(draft, occupation);

        var cut = Render<InvestigatorWizardPage>();
        await cut.WaitForStateAsync(() => cut.FindAll("[data-testid=wizard-next]").Count > 0);
        Assert.Contains("Не вложено очков:", cut.Find("[data-testid=wizard-hint]").TextContent, StringComparison.Ordinal);

        cut.Find("[data-testid=wizard-next]").Click();
        cut.WaitForAssertion(() => Assert.Contains("Осталось", cut.Find("[data-testid=unspent-text]").TextContent, StringComparison.Ordinal));

        cut.Find("[data-testid=unspent-stay]").Click();
        Assert.Empty(cut.FindAll("[data-testid=unspent-text]"));
        Assert.NotNull(cut.Find("[data-testid=wizard-step-3]").GetAttribute("aria-current"));

        cut.Find("[data-testid=wizard-next]").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid=unspent-go]"));
        cut.Find("[data-testid=unspent-go]").Click();
        await cut.WaitForStateAsync(() => cut.FindAll("[data-testid=wizard-Biography]").Count > 0);
        Assert.Empty(cut.FindAll("[data-testid=unspent-text]"));
    }

    [Fact]
    public async Task Stepper_names_only_the_current_step_and_the_footer_has_no_back_on_the_first_step()
    {
        SetUpPage();
        var cut = Render<InvestigatorWizardPage>();
        await cut.WaitForStateAsync(() => cut.FindAll("[data-testid=wizard-steps]").Count > 0);

        Assert.Empty(cut.FindAll("[data-testid=wizard-back]"));
        var current = cut.Find("[data-testid=wizard-steps] [aria-current=step]");
        Assert.Contains("Способ", current.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Характеристики", cut.Find("[data-testid=wizard-steps]").TextContent, StringComparison.Ordinal);
        Assert.Contains("Впишите имя сыщика", cut.Find("[data-testid=wizard-validation]").TextContent, StringComparison.Ordinal);
    }
}
