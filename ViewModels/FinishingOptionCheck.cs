using System.ComponentModel;
using RapidoWorkingOrders.Models;

namespace RapidoWorkingOrders.ViewModels;

public class FinishingOptionCheck : INotifyPropertyChanged
{
    public int Id { get; init; }
    public string Name { get; init; } = "";

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static FinishingOptionCheck FromModel(FinishingOption fo, bool isChecked = false)
        => new() { Id = fo.Id, Name = fo.Name, IsChecked = isChecked };
}
