using System.ComponentModel.DataAnnotations;

namespace SomethingSomethingApi.Models;

public class CreateInventoryNoteRequest
{
    [Required]
    [StringLength(500)]
    public string NoteText { get; set; } = string.Empty;
}
