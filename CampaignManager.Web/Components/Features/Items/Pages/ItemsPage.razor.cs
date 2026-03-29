using CampaignManager.Web.Components.Features.Items.Model;
using CampaignManager.Web.Components.Features.Items.Services;
using CampaignManager.Web.Components.Shared.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CampaignManager.Web.Components.Features.Items.Pages;

public partial class ItemsPage
{
    [Inject] private ItemService ItemService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<ItemsPage> Logger { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    // Editable item class for form binding
    public class EditableItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Type { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
    }

    private bool isSearchPanelVisible = true;

    // List to hold all items fetched from the service
    private List<Item>? items; // Nullable to indicate loading state
    private List<string>? itemTypes; // Available item types

    // Filtered list based on search query and other filters
    private IQueryable<Item> filteredItems => FilterItems(); // Pagination properties
    private int currentPage = 1;
    private int itemsPerPage = 20;
    private int totalPages => (int)Math.Ceiling((double)filteredItems.Count() / itemsPerPage);
    private IEnumerable<Item> paginatedItems => filteredItems.Skip((currentPage - 1) * itemsPerPage).Take(itemsPerPage);

    // Sorting properties
    private string? sortField;
    private bool sortAscending = true;

    // Search query bound to the input field
    private string searchQuery = string.Empty;

    // Advanced filtering variables
    private string? selectedType;
    private bool is1920Filter = true;
    private bool isModernFilter = false;
    private readonly Dictionary<string, string> columnFilters = new();

    // Modal visibility flags
    private bool showModal;
    private bool showDeleteModal; // State for Add/Edit modal
    private bool isEditMode;
    private EditableItem editItem = new(); // Model for the edit form
    private bool editItem1920;
    private bool editItemModern;

    // Item object targeted for deletion
    private Item? deleteItem;

    // Error message state
    private string? errorMessage;

    // Expanded item details
    private Guid? expandedItemId;

    // Form submission reference
    private ElementReference itemFormSubmitButton;

    // Lifecycle method: Load data when the component is initialized
    protected override async Task OnInitializedAsync()
    {
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
            // Log the error (e.g., to console or a logging service)
            Console.WriteLine($"Error loading items: {ex.Message}");
            errorMessage = "Не удалось загрузить список предметов. Пожалуйста, попробуйте позже.";
            items = new List<Item>(); // Ensure items is not null
            itemTypes = new List<string>();
        }
    }

    // Pagination methods
    private void GoToPage(int page)
    {
        if (page >= 1 && page <= totalPages)
        {
            currentPage = page;
        }
    }

    // Sorting methods
    private void ToggleSort(string field)
    {
        if (sortField == field)
        {
            sortAscending = !sortAscending;
        }
        else
        {
            sortField = field;
            sortAscending = true;
        }

        currentPage = 1; // Reset to first page when sorting
    }

    private string GetSortIcon(string field)
    {
        if (sortField != field)
            return "fa-sort";

        return sortAscending ? "fa-sort-up" : "fa-sort-down";
    }

    // Toggle item details
    private void ToggleItemDetails(Item item)
    {
        if (expandedItemId == item.Id)
        {
            expandedItemId = null;
        }
        else
        {
            expandedItemId = item.Id;
        }
    }

    // Reset all filters to their default state
    private void ResetFilters()
    {
        searchQuery = string.Empty;
        selectedType = null;
        is1920Filter = true;
        isModernFilter = true;
        columnFilters.Clear();
        currentPage = 1;

        // Apply filters to update the UI
        ApplyFilters();
    }

    // Handle column header filter change
    private void OnColumnHeaderFilterChanged(ChangeEventArgs e, string columnName)
    {
        var value = e.Value?.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(value))
        {
            if (columnFilters.ContainsKey(columnName))
            {
                columnFilters.Remove(columnName);
            }
        }
        else
        {
            columnFilters[columnName] = value;
        }

        currentPage = 1; // Reset to first page when filtering
        ApplyFilters();
    }


    private void ApplyFilters()
    {
        currentPage = 1; // Reset to first page when filtering
        // This will trigger a re-render which will use our updated filter values
        StateHasChanged();
    }

    private void ShowAddModal()
    {
        isEditMode = false;
        editItem = new EditableItem(); // Reset the edit model
        editItem1920 = true;
        editItemModern = false;
        errorMessage = null; // Clear errors
        showModal = true;
    } // Show the modal for editing an existing item

    private void ShowEditModal(Item item)
    {
        isEditMode = true;
        // Copy the item data to the editable model
        editItem = new EditableItem
        {
            Id = item.Id,
            Name = item.Name,
            Type = item.Type,
            Description = item.Description,
            ImageUrl = item.ImageUrl
        };
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

            var item = new Item
            {
                Id = editItem.Id,
                Name = editItem.Name,
                Type = editItem.Type,
                Era = era,
                Description = editItem.Description,
                ImageUrl = editItem.ImageUrl
            };

            if (isEditMode)
            {
                var success = await ItemService.UpdateItemAsync(item);
                if (!success)
                {
                    errorMessage = "Не удалось обновить предмет. Возможно, предмет с таким названием уже существует.";
                    return;
                }
            }
            else
            {
                var created = await ItemService.CreateItemAsync(item);
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
            Console.WriteLine($"Error saving item: {ex.Message}");
            errorMessage = $"Не удалось сохранить предмет: {ex.Message}";
            // Keep the modal open to show the error
        }
    }

    // Show the delete confirmation modal
    private void ShowDeleteModal(Item item)
    {
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

        errorMessage = null; // Clear previous errors
        try
        {
            await ItemService.DeleteItemAsync(deleteItem.Id); // Use Guid Id
            showDeleteModal = false;
            await LoadItemsAsync(); // Refresh the list
            deleteItem = null; // Clear the selected item
            ApplyFilters(); // Reapply filters
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting item: {ex.Message}");
            errorMessage = $"Не удалось удалить предмет: {ex.Message}";
            // Keep the modal open to show the error
        }
    }

    // Filter items based on all active filters
    private IQueryable<Item> FilterItems()
    {
        if (items == null)
        {
            return Enumerable.Empty<Item>().AsQueryable(); // Return empty IQueryable if source is null
        }

        var query = items.AsQueryable(); // Use AsQueryable for sorting

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

        // Apply column header filters
        foreach (var filter in columnFilters)
        {
            if (filter.Key == "Name" && !string.IsNullOrWhiteSpace(filter.Value))
            {
                query = query.Where(i => i.Name != null && i.Name.Contains(filter.Value, StringComparison.OrdinalIgnoreCase));
            }
            else if (filter.Key == "Type" && !string.IsNullOrWhiteSpace(filter.Value))
            {
                query = query.Where(i => i.Type != null && i.Type.Contains(filter.Value, StringComparison.OrdinalIgnoreCase));
            }
        }

        // Apply sorting
        if (!string.IsNullOrEmpty(sortField))
        {
            switch (sortField)
            {
                case nameof(Item.Name):
                    query = sortAscending ? query.OrderBy(i => i.Name) : query.OrderByDescending(i => i.Name);
                    break;
                case nameof(Item.Type):
                    query = sortAscending ? query.OrderBy(i => i.Type) : query.OrderByDescending(i => i.Type);
                    break;
                case nameof(Item.Era):
                    query = sortAscending ? query.OrderBy(i => i.Era) : query.OrderByDescending(i => i.Era);
                    break;
                default:
                    query = query.OrderBy(i => i.Name);
                    break;
            }
        }
        else
        {
            query = query.OrderBy(i => i.Name);
        }

        return query;
    }

    private string GetEraDisplay(Eras era)
    {
        if ((era & Eras.Classic) != 0 && (era & Eras.Modern) != 0)
            return "1920s & Modern";
        if ((era & Eras.Classic) != 0)
            return "1920s";
        if ((era & Eras.Modern) != 0)
            return "Modern";
        return "Unknown";
    }

    private void ToggleSearchPanel()
    {
        isSearchPanelVisible = !isSearchPanelVisible;
    }
}
