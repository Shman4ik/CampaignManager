using CampaignManager.Web.Components.Features.Items.Model;
using CampaignManager.Web.Components.Features.Items.Services;
using CampaignManager.Web.Components.Shared.Model;
using CampaignManager.Web.Utilities.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CampaignManager.Web.Components.Features.Items.Pages;

public partial class ItemsPage
{
    // Страница, сортировка и раскрытая строка — общие у всех каталогов; здесь только фильтры.
    private readonly CatalogListState<Item> catalog;

    public ItemsPage()
    {
        catalog = new CatalogListState<Item>(
            25,
            FilterItems,
            CatalogSort.By((Item i) => i.Name),
            new Dictionary<string, CatalogSort<Item>>
            {
                [nameof(Item.Type)] = CatalogSort.By((Item i) => i.Type)
            });
    }

    [Inject] private ItemService ItemService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IdentityService IdentityService { get; set; } = default!;
    [Inject] private ILogger<ItemsPage> Logger { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    // Справочник общий для всех кампаний: смотреть может любой вошедший, править — только
    // Хранитель и администратор. Сервис проверяет то же самое сам, здесь — чтобы не показывать
    // игроку кнопки, которые всё равно откажут.
    private bool canEdit;

    private bool isSearchPanelVisible = true;

    // List to hold all items fetched from the service
    private List<Item>? items; // Nullable to indicate loading state
    private List<string>? itemTypes; // Available item types

    // Search query bound to the input field
    private string searchQuery = string.Empty;

    // Advanced filtering variables
    private string? selectedType;
    private bool is1920Filter = true;
    private bool isModernFilter = false;

    // Метка эпохи в строке различает предметы, только когда список не сужен до одной
    // эпохи: при фильтре «1920-е» её несёт каждая строка и она превращается в шум.
    private bool showEra => is1920Filter == isModernFilter;

    // Modal visibility flags
    private bool showModal;
    private bool showDeleteModal; // State for Add/Edit modal
    private bool isEditMode;
    private Item editItem = NewItem(); // Model for the edit form
    private bool editItem1920;
    private bool editItemModern;

    // Item object targeted for deletion
    private Item? deleteItem;

    // Error message state
    private string? errorMessage;

    // Form submission reference
    private ElementReference itemFormSubmitButton;

    // Lifecycle method: Load data when the component is initialized
    protected override async Task OnInitializedAsync()
    {
        canEdit = await IdentityService.IsKeeper();
        await LoadItemsAsync();
    }

    // Method to load items data from the service
    private async Task LoadItemsAsync()
    {
        errorMessage = null; // Clear previous errors
        try
        {
            var itemsTask = ItemService.GetAllItemsAsync();
            var typesTask = ItemService.GetAllItemTypesAsync();

            await Task.WhenAll(itemsTask, typesTask);

            items = itemsTask.Result;
            itemTypes = typesTask.Result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading items");
            errorMessage = "Не удалось загрузить список предметов. Пожалуйста, попробуйте позже.";
            items = new List<Item>(); // Ensure items is not null
            itemTypes = new List<string>();
        }
    }

    // Сброс возвращает то же состояние, с которым страница открывается.
    private void ResetFilters()
    {
        searchQuery = string.Empty;
        selectedType = null;
        is1920Filter = true;
        isModernFilter = false;
        ApplyFilters();
    }

    private void HandleSearchInput(ChangeEventArgs e)
    {
        searchQuery = e.Value?.ToString() ?? string.Empty;
        ApplyFilters();
    }

    private void HandleTypeChanged(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        selectedType = string.IsNullOrWhiteSpace(value) ? null : value;
        ApplyFilters();
    }

    private void SetEra1920(ChangeEventArgs e)
    {
        is1920Filter = e.Value is true;
        ApplyFilters();
    }

    private void SetEraModern(ChangeEventArgs e)
    {
        isModernFilter = e.Value is true;
        ApplyFilters();
    }


    private void ApplyFilters()
    {
        catalog.ResetPage(); // Reset to first page when filtering
        // This will trigger a re-render which will use our updated filter values
        StateHasChanged();
    }

    private void ShowAddModal()
    {
        if (!canEdit) return;

        isEditMode = false;
        editItem = NewItem(); // Reset the edit model
        editItem1920 = true;
        editItemModern = false;
        errorMessage = null; // Clear errors
        showModal = true;
    } // Show the modal for editing an existing item

    private void ShowEditModal(Item item)
    {
        if (!canEdit) return;

        isEditMode = true;
        // Правим полную копию, а не экземпляр из кэша справочника: новое поле Item попадает
        // в неё само. Эпоху форма держит двумя флажками и собирает обратно при сохранении.
        editItem = EntityCloner.Clone(item);
        editItem1920 = (item.Era & Eras.Classic) != 0;
        editItemModern = (item.Era & Eras.Modern) != 0;
        errorMessage = null; // Clear errors
        showModal = true;
    }

    // Hide the Add/Edit modal
    private void HideModal()
    {
        showModal = false;
    }

    // Submit the item form programmatically
    private async Task SubmitItemForm()
    {
        await JSRuntime.InvokeVoidAsync("eval", "document.querySelector('#item-edit-form button[type=submit]').click()");
    }

    // Handle the valid submission of the Add/Edit form

    private async Task HandleValidSubmit()
    {
        errorMessage = null; // Clear previous errors
        try
        {
            // Set the era based on checkboxes
            var era = Eras.Classic; // Default value, but we'll override it
            if (editItem1920 && editItemModern)
                era = Eras.Classic | Eras.Modern;
            else if (editItem1920)
                era = Eras.Classic;
            else if (editItemModern)
                era = Eras.Modern;

            editItem.Era = era;

            if (isEditMode)
            {
                var success = await ItemService.UpdateItemAsync(editItem);
                if (!success)
                {
                    errorMessage = "Не удалось обновить предмет. Возможно, предмет с таким названием уже существует.";
                    return;
                }
            }
            else
            {
                var created = await ItemService.CreateItemAsync(editItem);
                if (created == null)
                {
                    errorMessage = "Не удалось создать предмет. Возможно, предмет с таким названием уже существует.";
                    return;
                }
            }

            showModal = false; // Close modal on success
            await LoadItemsAsync(); // Refresh the list
            ApplyFilters(); // Reapply filters
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving item {ItemId} {ItemName}", editItem.Id, editItem.Name);
            errorMessage = $"Не удалось сохранить предмет: {ex.Message}";
            // Keep the modal open to show the error
        }
    }

    // Show the delete confirmation modal
    private void ShowDeleteModal(Item item)
    {
        if (!canEdit) return;

        deleteItem = item;
        errorMessage = null; // Clear errors
        showDeleteModal = true;
    }

    // Hide the delete confirmation modal
    private void HideDeleteModal()
    {
        showDeleteModal = false;
        deleteItem = null;
    }

    // Confirm and execute the item deletion
    private async Task ConfirmDelete()
    {
        if (deleteItem == null) return; // Should not happen, but good practice

        var itemId = deleteItem.Id;
        errorMessage = null; // Clear previous errors
        try
        {
            await ItemService.DeleteItemAsync(itemId);
            showDeleteModal = false;
            await LoadItemsAsync(); // Refresh the list
            deleteItem = null; // Clear the selected item
            ApplyFilters(); // Reapply filters
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting item {ItemId}", itemId);
            errorMessage = $"Не удалось удалить предмет: {ex.Message}";
            // Keep the modal open to show the error
        }
    }

    /// <summary>Пустой предмет для формы «Добавить»; эпоху перед сохранением задают флажки.</summary>
    private static Item NewItem() => new() { Name = string.Empty, Era = Eras.Classic };

    // Filter items based on all active filters
    private IEnumerable<Item> FilterItems()
    {
        if (items == null)
        {
            return [];
        }

        IEnumerable<Item> query = items;

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            query = query.Where(i =>
                (i.Name != null && i.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                (i.Description != null && i.Description.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                (i.Type != null && i.Type.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
            );
        }

        // Apply type filter
        if (!string.IsNullOrWhiteSpace(selectedType))
        {
            query = query.Where(i => i.Type == selectedType);
        }

        // Apply era filters
        if (is1920Filter && !isModernFilter)
        {
            query = query.Where(i => (i.Era & Eras.Classic) != 0);
        }
        else if (!is1920Filter && isModernFilter)
        {
            query = query.Where(i => (i.Era & Eras.Modern) != 0);
        }
        else if (is1920Filter && isModernFilter)
        {
            query = query.Where(i => (i.Era & Eras.Classic) != 0 || (i.Era & Eras.Modern) != 0);
        }

        return query;
    }

}
