namespace SomethingSomething;

public class CreateInventoryItemRequest
{
    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string Category { get; set; } = string.Empty;
}
