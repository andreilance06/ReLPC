using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ReLPC.Views;

public partial class ConfirmDeleteDatasetWindowView : Window
{
    public ConfirmDeleteDatasetWindowView()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private void OnDeleteClick(object? sender, RoutedEventArgs e) => Close(true);
}
