namespace SomethingSomethingApi.Models;

public class InventoryItemDto
{
    public int ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string Category { get; set; } = string.Empty;
}
