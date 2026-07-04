using System.ComponentModel.DataAnnotations;

namespace SomethingSomethingApi.Models;

public class CreateInventoryItemRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Required]
    public string Category { get; set; } = string.Empty;
}
