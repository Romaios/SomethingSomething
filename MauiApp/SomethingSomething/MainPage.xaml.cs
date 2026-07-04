#if WINDOWS
using Windows.Media.Core;
using Windows.Media.Playback;
#elif ANDROID
using Android.Media;
#endif
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Threading;

namespace SomethingSomething;

public partial class MainPage : ContentPage
{
#if ANDROID
    private const string ApiBase = "http://10.0.2.2:1170";
#else
    private const string ApiBase = "http://localhost:1170";
#endif

    private const string AllCategoriesOption = "All Categories";
    private const string SortByNameAscending = "Name (A-Z)";
    private const string SortByNameDescending = "Name (Z-A)";
    private const string SortByQuantityAscending = "Quantity (Low to High)";
    private const string SortByQuantityDescending = "Quantity (High to Low)";
    private const string SortByCategoryAscending = "Category (A-Z)";
    private const string AddModeTitle = "Add New Item";
    private const string EditModeTitle = "Edit Item";
    private const string OverviewMode = "Overview";
    private const string SavedInventoryMode = "SavedInventory";
    private const string MudkipSoundFileName = "mudkip_click.wav";
    private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient = new();
    private readonly SemaphoreSlim _inventoryLoadLock = new(1, 1);
    private readonly ObservableCollection<InventoryItemDto> _items = new();
    private readonly ObservableCollection<CategorySummary> _categorySummaries = new();
    private readonly List<InventoryItemDto> _allItems = [];
    private readonly List<string> _categories =
    [
        "Electronics",
        "Office Supplies",
        "Food",
        "Cleaning",
        "Other"
    ];

    private bool _hasLoaded;
    private InventoryItemDto? _editingItem;
    private string _activeViewMode = OverviewMode;
    private string? _mudkipSoundPath;
    private CancellationTokenSource? _autoRefreshCancellationTokenSource;
#if WINDOWS
    private readonly MediaPlayer _mudkipPlayer = new();
#elif ANDROID
    private MediaPlayer? _mudkipPlayer;
#endif

    public MainPage()
    {
        InitializeComponent();

        // This picker is used when the user adds or edits an item.
        CategoryPicker.ItemsSource = _categories;

        // This picker controls which category is shown in the visible list.
        var filterCategories = new List<string> { AllCategoriesOption };
        filterCategories.AddRange(_categories);
        FilterCategoryPicker.ItemsSource = filterCategories;
        FilterCategoryPicker.SelectedIndex = 0;

        // This picker changes the order of the items already shown on screen.
        SortPicker.ItemsSource = new List<string>
        {
            SortByNameAscending,
            SortByNameDescending,
            SortByQuantityAscending,
            SortByQuantityDescending,
            SortByCategoryAscending
        };
        SortPicker.SelectedIndex = 0;

        ItemsCollectionView.ItemsSource = _items;
        CategorySummaryCollectionView.ItemsSource = _categorySummaries;

        Loaded += OnPageLoaded;
        UpdateViewMode();
        ResetInputForm();
        UpdateSummary();
    }

    // Loads inventory once when the page first appears.
    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await LoadInventoryAsync();
    }

    // Starts the silent background refresh loop whenever the page becomes visible again.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        StartAutoRefresh();

        if (_hasLoaded)
        {
            _ = LoadInventoryAsync(showFeedback: false, silentErrors: true);
        }
    }

    // Stops the background refresh loop when the user leaves this page.
    protected override void OnDisappearing()
    {
        StopAutoRefresh();
        base.OnDisappearing();
    }

    // Plays the Mudkip sound only when the mascot in the header is tapped.
    private async void OnMudkipTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            await PlayMudkipSoundAsync();
        }
        catch (Exception ex)
        {
            ShowStatus($"Could not play the Mudkip sound: {ex.Message}", isError: true);
        }
    }

    // Calls the API, stores the full list locally, and then reapplies client-side filters.
    // The silent mode is used by auto-refresh so background polling does not keep flashing status text.
    private async Task LoadInventoryAsync(bool showFeedback = true, bool silentErrors = false, bool skipIfBusy = false)
    {
        if (skipIfBusy)
        {
            if (!_inventoryLoadLock.Wait(0))
            {
                return;
            }
        }
        else
        {
            await _inventoryLoadLock.WaitAsync();
        }

        try
        {
            if (showFeedback)
            {
                ShowStatus($"Loading inventory from {ApiBase}...", isError: false);
            }

            var items = await _httpClient.GetFromJsonAsync<List<InventoryItemDto>>($"{ApiBase}/api/inventory")
                        ?? [];

            SyncAllItems(items);

            ApplyFilters();

            if (showFeedback)
            {
                ShowStatus($"Loaded {_allItems.Count} inventory item(s) from the API.", isError: false);
            }
        }
        catch (Exception ex)
        {
            if (!silentErrors)
            {
                ShowStatus($"Could not load inventory from the API: {ex.Message}", isError: true);
            }
        }
        finally
        {
            _inventoryLoadLock.Release();
        }
    }

    // Handles both add mode and edit mode by switching between POST and PUT.
    private async void OnAddItemClicked(object? sender, EventArgs e)
    {
        var name = ItemNameEntry.Text?.Trim();
        var quantityText = QuantityEntry.Text?.Trim();
        var category = CategoryPicker.SelectedItem as string;

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowStatus("Enter an item name before saving.", isError: true);
            return;
        }

        if (!int.TryParse(quantityText, out var quantity) || quantity <= 0)
        {
            ShowStatus("Quantity must be a whole number greater than zero.", isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            ShowStatus("Choose a category for the item.", isError: true);
            return;
        }

        var request = new CreateInventoryItemRequest
        {
            Name = name,
            Quantity = quantity,
            Category = category
        };

        try
        {
            var isEditing = _editingItem is not null;
            ShowStatus(isEditing
                ? "Updating item through the API..."
                : "Saving item through the API...",
                isError: false);

            var response = isEditing
                ? await _httpClient.PutAsJsonAsync($"{ApiBase}/api/inventory/{_editingItem!.ItemId}", request)
                : await _httpClient.PostAsJsonAsync($"{ApiBase}/api/inventory", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                ShowStatus(isEditing
                    ? $"Update failed: {errorText}"
                    : $"Save failed: {errorText}",
                    isError: true);
                return;
            }

            var successMessage = isEditing
                ? $"{name} updated successfully."
                : $"{name} saved successfully.";

            ResetInputForm();
            await LoadInventoryAsync();
            ShowStatus(successMessage, isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Could not reach the API: {ex.Message}", isError: true);
        }
    }

    // Loads the selected row into the form and brings the user back to the edit area.
    private async void OnEditItemClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not InventoryItemDto item)
        {
            ShowStatus("Could not determine which item to edit.", isError: true);
            return;
        }

        _activeViewMode = SavedInventoryMode;
        UpdateViewMode();

        _editingItem = item;
        ItemNameEntry.Text = item.Name;
        QuantityEntry.Text = item.Quantity.ToString();
        CategoryPicker.SelectedItem = item.Category;
        FormSectionTitleLabel.Text = EditModeTitle;
        SaveItemButton.Text = "Update Item";
        CancelEditButton.IsVisible = true;
        EditModeLabel.Text = $"Edit Mode: Updating item ID {item.ItemId}";
        EditModeLabel.IsVisible = true;
        await PageScrollView.ScrollToAsync(FormSectionTitleLabel, ScrollToPosition.Start, true);
        ItemNameEntry.Focus();
        ShowStatus($"Loaded {item.Name} into the form for editing.", isError: false);
    }

    // Confirms deletion, removes the item through the API, then refreshes the local list.
    private async void OnDeleteItemClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not InventoryItemDto item)
        {
            ShowStatus("Could not determine which item to delete.", isError: true);
            return;
        }

        var confirmDelete = await DisplayAlert(
            "Delete Item",
            $"Delete {item.Name} from inventory?",
            "Delete",
            "Cancel");

        if (!confirmDelete)
        {
            ShowStatus($"Deletion cancelled for {item.Name}.", isError: false);
            return;
        }

        try
        {
            ShowStatus($"Deleting {item.Name} through the API...", isError: false);

            var response = await _httpClient.DeleteAsync($"{ApiBase}/api/inventory/{item.ItemId}");

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                ShowStatus($"Delete failed: {errorText}", isError: true);
                return;
            }

            if (_editingItem?.ItemId == item.ItemId)
            {
                ResetInputForm();
            }

            await LoadInventoryAsync();
            ShowStatus($"{item.Name} deleted successfully.", isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Could not delete the item through the API: {ex.Message}", isError: true);
        }
    }

    // Opens the details page and passes the selected inventory item into Shell navigation.
    private async void OnViewDetailsTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not TapGestureRecognizer tapGesture || tapGesture.CommandParameter is not InventoryItemDto item)
        {
            ShowStatus("Could not determine which item to open.", isError: true);
            return;
        }

        var navigationParameters = new Dictionary<string, object>
        {
            [InventoryDetailsPage.SelectedItemParameter] = item
        };

        await Shell.Current.GoToAsync(nameof(InventoryDetailsPage), navigationParameters);
    }

    // Shows the dashboard section and hides the saved-inventory list section.
    private void OnOverviewModeClicked(object? sender, EventArgs e)
    {
        _activeViewMode = OverviewMode;
        UpdateViewMode();
    }

    // Shows the saved-inventory list section and hides the dashboard section.
    private void OnSavedInventoryModeClicked(object? sender, EventArgs e)
    {
        _activeViewMode = SavedInventoryMode;
        UpdateViewMode();
    }

    // Returns the form from edit mode back to the normal add-new-item state.
    private void OnCancelEditClicked(object? sender, EventArgs e)
    {
        ResetInputForm();
        ShowStatus("Edit mode cancelled.", isError: false);
    }

    // Recomputes the visible list whenever the search, category filter, or sort mode changes.
    private void OnFilterChanged(object? sender, EventArgs e)
    {
        ApplyFilters();
    }

    // Clears every client-side view option so the user can see the default list again.
    private void OnClearFiltersClicked(object? sender, EventArgs e)
    {
        SearchEntry.Text = string.Empty;
        FilterCategoryPicker.SelectedIndex = 0;
        SortPicker.SelectedIndex = 0;
        ApplyFilters();
        ShowStatus("Search, category filter, and sort order reset.", isError: false);
    }

    // Applies search, category filter, and sort order to the full in-memory list.
    private void ApplyFilters()
    {
        var searchText = SearchEntry?.Text?.Trim() ?? string.Empty;
        var selectedCategory = FilterCategoryPicker?.SelectedItem as string;
        var selectedSort = SortPicker?.SelectedItem as string ?? SortByNameAscending;

        var filteredItems = _allItems
            .Where(item => string.IsNullOrWhiteSpace(searchText)
                || item.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(selectedCategory)
                || selectedCategory == AllCategoriesOption
                || item.Category.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase));

        filteredItems = selectedSort switch
        {
            SortByNameDescending => filteredItems
                .OrderByDescending(item => item.Name)
                .ThenBy(item => item.Category),
            SortByQuantityAscending => filteredItems
                .OrderBy(item => item.Quantity)
                .ThenBy(item => item.Name),
            SortByQuantityDescending => filteredItems
                .OrderByDescending(item => item.Quantity)
                .ThenBy(item => item.Name),
            SortByCategoryAscending => filteredItems
                .OrderBy(item => item.Category)
                .ThenBy(item => item.Name),
            _ => filteredItems
                .OrderBy(item => item.Name)
                .ThenBy(item => item.Category)
        };

        var sortedItems = filteredItems.ToList();

        SynchronizeVisibleItems(sortedItems);

        UpdateCategorySummaries(sortedItems);
        UpdateSummary();
    }

    // Builds dashboard cards from the currently visible items instead of the full raw list.
    private void UpdateCategorySummaries(List<InventoryItemDto> visibleItems)
    {
        var summaries = visibleItems
            .GroupBy(item => item.Category)
            .Select(group => new CategorySummary
            {
                Category = group.Key,
                ItemCount = group.Count(),
                TotalQuantity = group.Sum(item => item.Quantity),
                LowStockCount = group.Count(item => item.IsLowStock)
            })
            .OrderBy(summary => summary.Category)
            .ToList();

        SynchronizeCategorySummaries(summaries);
    }

    // Updates the view-mode buttons and shows only the section that belongs to the active mode.
    private void UpdateViewMode()
    {
        var isOverviewMode = _activeViewMode == OverviewMode;

        OverviewSection.IsVisible = isOverviewMode;
        SavedInventorySection.IsVisible = !isOverviewMode;

        OverviewModeButton.BackgroundColor = isOverviewMode
            ? Color.FromArgb("#2563EB")
            : Color.FromArgb("#CBD5E1");
        OverviewModeButton.TextColor = isOverviewMode
            ? Colors.White
            : Color.FromArgb("#0F172A");

        SavedInventoryModeButton.BackgroundColor = isOverviewMode
            ? Color.FromArgb("#CBD5E1")
            : Color.FromArgb("#2563EB");
        SavedInventoryModeButton.TextColor = isOverviewMode
            ? Color.FromArgb("#0F172A")
            : Colors.White;
    }

    // Reuses existing item objects so auto-refresh updates bindings quietly instead of replacing the entire list.
    private void SyncAllItems(List<InventoryItemDto> latestItems)
    {
        var existingById = _allItems.ToDictionary(item => item.ItemId);
        var synchronizedItems = new List<InventoryItemDto>(latestItems.Count);

        foreach (var latestItem in latestItems)
        {
            if (existingById.TryGetValue(latestItem.ItemId, out var existingItem))
            {
                existingItem.UpdateFrom(latestItem);
                synchronizedItems.Add(existingItem);
            }
            else
            {
                synchronizedItems.Add(latestItem);
            }
        }

        _allItems.Clear();
        _allItems.AddRange(synchronizedItems);
    }

    // Adjusts the visible inventory rows in place so the CollectionView keeps its scroll position more reliably.
    private void SynchronizeVisibleItems(List<InventoryItemDto> desiredItems)
    {
        SynchronizeCollection(
            _items,
            desiredItems,
            item => item.ItemId,
            (existingItem, latestItem) => existingItem.UpdateFrom(latestItem));
    }

    // Updates overview cards in place for the same reason as the inventory list: less flicker during silent refresh.
    private void SynchronizeCategorySummaries(List<CategorySummary> desiredSummaries)
    {
        SynchronizeCollection(
            _categorySummaries,
            desiredSummaries,
            summary => summary.Category,
            (existingSummary, latestSummary) => existingSummary.UpdateFrom(latestSummary));
    }

    // Generic in-place collection sync used by both the inventory list and the overview summary cards.
    private static void SynchronizeCollection<TItem, TKey>(
        ObservableCollection<TItem> targetCollection,
        IReadOnlyList<TItem> desiredItems,
        Func<TItem, TKey> keySelector,
        Action<TItem, TItem> updateExistingItem)
        where TKey : notnull
    {
        for (var index = targetCollection.Count - 1; index >= 0; index--)
        {
            var currentKey = keySelector(targetCollection[index]);
            var stillExists = desiredItems.Any(item => EqualityComparer<TKey>.Default.Equals(keySelector(item), currentKey));

            if (!stillExists)
            {
                targetCollection.RemoveAt(index);
            }
        }

        for (var desiredIndex = 0; desiredIndex < desiredItems.Count; desiredIndex++)
        {
            var desiredItem = desiredItems[desiredIndex];
            var desiredKey = keySelector(desiredItem);

            if (desiredIndex < targetCollection.Count
                && EqualityComparer<TKey>.Default.Equals(keySelector(targetCollection[desiredIndex]), desiredKey))
            {
                updateExistingItem(targetCollection[desiredIndex], desiredItem);
                continue;
            }

            var existingIndex = -1;
            for (var scanIndex = desiredIndex + 1; scanIndex < targetCollection.Count; scanIndex++)
            {
                if (EqualityComparer<TKey>.Default.Equals(keySelector(targetCollection[scanIndex]), desiredKey))
                {
                    existingIndex = scanIndex;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                var existingItem = targetCollection[existingIndex];
                updateExistingItem(existingItem, desiredItem);
                targetCollection.Move(existingIndex, desiredIndex);
            }
            else
            {
                targetCollection.Insert(desiredIndex, desiredItem);
            }
        }
    }

    // Creates a lightweight timer that quietly polls the API every five seconds while the page stays open.
    private void StartAutoRefresh()
    {
        if (_autoRefreshCancellationTokenSource is not null)
        {
            return;
        }

        _autoRefreshCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _autoRefreshCancellationTokenSource.Token;

        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(AutoRefreshInterval);

            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        LoadInventoryAsync(showFeedback: false, silentErrors: true, skipIfBusy: true));
                }
            }
            catch (OperationCanceledException)
            {
                // Page navigation cancels the timer on purpose, so no user-facing action is needed here.
            }
        }, cancellationToken);
    }

    // Cancels the timer so the app is not polling in the background while another page is open.
    private void StopAutoRefresh()
    {
        if (_autoRefreshCancellationTokenSource is null)
        {
            return;
        }

        _autoRefreshCancellationTokenSource.Cancel();
        _autoRefreshCancellationTokenSource.Dispose();
        _autoRefreshCancellationTokenSource = null;
    }

    // Copies the packaged WAV file to cache once so platform players can read it from a normal file path.
    private async Task<string> GetMudkipSoundPathAsync()
    {
        if (!string.IsNullOrWhiteSpace(_mudkipSoundPath) && File.Exists(_mudkipSoundPath))
        {
            return _mudkipSoundPath;
        }

        var packageStream = await FileSystem.OpenAppPackageFileAsync(MudkipSoundFileName);
        var filePath = Path.Combine(FileSystem.CacheDirectory, MudkipSoundFileName);

        await using (packageStream)
        await using (var outputStream = File.Create(filePath))
        {
            await packageStream.CopyToAsync(outputStream);
        }

        _mudkipSoundPath = filePath;
        return filePath;
    }

    // Uses the local cached WAV file so the mascot can behave like a little sound button.
    private async Task PlayMudkipSoundAsync()
    {
        var soundPath = await GetMudkipSoundPathAsync();

#if WINDOWS
        _mudkipPlayer.Source = MediaSource.CreateFromUri(new Uri(soundPath));
        _mudkipPlayer.Play();
#elif ANDROID
        _mudkipPlayer?.Stop();
        _mudkipPlayer?.Release();
        _mudkipPlayer?.Dispose();

        _mudkipPlayer = new MediaPlayer();
        _mudkipPlayer.SetDataSource(soundPath);
        _mudkipPlayer.Prepare();
        _mudkipPlayer.Start();
#else
        ShowStatus("Mudkip sound playback is not configured for this platform yet.", isError: true);
#endif
    }

    // Resets all add/edit form controls so the page goes back to its natural state.
    private void ResetInputForm()
    {
        _editingItem = null;
        ItemNameEntry.Text = string.Empty;
        QuantityEntry.Text = "1";
        CategoryPicker.SelectedIndex = -1;
        FormSectionTitleLabel.Text = AddModeTitle;
        SaveItemButton.Text = "Add Item";
        CancelEditButton.IsVisible = false;
        EditModeLabel.Text = string.Empty;
        EditModeLabel.IsVisible = false;
    }

    // Updates the summary banner based on the items that are currently visible on screen.
    private void UpdateSummary()
    {
        var totalQuantity = _items.Sum(item => item.Quantity);
        var lowStockCount = _items.Count(item => item.IsLowStock);
        var visibleItemLabel = _items.Count == 1 ? "item" : "items";
        SummaryLabel.Text = $"{_items.Count} {visibleItemLabel} shown | {totalQuantity} total quantity | {lowStockCount} low stock";
    }

    // Shows one user-facing status message for add, edit, delete, and load actions.
    private void ShowStatus(string message, bool isError)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = isError
            ? Color.FromArgb("#B3261E")
            : Color.FromArgb("#2E7D32");
        StatusLabel.IsVisible = true;
    }
}
