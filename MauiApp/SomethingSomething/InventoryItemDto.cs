using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SomethingSomething;

public class InventoryItemDto : INotifyPropertyChanged
{
    public const int LowStockThreshold = 5;

    public int ItemId { get; set; }

    private string _name = string.Empty;
    private int _quantity;
    private string _category = string.Empty;

    // Name = the inventory item label shown in the form, list, and details page.
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    // Quantity = the current stock count returned by the API and database.
    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(IsLowStock));
                OnPropertyChanged(nameof(StockStatus));
            }
        }
    }

    // Category = the fixed classroom category used for grouping and filtering items.
    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public bool IsLowStock => Quantity <= LowStockThreshold;

    public string StockStatus => IsLowStock ? "Low Stock" : "In Stock";

    public event PropertyChangedEventHandler? PropertyChanged;

    // Copies API values into the existing row object so MAUI can update bindings without rebuilding the whole list.
    public void UpdateFrom(InventoryItemDto source)
    {
        ItemId = source.ItemId;
        Name = source.Name;
        Quantity = source.Quantity;
        Category = source.Category;
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
