namespace SomethingSomething;

public class InventoryNoteDto
{
    public int NoteId { get; set; }

    public int ItemId { get; set; }

    public string NoteText { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string CreatedAtDisplay => CreatedAt.ToLocalTime().ToString("MMM dd, yyyy hh:mm tt");
}
