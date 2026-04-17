using CampaignManager.Web.Components.Features.Campaigns.Models;
using CampaignManager.Web.Components.Features.Campaigns.Services;
using CampaignManager.Web.Components.Features.Characters.Components;
using CampaignManager.Web.Components.Features.Characters.Model;
using CampaignManager.Web.Components.Features.Characters.Services;
using CampaignManager.Web.Components.Features.Skills.Services;
using CampaignManager.Web.Components.Features.Weapons.Model;
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

    [Parameter] public Guid? CharacterId { get; set; }
    [Parameter] public Guid? CampaignId { get; set; }

    // Determine if we're in NPC creation mode based on the URL segment after CampaignId
    [Parameter] public string? Npc { get; set; }

    [SupplyParameterFromQuery] public Guid? ScenarioId { get; set; }
    private bool IsTemplate => Npc is "template" or "pregen";
    private bool IsNpc => Npc is "npc" or "template" || Character?.CharacterType == CharacterType.NonPlayerCharacter;

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
            else if (CampaignPlayer is null && CampaignId.HasValue && !IsNpc)
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
                PlayerName = IsNpc ? "NPC" : CampaignPlayer?.PlayerName ?? "Unknown"
            },
            Characteristics = new Characteristics(),
            Skills = skills,
            Backstory = string.Empty,
            Biography = new BiographyInfo(),
            Equipment = new Equipment(),
            Finances = new Finances(),
            Weapons = new List<Weapon>(),
            Notes = string.Empty,
            CharacterType = IsNpc ? CharacterType.NonPlayerCharacter : CharacterType.PlayerCharacter
        };
    }

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
                Character.PersonalInfo.Name = IsNpc ? "Безымянный NPC" : "Безымянный";
                ShowNotification($"Имя {(IsNpc ? "NPC" : "персонажа")} не было указано, установлено '{Character.PersonalInfo.Name}'.", "warning");
            }

            // Ensure character type is set correctly
            Character.CharacterType = IsNpc ? CharacterType.NonPlayerCharacter : CharacterType.PlayerCharacter;
            Character.PersonalInfo.PlayerName = IsNpc ? "NPC" : CampaignPlayer?.PlayerName ?? "Unknown";

            if (CharacterId.HasValue && CharacterId.Value != Guid.Empty && Character.Id == CharacterId.Value)
            {
                await CharacterService.UpdateCharacterAsync(Character);
                ShowNotification($"{(IsNpc ? "NPC" : "Персонаж")} успешно обновлен!", "success");
            }
            else
            {
                Character.Id = Guid.Empty;
                var createdCharacter = await CharacterService.CreateCharacterAsync(
                    Character,
                    CampaignPlayer?.Id,
                    IsTemplate ? CharacterStatus.Template : CharacterStatus.Active);

                if (createdCharacter.Id != Guid.Empty)
                {
                    Character.Id = createdCharacter.Id;
                    CharacterId = createdCharacter.Id;

                    // If created from scenario, link to it and redirect back
                    if (ScenarioId.HasValue && ScenarioId.Value != Guid.Empty)
                    {
                        await CharacterService.SaveCharacterTemplateWithScenarioAsync(
                            createdCharacter.Id, ScenarioId.Value);
                        ShowNotification($"{(IsNpc ? "NPC" : "Персонаж")} создан и привязан к сценарию!", "success");
                        NavigationManager.NavigateTo($"/scenarios/{ScenarioId.Value}", replace: true);
                    }
                    else
                    {
                        ShowNotification($"{(IsNpc ? "NPC" : "Персонаж")} успешно создан!", "success");
                        NavigationManager.NavigateTo($"/character/{createdCharacter.Id}", replace: true);
                    }
                }
                else
                {
                    ShowNotification($"Ошибка: Не удалось получить ID созданного {(IsNpc ? "NPC" : "персонажа")}.", "error");
                }
            }
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

        Character.PersonalInfo.PlayerName = IsNpc ? "NPC" : CampaignPlayer?.PlayerName ?? "No name";
        Character.CharacterType = IsNpc ? CharacterType.NonPlayerCharacter : CharacterType.PlayerCharacter;
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
            // Ждем, пока JavaScript функции загрузятся
            await Task.Delay(100);
        }

        await base.OnAfterRenderAsync(firstRender);
    }
}
