using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SomethingSomething;

public class CategorySummary : INotifyPropertyChanged
{
    private string _category = string.Empty;
    private int _itemCount;
    private int _totalQuantity;
    private int _lowStockCount;

    // Category = the group heading shown on each overview card.
    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    // ItemCount = how many distinct rows belong to this category in the current view.
    public int ItemCount
    {
        get => _itemCount;
        set => SetProperty(ref _itemCount, value);
    }

    // TotalQuantity = the sum of quantities for this category in the current view.
    public int TotalQuantity
    {
        get => _totalQuantity;
        set => SetProperty(ref _totalQuantity, value);
    }

    // LowStockCount = how many items in this category are currently below the threshold.
    public int LowStockCount
    {
        get => _lowStockCount;
        set => SetProperty(ref _lowStockCount, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // Reuses the same summary card object during auto-refresh so the overview does not flicker.
    public void UpdateFrom(CategorySummary source)
    {
        Category = source.Category;
        ItemCount = source.ItemCount;
        TotalQuantity = source.TotalQuantity;
        LowStockCount = source.LowStockCount;
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
