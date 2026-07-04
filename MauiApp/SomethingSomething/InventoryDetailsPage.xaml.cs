using System.Collections.ObjectModel;
using System.Net.Http.Json;

namespace SomethingSomething;

public partial class InventoryDetailsPage : ContentPage, IQueryAttributable
{
#if ANDROID
    private const string ApiBase = "http://10.0.2.2:1170";
#else
    private const string ApiBase = "http://localhost:1170";
#endif

    public const string SelectedItemParameter = "SelectedItem";

    private readonly HttpClient _httpClient = new();
    private readonly ObservableCollection<InventoryNoteDto> _notes = new();
    private InventoryItemDto? _selectedItem;

    public InventoryDetailsPage()
    {
        InitializeComponent();
        // This list shows every saved note for the selected inventory item.
        NotesCollectionView.ItemsSource = _notes;
    }

    // Receives the selected inventory item from Shell navigation and refreshes the note history.
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue(SelectedItemParameter, out var selectedItem)
            || selectedItem is not InventoryItemDto item)
        {
            return;
        }

        _selectedItem = item;
        ItemIdLabel.Text = $"ID: {item.ItemId}";
        NameLabel.Text = item.Name;
        CategoryLabel.Text = $"Category: {item.Category}";
        QuantityLabel.Text = $"Quantity: {item.Quantity}";
        StockBadgeLabel.Text = item.StockStatus;
        StockBadgeLabel.BackgroundColor = item.IsLowStock
            ? Color.FromArgb("#F59E0B")
            : Color.FromArgb("#2563EB");

        _ = LoadNotesAsync();
    }

    // Sends a new note to the API for the currently selected item.
    private async void OnAddNoteClicked(object? sender, EventArgs e)
    {
        if (_selectedItem is null)
        {
            ShowNotesStatus("Select an item before adding a note.", isError: true);
            return;
        }

        var noteText = NoteEditor.Text?.Trim();
        if (string.IsNullOrWhiteSpace(noteText))
        {
            ShowNotesStatus("Enter note text before saving.", isError: true);
            return;
        }

        try
        {
            ShowNotesStatus("Saving note through the API...", isError: false);

            var request = new CreateInventoryNoteRequest
            {
                NoteText = noteText
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{ApiBase}/api/inventory/{_selectedItem.ItemId}/notes",
                request);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                ShowNotesStatus($"Note save failed: {errorText}", isError: true);
                return;
            }

            NoteEditor.Text = string.Empty;
            await LoadNotesAsync();
            ShowNotesStatus("Note saved successfully.", isError: false);
        }
        catch (Exception ex)
        {
            ShowNotesStatus($"Could not save the note: {ex.Message}", isError: true);
        }
    }

    // Reads the stored note history for the selected item from the API.
    private async Task LoadNotesAsync()
    {
        if (_selectedItem is null)
        {
            return;
        }

        try
        {
            ShowNotesStatus("Loading notes from the API...", isError: false);

            var notes = await _httpClient.GetFromJsonAsync<List<InventoryNoteDto>>(
                $"{ApiBase}/api/inventory/{_selectedItem.ItemId}/notes")
                ?? [];

            _notes.Clear();
            foreach (var note in notes)
            {
                _notes.Add(note);
            }

            ShowNotesStatus($"Loaded {_notes.Count} note(s).", isError: false);
        }
        catch (Exception ex)
        {
            ShowNotesStatus($"Could not load notes from the API: {ex.Message}", isError: true);
        }
    }

    // Shows note-specific status text so the user knows whether note loading/saving worked.
    private void ShowNotesStatus(string message, bool isError)
    {
        NotesStatusLabel.Text = message;
        NotesStatusLabel.TextColor = isError
            ? Color.FromArgb("#B3261E")
            : Color.FromArgb("#2E7D32");
        NotesStatusLabel.IsVisible = true;
    }
}
