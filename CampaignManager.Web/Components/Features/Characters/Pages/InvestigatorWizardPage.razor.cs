using CampaignManager.Web.Components.Features.Campaigns.Models;
using CampaignManager.Web.Components.Features.Campaigns.Services;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Characters.Services;
using CampaignManager.Web.Components.Features.Scenarios.Services;
using CampaignManager.Web.Components.Shared.Model;
using CampaignManager.Web.Model;
using Microsoft.AspNetCore.Components;

namespace CampaignManager.Web.Components.Features.Characters.Pages;

/// <summary>
///     Помощник создания сыщика: пять шагов главы 3 «Создание сыщиков» («Зов Ктулху» 7e, стр. 26–46)
///     плюс выбор способа генерации и итоговая сводка.
/// </summary>
public partial class InvestigatorWizardPage
{
    [Inject] private CharacterService CharacterService { get; set; } = default!;
    [Inject] private CampaignService CampaignService { get; set; } = default!;
    [Inject] private OccupationService OccupationService { get; set; } = default!;
    [Inject] private ScenarioService ScenarioService { get; set; } = default!;
    [Inject] private InvestigatorFactory Factory { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<InvestigatorWizardPage> Logger { get; set; } = default!;

    /// <summary>Кампания, в которой игрок заводит сыщика.</summary>
    [SupplyParameterFromQuery(Name = "campaignId")]
    public Guid? CampaignId { get; set; }

    /// <summary>Сценарий, куда преген или НПС попадёт сразу после создания.</summary>
    [SupplyParameterFromQuery(Name = "scenarioId")]
    public Guid? ScenarioId { get; set; }

    /// <summary>Вид листа: <c>npc</c>, <c>pregen</c> или пусто — персонаж игрока.</summary>
    [SupplyParameterFromQuery(Name = "kind")]
    public string? Kind { get; set; }

    /// <summary>Шаг помощника: заголовок, подсказка и страница правил, откуда он взят.</summary>
    public sealed record WizardStep(string Title, string Hint, string Reference);

    public static readonly IReadOnlyList<WizardStep> Steps =
    [
        new("Способ", "Выберите, как определять характеристики, и укажите возраст сыщика: возрастные модификаторы применяются к броскам сразу.", "стр. 30, 45–46"),
        new("Характеристики", "Восемь характеристик, возрастные модификаторы и Удача. Вторичные атрибуты считаются автоматически.", "стр. 28–31"),
        new("Род занятий", "Профессия задаёт список навыков и число очков профессиональных навыков.", "стр. 31, 38–39"),
        new("Навыки", "Распределите очки профессии между её навыками и очки личного интереса (ИНТ × 2) — между любыми, кроме Мифов Ктулху.", "стр. 34"),
        new("Биография", "Заполните хотя бы три графы из шести и отметьте одну как ключевую связь.", "стр. 37, 40–43"),
        new("Снаряжение", "Достаток считается по навыку Средства; допишите вещи, которые сыщик носит с собой.", "стр. 44–45"),
        new("Итог", "Проверьте лист целиком — после создания его можно править как обычно.", "стр. 47")
    ];

    private readonly NotificationModel _notification = new();
    private InvestigatorDraft _draft = new();
    private List<Occupation> _occupations = [];
    private SkillsModel _catalog = new();
    private List<OccupationSlot> _slots = [];
    private CampaignPlayer? _campaignPlayer;
    private bool _eraLocked;
    private bool _loading = true;
    private bool _isBusy;

    /// <summary>
    ///     Точка, за которую Blazor держит незаконченного сыщика (см. корневой CLAUDE.md,
    ///     «Circuit State Persistence»): страница после паузы собирается заново, а восстанавливается
    ///     только помеченное этим атрибутом. Без него уход на другую вкладку стирал бы всю работу.
    /// </summary>
    [PersistentState]
    public InvestigatorDraft? PersistedDraft
    {
        get => _draft;
        set => _restoredDraft = value;
    }

    private InvestigatorDraft? _restoredDraft;

    private WizardStep CurrentStep => Steps[Math.Clamp(_draft.StepIndex, 0, Steps.Count - 1)];

    private Occupation? SelectedOccupation =>
        _occupations.FirstOrDefault(o => o.Id == _draft.OccupationId);

    private string EraLabel => _draft.ModernEra ? "Современность" : "1920-е";

    private CharacterKind CreationKind => Kind switch
    {
        "npc" => CharacterKind.Npc,
        "pregen" => CharacterKind.Pregen,
        _ => CharacterKind.PlayerCharacter
    };

    protected override async Task OnInitializedAsync()
    {
        try
        {
            if (_restoredDraft is not null)
            {
                _draft = _restoredDraft;
                _restoredDraft = null;
            }

            if (CampaignId is { } campaignId)
            {
                var campaign = await CampaignService.GetCampaignWithCharactersAsync(campaignId);
                if (campaign?.Era is { } era)
                {
                    _draft.ModernEra = era.HasFlag(Eras.Modern) && !era.HasFlag(Eras.Classic);
                    _eraLocked = true;
                }

                if (CreationKind is CharacterKind.PlayerCharacter)
                    _campaignPlayer = await CampaignService.GetCampaignPlayerAsync(campaignId);
            }

            _occupations = await OccupationService.GetAllOccupationsAsync();
            await ReloadCatalogAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Не удалось открыть помощник создания сыщика");
            ShowNotification($"Ошибка при загрузке справочников: {ex.Message}", "error");
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Справочник навыков зависит от эпохи, поэтому перечитывается при её смене.</summary>
    private async Task ReloadCatalogAsync()
    {
        _catalog = await Factory.BuildCatalogAsync(_draft.ModernEra);
        RebuildSlots();
    }

    private void RebuildSlots()
    {
        var occupation = SelectedOccupation;
        var allSkills = _catalog.SkillGroups.SelectMany(g => g.Skills).ToList();

        _slots = occupation is null
            ? []
            : OccupationSkillResolver.BuildSlots(occupation, allSkills);

        while (_draft.SlotChoices.Count < _slots.Count)
            _draft.SlotChoices.Add("");
        if (_draft.SlotChoices.Count > _slots.Count)
            _draft.SlotChoices.RemoveRange(_slots.Count, _draft.SlotChoices.Count - _slots.Count);
    }

    private void OnDraftChanged() => StateHasChanged();

    /// <summary>
    ///     Слоты профессии пересобираются здесь, а не в шаге: их же читает следующий шаг,
    ///     и индекс выбора игрока обязан совпадать у обоих.
    /// </summary>
    private void OnOccupationChanged()
    {
        RebuildSlots();
        StateHasChanged();
    }

    private async Task Next()
    {
        if (ValidationMessage is not null)
            return;

        if (_draft.StepIndex == 0)
            await ReloadCatalogAsync();

        _draft.StepIndex = Math.Min(_draft.StepIndex + 1, Steps.Count - 1);
        ClearNotification();
    }

    private void Back()
    {
        _draft.StepIndex = Math.Max(_draft.StepIndex - 1, 0);
        ClearNotification();
    }

    /// <summary>Назад по шагам можно прыгать свободно, вперёд — только через проверку текущего шага.</summary>
    private void GoTo(int index)
    {
        if (index <= _draft.StepIndex)
            _draft.StepIndex = index;
    }

    /// <summary>Что мешает уйти с текущего шага; null — всё заполнено.</summary>
    private string? ValidationMessage => _draft.StepIndex switch
    {
        0 => _draft.Age is < InvestigatorCreationRules.MinAge or > InvestigatorCreationRules.MaxAge
            ? $"Возраст сыщика — от {InvestigatorCreationRules.MinAge} до {InvestigatorCreationRules.MaxAge} лет"
            : null,
        1 => ValidateCharacteristics(),
        2 => ValidateOccupation(),
        3 => ValidateSkills(),
        4 => string.IsNullOrWhiteSpace(_draft.Info.Name) ? "Впишите имя сыщика" : null,
        _ => null
    };

    private string? ValidateCharacteristics()
    {
        var band = InvestigatorCreationRules.BandFor(_draft.Age);

        if (!_draft.CharacteristicsFilled)
            return "Определите все восемь характеристик";

        if (_draft.Method is CreationMethod.PointBuy)
        {
            var spent = _draft.Rolled.Values.Sum();
            if (spent != InvestigatorCreationRules.PointBuyBudget)
                return $"Распределите ровно {InvestigatorCreationRules.PointBuyBudget} пунктов (сейчас {spent})";
        }

        if (_draft.RemainingAgePenalty(band) != 0)
            return $"Распределите вычет за возраст: осталось {_draft.RemainingAgePenalty(band)}";

        if (_draft.ExtraClass && _draft.ExtraClassPool is null)
            return "Бросьте 1d10 для сыщика экстра-класса";

        if (_draft.RemainingExtraClass() != 0)
            return $"Распределите пункты сыщика экстра-класса: осталось {_draft.RemainingExtraClass()}";

        if (_draft.EducationChecks.Count != band.EducationChecks)
            return $"Выполните проверки улучшения ОБР: {_draft.EducationChecks.Count} из {band.EducationChecks}";

        return _draft.Luck <= 0 ? "Определите Удачу (3d6 × 5)" : null;
    }

    private string? ValidateOccupation()
    {
        if (SelectedOccupation is null)
            return "Выберите род занятий";

        if (RequiresFormulaChoice && _draft.FormulaChoice is null)
            return "Выберите характеристику в формуле очков профессии";

        for (var i = 0; i < _slots.Count; i++)
        {
            if (!_slots[i].NeedsChoice)
                continue;

            if (i >= _draft.SlotChoices.Count || string.IsNullOrWhiteSpace(_draft.SlotChoices[i]))
                return $"Выберите навык для слота «{_slots[i].Label}»";
        }

        return null;
    }

    private bool RequiresFormulaChoice =>
        SelectedOccupation is { } occupation &&
        InvestigatorFactory.FormulaChoices(occupation.SkillPointFormula).Count > 0;

    private string? ValidateSkills()
    {
        var occupation = SelectedOccupation;
        if (occupation is null)
            return "Выберите род занятий";

        if (_draft.CreditRating < occupation.CreditRatingMin || _draft.CreditRating > occupation.CreditRatingMax)
            return $"Средства должны быть в пределах профессии: {occupation.CreditRatingMin}–{occupation.CreditRatingMax}%";

        // Блиц-метод раздаёт готовые значения вместо очков, поэтому бюджет ему не считают (стр. 46).
        if (_draft.Method is CreationMethod.Blitz)
            return ValidateBlitzSkills();

        var characteristics = InvestigatorFactory.BuildCharacteristics(_draft);
        var occupationPoints = InvestigatorFactory.OccupationPointsFor(occupation, characteristics, _draft.FormulaChoice);
        var personalPoints = InvestigatorFactory.PersonalPointsFor(characteristics);

        if (_draft.SpentOccupationPoints > occupationPoints)
            return $"Очков профессии потрачено больше, чем есть: {_draft.SpentOccupationPoints} из {occupationPoints}";

        if (_draft.SpentPersonalPoints > personalPoints)
            return $"Очков личного интереса потрачено больше, чем есть: {_draft.SpentPersonalPoints} из {personalPoints}";

        return null;
    }

    /// <summary>
    ///     Блиц-метод: каждый профессиональный навык получает своё значение из набора, а четыре
    ///     личных навыка — по +20 (стр. 46).
    /// </summary>
    private string? ValidateBlitzSkills()
    {
        var required = InvestigatorFactory.ChosenOccupationSkills(_slots, _draft)
            .Where(name => name != OccupationSkillResolver.CreditRatingSkill)
            .ToList();

        var missing = required.Count(name => !_draft.BlitzValues.ContainsKey(name));
        if (missing > 0)
            return $"Раздайте блиц-значения профессиональным навыкам: осталось {missing}";

        var chosen = _draft.BlitzPersonalSkills.Count(name => !string.IsNullOrWhiteSpace(name));
        return chosen < InvestigatorCreationRules.BlitzPersonalSkillCount
            ? $"Выберите личные навыки: {chosen} из {InvestigatorCreationRules.BlitzPersonalSkillCount}"
            : null;
    }

    private async Task CreateInvestigatorAsync()
    {
        if (_isBusy)
            return;

        _isBusy = true;
        ClearNotification();

        try
        {
            var character = Factory.Build(_draft, _catalog, SelectedOccupation);
            character.PersonalInfo.PlayerName = CreationKind is CharacterKind.Npc
                ? "НПС"
                : _campaignPlayer?.PlayerName ?? _draft.Info.PlayerName;

            var created = await CharacterService.CreateCharacterAsync(
                character,
                CreationKind,
                _campaignPlayer?.Id,
                CreationKind is CharacterKind.Npc ? CampaignId : null,
                CreationKind is CharacterKind.Pregen ? ScenarioId : null);

            // НПС попадает в сценарий связью, а не копией листа — см. Scenarios/CLAUDE.md.
            if (CreationKind is CharacterKind.Npc && ScenarioId is { } scenarioId)
                await ScenarioService.AddNpcToScenarioAsync(scenarioId, created.Id);

            NavigationManager.NavigateTo($"/character/{created.Id}", replace: true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Не удалось создать сыщика из помощника");
            ShowNotification($"Ошибка при создании: {ex.Message}", "error");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void Cancel()
    {
        if (CampaignId is { } campaignId && CreationKind is CharacterKind.PlayerCharacter)
        {
            NavigationManager.NavigateTo($"/campaigns/{campaignId}");
            return;
        }

        if (ScenarioId is { } scenarioId)
        {
            NavigationManager.NavigateTo($"/scenarios/{scenarioId}");
            return;
        }

        NavigationManager.NavigateTo("/");
    }

    private void ShowNotification(string message, string type)
    {
        _notification.Message = message;
        _notification.Type = type;
    }

    private void ClearNotification() => _notification.Message = null;
}
