using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ReLPC.Views;

public partial class ConfirmLogoutWindowView : Window
{
    public ConfirmLogoutWindowView()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private void OnLogoutClick(object? sender, RoutedEventArgs e) => Close(true);
}
