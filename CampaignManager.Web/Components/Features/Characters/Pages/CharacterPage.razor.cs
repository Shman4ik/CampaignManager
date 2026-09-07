using CampaignManager.Web.Components.Features.Campaigns.Models;
using CampaignManager.Web.Components.Features.Campaigns.Services;
using CampaignManager.Web.Components.Features.Characters.Components;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Characters.Services;
using CampaignManager.Web.Components.Features.Scenarios.Services;
using CampaignManager.Web.Components.Features.Skills.Services;
using CampaignManager.Web.Components.Features.Weapons.Model;
using CampaignManager.Web.Components.Layout.Services;
using CampaignManager.Web.Components.Shared.Model;
using CampaignManager.Web.Model;
using CampaignManager.Web.Utilities.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace CampaignManager.Web.Components.Features.Characters.Pages;

public partial class CharacterPage
{
    [Inject] private CharacterService CharacterService { get; set; } = default!;
    [Inject] private CampaignService CampaignService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private CharacterGenerationService CharacterGenerationService { get; set; } = default!;
    [Inject] private OccupationService OccupationService { get; set; } = default!;
    [Inject] private SkillService SkillService { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
    [Inject] private LlmCharacterValidationService LlmService { get; set; } = default!;
    [Inject] private IdentityService IdentityService { get; set; } = default!;
    [Inject] private LastCharacterService LastCharacterService { get; set; } = default!;
    [Inject] private ScenarioService ScenarioService { get; set; } = default!;

    [Parameter] public Guid? CharacterId { get; set; }

    /// <summary>Кампания из маршрута: создаём лист игрока в этой кампании.</summary>
    [Parameter] public Guid? CampaignId { get; set; }

    /// <summary>Вид создаваемого персонажа из маршрута: <c>npc</c> или <c>pregen</c>.</summary>
    [Parameter] public string? Kind { get; set; }

    /// <summary>Сценарий, куда персонаж попадёт сразу после создания.</summary>
    [SupplyParameterFromQuery(Name = "scenarioId")] public Guid? ScenarioId { get; set; }

    /// <summary>Кампания-владелец нового НПС; пусто — общая библиотека.</summary>
    [SupplyParameterFromQuery(Name = "campaignId")] public Guid? OwnerCampaignId { get; set; }

    /// <summary>Вид, который получит новый лист. «template» оставлен ради старых ссылок.</summary>
    private CharacterKind CreationKind => Kind switch
    {
        "npc" or "template" => CharacterKind.Npc,
        "pregen" => CharacterKind.Pregen,
        _ => CharacterKind.PlayerCharacter
    };

    /// <summary>Вид открытого листа: у существующего берём из базы, у нового — из маршрута.</summary>
    private CharacterKind CurrentKind => CharacterStorageDto?.Kind ?? CreationKind;

    private bool IsNpc => CurrentKind is CharacterKind.Npc;

    private string NewCharacterTitle => CreationKind switch
    {
        CharacterKind.Npc => "Новый НПС",
        CharacterKind.Pregen => "Новый преген",
        _ => "Новый персонаж"
    };

    /// <summary>Выбор владельца показываем только при создании НПС.</summary>
    private bool ShowOwnerPicker => CharacterStorageDto is null && CreationKind is CharacterKind.Npc && _isKeeper;

    private Character? Character { get; set; }
    private CharacterStorageDto? CharacterStorageDto { get; set; }
    private ErrorBoundary? _errorBoundary;
    private bool _isLoading = true;
    private bool _isBusy;
    private readonly NotificationModel _notification = new();
    private CampaignPlayer? CampaignPlayer { get; set; }
    private Eras? CampaignEra { get; set; }
    private CharacterGenerationLog? _generationLog;
    private bool _showGenerationLog = true;
    private bool _showGenerateModal;
    private bool _isKeeper;
    private LlmSuggestionsModal? _llmModal;
    private List<Campaign> _keeperCampaigns = [];
    private string _ownerCampaignId = "";

    // Section visibility state
    private readonly Dictionary<string, bool> _sectionVisibility = new()
    {
        { "personal", true },
        { "skills", true },
        { "combat", true },
        { "equipment", true },
        { "sanity", true },
        { "biography", true }
    };

    // Toggle section visibility
    private void ToggleSectionVisibility(string section)
    {
        if (_sectionVisibility.ContainsKey(section))
        {
            _sectionVisibility[section] = !_sectionVisibility[section];
        }
    }

    private void ToggleGenerationLog() => _showGenerationLog = !_showGenerationLog;

    private void ClearGenerationLog() => _generationLog = null;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _isLoading = true;
            _isKeeper = await IdentityService.IsKeeper();

            if (_isKeeper && CharacterId is null)
            {
                _keeperCampaigns = await CampaignService.GetKeeperCampaignsAsync();
                _ownerCampaignId = OwnerCampaignId?.ToString() ?? "";
            }

            await LoadCharacterDataAsync();
        }
        catch (Exception ex)
        {
            ShowNotification($"Ошибка при инициализации: {ex.Message}", "error");
            Character = await CreateNewCharacterTemplateAsync();
            _errorBoundary?.Recover();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadCharacterDataAsync()
    {
        try
        {
            if (CampaignId.HasValue)
            {
                var campaign = await CampaignService.GetCampaignWithCharactersAsync(CampaignId.Value);
                CampaignEra = campaign?.Era;
            }

            if (CharacterId.HasValue && CharacterId.Value != Guid.Empty)
            {
                CharacterStorageDto = await CharacterService.GetCharacterByIdAsync(CharacterId.Value);
                Character = CharacterStorageDto?.Character;
                if (Character == null)
                {
                    ShowNotification($"Персонаж с ID {CharacterId.Value} не найден. Создан новый шаблон.", "warning");
                    Character = await CreateNewCharacterTemplateAsync();
                    CharacterId = null;
                }
            }
            else
            {
                Character = await CreateNewCharacterTemplateAsync();
            }

            if (CharacterStorageDto?.CampaignPlayerId is not null)
                CampaignPlayer = await CharacterService.GetCampaignPlayerAsync(CharacterStorageDto.CampaignPlayerId.Value);
            else if (CampaignPlayer is null && CampaignId.HasValue && CreationKind is CharacterKind.PlayerCharacter)
                CampaignPlayer = await CampaignService.GetCampaignPlayerAsync(CampaignId.Value);
        }
        catch (Exception ex)
        {
            ShowNotification($"Ошибка при загрузке персонажа: {ex.Message}", "error");
            Character = await CreateNewCharacterTemplateAsync();
        }
    }

    private async Task<Character> CreateNewCharacterTemplateAsync()
    {
        var skills = await SkillService.BuildDefaultSkillsModelAsync(CampaignEra);
        return new Character
        {
            PersonalInfo = new PersonalInfo
            {
                PlayerName = PlayerNameForSheet
            },
            Characteristics = new Characteristics(),
            Skills = skills,
            Backstory = string.Empty,
            Biography = new BiographyInfo(),
            Equipment = new Equipment(),
            Finances = new Finances(),
            Weapons = new List<Weapon>(),
            Notes = string.Empty
        };
    }

    /// <summary>Подпись «чей это лист»: у НПС игрока нет.</summary>
    private string PlayerNameForSheet => CurrentKind switch
    {
        CharacterKind.Npc => "НПС",
        _ => CampaignPlayer?.PlayerName ?? "Unknown"
    };

    private async Task SaveCharacterAsync()
    {
        if (Character == null || _isBusy)
            return;

        _isBusy = true;
        ClearNotification();

        try
        {
            if (string.IsNullOrWhiteSpace(Character.PersonalInfo.Name))
            {
                Character.PersonalInfo.Name = IsNpc ? "Безымянный НПС" : "Безымянный";
                ShowNotification($"Имя {(IsNpc ? "НПС" : "персонажа")} не было указано, установлено '{Character.PersonalInfo.Name}'.", "warning");
            }

            Character.PersonalInfo.PlayerName = PlayerNameForSheet;

            // Существующий лист узнаём по загруженной строке, а не по совпадению
            // идентификаторов: раньше их расхождение молча создавало дубликат.
            if (CharacterStorageDto is not null)
            {
                await CharacterService.UpdateCharacterAsync(Character);
                ShowNotification($"{KindLabel} успешно обновлён!", "success");
                return;
            }

            Guid? ownerCampaignId = CreationKind is CharacterKind.Npc && Guid.TryParse(_ownerCampaignId, out var chosen)
                ? chosen
                : null;

            var created = await CharacterService.CreateCharacterAsync(
                Character,
                CreationKind,
                CampaignPlayer?.Id,
                ownerCampaignId,
                CreationKind is CharacterKind.Pregen ? ScenarioId : null);

            Character.Id = created.Id;
            CharacterId = created.Id;
            CharacterStorageDto = created;

            // НПС появляется в сценарии связью — копия листа больше не создаётся.
            if (CreationKind is CharacterKind.Npc && ScenarioId is { } scenarioId)
                await ScenarioService.AddNpcToScenarioAsync(scenarioId, created.Id);

            if (ScenarioId is { } targetScenarioId && CreationKind is not CharacterKind.PlayerCharacter)
            {
                ShowNotification($"{KindLabel} создан и добавлен в сценарий!", "success");
                NavigationManager.NavigateTo($"/scenarios/{targetScenarioId}", replace: true);
                return;
            }

            ShowNotification($"{KindLabel} успешно создан!", "success");
            NavigationManager.NavigateTo($"/character/{created.Id}", replace: true);
        }
        catch (Exception ex)
        {
            ShowNotification($"Ошибка при сохранении: {ex.Message}", "error");
            _errorBoundary?.Recover();
        }
        finally
        {
            _isBusy = false;
        }
    }

    /// <summary>Как называть лист в сообщениях.</summary>
    private string KindLabel => CurrentKind switch
    {
        CharacterKind.Npc => "НПС",
        CharacterKind.Pregen => "Преген",
        _ => "Персонаж"
    };

    private void ShowNotification(string message, string type)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _notification.Message = message;
        _notification.Type = type;
    }

    private void ClearNotification()
    {
        _notification.Message = null;
    }

    private void OpenGenerateModal() => _showGenerateModal = true;

    private void CloseGenerateModal() => _showGenerateModal = false;

    private async Task OpenLlmModal()
    {
        if (_llmModal is not null)
            await _llmModal.OpenAsync();
    }

    private async Task HandleLlmApplied(Character updated)
    {
        await CharacterService.UpdateCharacterAsync(updated);
        CharacterStorageDto = await CharacterService.GetCharacterByIdAsync(updated.Id);
        Character = CharacterStorageDto?.Character;
        ShowNotification("Персонаж обновлён по рекомендациям LLM", "success");
    }

    private async Task HandleGenerate(GenerateCharacterModal.GenerationParameters parameters)
    {
        _showGenerateModal = false;

        var occupations = await OccupationService.GetAllOccupationsAsync();
        var result = await CharacterGenerationService.GenerateRandomCharacter(
            occupations,
            parameters.Occupation,
            parameters.Gender,
            parameters.Age,
            CampaignEra);

        Character = result.Character;
        _generationLog = result.Log;

        Character.PersonalInfo.PlayerName = PlayerNameForSheet;
    }

    private async Task ScrollToSection(string sectionId, bool navMenuOpen = false)
    {
        _navMenuOpen = navMenuOpen;

        try
        {
            await JsRuntime.InvokeVoidAsync("scrollToElement", sectionId);
        }
        catch (Exception)
        {
            // Fallback - если JavaScript не загрузился, попробуем альтернативный метод
            try
            {
                await JsRuntime.InvokeVoidAsync("eval", $@"
                    const element = document.getElementById('{sectionId}');
                    if (element) {{
                        const headerOffset = 120;
                        const elementPosition = element.getBoundingClientRect().top;
                        const offsetPosition = elementPosition + window.pageYOffset - headerOffset;
                        window.scrollTo({{ top: offsetPosition, behavior: 'smooth' }});
                    }}
                ");
            }
            catch
            {
                // Игнорируем - скрипт не загрузился
            }
        }
    }

    private void UpdateCharacteristic(AttributeValue value)
    {
        value.UpdateDerived();
        StateHasChanged();
    }

    private void ResetUsedSkills()
    {
        if (Character?.Skills.SkillGroups == null)
            return;

        foreach (var group in Character.Skills.SkillGroups)
        {
            foreach (var skill in group.Skills)
            {
                skill.IsUsed = false;
            }
        }

        ShowNotification("Все использованные навыки сброшены", "success");
        StateHasChanged();
    }

    private bool _navMenuOpen;
    private string _skillSearchQuery = string.Empty;

    private void ToggleNavMenu()
    {
        _navMenuOpen = !_navMenuOpen;
    }

    private void ClearSkillSearch()
    {
        _skillSearchQuery = string.Empty;
    }

    private List<SkillGroup> GetFilteredSkillGroups()
    {
        if (Character?.Skills.SkillGroups == null)
            return new List<SkillGroup>();

        if (string.IsNullOrWhiteSpace(_skillSearchQuery))
            return Character.Skills.SkillGroups;

        var searchTerm = _skillSearchQuery.Trim().ToLowerInvariant();
        var filteredGroups = new List<SkillGroup>();

        foreach (var group in Character.Skills.SkillGroups)
        {
            // Проверяем, соответствует ли название группы поисковому запросу
            var groupNameMatches = group.Name.ToLowerInvariant().Contains(searchTerm);

            // Фильтруем навыки в группе
            var matchingSkills = group.Skills
                .Where(s => s.Name.ToLowerInvariant().Contains(searchTerm))
                .ToList();

            // Если название группы совпадает, включаем все навыки из этой группы
            if (groupNameMatches)
            {
                filteredGroups.Add(group);
            }
            // Иначе, если есть совпадающие навыки, создаем новую группу только с этими навыками
            else if (matchingSkills.Any())
            {
                var filteredGroup = new SkillGroup
                {
                    Name = group.Name,
                    Skills = matchingSkills
                };
                filteredGroups.Add(filteredGroup);
            }
        }

        return filteredGroups;
    }

    private void HandleStatusUpdate(CharacterStatus newStatus)
    {
        if (CharacterStorageDto != null)
        {
            CharacterStorageDto.Status = newStatus;
            ShowNotification($"Статус персонажа изменен на: {newStatus}", "success");
            StateHasChanged();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Task.Delay(100);

            if (CharacterId.HasValue && CharacterId.Value != Guid.Empty && Character is not null && !IsNpc)
            {
                var name = Character.PersonalInfo.Name ?? "Персонаж";
                LastCharacterService.Set(CharacterId.Value.ToString(), name);
                await JsRuntime.InvokeVoidAsync("localStorage.setItem", "last-character-id", CharacterId.Value.ToString());
                await JsRuntime.InvokeVoidAsync("localStorage.setItem", "last-character-name", name);
            }
        }

        await base.OnAfterRenderAsync(firstRender);
    }
}
