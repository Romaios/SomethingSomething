namespace SomethingSomethingApi.Models;

public class InventoryNoteDto
{
    public int NoteId { get; set; }

    public int ItemId { get; set; }

    public string NoteText { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
